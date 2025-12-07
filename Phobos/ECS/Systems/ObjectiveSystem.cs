using System.Runtime.CompilerServices;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.ECS.Systems;

public class ObjectiveSystem(MovementSystem movementSystem) : BaseActorTaskSystem
{
    private const float ObjectiveReachedDistSqr = 5f * 5f;
    private const float ObjectiveVicinityDistSqr = 35f * 35f;
    
    public void BeginObjective(Actor actor, Location location)
    {
        AddActor(actor);
        SetObjective(actor, location);
        movementSystem.MoveToDestination(actor, location.Position);
        DebugLog.Write($"Assigned {location} to {actor}");
    }

    public void Update()
    {
        for (var i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i];

            if (!actor.IsActive)
            {
                // Reset the objective, this will cause the squad to re-assign it once the actor becomes active
                ResetObjective(actor);
                RemoveActor(actor);
                return;
            }
            
            UpdateObjective(actor);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateObjective(Actor actor)
    {
        var bot = actor.Bot;
        var objective = actor.Objective;
        
        if (objective.Status is ObjectiveStatus.Failed or ObjectiveStatus.Suspended)
            return;

        if (actor.Movement.Status == MovementStatus.Failed)
        {
            objective.Status = ObjectiveStatus.Failed;
            return;
        }
        
        // Failsafe
        if (objective.Location == null)
        {
            Plugin.Log.LogError($"Null objective for {actor} even though the status is {objective.Status}");
            objective.Status = ObjectiveStatus.Suspended;
            return;
        }
        
        objective.DistanceSqr = (objective.Location.Position - bot.Position).sqrMagnitude;

        if (objective.DistanceSqr <= ObjectiveReachedDistSqr)
        {
            objective.Status = ObjectiveStatus.Completed;
        }
        
        var targetSpeed = Mathf.Lerp(0.5f, 1f, Mathf.Pow(objective.DistanceSqr / ObjectiveVicinityDistSqr, 2));
        actor.Movement.Speed = targetSpeed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetObjective(Actor actor, Location location)
    {
        actor.Objective.Location = location;
        actor.Objective.Status = ObjectiveStatus.Active;
        actor.Movement.Speed = 1f;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetObjective(Actor actor)
    {
        actor.Objective.Location = null;
        actor.Objective.Status = ObjectiveStatus.Suspended;
        actor.Movement.Speed = 1f;
    }
}