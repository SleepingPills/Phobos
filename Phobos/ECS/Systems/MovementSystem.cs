using System;
using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.ECS.Systems;

public class MovementSystem(NavJobExecutor navJobExecutor) : BaseActorSystem
{
    private const int RetryLimit = 10;

    // If we are further than this from a corner, allow the bot to sprint even if there are sharp turns later.
    private const float SprintCancelPathCornerDistanceSqr = 10f * 10f;
    private const float TargetReachedDistanceSqr = 5f * 5f;
    private const float TargetVicinityDistanceSqr = 25f * 25f;
    private const float LookAheadDistanceSqr = 1.5f;

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

                // Bail out if the actor is inactive or the pathfinding failed
                if (!actor.IsActive)
                    continue;

                StartMovement(actor, job);
            }
        }

        for (var i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i];
            
            // Bail out if the actor is inactive
            if (!actor.IsActive)
            {
                // Set status to suspended if we were active
                if (actor.Movement.Status == MovementStatus.Active)
                    actor.Movement.Status = MovementStatus.Suspended;
                        
                continue;
            }

            UpdateMovement(actor);
        }
    }

    public void MoveToDestination(Actor actor, Vector3 destination)
    {
        ScheduleMoveJob(actor, destination);
        actor.Movement.Retry = 0;
    }

    private void MoveRetry(Actor actor)
    {
        ScheduleMoveJob(actor, actor.Movement.Target.Position);
        actor.Movement.Retry++;
    }

    private void ScheduleMoveJob(Actor actor, Vector3 destination)
    {
        // Queues up a pathfinding job, once that's ready, we move the bot along the path.
        NavMesh.SamplePosition(actor.Bot.Position, out var origin, 5f, NavMesh.AllAreas);
        var job = navJobExecutor.Submit(origin.position, destination);
        _moveJobs.Enqueue((actor, job));
        actor.Movement.Status = MovementStatus.Suspended;
        // DebugLog.Write($"{actor} {actor.Movement} move job scheduled");
    }

    private static void StartMovement(Actor actor, NavJob job)
    {
        if (job.Status == NavMeshPathStatus.PathInvalid)
        {
            actor.Movement.Target = null;
            actor.Movement.Status = MovementStatus.Failed;
            return;
        }

        actor.Movement.Set(job);
        actor.Movement.Status = MovementStatus.Active;

        actor.Bot.Mover.GoToByWay(job.Path, 2);
        actor.Bot.Mover.ActualPathFinder.SlowAtTheEnd = true;

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
        // DebugLog.Write($"{actor} {actor.Movement} movement commenced");
    }

    private void UpdateMovement(Actor actor)
    {
        var bot = actor.Bot;
        var movement = actor.Movement;

        if (movement.Status == MovementStatus.Failed)
            return;

        if (movement.Status == MovementStatus.Suspended)
        {
            // If movement got suspended before, but we have a target, try to resume it
            if (movement.Target != null)
                MoveRetry(actor);

            return;
        }

        // Failsafe
        if (movement.Target == null)
        {
            movement.Status = MovementStatus.Suspended;
            Plugin.Log.LogError($"Null target for {actor} even though the status is {movement.Status}");
            return;
        }

        movement.Target.DistanceSqr = (movement.Target.Position - bot.Position).sqrMagnitude;

        // Handle the case where we aren't following a path for some reason. The usual reasons are:
        // 1. The path was partial, and we arrived at the end
        // 2. We arrived at the destination.
        if (movement.ActualPath == null)
        {
            // If we arrived at the destination and have no active path, we are done
            if (movement.Status == MovementStatus.Completed)
            {
                return;
            }

            // Otherwise try to find a new path.
            if (movement.Retry < RetryLimit)
            {
                MoveRetry(actor);
            }
            else
            {
                movement.Status = MovementStatus.Failed;
            }

            return;
        }

        if (movement.Target.DistanceSqr < TargetReachedDistanceSqr)
        {
            movement.Status = MovementStatus.Completed;
        }

        // We'll enforce these whenever the bot is under way
        bot.SetPose(1f);
        bot.BotLay.GetUp(true);

        // Bots will not move at full speed without this
        bot.SetTargetMoveSpeed(1f);

        var shouldSprint = ShouldSprint(actor);
        bot.Mover.Sprint(shouldSprint);

        var lookPoint = CalculateForwardPointOnPath(movement.ActualPath.Vector3_0, bot.Position, movement.ActualPath.CurIndex) + 1.5f * Vector3.up;
        bot.Steering.LookToPoint(lookPoint, 360f);
    }

    private static bool ShouldSprint(Actor actor)
    {
        var bot = actor.Bot;
        var isFarFromDestination = actor.Movement.Target.DistanceSqr > TargetVicinityDistanceSqr;
        var isOutside = bot.AIData.EnvironmentId == 0;
        var isAbleToSprint = !bot.Mover.NoSprint && bot.GetPlayer.MovementContext.CanSprint;
        var isPathSmooth = CalculatePathAngleJitter(
            bot.Position,
            actor.Movement.ActualPath.Vector3_0,
            actor.Movement.ActualPath.CurIndex
        ) < 15f;

        return isOutside && isAbleToSprint && isPathSmooth && isFarFromDestination;
    }

    private static Vector3 CalculateForwardPointOnPath(Vector3[] corners, Vector3 position, int cornerIndex)
    {
        if (cornerIndex >= corners.Length)
            return position;

        // Track squared distance remaining
        var remainingDistanceSqr = LookAheadDistanceSqr;
        // Start from bot's current position
        var currentPoint = position;
        // Start checking from the next corner
        var currentIndex = cornerIndex;

        while (remainingDistanceSqr > 0 && currentIndex < corners.Length)
        {
            // Calculate vector and squared distance to the next corner
            var toCorner = corners[currentIndex] - currentPoint;
            var distanceToCornerSqr = toCorner.sqrMagnitude;

            // If the next corner is far enough, our target point is along this segment
            if (distanceToCornerSqr >= remainingDistanceSqr)
            {
                // Need actual distance for the final lerp/movement calculation
                var remainingDistance = Mathf.Sqrt(remainingDistanceSqr);
                return currentPoint + toCorner.normalized * remainingDistance;
            }

            // The corner is closer than our remaining distance, so "consume" this segment
            // Subtract squared distance covered
            remainingDistanceSqr -= distanceToCornerSqr;
            // Jump to this corner
            currentPoint = corners[currentIndex];
            // Move to next corner
            currentIndex++;
        }

        // We've run out of path - return the final corner as the furthest point
        return corners[^1];
    }
    
    private static float CalculatePathAngleJitter(Vector3 position, Vector3[] path, int startIndex = 0, int count = 2)
    {
        // Clamp count to available corners
        count = Mathf.Min(count, path.Length - startIndex - 2);

        if (count <= 0)
            return 0f;

        // Bail out if we are far from the next point
        if ((path[startIndex] - position).sqrMagnitude > SprintCancelPathCornerDistanceSqr)
        {
            return 0f;
        }
        
        var angleMax = 0f;

        // Calculate angles between consecutive segments
        for (var i = startIndex; i < startIndex + count; i++)
        {
            var pointA = path[i];
            var pointB = path[i + 1];
            var pointC = path[i + 2];

            // Calculate direction vectors
            var directionAb = (pointB - pointA).normalized;
            var directionBc = (pointC - pointB).normalized;

            // Calculate angle between the two direction vectors
            var angle = Vector3.Angle(directionAb, directionBc);
            if (angle > angleMax)
                angleMax = angle;
        }

        return angleMax;
    }
}