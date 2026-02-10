using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Components;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Phobos.Tasks.Actions;

public class GuardAction(AgentData dataset, MovementSystem movementSystem, float hysteresis) : Task<Agent>(hysteresis)
{
    private const float UtilityBoost = 0.45f;
    private const float UtilityBase = 0.2f;
    private const float InnerRadiusRatio = 0.95f * 0.95f;
    private const float SweepAngle = 45f;
    private const int MaxWatchCandidateCount = 25;

    private readonly List<Vector3> _candidateBuffer = [];
    private readonly List<ValueTuple<float, Vector3>> _sortBuffer = [];

    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var location = agent.Objective.Location;

            // If we don't have an objective or cover point selected at all, bail out with zero
            if (location == null || agent.Guard.CoverPoint == null)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }

            // The utility quickly increases to 0.65f
            var distSqr = (location.Position - agent.Position).sqrMagnitude;
            var utilityScale = Mathf.InverseLerp(location.RadiusSqr, InnerRadiusRatio * location.RadiusSqr, distSqr);
            agent.TaskScores[ordinal] = UtilityBase + utilityScale * UtilityBoost;
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            var guard = agent.Guard;

            if (guard.CoverPoint == null)
            {
                continue;
            }

            var coverPoint = guard.CoverPoint.Value;

            switch (agent.Guard.State)
            {
                case GuardState.None:
                    movementSystem.MoveToByPath(agent, coverPoint.Position, sprint: true, urgency: MovementUrgency.Low);
                    guard.State = GuardState.Moving;
                    Log.Debug($"{agent} guarding: moving to cover point");
                    break;
                case GuardState.Moving:
                    if (agent.Movement.Status == MovementStatus.Moving) continue;

                    // If we are no longer moving, crouch and submit the area sweep job
                    if (agent.Movement.Pose > 0.3f && (coverPoint.Level != CoverLevel.Stay || Random.value > 0.5f))
                    {
                        MovementSystem.ResetGait(agent, pose: 0.25f);
                    }

                    SubmitAreaSweepJob(agent, coverPoint);
                    guard.State = GuardState.Sweep;
                    Log.Debug($"{agent} guarding: submitted area sweep job");
                    break;
                case GuardState.Sweep:
                    if (guard.AreaSweepJob == null)
                    {
                        continue;
                    }

                    CompleteAreaSweepJob(guard, guard.AreaSweepJob.Value);
                    guard.State = GuardState.Watch;
                    Log.Debug($"{agent} guarding: completed area sweep job");
                    break;
                case GuardState.Watch:
                    if (guard.WatchDirections.Count == 0 || guard.WatchTimeout > Time.time)
                    {
                        continue;
                    }

                    var direction = guard.WatchDirections[Random.Range(0, guard.WatchDirections.Count)];
                    var randomDirection = LookSystem.RandomDirectionInEllipse(direction, SweepAngle, 15f);
                    LookSystem.LookToDirection(agent, randomDirection, 60f);
                    guard.WatchTimeout = Time.time + Random.Range(2.5f, 10f);

                    DebugGizmos.Line(
                        agent.Player.PlayerBones.Head.position, agent.Player.PlayerBones.Head.position + 25f * randomDirection, color: Color.red
                    );
                    Log.Debug($"{agent} guarding: set new watch direction");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    protected override void Deactivate(Agent entity)
    {
        var guard = entity.Guard;

        guard.State = GuardState.None;
        guard.AreaSweepJob = null;
        guard.WatchDirections.Clear();
        guard.WatchTimeout = 0f;

        entity.Look.Target = null;
    }

    private void SubmitAreaSweepJob(Agent agent, CoverPoint coverPoint)
    {
        var origin = agent.Player.PlayerBones.Head.position;

        _candidateBuffer.Clear();

        if (coverPoint.Category == CoverCategory.Hard)
        {
            _candidateBuffer.Add(-1 * coverPoint.Direction);

            // If it's not standing cover, also try to look in direction of the wall
            if (coverPoint.Level != CoverLevel.Stay)
            {
                _candidateBuffer.Add(coverPoint.Direction);
            }
        }

        // The vertical position of objectives can result in jank directions relative to the origin. Force it to be in the same elevation.
        var coplanarObjective = new Vector3(agent.Objective.Location.Position.x, origin.y, agent.Objective.Location.Position.z);
        var objectiveVector = coplanarObjective - origin;
        objectiveVector.Normalize();

        DebugGizmos.Line(origin, origin - 25f * objectiveVector, expiretime: 0f, color: Color.yellow);

        // Add vector to and away from the objective
        _candidateBuffer.Add(objectiveVector);
        _candidateBuffer.Add(-objectiveVector);

        // Radial sweep
        const float angleStep = 360f / 10f;

        var forward = agent.Player.Transform.forward;

        for (var i = 0; i < 6; i++)
        {
            var angle = i * angleStep;
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);
            var direction = rotation * forward;
            _candidateBuffer.Add(direction);
        }

        // Find all arrival path points
        if (agent.Objective.ArrivalPath != null)
        {
            var cornerSweepCount = Math.Min(agent.Objective.ArrivalPath.Length, 10) + 1;
            
            for (var i = 1; i < cornerSweepCount; i++)
            {
                if (_candidateBuffer.Count >= MaxWatchCandidateCount)
                {
                    break;
                }

                var target = agent.Objective.ArrivalPath[^i] + 1.5f * Vector3.up;
                var direction = target - origin;
                direction.Normalize();
                _candidateBuffer.Add(direction);

                DebugGizmos.Line(origin, target, expiretime: 0f, color: Color.magenta);
            }
        }

        // Find all nearby doors
        for (var i = 0; i < agent.Objective.Location.Doors.Count; i++)
        {
            if (_candidateBuffer.Count >= MaxWatchCandidateCount)
            {
                break;
            }

            var target = agent.Objective.Location.Doors[i].transform.position;
            var direction = target - origin;
            direction.Normalize();
            _candidateBuffer.Add(direction);
        }
        
        // Pad with nearby cover points
        for (var i = 0; i < agent.Objective.Location.CoverPoints.Count; i++)
        {
            if (_candidateBuffer.Count >= MaxWatchCandidateCount)
            {
                break;
            }

            var target = agent.Objective.Location.CoverPoints[i].Position;
            var direction = target - origin;
            direction.Normalize();
            _candidateBuffer.Add(direction);
        }

        // Allocate raycast commands and results
        var commands = new NativeArray<RaycastCommand>(_candidateBuffer.Count, Allocator.TempJob);
        var results = new NativeArray<RaycastHit>(_candidateBuffer.Count, Allocator.TempJob);

        // Set up raycast commands
        for (var i = 0; i < _candidateBuffer.Count; i++)
        {
            var direction = _candidateBuffer[i];
            var parameters = new QueryParameters { layerMask = LayerMasksDataAbstractClass.HitMask };
            commands[i] = new RaycastCommand(origin, direction, parameters, 100);
        }

        Log.Debug($"{agent} found {_candidateBuffer.Count} watch candidates");

        // Schedule the batch
        agent.Guard.AreaSweepJob = new AreaSweepJob
        {
            Handle = RaycastCommand.ScheduleBatch(commands, results, 1),
            Commands = commands,
            Hits = results,
        };
    }

    private void CompleteAreaSweepJob(Guard guard, AreaSweepJob job)
    {
        job.Handle.Complete();

        _sortBuffer.Clear();

        for (var i = 0; i < job.Hits.Length; i++)
        {
            var cmd = job.Commands[i];
            var hit = job.Hits[i];

            var distance = hit.collider == null ? cmd.distance : hit.distance;

            _sortBuffer.Add(new(distance, cmd.direction));
        }

        job.Commands.Dispose();
        job.Hits.Dispose();

        _sortBuffer.Sort(Comparer.Instance);

        // Pick the last 5 entries in the sort buffer
        var limit = Math.Min(_sortBuffer.Count, 5) + 1;
        
        for (var i = 1; i < limit; i++)
        {
            Log.Debug($"picked item direction: {_sortBuffer[i]}");
            guard.WatchDirections.Add(_sortBuffer[^i].Item2);
        }
    }

    public sealed class Comparer : Comparer<ValueTuple<float, Vector3>>
    {
        public static readonly Comparer Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(ValueTuple<float, Vector3> x, ValueTuple<float, Vector3> y)
        {
            return x.Item1.CompareTo(y.Item1);
        }
    }
}