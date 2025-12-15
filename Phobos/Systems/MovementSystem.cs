using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using EFT.Interactive;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Entities;
using Phobos.Helpers;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.ECS.Systems;

public class MovementSystem(NavJobExecutor navJobExecutor, AgentList liveAgents)
{
    private const int RetryLimit = 10;

    private const float TargetReachedDistSqr = 5f;
    private const float LookAheadDistSqr = 1.5f;

    private readonly Queue<ValueTuple<Agent, NavJob>> _moveJobs = new(20);

    public void Update()
    {
        if (_moveJobs.Count > 0)
        {
            for (var i = 0; i < _moveJobs.Count; i++)
            {
                var (agent, job) = _moveJobs.Dequeue();

                // If the job is not ready, re-enqueue and skip to the next
                if (!job.IsReady)
                {
                    _moveJobs.Enqueue((agent, job));
                    continue;
                }

                // Discard the move job if the agent is inactive (Phobos might be deactivated, or the bot died, etc...)
                if (!agent.IsActive)
                    continue;

                StartMovement(agent, job);
            }
        }

        for (var i = 0; i < liveAgents.Count; i++)
        {
            var agent = liveAgents[i];

            // Bail out if the agent is inactive
            if (!agent.IsActive)
            {
                // Set status to suspended if we were active
                if (agent.Movement.Status == MovementStatus.Active)
                {
                    ResetTarget(agent.Movement, MovementStatus.Suspended);
                }

                continue;
            }

            UpdateMovement(agent);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToDestination(Agent agent, Vector3 destination)
    {
        ScheduleMoveJob(agent, destination);
        agent.Movement.Retry = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveRetry(Agent agent, Vector3 destination)
    {
        ScheduleMoveJob(agent, destination);
        agent.Movement.Retry++;
    }

    private void ScheduleMoveJob(Agent agent, Vector3 destination)
    {
        // Queues up a pathfinding job, once that's ready, we move the bot along the path.
        NavMesh.SamplePosition(agent.Bot.Position, out var origin, 5f, NavMesh.AllAreas);
        var job = navJobExecutor.Submit(origin.position, destination);
        _moveJobs.Enqueue((agent, job));
        ResetTarget(agent.Movement, MovementStatus.Suspended);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StartMovement(Agent agent, NavJob job)
    {
        if (job.Status == NavMeshPathStatus.PathInvalid)
        {
            ResetTarget(agent.Movement, MovementStatus.Failed);
            return;
        }

        AssignTarget(agent.Movement, job);

        agent.Bot.Mover.GoToByWay(job.Path, 2);
        agent.Bot.Mover.ActualPathFinder.SlowAtTheEnd = true;

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement(Agent agent)
    {
        var bot = agent.Bot;
        var movement = agent.Movement;

        var moveSpeedMult = 1f;
        
        // Door handling
        if (HandleDoors(agent))
            moveSpeedMult = 0.25f;
        
        // Movement control - must be always applied if Phobos is active.
        // The sprint flag has to be enforced on every frame, as the BSG code can sometimes decide to change it randomly.
        // We disable sprint for now as it looks jank and can get bots into weird spots sometimes.
        bot.Mover.Sprint(false);
        bot.SetTargetMoveSpeed(movement.Speed * moveSpeedMult);

        if (movement.Status != MovementStatus.Active)
            return;

        // Failsafe
        if (movement.Target == null)
        {
            Plugin.Log.LogError($"Null target for {agent} even though the status is {movement.Status}");
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
                MoveRetry(agent, agent.Movement.Target.Position);
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

    private static bool HandleDoors(Agent agent)
    {
        var currentVoxel = agent.Bot.VoxelesPersonalData.CurVoxel;

        if (currentVoxel == null) return false;

        var foundDoors = false;
        
        for (var i = 0; i < currentVoxel.DoorLinks.Count; i++)
        {
            var door = currentVoxel.DoorLinks[i].Door;
            var shouldOpen = door.enabled && door.gameObject.activeInHierarchy && door.Operatable && (door.DoorState & EDoorState.Open) == 0;

            if (!shouldOpen || !((door.transform.position - agent.Bot.Position).sqrMagnitude < 9f)) continue;
            
            foundDoors = true;
            door.Open();
        }

        return foundDoors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignTarget(MovementComponent movement, NavJob job)
    {
        movement.Set(job);
        movement.Status = MovementStatus.Active;        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetTarget(MovementComponent movement, MovementStatus status)
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