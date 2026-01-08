using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EFT;
using EFT.Interactive;
using Phobos.Diag;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Location = Phobos.Navigation.Location;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Phobos.Systems;

public struct Cell(int id)
{
    public readonly int Id = id;
    public readonly List<Location> Locations = [];
    public int Congestion = 0;

    public bool HasLocations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Locations.Count > 0;
    }
}

public class LocationSystem
{
    private const int MinCells = 3;
    private const float MaxCellSize = 50f;

    private static readonly string[] HotSpots =
    [
        "ZoneSubStorage", "ZoneBarrack", // rezervbase
        "ZoneSanatorium1", "ZoneSanatorium2", // shoreline
        "Zone_LongRoad", "Zone_Chalet", "Zone_Village", // lighthouse
        "ZoneCenter", "ZoneCenterBot", // interchange
        "ZoneDormitory", "ZoneScavBase", "ZoneOldAZS", "ZoneGasStation" // customs
    ];

    private readonly Cell[,] _cells;
    private readonly float _cellSize;
    private readonly Vector2Int _gridSize;
    private readonly Vector2 _worldOffset; // Bottom-left corner in world space

    private readonly Vector2Int _hotSpot;
    private readonly float _convergenceFactor;

    private readonly SortedSet<Vector2Int> _coordsByCongestion;
    private readonly List<Vector2Int> _tempCoordsBuffer = [];

    private static int _idCounter;

    public LocationSystem(BotsController botsController)
    {
        var locations = CollectLocations();
        Shuffle(locations);

        // Calculate bounds from positions
        var worldMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var worldMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (var i = 0; i < locations.Count; i++)
        {
            var pos = locations[i].Position;

            worldMin.x = Mathf.Min(worldMin.x, pos.x);
            worldMin.z = Mathf.Min(worldMin.z, pos.z);
            worldMax.x = Mathf.Max(worldMax.x, pos.x);
            worldMax.z = Mathf.Max(worldMax.z, pos.z);
        }

        // Add padding to bounds
        worldMin.x -= 10f;
        worldMin.z -= 10f;
        worldMax.x += 10f;
        worldMax.z += 10f;

        _worldOffset = new Vector2(worldMin.x, worldMin.z);

        var worldWidth = worldMax.x - worldMin.x;
        var worldHeight = worldMax.z - worldMin.z;

        // Calculate cell size that gives us at least minCells cells
        var cellSizeFromWidth = worldWidth / MinCells;
        var cellSizeFromHeight = worldHeight / MinCells;

        // Take the minimum of the three constraints
        _cellSize = Mathf.Min(MaxCellSize, Mathf.Max(cellSizeFromWidth, cellSizeFromHeight));

        // Calculate resulting grid dimensions
        var cols = Mathf.CeilToInt(worldWidth / _cellSize);
        var rows = Mathf.CeilToInt(worldHeight / _cellSize);

        _gridSize = new Vector2Int(cols, rows);
        _cells = new Cell[cols, rows];

        var searchRadius = Math.Max(worldWidth, worldHeight) / 2f;

        DebugLog.Write($"Location grid cell size: {_gridSize}, radius: {_cellSize:F1}, locations: {locations.Count}");
        DebugLog.Write($"Location grid world bounds: [{worldMin.x:F0},{worldMin.z:F0}] -> [{worldMax.x:F0},{worldMax.z:F0}]");
        DebugLog.Write($"Location grid world size: {worldWidth:F0}x{worldHeight:F0} search radius: {searchRadius}");

        // Cell initialization. We want to randomize the ids assigned to cells, so that their id based ordering is random.
        // The ids will be later used by the sorted set to tie-break, and we don't want deterministic ordering.
        for (var x = 0; x < cols; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                _tempCoordsBuffer.Add(new Vector2Int(x, y));
            }
        }

        // Shuffle the coords here so that they get assigned to different ids each raid
        Shuffle(_tempCoordsBuffer);

        for (var i = 0; i < _tempCoordsBuffer.Count; i++)
        {
            var coords = _tempCoordsBuffer[i];
            _cells[coords.x, coords.y] = new Cell(i);
        }

        _tempCoordsBuffer.Clear();

        // Add the BSG locations
        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            var coords = WorldToCell(location.Position);
            DebugLog.Write($"Adding {location} to cell at [{coords.x}, {coords.y}]");
            _cells[coords.x, coords.y].Locations.Add(location);
        }

        // Loop through all the cells and check if a path can be found from the cell center or BSG locations to a neighboring cell location.
        for (var x = 0; x < _gridSize.x; x++)
        {
            for (var y = 0; y < _gridSize.y; y++)
            {
                var cellCoords = new Vector2Int(x, y);
                ref var cell = ref _cells[cellCoords.x, cellCoords.y];

                // If we already have BSG issued locations, bail out
                if (cell.HasLocations)
                {
                    continue;
                }

                DebugLog.Write($"Cell at [{x}, {y}] has no BSG locations, attempting to create a synthetic cell center");

                // Try to find a navmesh position as close to the cell center as possible.
                var worldPos = CellToWorld(cellCoords);

                if (NavMesh.SamplePosition(worldPos, out var hit, searchRadius, NavMesh.AllAreas))
                {
                    if (WorldToCell(hit.position) == cellCoords)
                    {
                        if (CheckPathing(cellCoords, hit.position))
                        {
                            DebugLog.Write($"Cell {cellCoords}: found center sample on navmesh at {worldPos} -> {hit.position}");
                            cell.Locations.Add(BuildSyntheticLocation(hit.position));
                            continue;
                        }

                        DebugLog.Write($"Cell {cellCoords}: non-traversable center sample");
                    }
                }

                DebugLog.Write($"Cell {cellCoords}: no navmesh positions found");
            }
        }

        // Congestion ranking
        _coordsByCongestion = new SortedSet<Vector2Int>(new CellCongestionComparer(_cells));
        for (var x = 0; x < _gridSize.x; x++)
        {
            for (var y = 0; y < _gridSize.y; y++)
            {
                var cellCoords = new Vector2Int(x, y);
                var cell = _cells[cellCoords.x, cellCoords.y];

                // Skip empty cells
                if (!cell.HasLocations)
                {
                    continue;
                }

                _coordsByCongestion.Add(cellCoords);
            }
        }

        // Hotspot
        var botZones = botsController.BotSpawner.AllBotZones;
        var validZones = new List<BotZone>();

        for (var i = 0; i < HotSpots.Length; i++)
        {
            var hotSpotName = HotSpots[i];
            validZones.AddRange(from zone in botZones where zone.NameZone == hotSpotName select zone);
        }

        if (validZones.Count > 0)
        {
            var pick = validZones[Random.Range(0, validZones.Count)];
            _hotSpot = WorldToCell(pick.CenterOfSpawnPoints);
            DebugLog.Write($"Picked hotspot {pick.NameZone} at cell {_hotSpot}");
        }
        else
        {
            // Pick the center cell
            _hotSpot = new Vector2Int(_gridSize.x / 2, _gridSize.y / 2);
            DebugLog.Write($"Picked central hotspot at cell {_hotSpot}");
        }

        _convergenceFactor = Random.Range(-Plugin.RaidConvergenceRandomness.Value, Plugin.RaidConvergenceRandomness.Value);
        DebugLog.Write($"Raid Convergence Randomization Factor: {_convergenceFactor}");
    }

    public Location RequestNear(Vector3 worldPos, Location previous)
    {
        var requestCoords = WorldToCell(worldPos);

        // If the previous location is null, use the current position as this removes any bias from the direction
        var previousCoords = previous == null ? WorldToCell(worldPos) : WorldToCell(previous.Position);

        DebugLog.Write($"Requesting location around {requestCoords} | {worldPos} with previous coords {previousCoords}");

        _tempCoordsBuffer.Clear();

        var congestionRms = 0f;

        // First pass: determine the average congestion in the surrounding cells
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var coords = new Vector2Int(requestCoords.x + dx, requestCoords.y + dy);

                if (!IsValidCell(coords))
                    continue;

                ref var cell = ref _cells[coords.x, coords.y];

                if (!cell.HasLocations)
                    continue;

                _tempCoordsBuffer.Add(coords);
                congestionRms += cell.Congestion * cell.Congestion;
            }
        }

        if (_tempCoordsBuffer.Count == 0)
        {
            // We can't go to any neighboring cell for some reason, grab something from the current cell, and if that fails too, search map-wide. 
            var currentCell = _cells[requestCoords.x, requestCoords.y];
            return currentCell.HasLocations ? AssignLocation(requestCoords) : RequestFar();
        }

        congestionRms = Mathf.Sqrt(congestionRms / _tempCoordsBuffer.Count);

        Vector2Int? bestCoords = null;
        var bestScore = float.MinValue;

        // Second pass: normalize the congestions by the average, subtract one and multiply by -1 to get the congestion penalty
        // The previously visited cell gets -1 weight and it's neighbors get -0.5
        // Add a random weight between 0 and 1
        for (var i = 0; i < _tempCoordsBuffer.Count; i++)
        {
            var coords = _tempCoordsBuffer[i];
            ref var cell = ref _cells[coords.x, coords.y];

            // The vacancy score is +ve for cells with below average congestion and -ve for above average
            var vacancyScore = -1 * (cell.Congestion - congestionRms);

            // The momentum score penalizes the previously visited cell and it's neighbors and thus imparts a momentum on the next pick
            // We use the Chebyshev distance to avoid diagonals getting extra penalties
            var chebyshev = Mathf.Max(Mathf.Abs(coords.x - previousCoords.x), Mathf.Abs(coords.y - previousCoords.y));
            var momentumScore = -1 * (1f - Mathf.InverseLerp(0f, 3f, chebyshev));

            // Convergence
            var moveVector = coords - requestCoords;
            var convergenceVector = _hotSpot - requestCoords;
            var convergenceAngle = Vector2.Angle(moveVector, convergenceVector);
            var convergenceScore = (1f + _convergenceFactor) * Plugin.RaidConvergence.Value * Mathf.InverseLerp(90f, 0f, convergenceAngle);

            // Add some randomization
            var randomization = Random.Range(0f, 1f);

            var score = vacancyScore + momentumScore + convergenceScore + randomization;

            DebugLog.Write(
                $"Cell {coords} score: {score} vac: {vacancyScore} mom: {momentumScore} cnvrg {convergenceScore} cnvrgang {convergenceAngle} rand: {randomization}"
            );

            if (!(score > bestScore)) continue;

            bestScore = score;
            bestCoords = coords;
        }

        DebugLog.Write($"Best pick is {bestCoords} with score: {bestScore}");

        return bestCoords.HasValue ? AssignLocation(bestCoords.Value) : RequestFar();
    }

    public void Return(Location location)
    {
        var coords = WorldToCell(location.Position);
        ref var cell = ref _cells[coords.x, coords.y];

        _coordsByCongestion.Remove(coords);
        cell.Congestion--;
        _coordsByCongestion.Add(coords);

        if (cell.Congestion >= 0) return;

        cell.Congestion = 0;
        DebugLog.Write($"Returning {location} to the pool resulted in negative congestion");
    }

    private Location RequestFar()
    {
        return AssignLocation(_coordsByCongestion.Min);
    }

    private Location AssignLocation(Vector2Int coords)
    {
        ref var cell = ref _cells[coords.x, coords.y];

        _coordsByCongestion.Remove(coords);
        cell.Congestion++;
        _coordsByCongestion.Add(coords);

        return cell.Locations[Random.Range(0, cell.Locations.Count)];
    }

    private bool CheckPathing(Vector2Int center, Vector3 candidatePos)
    {
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        queue.Enqueue(center);
        visited.Add(center);

        var tempPath = new NavMeshPath();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!IsValidCell(current))
                continue;

            var currentCell = _cells[current.x, current.y];

            // Check each location in this cell against the candidate location for traversability
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < currentCell.Locations.Count; i++)
            {
                var location = currentCell.Locations[i];

                if (!NavMesh.CalculatePath(candidatePos, location.Position, NavMesh.AllAreas, tempPath)) continue;

                // Only accept paths that actually arrive at the destination
                if (tempPath.corners.Length > 0 && (tempPath.corners[^1] - location.Position).sqrMagnitude <= 1)
                    return true;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    var coords = new Vector2Int(current.x + dx, current.y + dy);

                    // Returns false if already visited
                    if (visited.Add(coords))
                    {
                        queue.Enqueue(coords);
                    }
                }
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsValidCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _gridSize.x && cell.y >= 0 && cell.y < _gridSize.y;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 CellToWorld(Vector2Int cell)
    {
        var x = _worldOffset.x + (cell.x + 0.5f) * _cellSize;
        var z = _worldOffset.y + (cell.y + 0.5f) * _cellSize;

        return new Vector3(x, 0, z);
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        var x = Mathf.FloorToInt((worldPos.x - _worldOffset.x) / _cellSize);
        var y = Mathf.FloorToInt((worldPos.z - _worldOffset.y) / _cellSize);

        return new Vector2Int(x, y);
    }

    private static void Shuffle<T>(List<T> items)
    {
        // Fisher-Yates in-place shuffle
        for (var i = 0; i < items.Count; i++)
        {
            var randomIndex = Random.Range(i, items.Count);
            (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
        }
    }

    private static List<Location> CollectLocations()
    {
        var collection = new List<Location>();

        DebugLog.Write("Collecting quests POIs");

        _idCounter = 0;

        foreach (var trigger in Object.FindObjectsOfType<TriggerWithId>())
        {
            if (trigger.transform == null)
                continue;

            ValidateAndAddLocation(collection, LocationCategory.Quest, trigger.name, trigger.transform.position);
        }

        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;

            ValidateAndAddLocation(collection, LocationCategory.ContainerLoot, container.name, container.transform.position);
        }

        DebugLog.Write($"Collected {collection.Count} points of interest");

        return collection;
    }

    private static void ValidateAndAddLocation(List<Location> collection, LocationCategory category, string name, Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out var target, 2f, NavMesh.AllAreas))
        {
            var objective = new Location(_idCounter, category, name, target.position);
            collection.Add(objective);
            _idCounter++;
        }
        else
        {
            DebugLog.Write($"Skipping Location({category}, {name}, {position}), too far from navmesh");
        }
    }

    private static Location BuildSyntheticLocation(Vector3 position)
    {
        var location = new Location(_idCounter, LocationCategory.Synthetic, $"Synthetic_{_idCounter}", position);
        _idCounter++;
        return location;
    }

    private class CellCongestionComparer(Cell[,] cells) : IComparer<Vector2Int>
    {
        public int Compare(Vector2Int a, Vector2Int b)
        {
            var cellA = cells[a.x, a.y];
            var cellB = cells[b.x, b.y];

            // Compare congestion and then tie-break by Id
            return cellA.Congestion != cellB.Congestion ? cellA.Congestion.CompareTo(cellB.Congestion) : cellA.Id.CompareTo(cellB.Id);
        }
    }
}