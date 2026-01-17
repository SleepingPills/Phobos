using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;
using Phobos.Diag;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Phobos.Navigation;

public class LocationGatherer(float cellSize)
{
    private static int _idCounter;

    public List<Location> CollectBuiltinLocations()
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

        DebugLog.Write("Collecting loot POIs");

        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;

            ValidateAndAddLocation(collection, LocationCategory.ContainerLoot, container.name, container.transform.position);
        }

        DebugLog.Write("Collecting exfil POIs");

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

        DebugLog.Write($"Found {uniqueExfils.Count} exfils");

        foreach (var exfil in uniqueExfils)
        {
            DebugLog.Write($"Trying to add exfil {exfil.Point.name} with ID {exfil.Point.Id}");
            ValidateAndAddLocation(collection, LocationCategory.Exfil, exfil.Point.name, exfil.Point.transform.position, 5f);
        }

        DebugLog.Write($"Collected {collection.Count} points of interest");
        
        Shuffle(collection);

        return collection;
    }

    public Location CreateSyntheticLocation(Vector3 position)
    {
        var radius = Mathf.Max(10f, cellSize / 2f);
        var radiusSqr = radius * radius;
        var location = new Location(_idCounter, LocationCategory.Synthetic, $"Synthetic_{_idCounter}", position, radiusSqr);
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
            DebugLog.Write($"Skipping Location({category}, {name}, {position}), too far from navmesh");
        }
    }

    private Location CreateBuiltinLocation(LocationCategory category, string name, Vector3 position)
    {
        var radius = category switch
        {
            LocationCategory.ContainerLoot => 10f,
            LocationCategory.LooseLoot => 10f,
            LocationCategory.Quest => Mathf.Max(10f, cellSize / 2f),
            LocationCategory.Synthetic => Mathf.Max(10f, cellSize / 2f),
            LocationCategory.Exfil => Mathf.Max(10f, cellSize / 2f),
            _ => 10f
        };
        var radiusSqr = radius * radius;
        var location = new Location(_idCounter, category, name, position, radiusSqr);
        _idCounter++;
        return location;
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

    private readonly struct CoverData(List<Door> doors, List<GroupPoint> coverPoints)
    {
        public readonly List<Door> Doors = doors;
        public readonly List<GroupPoint> CoverPoints = coverPoints;
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