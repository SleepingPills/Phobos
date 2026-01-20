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
    private const float TargetEpsSqr = 1.5f * 1.5f;
    private const float CornerWalkEpsSqr = 0.35f * 0.35f;
    private const float CornerSprintEpsSqr = 0.6f * 0.6f;
    private const int RetryLimit = 10;

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
    public static bool IsMovementTargetCurrent(Agent agent, Vector3 destination)
    {
        return (agent.Movement.Target - destination).sqrMagnitude <= TargetEpsSqr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToByPath(Agent agent, Vector3 destination)
    {
        // Generally, calling code can determine whether the current movement target is set correctly by checking this value.
        // As such, the target to be set immediately to ensure that these checks are correctly done.
        agent.Movement.Target = destination;
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
            ResetPath(agent, MovementStatus.Failed);
            return;
        }

        AssignPath(agent.Movement, job);

        agent.Bot.Mover.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMovement(Agent agent)
    {
        var bot = agent.Bot;
        var player = agent.Player;
        var movement = agent.Movement;

        if (!movement.HasPath || movement.Status == MovementStatus.Failed || movement.Status == MovementStatus.Stopped)
            return;

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
            var cornerReachedEps = bot.Mover.Sprinting ? CornerSprintEpsSqr : CornerWalkEpsSqr;
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
            if ((movement.Target - movement.Path[movement.CurrentCorner]).sqrMagnitude > TargetEpsSqr)
            {
                MoveRetry(agent, movement.Target);
                return;
            }

            // We retry pathing above if the last corner doesn't reach far enough, here just check whether we reached that corner.
            if ((movement.Path[movement.CurrentCorner] - agent.Player.Position).sqrMagnitude <= TargetEpsSqr)
            {
                Log.Debug($"{agent} destination reached");
                // Don't reset the target here. Our target hasn't changed, we just reached it. 
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
    private static void ResetPath(Agent agent, MovementStatus status = MovementStatus.Stopped)
    {
        // We explicitly don't reset the target here - it hasn't changed. Only the path is supposed to be deleted.
        agent.Movement.Path = null;
        agent.Movement.Status = status;
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
        private readonly SoftStuckRemediation _softRemediation = new(0.2f);
        private readonly HardStuckRemediation _hardRemediation = new(movementSystem, humanPlayers, 0.2f);

        public void Update(Agent agent)
        {
            var stuck = agent.Stuck;
            
            if (stuck.Pacing.Blocked())
                return;
            
            _softRemediation.Update(agent);
            _hardRemediation.Update(agent);
        }
    }
    
    private class SoftStuckRemediation(float staleThreshold)
    {
        // Thresholds
        private const float SpeedThreshold = 3.5f / 2f; // Bots moving at half the moveSpeed adjusted threshold are expected to be stuck
        private const float VaultAttemptDelay = 1.5f;
        private const float JumpAttemptDelay = 1.5f + VaultAttemptDelay;
        private const float FailedDelay = 3f + JumpAttemptDelay;
        
        public void Update(Agent agent)
        {
            var stuck = agent.Stuck.Soft;

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
                stuck.Reset();
                return;
            }

            // Discard measurements after long periods of dormancy 
            if (deltaTime > staleThreshold)
            {
                stuck.Reset();
                return;
            }

            // Calculate expected movement distance based on current speed setting
            var expectedSpeed = SpeedThreshold * moveSpeed;
            var stuckThreshold = expectedSpeed * deltaTime;

            // Check if bot has moved significantly
            var moveVector = currentPos - lastPos;
            // Ignore the vertical axis (filter out jumps)!
            moveVector.y = 0f;

            var distanceMoved = moveVector.magnitude;
            if (distanceMoved > stuckThreshold)
            {
                stuck.Reset();
                return;
            }

            // Bot appears stuck - increment timer
            stuck.Timer += deltaTime;

            switch (stuck.Status)
            {
                // Apply remediation based on stuck duration
                case SoftStuckStatus.None when stuck.Timer >= VaultAttemptDelay:
                    Log.Debug($"{agent} is stuck, attempting to vault.");
                    stuck.Status = SoftStuckStatus.Vaulting;
                    agent.Player.MovementContext?.TryVaulting();
                    break;
                case SoftStuckStatus.Vaulting when stuck.Timer >= JumpAttemptDelay:
                    Log.Debug($"{agent} is stuck, attempting to jump.");
                    stuck.Status = SoftStuckStatus.Jumping;
                    agent.Player.MovementContext?.TryJump();
                    break;
                case SoftStuckStatus.Jumping when stuck.Timer >= FailedDelay:
                    stuck.Status = SoftStuckStatus.Failed;
                    break;
                case SoftStuckStatus.Failed:
                default:
                    break;
            }
        }
    }

    
    private class HardStuckRemediation(MovementSystem movementSystem, List<Player> humanPlayers, float staleThreshold)
    {
        private const float StuckRadiusSqr = 3f * 3f;
        
        private const float PathRetryDelay = 5f;
        private const float TeleportDelay = 5f + PathRetryDelay;
        private const float FailedDelay = 5f + TeleportDelay;

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
            // The logic is as follows:
            // If the bot stays within a radius of its position 5 seconds ago for extended periods of time, we consider it stuck
            // The radius size is modulated by the bots target velocity, to account for deliberate slow movement
            var stuck = agent.Stuck.Hard;

            stuck.PositionHistory.Update(agent.Position);
            stuck.AverageSpeed.Update(agent.Player.MovementContext.CharacterMovementSpeed);

            // Update timing
            var deltaTime = Time.time - stuck.LastUpdate;
            stuck.LastUpdate = Time.time;

            // Discard measurements after long periods of dormancy 
            if (deltaTime > staleThreshold)
            {
                Reset(stuck);
                return;
            }
            
            // Apply an asymmetric speed buffering:
            // If the current speed is slower than the average speed, use the current speed to avoid overestimating the required distance.
            // Otherwise, use the moving average to give the agent a chance to actually build distance.
            var averageSpeed = stuck.AverageSpeed.Value;
            var currentSpeed = agent.Player.MovementContext.CharacterMovementSpeed;
            // NB: Movespeed is a number between 0-1
            var moveSpeed = currentSpeed <= averageSpeed ? currentSpeed : averageSpeed;

            // Don't bother if we are basically stationary
            if (moveSpeed <= 0.01 && stuck.Status != HardStuckStatus.None)
            {
                Reset(stuck);
                return;
            }

            // If the bot moved more than the radius * moveSpeed from it's oldest position, we assume it's not stuck and reset the state
            var moveDistanceSqr = stuck.PositionHistory.GetDistanceSqr();
            var stuckThresholdSqr = StuckRadiusSqr * moveSpeed; 
            
            // Reset the state if neccessary
            if (moveDistanceSqr > stuckThresholdSqr)
            {
                Reset(stuck);
                return;
            }

            // Bot appears stuck - increment timer
            stuck.Timer += deltaTime;

            switch (stuck.Status)
            {
                case HardStuckStatus.None when stuck.Timer >= PathRetryDelay:
                    Log.Debug($"{agent} is hard stuck, attempting to recalculate path.");
                    stuck.Status = HardStuckStatus.Retrying;
                    movementSystem.MoveRetry(agent, agent.Movement.Target);
                    break;
                case HardStuckStatus.Retrying when stuck.Timer >= TeleportDelay:
                    Log.Debug($"{agent} is hard stuck, attempting to teleport.");
                    stuck.Status = HardStuckStatus.Teleport;
                    AttemptTeleport(agent);
                    break;
                case HardStuckStatus.Teleport when stuck.Timer >= FailedDelay:
                    Log.Debug($"{agent} is hard stuck, giving up.");
                    stuck.Status = HardStuckStatus.Failed;
                    ResetPath(agent, MovementStatus.Failed);
                    break;
                case HardStuckStatus.Failed:
                default:
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Reset(HardStuck stuck)
        {
            // If we weren't stuck, only reset the timer
            if (stuck.Status == HardStuckStatus.None)
            {
                stuck.Timer = 0f;
                return;
            }
            
            stuck.AverageSpeed.Reset();
            stuck.PositionHistory.Reset();
            stuck.Status = HardStuckStatus.None;
            stuck.Timer = 0f;
        }

        private void AttemptTeleport(Agent agent)
        {
            for (var i = 0; i < humanPlayers.Count; i++)
            {
                var player = humanPlayers[i];

                if (player?.HealthController is not { IsAlive: true })
                {
                    continue;
                }

                // Don't allow teleports when a human player is closer than 10m
                if ((player.Position - agent.Position).sqrMagnitude <= 100f)
                {
                    Log.Debug($"{agent} teleport proximity check failed: {player.Profile.Nickname} too close");
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

                    Log.Debug(
                        $"{agent} teleport vis check failed: player {player.Profile.Nickname} can see body part {bodyPart.BodyPartColliderType}"
                    );

                    return;
                }
            }

            var teleportPos = agent.Movement.Path[agent.Movement.CurrentCorner];
            teleportPos.y += 0.25f;
            agent.Player.Teleport(teleportPos);
            Log.Debug($"{agent} teleporting to {teleportPos}");
        }
    }
}