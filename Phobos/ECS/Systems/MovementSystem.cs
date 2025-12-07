using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Helpers;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.ECS.Systems;

public class MovementSystem(NavJobExecutor navJobExecutor, ActorList liveActors)
{
    private const int RetryLimit = 10;

    private const float TargetReachedDistSqr = 5f;
    private const float LookAheadDistSqr = 1.5f;

    private readonly Queue<ValueTuple<Actor, NavJob>> _moveJobs = new(20);

    public void Update()
    {
        if (_moveJobs.Count > 0)
        {
            for (var i = 0; i < _moveJobs.Count; i++)
            {
                var (actor, job) = _moveJobs.Dequeue();

                // If the job is not ready, re-enqueue and skip to the next
                if (!job.IsReady)
                {
                    _moveJobs.Enqueue((actor, job));
                    continue;
                }

                // Discard the move job if the actor is inactive (Phobos might be deactivated, or the bot died, etc...)
                if (!actor.IsActive)
                    continue;

                StartMovement(actor, job);
            }
        }

        for (var i = 0; i < liveActors.Count; i++)
        {
            var actor = liveActors[i];

            // Bail out if the actor is inactive
            if (!actor.IsActive)
            {
                // Set status to suspended if we were active
                if (actor.Movement.Status == MovementStatus.Active)
                {
                    ResetTarget(actor.Movement, MovementStatus.Suspended);
                }

                continue;
            }

            UpdateMovement(actor);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToDestination(Actor actor, Vector3 destination)
    {
        ScheduleMoveJob(actor, destination);
        actor.Movement.Retry = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveRetry(Actor actor, Vector3 destination)
    {
        ScheduleMoveJob(actor, destination);
        actor.Movement.Retry++;
    }

    private void ScheduleMoveJob(Actor actor, Vector3 destination)
    {
        // Queues up a pathfinding job, once that's ready, we move the bot along the path.
        NavMesh.SamplePosition(actor.Bot.Position, out var origin, 5f, NavMesh.AllAreas);
        var job = navJobExecutor.Submit(origin.position, destination);
        _moveJobs.Enqueue((actor, job));
        ResetTarget(actor.Movement, MovementStatus.Suspended);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StartMovement(Actor actor, NavJob job)
    {
        if (job.Status == NavMeshPathStatus.PathInvalid)
        {
            ResetTarget(actor.Movement, MovementStatus.Failed);
            return;
        }

        AssignTarget(actor.Movement, job);

        actor.Bot.Mover.GoToByWay(job.Path, 2);
        actor.Bot.Mover.ActualPathFinder.SlowAtTheEnd = true;

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement(Actor actor)
    {
        var bot = actor.Bot;
        var movement = actor.Movement;
        
        // Movement control - must be always applied if we are active.
        // The sprint flag has to be enforced on every frame, as the BSG code can sometimes decide to change it randomly.
        // We disable sprint for now as it looks jank and can get bots into weird spots sometimes.
        bot.Mover.Sprint(false);
        bot.SetTargetMoveSpeed(movement.Speed);

        if (movement.Status is MovementStatus.Failed or MovementStatus.Suspended)
            return;

        // Failsafe
        if (movement.Target == null)
        {
            Plugin.Log.LogError($"Null target for {actor} even though the status is {movement.Status}");
            movement.Status = MovementStatus.Suspended;
            return;
        }

        movement.Target.DistanceSqr = (movement.Target.Position - bot.Position).sqrMagnitude;
        
        if (movement.Target.DistanceSqr < TargetReachedDistSqr)
        {
            ResetTarget(movement, MovementStatus.Suspended);
            return;
        }

        // Handle the case where we aren't following a path for some reason. The usual reason is that the path was partial. 
        if (movement.ActualPath == null)
        {
            // Try to find a new path.
            if (movement.Retry < RetryLimit)
            {
                MoveRetry(actor, actor.Movement.Target.Position);
            }
            else
            {
                ResetTarget(movement, MovementStatus.Failed);
            }
            
            return;
        }

        // We'll enforce these whenever the bot is under way
        bot.SetPose(1f);
        bot.BotLay.GetUp(true);

        // Move these out into a LookSystem
        var lookPoint = PathHelper.CalculateForwardPointOnPath(
            movement.ActualPath.Vector3_0, bot.Position, movement.ActualPath.CurIndex, LookAheadDistSqr
        ) + 1.5f * Vector3.up;
        bot.Steering.LookToPoint(lookPoint, 360f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignTarget(Movement movement, NavJob job)
    {
        movement.Set(job);
        movement.Status = MovementStatus.Active;        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetTarget(Movement movement, MovementStatus status)
    {
        movement.Status = status;
    }

    // private static bool ShouldSprint(Actor actor)
    // {
    //     var bot = actor.Bot;
    //     var isFarFromDestination = actor.Movement.Target.DistanceSqr > TargetVicinityDistanceSqr;
    //     var isOutside = bot.AIData.EnvironmentId == 0;
    //     var isAbleToSprint = !bot.Mover.NoSprint && bot.GetPlayer.MovementContext.CanSprint;
    //     var isPathSmooth = CalculatePathAngleJitter(
    //         bot.Position,
    //         actor.Movement.ActualPath.Vector3_0,
    //         actor.Movement.ActualPath.CurIndex
    //     ) < 15f;
    //
    //     return isOutside && isAbleToSprint && isPathSmooth && isFarFromDestination;
    // }
}