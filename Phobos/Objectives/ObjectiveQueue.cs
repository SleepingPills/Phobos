using System;
using System.Collections.Generic;
using EFT.Interactive;
using Phobos.Diag;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Phobos.Objectives;

public class ObjectiveQueue
{
    private readonly Queue<Objective> _queue;
    private readonly HashSet<ValueTuple<LocationCategory, string>> _dupeCheck = new ();

    public ObjectiveQueue()
    {
        var objectives = Collect();
        Shuffle(objectives);
        _queue = new Queue<Objective>(objectives);
    }

    public Objective Next()
    {
        var objective = _queue.Dequeue();
        _queue.Enqueue(objective);
        return objective;
    }
    
    private static void Shuffle(List<Objective> objectives)
    {
        // Fisher-Yates in-place shuffle
        for (var i = 0; i < objectives.Count; i++)
        {
            var randomIndex = Random.Range(i, objectives.Count);
            (objectives[i], objectives[randomIndex]) = (objectives[randomIndex], objectives[i]);
        }
    }
    
    private List<Objective> Collect()
    {
        var collection = new List<Objective>();

        DebugLog.Write("Collecting quests POIs");

        foreach (var trigger in Object.FindObjectsOfType<TriggerWithId>())
        {
            if (trigger.transform == null)
                continue;

            var objectiveId = (LocationCategory.Quest, trigger.name);
            AddValid(collection, objectiveId, trigger.transform.position);
        }
        
        foreach (var container in Object.FindObjectsOfType<LootableContainer>())
        {
            if (container.transform == null || !container.enabled || container.Template == null)
                continue;
            
            var objectiveId = (LocationCategory.ContainerLoot, container.name);
            AddValid(collection, objectiveId, container.transform.position);
        }
        
        DebugLog.Write($"Collected {collection.Count} points of interest");
        
        return collection;
    }

    private void AddValid(List<Objective> collection, ValueTuple<LocationCategory, string> id, Vector3 position)
    {
        if (!_dupeCheck.Add(id))
        {
            DebugLog.Write($"Objective {id} skipped as duplicate");
            return;
        }
        
        if (NavMesh.SamplePosition(position, out var target, 10, NavMesh.AllAreas))
        {
            collection.Add(new Objective(id, target.position));
            DebugLog.Write($"Objective {id} added as location");
        }
        else
        {
            DebugLog.Write($"Objective {id} too far from navmesh");
        }
    }
}