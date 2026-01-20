using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Config;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Helpers;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Location = Phobos.Navigation.Location;
using Random = UnityEngine.Random;

namespace Phobos.Systems;

public struct Cell()
{
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
    private readonly Cell[,] _cells;
    private readonly float _cellSize;
    private readonly float _cellSubSize;
    private readonly Vector2Int _gridSize;
    private readonly Vector2 _worldMin;

    private readonly Queue<Vector2Int> _validCellQueue;
    private readonly Dictionary<Entity, Vector2Int> _assignments;

    private readonly BotsController _botsController;
    private readonly ConfigBundle<LocationConfig.MapZone> _zoneConfig;
    private readonly List<Zone> _zones;
    private readonly Vector2[,] _advectionField;

    private readonly List<Vector2Int> _tempCoordsBuffer = [];
    private readonly LocationGatherer _locationGatherer;

    private readonly List<Player> _humanPlayers;
    private float _convergenceRadius;
    private float _convergenceForce;
    private readonly Vector2[,] _convergenceField;
    private readonly TimePacing _convergenceUpdatePacing = new(30f);

    public Cell[,] Cells => _cells;
    public Vector2Int GridSize => _gridSize;
    public Vector2 WorldMin => _worldMin;
    public float CellSize => _cellSize;
    public Vector2[,] AdvectionField => _advectionField;
    public Vector2[,] ConvergenceField => _convergenceField;
    public List<Zone> Zones => _zones;

    public LocationSystem(string mapId, PhobosConfig phobosConfig, BotsController botsController, List<Player> humanPlayers)
    {
        _zoneConfig = phobosConfig.Location.MapZones[mapId];
        _botsController = botsController;
        _humanPlayers = humanPlayers;

        Log.Info("Gathering built in locations");
        _locationGatherer = new LocationGatherer(_cellSize);
        // Add the builtin locations
        var builtinLocations = _locationGatherer.CollectBuiltinLocations();

        Log.Info("Calculating world geometry");
        var geometryConfig = phobosConfig.Location.MapGeometries.Value[mapId];

        // Calculate bounds from positions
        _worldMin = geometryConfig.Min;
        var worldMax = geometryConfig.Max;

        for (var i = 0; i < builtinLocations.Count; i++)
        {
            var pos = builtinLocations[i].Position;
            _worldMin.x = Mathf.Min(_worldMin.x, pos.x);
            _worldMin.y = Mathf.Min(_worldMin.y, pos.z);
            worldMax.x = Mathf.Max(worldMax.x, pos.x);
            worldMax.y = Mathf.Max(worldMax.y, pos.z);
        }

        var worldWidth = worldMax.x - _worldMin.x;
        var worldHeight = worldMax.y - _worldMin.y;

        // Take the minimum of the three constraints
        _cellSize = geometryConfig.CellSize;
        _cellSubSize = _cellSize / 2f;

        // Calculate resulting grid dimensions
        var cols = Mathf.CeilToInt(worldWidth / _cellSize);
        var rows = Mathf.CeilToInt(worldHeight / _cellSize);

        _gridSize = new Vector2Int(cols, rows);
        _cells = new Cell[cols, rows];

        var searchRadius = Math.Max(worldWidth, worldHeight) / 2f;

        Log.Info("Constructing location system cells");
        // Cell initialization
        for (var x = 0; x < cols; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                _cells[x, y] = new Cell();
            }
        }

        Log.Info("Populating cells with builtin locations");
        for (var i = 0; i < builtinLocations.Count; i++)
        {
            var location = builtinLocations[i];
            var coords = WorldToCell(location.Position);

            if (!IsValidCell(coords))
            {
                Log.Warning($"{location} with coords {coords} doesn't fall inside valid cell (grid size {_gridSize})");
                continue;
            }

            _cells[coords.x, coords.y].Locations.Add(location);
        }

        // Loop through all the cells and try to populate them with synthetic locations if there aren't any builtin ones
        Log.Debug("Populating cells with synthetic locations");
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

                Log.Info($"Cell at [{x}, {y}] has no builtin locations, attempting to create a synthetic cell center");

                // Try to find a navmesh position as close to the cell center as possible.
                var worldPos = CellToWorld(cellCoords);

                if (NavMesh.SamplePosition(worldPos, out var hit, searchRadius, NavMesh.AllAreas))
                {
                    if (WorldToCell(hit.position) == cellCoords)
                    {
                        if (PopulateCell(cell, cellCoords, hit.position)) continue;
                    }
                }

                Log.Info($"Cell {cellCoords}: no reachable synthetic locations found");
            }
        }

        _assignments = new Dictionary<Entity, Vector2Int>();

        
        // Advection
        _zones = [];
        _advectionField = new Vector2[_gridSize.x, _gridSize.y];
        CalculateAdvectionZones();
        
        // Convergence
        _convergenceRadius = _zoneConfig.Value.Convergence.Radius.SampleUniform();
        _convergenceForce = _zoneConfig.Value.Convergence.Force.SampleUniform();
        _convergenceField = new Vector2[_gridSize.x, _gridSize.y];

        Log.Info($"Location grid size: {_gridSize}, cell size: {_cellSize:F1}, locations: {builtinLocations.Count}");
        Log.Info($"Location grid world bounds: [{_worldMin.x:F0},{_worldMin.y:F0}] -> [{worldMax.x:F0},{worldMax.y:F0}]");
        Log.Info($"Location grid world size: {worldWidth:F0}x{worldHeight:F0} search radius: {searchRadius}");
        Log.Info($"Convergence radius: {_convergenceRadius} force: {_convergenceForce}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update()
    {
        if (_convergenceUpdatePacing.Blocked())
            return;

        Log.Debug("Updating convergence field");
        CalculateConvergence();
    }

    public void ReloadConfig()
    {
        _zoneConfig.Reload();
        
        var convergenceConfig = _zoneConfig.Value.Convergence;
        
        if (!convergenceConfig.Enabled)
        {
            for (var x = 0; x < _gridSize.x; x++)
            {
                for (var y = 0; y < _gridSize.y; y++)
                {
                    _convergenceField[x, y] = Vector2.zero;
                }
            }
            
            return;
        }

        _convergenceRadius = convergenceConfig.Radius.SampleUniform();
        _convergenceForce = convergenceConfig.Force.SampleUniform();
        Log.Info($"Convergence radius: {_convergenceRadius} force: {_convergenceForce}");
    }

    public void CalculateConvergence()
    {
        if (!_zoneConfig.Value.Convergence.Enabled)
            return;

        // Collect the unique player coordinates we have. Players will often be concentrated in one cell, there's no point in calculating separate
        // convergence for them all in this case.
        _tempCoordsBuffer.Clear();
        for (var i = 0; i < _humanPlayers.Count; i++)
        {
            var player = _humanPlayers[i];

            if (player?.HealthController is not { IsAlive: true })
            {
                continue;
            }

            var playerCoords = WorldToCell(player.Position);

            if (_tempCoordsBuffer.Count > 0 && _tempCoordsBuffer.Contains(playerCoords))
            {
                continue;
            }
            
            _tempCoordsBuffer.Add(playerCoords);
        }
        
        var normRadius = Plugin.ConvergenceRadiusScale.Value * _convergenceRadius;
        var forceScale = Plugin.ConvergenceForceScale.Value * _convergenceForce;

        for (var x = 0; x < _gridSize.x; x++)
        {
            for (var y = 0; y < _gridSize.y; y++)
            {
                var cellCoords = new Vector2(x, y);
                var convergenceVector = Vector2.zero;

                // Add up all the hotspot contributions to this cell
                for (var i = 0; i < _tempCoordsBuffer.Count; i++)
                {
                    var playerCoords = _tempCoordsBuffer[i];
                    var playerVector = playerCoords - cellCoords;
                    var inverseDistSqrFactor = 1 - Mathf.Min(playerVector.magnitude * _cellSize / normRadius, 1f);
                    playerVector.Normalize();
                    playerVector *= Mathf.Sqrt(inverseDistSqrFactor);
                    convergenceVector += playerVector;
                }
                convergenceVector /= _humanPlayers.Count;
                convergenceVector *= forceScale;

                _convergenceField[x, y] = convergenceVector;
            }
        }
    }

    public void CalculateAdvectionZones()
    {
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

            var coords = WorldToCell(customZone.Position);

            if (!IsValidCell(coords))
            {
                Log.Debug($"Custom zone at {customZone.Position} with cell coords {coords} falls outside of bounds {_gridSize}");
                continue;
            }

            var zone = new Zone(
                coords,
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
                    var force = Mathf.Clamp01(1f - worldDist / (zone.Radius * Plugin.AdvectionZoneRadiusScale.Value));
                    // Apply a decay factor (1 is linear, <1 sublinear and >1 exponential).
                    force = Mathf.Pow(force, zone.Decay * Plugin.AdvectionZoneRadiusDecayScale.Value);
                    force *= zone.Force * Plugin.AdvectionZoneForceScale.Value;
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

    public Location RequestNear(Entity entity, Vector3 worldPos, Location previous)
    {
        // Always try and return assignments first to avoid counting our own influence into the decision
        Return(entity);

        var requestCoords = WorldToCell(worldPos);

        if (!IsValidCell(requestCoords))
        {
            return RequestFar(entity);
        }

        var previousCoords = previous == null ? requestCoords : WorldToCell(previous.Position);
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

        var advectionVector = _advectionField[requestCoords.x, requestCoords.y];
        var convergenceVector = _convergenceField[requestCoords.x, requestCoords.y];
        var randomization = Random.insideUnitCircle;
        randomization *= 0.5f;
        var momentumVector = (Vector2)(requestCoords - previousCoords);
        momentumVector.Normalize();
        momentumVector *= 0.5f;

        var prefDirection = convergenceVector + momentumVector + advectionVector + randomization;

        Log.Debug(
            $"Location search from {requestCoords} direction: {prefDirection} conv: {convergenceVector} mom: {momentumVector} adv: {advectionVector} rand: {randomization}"
        );

        if (_tempCoordsBuffer.Count == 0 || prefDirection == Vector2.zero)
        {
            Log.Debug("Zero vector preferred direction, trying the current cell, and failing that the map-wide least congested cell");
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
        Log.Debug($"Returning the assignment for {entity} to the pool resulted in negative congestion");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        var x = Mathf.FloorToInt((worldPos.x - _worldMin.x) / _cellSize);
        var y = Mathf.FloorToInt((worldPos.z - _worldMin.y) / _cellSize);

        return new Vector2Int(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2Int WorldToCell(Vector2 worldPos)
    {
        var x = Mathf.FloorToInt((worldPos.x - _worldMin.x) / _cellSize);
        var y = Mathf.FloorToInt((worldPos.y - _worldMin.y) / _cellSize);

        return new Vector2Int(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 CellToWorld(Vector2Int cell)
    {
        var x = _worldMin.x + (cell.x + 0.5f) * _cellSize;
        var z = _worldMin.y + (cell.y + 0.5f) * _cellSize;

        return new Vector3(x, 0, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 CellToWorld(int x, int y)
    {
        var xWorld = _worldMin.x + (x + 0.5f) * _cellSize;
        var zWorld = _worldMin.y + (y + 0.5f) * _cellSize;

        return new Vector3(xWorld, 0, zWorld);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsValidCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _gridSize.x && cell.y >= 0 && cell.y < _gridSize.y;
    }

    private Location RequestFar(Entity entity)
    {
        var pick = _validCellQueue.Dequeue();
        _validCellQueue.Enqueue(pick);
        var location = AssignLocation(entity, pick);
        Log.Debug($"Requesting {location} in far cell {pick}");
        return location;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Location AssignLocation(Entity entity, Vector2Int coords)
    {
        ref var cell = ref _cells[coords.x, coords.y];
        cell.Congestion += 1;
        PropagateForce(coords, 1f);
        _assignments[entity] = coords;
        Log.Debug($"Assigning location in {coords}");
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
                var distanceNorm = direction.sqrMagnitude;

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

                cell.Locations.Add(_locationGatherer.CreateSyntheticLocation(hit.position));
                pointsFound++;
            }
        }

        Log.Debug($"Cell {cellCoords}: found a total of {pointsFound} synthetic points");

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