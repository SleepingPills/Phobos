using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Interactive;
using Phobos.Diag;
using Phobos.Helpers;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Phobos.Navigation;

public class LocationGatherer(float cellSize, BotsController botsController)
{
    private static int _idCounter;

    public List<Location> CollectBuiltinLocations()
    {
        var collection = new List<Location>();

        Log.Debug("Collecting quests POIs");

        _idCounter = 0;

        foreach (var trigger in Object.FindObjectsOfType<TriggerWithId>())
        {
            if (trigger.transform == null)
                continue;

            ValidateAndAddLocation(collection, LocationCategory.Quest, trigger.name, trigger.transform.position);
        }

        Log.Debug("Collecting loot POIs");

        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;

            ValidateAndAddLocation(collection, LocationCategory.ContainerLoot, container.name, container.transform.position);
        }

        Log.Debug("Collecting exfil POIs");

        var uniqueExfils = new HashSet<Exfil>();

        foreach (var point in LocationScene.GetAllObjects<ExfiltrationPoint>())
        {
            // Skip non-shared scav exfils to mirror the BSG logic in ExfiltrationControllerClass 
            if (point is ScavExfiltrationPoint and not SharedExfiltrationPoint)
            {
                continue;
            }

            uniqueExfils.Add(new Exfil(point));
        }

        Log.Debug($"Found {uniqueExfils.Count} exfils");

        foreach (var exfil in uniqueExfils)
        {
            Log.Debug($"Trying to add exfil {exfil.Point.name} with ID {exfil.Point.Id}");
            ValidateAndAddLocation(collection, LocationCategory.Exfil, exfil.Point.name, exfil.Point.transform.position, 5f);
        }

        Log.Debug($"Collected {collection.Count} points of interest");

        Shuffle(collection);

        return collection;
    }

    public Location CreateSyntheticLocation(Vector3 position)
    {
        var radius = Mathf.Clamp(cellSize / 2f, 10f, 25f);
        var radiusSqr = radius * radius;
        var name = $"Synthetic_{_idCounter}";
        const LocationCategory category = LocationCategory.Synthetic;

        var coverData = CollectBuiltinCoverData(position, radius);
        
        if (coverData.CoverPoints.Count < 16)
        {
            var extraPoints = 16 - coverData.CoverPoints.Count;
            CollectSyntheticCoverData(coverData.CoverPoints, position, radius, extraPoints);
        }
        
        if (coverData.CoverPoints.Count == 0)
        {
            Log.Debug($"Location {category}:{name} has 0 cover points in proximity");
        }
        
        var location = new Location(_idCounter, category, name, position, radiusSqr, coverData.Doors, coverData.CoverPoints);
        _idCounter++;
        return location;
    }

    private Location CreateBuiltinLocation(LocationCategory category, string name, Vector3 position)
    {
        var radius = category switch
        {
            LocationCategory.ContainerLoot => 10f,
            LocationCategory.LooseLoot => 10f,
            LocationCategory.Quest => Mathf.Clamp(cellSize / 2f, 10f, 15f),
            LocationCategory.Synthetic => Mathf.Clamp(cellSize / 2f, 10f, 15f),
            LocationCategory.Exfil => Mathf.Clamp(cellSize / 2f, 10f, 15f),
            _ => 10f
        };
        
        var coverData = CollectBuiltinCoverData(position, radius);
        
        if (coverData.CoverPoints.Count < 16)
        {
            var extraPoints = 16 - coverData.CoverPoints.Count;
            CollectSyntheticCoverData(coverData.CoverPoints, position, radius, extraPoints);
        }
        
        if (coverData.CoverPoints.Count == 0)
        {
            Log.Debug($"Location {category}:{name} has 0 cover points in proximity");
        }

        var radiusSqr = radius * radius;
        var location = new Location(_idCounter, category, name, position, radiusSqr, coverData.Doors, coverData.CoverPoints);
        _idCounter++;
        return location;
    }

    private void ValidateAndAddLocation(List<Location> collection, LocationCategory category, string name, Vector3 position, float maxDistance = 2f)
    {
        if (NavMesh.SamplePosition(position, out var target, maxDistance, NavMesh.AllAreas))
        {
            var objective = CreateBuiltinLocation(category, name, target.position);
            collection.Add(objective);
        }
        else
        {
            Log.Debug($"Skipping Location({category}, {name}, {position}), too far from navmesh");
        }
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

    private CoverData CollectBuiltinCoverData(Vector3 position, float radius)
    {
        // The voxel shape is actually 10x5x10 but we just round it out for out purposes.
        const float voxelSize = 10f;
        var innerRadius = 0.75f * radius;
        var voxelSearchRange = Mathf.CeilToInt(2f * radius / voxelSize);
        var voxelIndex = botsController.CoversData.GetIndexes(position);

        var neighborVoxels = botsController.CoversData.GetVoxelesExtended(
            voxelIndex.x, voxelIndex.y, voxelIndex.z, voxelSearchRange, true
        );

        var doors = new HashSet<Door>();
        var coverPoints = new HashSet<CoverPoint>();

        for (var i = 0; i < neighborVoxels.Count; i++)
        {
            var voxel = neighborVoxels[i];

            for (var j = 0; j < voxel.DoorLinks.Count; j++)
            {
                var doorLink = voxel.DoorLinks[j];

                if ((doorLink.Door.transform.position - position).magnitude > radius)
                    continue;

                doors.Add(doorLink.Door);
            }

            for (var j = 0; j < voxel.Points.Count; j++)
            {
                var groupPoint = voxel.Points[j];

                if ((groupPoint.Position - position).magnitude > innerRadius)
                {
                    continue;
                }
                
                if (!IsReachable(position, groupPoint.Position, innerRadius))
                {
                    continue;
                }

                var coverPoint = new CoverPoint(groupPoint.Position, groupPoint.WallDirection, groupPoint.CoverType, groupPoint.CoverLevel);

                coverPoints.Add(coverPoint);
            }
        }

        return new CoverData(doors.ToList(), coverPoints.ToList());
    }

    private static void CollectSyntheticCoverData(List<CoverPoint> coverPoints, Vector3 locationPosition, float radius, int count)
    {
        var innerRadius = 0.75f * radius;
        var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        // 0.5 × r × √(π/N) ~= 0.886 × r / √N
        var navmeshSampleEps = 0.886f * innerRadius / Mathf.Sqrt(count);
        
        // Sunflower seed pattern
        for (var i = 0; i < count; i++)
        {
            var theta = i * goldenAngle;
            var r = innerRadius * Mathf.Sqrt((float)i / count);
            
            var x = locationPosition.x + r * Mathf.Cos(theta);
            var z = locationPosition.z + r * Mathf.Sin(theta);
            
            var candidatePosition = new Vector3(x, locationPosition.y, z);
            
            if (!NavMesh.SamplePosition(candidatePosition, out var target, navmeshSampleEps, NavMesh.AllAreas))
            {
                continue;
            }

            if (!IsReachable(locationPosition, target.position, innerRadius))
            {
                continue;
            }

            var coverPoint = new CoverPoint(target.position, Vector3.zero, CoverCategory.None, CoverLevel.Lay);
            coverPoints.Add(coverPoint);
        }

        // Add the location itself as a cover point if nothing else is available
        if (coverPoints.Count == 0)
        {
            coverPoints.Add(new CoverPoint(locationPosition, Vector3.zero, CoverCategory.None, CoverLevel.Lay));
        }
    }

    private static bool IsReachable(Vector3 origin, Vector3 target, float lengthLimit)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, path))
        {
            return false;
        }

        return path.status == NavMeshPathStatus.PathComplete && PathHelper.TotalLength(path.corners) <= lengthLimit;
    }

    private readonly struct CoverData(List<Door> doors, List<CoverPoint> coverPoints)
    {
        public readonly List<Door> Doors = doors;
        public readonly List<CoverPoint> CoverPoints = coverPoints;
    }

    private readonly struct Exfil(ExfiltrationPoint point) : IEquatable<Exfil>
    {
        private readonly MongoID _id = point.Id;

        public readonly ExfiltrationPoint Point = point;

        public bool Equals(Exfil other)
        {
            return _id.Equals(other._id);
        }

        public override bool Equals(object obj)
        {
            return obj is Exfil other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}