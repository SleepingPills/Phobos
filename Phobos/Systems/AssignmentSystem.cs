using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using EFT.Interactive;
using Phobos.Config;
using Phobos.Diag;
using Phobos.Entities;
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

public class AssignmentSystem
{
    private readonly Cell[,] _cells;
    private readonly float _cellSize;
    private readonly float _cellSubSize;
    private readonly Vector2Int _gridSize;
    private readonly Vector2 _worldOffset; // Bottom-left corner in world space
    private readonly Vector2 _worldMin;
    private readonly Vector2 _worldMax;

    private readonly Queue<Vector2Int> _validCellQueue;
    private readonly Dictionary<Entity, Vector2Int> _assignments;

    private readonly List<Vector2Int> _tempCoordsBuffer = [];

    private readonly BotsController _botsController;
    private readonly ConfigBundle<LocationConfig.MapZone> _zoneConfig;
    private readonly List<Zone> _zones;
    private readonly Vector2[,] _advectionField;

    private static int _idCounter;

    public Cell[,] Cells => _cells;
    public Vector2Int GridSize => _gridSize;
    public Vector2 WorldMin => _worldMin;
    public Vector2 WorldMax => _worldMax;
    public Vector2[,] AdvectionField => _advectionField;
    public List<Zone> Zones => _zones;

    public AssignmentSystem(string mapId, PhobosConfig phobosConfig, BotsController botsController)
    {
        _zoneConfig = phobosConfig.Location.MapZones[mapId];
        _botsController = botsController;

        var geometryConfig = phobosConfig.Location.MapGeometries.Value[mapId];

        // Calculate bounds from positions
        _worldMin = geometryConfig.Min;
        _worldMax = geometryConfig.Max;

        _worldOffset = new Vector2(_worldMin.x, _worldMin.y);

        var worldWidth = _worldMax.x - _worldMin.x;
        var worldHeight = _worldMax.y - _worldMin.y;

        // Take the minimum of the three constraints
        _cellSize = geometryConfig.CellSize;
        _cellSubSize = _cellSize / 2f;

        // Calculate resulting grid dimensions
        var cols = Mathf.CeilToInt(worldWidth / _cellSize);
        var rows = Mathf.CeilToInt(worldHeight / _cellSize);

        _gridSize = new Vector2Int(cols, rows);
        _cells = new Cell[cols, rows];

        var searchRadius = Math.Max(worldWidth, worldHeight) / 2f;

        // Cell initialization
        var cellId = 0;
        for (var x = 0; x < cols; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                _cells[x, y] = new Cell(cellId);
                cellId++;
            }
        }

        // Add the builtin locations
        var locations = CollectLocations();
        Shuffle(locations);

        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            var coords = WorldToCell(location.Position);
            _cells[coords.x, coords.y].Locations.Add(location);
        }

        // Loop through all the cells and try to populate them with synthetic locations if there aren't any builtin ones
        _validCellQueue = new Queue<Vector2Int>();
        for (var x = 0; x < _gridSize.x; x++)
        {
            for (var y = 0; y < _gridSize.y; y++)
            {
                var cellCoords = new Vector2Int(x, y);
                ref var cell = ref _cells[cellCoords.x, cellCoords.y];

                // If we already have builting locations, bail out
                if (cell.HasLocations)
                {
                    _validCellQueue.Enqueue(cellCoords);
                    continue;
                }

                DebugLog.Write($"Cell at [{x}, {y}] has no builtin locations, attempting to create a synthetic cell center");

                // Try to find a navmesh position as close to the cell center as possible.
                var worldPos = CellToWorld(cellCoords);

                if (NavMesh.SamplePosition(worldPos, out var hit, searchRadius, NavMesh.AllAreas))
                {
                    if (WorldToCell(hit.position) == cellCoords)
                    {
                        if (PopulateCell(cell, cellCoords, hit.position)) continue;
                    }
                }

                DebugLog.Write($"Cell {cellCoords}: no reachable synthetic locations found");
            }
        }

        _assignments = new Dictionary<Entity, Vector2Int>();
        
        // Zones
        _zones = [];
        _advectionField = new Vector2[_gridSize.x, _gridSize.y];
        CalculateZones();

        DebugLog.Write($"Location grid size: {_gridSize}, cell size: {_cellSize:F1}, locations: {locations.Count}");
        DebugLog.Write($"Location grid world bounds: [{_worldMin.x:F0},{_worldMin.y:F0}] -> [{_worldMax.x:F0},{_worldMax.y:F0}]");
        DebugLog.Write($"Location grid world size: {worldWidth:F0}x{worldHeight:F0} search radius: {searchRadius}");
    }

    public Location RequestNear(Entity entity, Vector3 worldPos, Location previous)
    {
        var requestCoords = WorldToCell(worldPos);
        var previousCoords = previous == null ? WorldToCell(worldPos) : WorldToCell(previous.Position);

        DebugLog.Write($"Requesting location around {requestCoords} | {worldPos} with previous coords {previousCoords}");

        _tempCoordsBuffer.Clear();

        // First pass: determine preferential direction
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var direction = new Vector2Int(dx, dy);
                var coords = requestCoords + direction;

                if (!IsValidCell(coords))
                    continue;

                ref var cell = ref _cells[coords.x, coords.y];

                if (!cell.HasLocations)
                    continue;

                _tempCoordsBuffer.Add(direction);
            }
        }

        var randomization = Random.insideUnitCircle;
        var advectionVector = _advectionField[requestCoords.x, requestCoords.y];
        var momentumVector = (Vector2)(requestCoords - previousCoords);
        momentumVector.Normalize();

        var prefDirection = momentumVector + advectionVector + randomization;

        DebugLog.Write($"Direction: {prefDirection} mom: {momentumVector} adv: {advectionVector} rand: {randomization}");

        if (prefDirection == Vector2.zero)
        {
            DebugLog.Write("Zero vector preferred direction, trying the current cell, and failing that the map-wide least congested cell");
            // We can't go to any neighboring cell for some reason, grab something from the current cell, and if that fails too, search map-wide. 
            var currentCell = _cells[requestCoords.x, requestCoords.y];
            return currentCell.HasLocations ? AssignLocation(entity, requestCoords) : RequestFar(entity);
        }

        prefDirection.Normalize();

        Vector2Int? bestNeighbor = null;
        var bestAngle = float.MaxValue;

        // Second pass: find the neighboring cell closest to the picked direction
        for (var i = 0; i < _tempCoordsBuffer.Count; i++)
        {
            var candidateDirection = _tempCoordsBuffer[i];
            var angle = Vector2.Angle(candidateDirection, prefDirection);

            if (angle >= bestAngle) continue;

            bestAngle = angle;
            bestNeighbor = requestCoords + candidateDirection;
        }

        DebugLog.Write($"Best pick is {bestNeighbor} with angle: {bestAngle}");

        return bestNeighbor.HasValue ? AssignLocation(entity, bestNeighbor.Value) : RequestFar(entity);
    }

    public void Return(Entity entity)
    {
        if (!_assignments.Remove(entity, out var coords))
        {
            return;
        }

        ref var cell = ref _cells[coords.x, coords.y];

        cell.Congestion--;
        PropagateForce(coords, -1f);

        if (cell.Congestion >= 0) return;

        cell.Congestion = 0;
        DebugLog.Write($"Returning the assignment for {entity} to the pool resulted in negative congestion");
    }

    public void CalculateZones()
    {
        _zoneConfig.Reload();
        _zones.Clear();

        for (var i = 0; i < _botsController.BotSpawner.AllBotZones.Length; i++)
        {
            var botZone = _botsController.BotSpawner.AllBotZones[i];

            if (!_zoneConfig.Value.BuiltinZones.TryGetValue(botZone.name, out var builtinZone))
                continue;

            var minRadius = Mathf.Min(builtinZone.Radius.Min, builtinZone.Radius.Max);

            if (minRadius < 1)
            {
                throw new ArgumentException("The zone radius must be greater than or equal to 1");
            }

            var zone = new Zone(
                WorldToCell(botZone.CenterOfSpawnPoints),
                builtinZone.Radius.SampleGaussian(),
                builtinZone.Force.SampleGaussian(),
                builtinZone.Decay
            );
            _zones.Add(zone);
        }

        for (var i = 0; i < _zoneConfig.Value.CustomZones.Count; i++)
        {
            var customZone = _zoneConfig.Value.CustomZones[i];

            var minRadius = Mathf.Min(customZone.Radius.Min, customZone.Radius.Max);

            if (minRadius < 1)
            {
                throw new ArgumentException("The zone radius must be greater than or equal to 1");
            }

            var zone = new Zone(
                WorldToCell(customZone.Position),
                customZone.Radius.SampleGaussian(),
                customZone.Force.SampleGaussian(),
                customZone.Decay
            );
            _zones.Add(zone);
        }

        for (var x = 0; x < _gridSize.x; x++)
        {
            for (var y = 0; y < _gridSize.y; y++)
            {
                var cellCoords = new Vector2(x, y);

                _advectionField[x, y] = Vector2.zero;

                // Add up all the hotspot contributions to this cell
                for (var i = 0; i < _zones.Count; i++)
                {
                    var zone = _zones[i];
                    var zoneCoords = (Vector2)zone.Coords;
                    // Get the world space distance between the hotspot and the current cell
                    var worldDist = Vector2.Distance(zoneCoords, cellCoords) * _cellSize;
                    // The force is the cartesian distance to the hotspot normalized by the hotspot radius and clamped 
                    var force = Mathf.Clamp01(1f - worldDist / (zone.Radius * Plugin.ZoneRadiusScale.Value));
                    // Apply a decay factor (1 is linear, <1 sublinear and >1 exponential).
                    force = Mathf.Pow(force, zone.Decay * Plugin.ZoneRadiusDecayScale.Value);
                    force *= zone.Force * Plugin.ZoneForceScale.Value;
                    // Accumulate the advection
                    _advectionField[x, y] += force * (zoneCoords - cellCoords).normalized;
                }
            }
        }

        // Propagate the forces for each assignment
        foreach (var coords in _assignments.Values)
        {
            PropagateForce(coords, 1f);
        }
    }

    private Location RequestFar(Entity entity)
    {
        var pick = _validCellQueue.Dequeue();
        _validCellQueue.Enqueue(pick);
        var location = AssignLocation(entity, pick);
        DebugLog.Write($"Requesting {location} in far cell {pick}");
        return location;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Location AssignLocation(Entity entity, Vector2Int coords)
    {
        ref var cell = ref _cells[coords.x, coords.y];
        cell.Congestion += 1;
        PropagateForce(coords, 1f);
        _assignments[entity] = coords;
        return cell.Locations[Random.Range(0, cell.Locations.Count)];
    }

    private void PropagateForce(Vector2Int sourceCoords, float forceMul, int range = 3)
    {
        const float baseForce = 0.5f;
        var maxForce = forceMul * baseForce;
        
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dy = -range; dy <= range; dy++)
            {
                // Skip source cell
                if (dx == 0 && dy == 0) continue;

                var targetCoords = new Vector2Int(sourceCoords.x + dx, sourceCoords.y + dy);

                // Skip invalid cells
                if (!IsValidCell(targetCoords)) continue;

                // Direction from source to target in cell coordinates
                var direction = new Vector2(dx, dy);
                var distanceNorm = direction.magnitude;

                // Normalize direction and apply inverse squared distance falloff
                var force = direction.normalized * maxForce / distanceNorm;

                // Accumulate into advection field
                _advectionField[targetCoords.x, targetCoords.y] += force;
            }
        }
    }

    private bool PopulateCell(Cell cell, Vector2Int cellCoords, Vector3 centerPoint)
    {
        var pointsFound = 0;

        const float resolution = 3;
        var spacing = _cellSubSize / (resolution - 1);
        var halfSize = _cellSubSize / 2f;

        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var xOffset = x * spacing - halfSize;
                var zOffset = z * spacing - halfSize;

                var candidatePoint = new Vector3(centerPoint.x + xOffset, centerPoint.y, centerPoint.z + zOffset);

                if (!NavMesh.SamplePosition(candidatePoint, out var hit, _cellSubSize, NavMesh.AllAreas))
                    continue;

                if (WorldToCell(hit.position) != cellCoords)
                    continue;

                if (!CheckPathing(cellCoords, hit.position))
                    continue;

                cell.Locations.Add(BuildSyntheticLocation(hit.position));
                pointsFound++;
            }
        }

        DebugLog.Write($"Cell {cellCoords}: found a total of {pointsFound} synthetic points");

        return pointsFound > 0;
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
                if (tempPath.corners.Length > 0 && (tempPath.corners[^1] - location.Position).sqrMagnitude <= 1f)
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

    private Vector2Int WorldToCell(Vector2 worldPos)
    {
        var x = Mathf.FloorToInt((worldPos.x - _worldOffset.x) / _cellSize);
        var y = Mathf.FloorToInt((worldPos.y - _worldOffset.y) / _cellSize);

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

    public readonly struct Zone(Vector2Int coords, float radius, float force, float decay)
    {
        public readonly Vector2Int Coords = coords;
        public readonly float Radius = radius;
        public readonly float Force = force;
        public readonly float Decay = decay;

        public override string ToString()
        {
            return $"Zone(position: {Coords}, radius: {Radius}, force: {Force}, decay: {Decay})";
        }
    }
}