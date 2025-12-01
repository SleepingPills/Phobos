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
    private const float SqrDistanceEpsilon = 5f * 5f;

    private readonly Queue<ValueTuple<Actor, NavJob>> _pathJobs = new(20);

    public void MoveTo(Actor actor, Vector3 destination)
    {
        // Queues up a pathfinding job, once that's ready, moves the bot to that path.
        NavMesh.SamplePosition(actor.Bot.Position, out var origin, 5f, NavMesh.AllAreas);
        var job = navJobExecutor.Submit(origin.position, destination);
        _pathJobs.Enqueue((actor, job));
    }

    public void Update()
    {
        if (_pathJobs.Count > 0)
        {
            for (var i = 0; i < _pathJobs.Count; i++)
            {
                var (actor, job) = _pathJobs.Dequeue();

                // If the job is not ready, re-enqueue and skip to the next
                if (!job.IsReady)
                {
                    _pathJobs.Enqueue((actor, job));
                    continue;
                }

                // Bail out if the actor is inactive or the pathfinding failed
                if (!actor.IsActive || job.Status == NavMeshPathStatus.PathInvalid)
                    continue;

                StartMovement(actor, job);
            }
        }

        for (var i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i];

            // Bail out if the actor is inactive
            if (!actor.IsActive)
                continue;

            UpdateMovement(actor);
        }
    }

    private static void StartMovement(Actor actor, NavJob job)
    {
        actor.Routing.Set(job);
        actor.Bot.Mover.GoToByWay(job.Path, 1);
        actor.Bot.Mover.ActualPathFinder.SlowAtTheEnd = true;

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
    }

    private static void UpdateMovement(Actor actor)
    {
        var bot = actor.Bot;
        var routing = actor.Routing;

        // Skip bots with no current pathing
        if (routing.ActualPath == null || routing.Target == null)
            return;

        routing.SqrDistance = (routing.Target.Position - bot.Position).sqrMagnitude;

        if (routing.SqrDistance < SqrDistanceEpsilon)
            routing.Status = RoutingStatus.Completed;

        // We'll enforce these whenever the bot is under way
        bot.SetPose(1f);
        bot.BotLay.GetUp(true);

        // Bots will not move at full speed without this
        bot.SetTargetMoveSpeed(1f);

        var shouldSprint = ShouldSprint(actor);
        bot.Mover.Sprint(shouldSprint);

        var lookPoint = CalculateForwardPointOnPath(routing.ActualPath.Vector3_0, bot.Position, routing.ActualPath.CurIndex) + 1.5f * Vector3.up;
        bot.Steering.LookToPoint(lookPoint, 520);
        DebugGizmos.Line(bot.Fireport.position, lookPoint, Color.blue);
    }

    private static bool ShouldSprint(Actor actor)
    {
        var bot = actor.Bot;
        var isFarFromDestination = actor.Routing.SqrDistance > SqrDistanceEpsilon;
        var isOutside = bot.AIData.EnvironmentId == 0;
        var isAbleToSprint = !bot.Mover.NoSprint && bot.GetPlayer.MovementContext.CanSprint;
        var isPathSmooth = CalculatePathAngleJitter(
            bot.Mover.ActualPathController.CurPath.Vector3_0,
            bot.Mover.ActualPathController.CurPath.CurIndex
        ) < 15f;

        return isOutside && isAbleToSprint && isPathSmooth && isFarFromDestination;
    }

    private static Vector3 CalculateAverageForwardPosition(Vector3[] path, int startIndex = 0, int count = 2)
    {
        // Clamp count to available corners
        count = Mathf.Min(count, path.Length - startIndex - 1);

        // If we have no points remaining to average over, just return the last index
        if (count <= 0)
            return path[^1];

        var posAvg = Vector3.zero;

        // Calculate angles between consecutive segments
        for (var i = startIndex; i < startIndex + count; i++)
        {
            posAvg += path[i];
        }

        return posAvg / count;
    }

    private static Vector3 CalculateForwardPointOnPath(Vector3[] corners, Vector3 currentPosition, int nextCornerIndex,
        float lookAheadDistanceSqr = 25f)
    {
        if (nextCornerIndex >= corners.Length)
            return currentPosition;

        // Track squared distance remaining
        var remainingDistanceSqr = lookAheadDistanceSqr;
        // Start from bot's current position
        var currentPoint = currentPosition;
        // Start checking from the next corner
        var currentIndex = nextCornerIndex;

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


    private static float CalculatePathAngleJitter(Vector3[] path, int startIndex = 0, int count = 3)
    {
        // Clamp count to available corners
        count = Mathf.Min(count, path.Length - startIndex - 2);

        if (count <= 0)
            return 0f;

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