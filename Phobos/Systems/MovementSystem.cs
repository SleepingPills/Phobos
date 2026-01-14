using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using EFT.Interactive;
using Phobos.Components;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Helpers;
using Phobos.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.Systems;

public class MovementSystem
{
    private const int RetryLimit = 10;
    private const float CornerReachedWalkDistSqr = 0.35f * 0.35f;
    private const float CornerReachedSprintDistSqr = 0.6f * 0.6f;
    private const float TargetReachedDistSqr = 1.5f * 1.5f;

    private readonly NavJobExecutor _navJobExecutor;
    private readonly Queue<ValueTuple<Agent, NavJob>> _moveJobs;
    private readonly StuckRemediation _stuckRemediation;

    public MovementSystem(NavJobExecutor navJobExecutor, List<Player> humanPlayers)
    {
        _navJobExecutor = navJobExecutor;
        _moveJobs = new Queue<(Agent, NavJob)>(20);
        _stuckRemediation = new StuckRemediation(this, humanPlayers);
    }

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
                if (agent.Movement.HasPath)
                {
                    ResetPath(agent);
                }

                continue;
            }

            UpdateMovement(agent);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToByPath(Agent agent, Vector3 destination)
    {
        ScheduleMoveJob(agent, destination);
        ResetGait(agent);
        ResetPath(agent);
        agent.Movement.Retry = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToDirect(Agent agent, Vector3 destination)
    {
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveRetry(Agent agent, Vector3 destination)
    {
        ResetPath(agent);

        if (agent.Movement.Retry >= RetryLimit)
        {
            agent.Movement.Status = MovementStatus.Failed;
            return;
        }

        ScheduleMoveJob(agent, destination);
        agent.Movement.Retry++;
    }

    private void ScheduleMoveJob(Agent agent, Vector3 destination)
    {
        // Queues up a pathfinding job, once that's ready, we move the bot along the path.
        var job = _navJobExecutor.Submit(agent.Position, destination);
        _moveJobs.Enqueue((agent, job));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StartMovement(Agent agent, NavJob job)
    {
        if (job.Status == NavMeshPathStatus.PathInvalid)
        {
            agent.Movement.Target = job.Target;
            agent.Movement.Status = MovementStatus.Failed;
            ResetPath(agent);
            return;
        }

        AssignPath(agent.Movement, job);

        agent.Bot.Mover.Stop();

        // Debug
        PathVis.Show(job.Path, thickness: 0.1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement(Agent agent)
    {
        var bot = agent.Bot;
        var player = agent.Player;
        var movement = agent.Movement;

        if (!movement.HasPath || movement.Status == MovementStatus.Failed || movement.Status == MovementStatus.Stopped)
            return;
        
        DebugGizmos.Line(agent.Position, movement.Target, color: Color.blue, expiretime: 0.15f);

        if (movement.VoxelUpdatePacing.Allowed())
        {
            bot.AIData.SetPosToVoxel(agent.Position);
        }

        var moveSpeedMult = 1f;

        // Door handling
        if (HandleDoors(agent))
        {
            moveSpeedMult = 0.25f;
        }

        // Speed
        var movementSpeed = movement.Speed * moveSpeedMult;
        var speedDelta = movementSpeed - player.Speed;
        if (Math.Abs(speedDelta) > 1e-8)
        {
            bot.Mover.SetTargetMoveSpeed(movementSpeed);
            // player.ChangeSpeed(speedDelta);
        }

        // Sprint
        if (movement.Sprint != bot.Mover.Sprinting)
        {
            bot.Mover.Sprint(movement.Sprint);
            // player.EnableSprint(movement.Sprint);
        }

        // Pose
        var poseDelta = movement.Pose - player.PoseLevel;
        if (Math.Abs(poseDelta) > 1e-2)
        {
            bot.SetPose(movement.Pose);
            // player.ChangePose(poseDelta);
        }

        // Prone
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

        // Run stuck remediation before movement logic
        _stuckRemediation.Update(agent);

        // The stuck remediation might've nulled out the path
        if (movement.Path == null)
        {
            return;
        }

        // Path handling
        var moveVector = movement.Path[movement.CurrentCorner] - agent.Position;
        var nextCornerIndex = movement.CurrentCorner + 1;
        var hasNextCorner = nextCornerIndex < movement.Path.Length;

        if (hasNextCorner)
        {
            var cornerReached = false;
            var cornerReachedEps = bot.Mover.Sprinting ? CornerReachedSprintDistSqr : CornerReachedWalkDistSqr;
            var moveVectorSqrMag = moveVector.sqrMagnitude;

            if (moveVectorSqrMag <= cornerReachedEps)
            {
                cornerReached = true;
            }
            else if (moveVectorSqrMag < 1f)
            {
                var nextCorner = movement.Path[nextCornerIndex];

                if (!NavMesh.Raycast(agent.Position, nextCorner, out _, NavMesh.AllAreas))
                {
                    cornerReached = true;
                }
            }

            if (cornerReached)
            {
                movement.CurrentCorner = nextCornerIndex;
                moveVector = movement.Path[movement.CurrentCorner] - agent.Position;
            }
        }
        else
        {
            // If the path doesn't reach far enough, retry again. Don't rely on unity's path status value here.
            if ((movement.Target - movement.Path[movement.CurrentCorner]).sqrMagnitude > TargetReachedDistSqr)
            {
                MoveRetry(agent, movement.Target);
                return;
            }
            
            // We retry pathing above if the last corner doesn't reach far enough, here just check whether we reached that corner.
            if ((movement.Path[movement.CurrentCorner] - agent.Player.Position).sqrMagnitude <= TargetReachedDistSqr)
            {
                DebugLog.Write($"{agent} destination reached");
                ResetPath(agent);
                return;
            }
        }

        // Calculate a deviation from the path in 2d space to avoid cases where the deviation pulls the agent backwards due to terrain geometry.
        var agentPos2d = new Vector2(agent.Position.x, agent.Position.z);
        var closestPointOnPath = PathHelper.ClosestPointOnLine(
            movement.Path[Math.Max(0, movement.CurrentCorner - 1)].ToVector2(),
            movement.Path[movement.CurrentCorner].ToVector2(),
            agentPos2d
        );

        // A spring to pull the bot back to the path if it veers off
        var pathDeviationSpring = (closestPointOnPath - agentPos2d).ToVector3();

        // Steering
        moveVector.Normalize();
        moveVector += pathDeviationSpring;
        moveVector.Normalize();

        var moveDir = CalcMoveDirection(moveVector, player.Rotation);
        player.CharacterController.SetSteerDirection(moveVector);
        player.Move(moveDir);
        bot.AimingManager.CurrentAiming.Move(player.Speed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 CalcMoveDirection(Vector3 direction, Vector2 rotation)
    {
        var vector = Quaternion.Euler(0f, 0f, rotation.x) * new Vector2(direction.x, direction.z);
        return new Vector2(vector.x, vector.y);
    }

    private static bool HandleDoors(Agent agent)
    {
        var currentVoxel = agent.Bot.VoxelesPersonalData.CurVoxel;

        if (currentVoxel == null) return false;

        if (currentVoxel.DoorLinks.Count == 0)
            return false;

        var foundDoors = false;

        for (var i = 0; i < currentVoxel.DoorLinks.Count; i++)
        {
            var door = currentVoxel.DoorLinks[i].Door;

            var shouldOpen = door.enabled && door.Operatable && door.DoorState != EDoorState.Open &&
                             (door.transform.position - agent.Position).sqrMagnitude < 9f;

            if (!shouldOpen) continue;

            door.Open();
            foundDoors = true;
        }

        return foundDoors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetGait(Agent agent)
    {
        agent.Movement.Pose = 1f;
        agent.Movement.Speed = 1f;
        agent.Movement.Prone = false;
        agent.Movement.Sprint = false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetPath(Agent agent)
    {
        agent.Movement.Target = Vector3.zero;
        agent.Movement.Path = null;
        agent.Movement.Status = MovementStatus.Stopped;
        agent.Movement.CurrentCorner = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignPath(Movement movement, NavJob job)
    {
        movement.Target = job.Target;
        movement.Path = job.Path;
        movement.Status = MovementStatus.Moving;
        movement.CurrentCorner = 0;
    }

    // private static bool ShouldSprint(Actor actor)
    // {
    //     var bot = actor.Bot;
    //     var isFarFromDestination = actor.Movement.Target.DistanceSqr > TargetVicinityDistanceSqr;
    //     var isOutside = bot.AIData.EnvironmentId == 0;
    //     var isAbleToSprint = !bot.Mover.NoSprint && bot.GetPlayer.MovementContext.CanSprint;
    //     var isPathSmooth = CalculatePathAngleJitter(
    //         agent.Position,
    //         actor.Movement.ActualPath.Vector3_0,
    //         actor.Movement.ActualPath.CurIndex
    //     ) < 15f;
    //
    //     return isOutside && isAbleToSprint && isPathSmooth && isFarFromDestination;
    // }

    private class StuckRemediation(MovementSystem movementSystem, List<Player> humanPlayers)
    {
        // Thresholds
        private const float MaxMoveSpeed = 5f; // Maximum bot movement speed in m/s
        private const float StuckThresholdMultiplier = 0.5f; // Bot moving at less than 50% expected speed is stuck
        private const float VaultAttemptDelay = 1.5f;
        private const float JumpAttemptDelay = 1.5f + VaultAttemptDelay;
        private const float PathRetryDelay = 3f + JumpAttemptDelay;
        private const float TeleportDelay = 3f + PathRetryDelay;
        private const float FailedDelay = 3f + PathRetryDelay;

        private static readonly LayerMask LayerMaskVisCheck = 0b0000_00000_0000_0001_1000_0000_0000;

        private static readonly EBodyPartColliderType[] VisCheckBodyParts =
        [
            EBodyPartColliderType.HeadCommon,
            EBodyPartColliderType.Pelvis,
            EBodyPartColliderType.LeftForearm,
            EBodyPartColliderType.RightForearm,
            EBodyPartColliderType.LeftCalf,
            EBodyPartColliderType.RightCalf
        ];

        public void Update(Agent agent)
        {
            var stuck = agent.Stuck;

            if (stuck.Pacing.Blocked())
                return;

            // Update timing
            var deltaTime = Time.time - stuck.LastUpdate;
            stuck.LastUpdate = Time.time;

            // Update positions
            var currentPos = agent.Position;
            var lastPos = stuck.LastPosition;
            stuck.LastPosition = currentPos;

            // Apply an asymmetric speed buffering:
            // If the current speed is slower than the last observed speed, use the current speed to avoid overestimating the required distance
            // If the current speed is faster than the last observed speed, use an exponentially weighted moving average with alpha=0.9 to
            // give the agent a chance to actually build distance.
            var currentSpeed = agent.Player.MovementContext.CharacterMovementSpeed;
            var moveSpeed = currentSpeed <= stuck.LastSpeed ? currentSpeed : 0.9f * stuck.LastSpeed + 0.1f * currentSpeed;
            stuck.LastSpeed = moveSpeed;

            // Don't bother if we are basically stationary
            if (moveSpeed <= 0.01)
            {
                ResetStuck(stuck);
                return;
            }

            // Discard measurements after long periods of dormancy 
            if (deltaTime > 2f * stuck.Pacing.Interval)
            {
                ResetStuck(stuck);
                return;
            }

            // Calculate expected movement distance based on current speed setting
            var expectedSpeed = MaxMoveSpeed * moveSpeed;
            var expectedDistance = expectedSpeed * deltaTime;
            var stuckThreshold = expectedDistance * StuckThresholdMultiplier;

            // Check if bot has moved significantly
            var moveVector = currentPos - lastPos;
            // Ignore the vertical axis (filter out jumps)!
            moveVector.y = 0f;

            var distanceMoved = moveVector.magnitude;
            if (distanceMoved > stuckThreshold)
            {
                // Reset stuck state if we were stuck before
                ResetStuck(stuck);
                return;
            }

            // Bot appears stuck - increment timer
            stuck.Timer += deltaTime;

            switch (stuck.State)
            {
                // Apply remediation based on stuck duration
                case StuckState.None when stuck.Timer >= VaultAttemptDelay:
                    DebugLog.Write($"{agent} is stuck, attempting to vault.");
                    stuck.State = StuckState.Vaulting;
                    agent.Player.MovementContext?.TryVaulting();
                    break;
                case StuckState.Vaulting when stuck.Timer >= JumpAttemptDelay:
                    DebugLog.Write($"{agent} is stuck, attempting to jump.");
                    stuck.State = StuckState.Jumping;
                    agent.Player.MovementContext?.TryJump();
                    break;
                case StuckState.Jumping when stuck.Timer >= PathRetryDelay:
                {
                    DebugLog.Write($"{agent} is stuck, attempting to recalculate path.");
                    stuck.State = StuckState.Retrying;
                    movementSystem.MoveRetry(agent, agent.Movement.Target);
                    break;
                }
                case StuckState.Retrying when stuck.Timer >= TeleportDelay:
                    DebugLog.Write($"{agent} is stuck, attempting to teleport.");
                    stuck.State = StuckState.Teleport;
                    AttemptTeleport(agent);
                    break;
                case StuckState.Teleport when stuck.Timer >= FailedDelay:
                    DebugLog.Write($"{agent} is stuck, giving up.");
                    stuck.State = StuckState.Failed;
                    ResetPath(agent);
                    agent.Movement.Status = MovementStatus.Failed;
                    break;
                case StuckState.Failed:
                    Debug.Log("Skipping failed state");
                    break;
                default:
                    Debug.Log($"Unexpected bot stuck state: {stuck}");
                    break;
            }
        }

        private void AttemptTeleport(Agent agent)
        {
            for (var i = 0; i < humanPlayers.Count; i++)
            {
                var player = humanPlayers[i];

                if (!player.HealthController.IsAlive)
                {
                    continue;
                }

                // Don't allow teleports when a human player is closer than 10m
                if ((player.Position - agent.Position).sqrMagnitude <= 100f)
                {
                    DebugLog.Write($"{agent} teleport proximity check failed: {player.Profile.Nickname} too close");
                    return;
                }

                var humanHeadPos = player.PlayerBones.Head.Original.position;
                var agentBodyParts = agent.Player.PlayerBones.BodyPartCollidersDictionary;

                for (var j = 0; j < VisCheckBodyParts.Length; j++)
                {
                    var bodyPartType = VisCheckBodyParts[j];
                    var bodyPart = agentBodyParts[bodyPartType];

                    // If we don't hit anything solid on the way to the destination, we assume it's visible
                    if (Physics.Linecast(humanHeadPos, bodyPart.transform.position, out _, LayerMaskVisCheck.value)) continue;

                    DebugLog.Write(
                        $"{agent} teleport vis check failed: player {player.Profile.Nickname} can see body part {bodyPart.BodyPartColliderType}"
                    );

                    return;
                }
            }

            var teleportPos = agent.Movement.Path[agent.Movement.CurrentCorner];
            teleportPos.y += 0.25f;
            agent.Player.Teleport(teleportPos);
            DebugLog.Write($"{agent} teleporting to {teleportPos}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResetStuck(Stuck stuckDetection)
        {
            stuckDetection.Timer = 0f;
            stuckDetection.State = StuckState.None;
        }
    }
}