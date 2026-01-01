using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT.Interactive;
using Phobos.Components;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Helpers;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.Systems;

public class MovementSystem(NavJobExecutor navJobExecutor)
{
    private const int RetryLimit = 10;

    private const float TargetReachedDistSqr = 5f;
    private const float LookAheadDistSqr = 1.5f;

    private readonly Queue<ValueTuple<Agent, NavJob>> _moveJobs = new(20);

    public void Update(List<Agent> liveAgents)
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

                // Discard the move job if the agent is inactive
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
                if (agent.Movement.Target != null)
                {
                    Reset(agent);
                }

                continue;
            }

            UpdateMovement(agent);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(Agent agent)
    {
        agent.Movement.Target = null;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToByPath(Agent agent, Vector3 destination)
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
        var job = navJobExecutor.Submit(agent.Bot.Position, destination);
        _moveJobs.Enqueue((agent, job));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StartMovement(Agent agent, NavJob job)
    {
        if (job.Status == NavMeshPathStatus.PathInvalid)
        {
            agent.Movement.Target = new MovementTarget(job.Destination, true);
            return;
        }
        
        agent.Movement.Target = new MovementTarget(job.Destination);
        agent.Bot.Mover.GoToByWay(job.Path, 2);

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement(Agent agent)
    {
        var bot = agent.Bot;
        var movement = agent.Movement;

        if (movement.Target == null)
        {
            return;
        }
        
        var moveSpeedMult = 1f;
        
        if (HandleDoors(agent))
        {
            moveSpeedMult = 0.25f;
        }
        
        if (movement.Sprint != bot.Mover.Sprinting)
        {
            bot.Mover.Sprint(movement.Sprint);
        }

        var targetSpeed = movement.Speed * moveSpeedMult;
        if (Math.Abs(targetSpeed - bot.Mover.DestMoveSpeed) > 1e-2)
        {
            bot.SetTargetMoveSpeed(targetSpeed);
        }

        if (Math.Abs(movement.Pose - bot.Mover.TargetPose) > 1e-2)
        {
            bot.SetPose(movement.Pose);
        }

        if (bot.BotLay.IsLay != movement.Prone)
        {
            if (movement.Prone)
            {
                bot.BotLay.TryLay();
            }
            else
            {
                bot.BotLay.GetUp(true);
            }
        }

        var target = movement.Target.Value;

        if (target.Failed)
        {
            return;
        }
        
        if ((target.Position - bot.Position).sqrMagnitude < TargetReachedDistSqr)
        {
            Reset(agent);
            return;
        }

        // Handle the case where we aren't following a path for some reason. The usual reason is that the path was partial. 
        if (movement.ActualPath == null)
        {
            // Try to find a new path.
            if (movement.Retry < RetryLimit)
            {
                MoveRetry(agent, target.Position);
            }
            else
            {
                movement.Target = new MovementTarget(target.Position, true);
            }
            
            return;
        }

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