using System.Collections.Generic;
using EFT.Interactive;
using Phobos.Diag;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Phobos.Navigation;

public class LocationQueue
{
    private readonly Queue<Location> _queue;
    private readonly HashSet<Location> _dupeCheck = [];

    public LocationQueue()
    {
        var objectives = Collect();
        Shuffle(objectives);
        _queue = new Queue<Location>(objectives);
    }

    public Location Next()
    {
        var objective = _queue.Dequeue();
        _queue.Enqueue(objective);
        return objective;
    }
    
    private static void Shuffle(List<Location> objectives)
    {
        // Fisher-Yates in-place shuffle
        for (var i = 0; i < objectives.Count; i++)
        {
            var randomIndex = Random.Range(i, objectives.Count);
            (objectives[i], objectives[randomIndex]) = (objectives[randomIndex], objectives[i]);
        }
    }
    
    private List<Location> Collect()
    {
        var collection = new List<Location>();

        DebugLog.Write("Collecting quests POIs");
        
        var idCounter = 0;

        foreach (var trigger in Object.FindObjectsOfType<TriggerWithId>())
        {
            if (trigger.transform == null)
                continue;

            AddValid(idCounter, collection, LocationCategory.Quest, trigger.name, trigger.transform.position);

            idCounter++;
        }
        
        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;
            
            AddValid(idCounter, collection, LocationCategory.ContainerLoot, container.name, container.transform.position);
            
            idCounter++;
        }
        
        DebugLog.Write($"Collected {collection.Count} points of interest");
        
        return collection;
    }

    private void AddValid(int idCounter, List<Location> collection, LocationCategory category, string name, Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out var target, 5f, NavMesh.AllAreas))
        {
            var objective = new Location(idCounter, category, name, target.position);
            
            if (!_dupeCheck.Add(objective))
            {
                DebugLog.Write($"{objective} skipped as duplicate");
                return;
            }
            collection.Add(objective);
            DebugLog.Write($"{objective} added as location");
        }
        else
        {
            DebugLog.Write($"Objective({category}, {name}, {position}) too far from navmesh");
        }
    }
}