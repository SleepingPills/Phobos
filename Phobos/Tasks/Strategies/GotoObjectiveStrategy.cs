using System.Runtime.CompilerServices;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
using UnityEngine;
using Range = Phobos.Config.Range;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData squadData, AssignmentSystem assignmentSystem, float hysteresis) : Task<Squad>(hysteresis)
{
    private static Range _moveTimeout = new(400, 600);
    private Range _guardDuration = new(Plugin.ObjectiveGuardDuration.Value.x, Plugin.ObjectiveGuardDuration.Value.y);
    private Range _guardDurationCut = new(Plugin.ObjectiveGuardDurationCut.Value.x, Plugin.ObjectiveGuardDurationCut.Value.y);
    private Range _adjustedGuardDuration = new(Plugin.ObjectiveAdjustedGuardDuration.Value.x, Plugin.ObjectiveAdjustedGuardDuration.Value.y);

    public override void UpdateScore(int ordinal)
    {
        var squads = squadData.Entities.Values;
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            squad.TaskScores[ordinal] = 0.5f;
        }
    }

    public override void Activate(Squad entity)
    {
        base.Activate(entity);

        if (entity.Objective.Location == null) return;

        // If we have an objective, reset the timer on activation
        var timeout = entity.Objective.Status == ObjectiveState.Wait
            ? _guardDuration.SampleGaussian()
            : _moveTimeout.SampleGaussian();

        ResetDuration(entity.Objective, timeout);
    }

    public override void Deactivate(Entity entity)
    {
        // Ensure that we return any assignments
        assignmentSystem.Return(entity);
        base.Deactivate(entity);
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var squad = ActiveEntities[i];
            var squadObjective = squad.Objective;

            if (squadObjective.Location == null)
            {
                DebugLog.Write($"{squad} objective is null, requesting new assignment");
                AssignNewObjective(squad);
                continue;
            }
            
            var finishedCount = UpdateAgents(squad);

            if (finishedCount == squad.Size)
            {
                if (squadObjective.Status == ObjectiveState.Active)
                {
                    DebugLog.Write($"{squad} all members failed their objective en-route, requesting new assignment");
                    AssignNewObjective(squad);
                    continue;
                }

                if (!squadObjective.DurationAdjusted)
                {
                    switch (squadObjective.Location.Category)
                    {
                        case LocationCategory.ContainerLoot:
                        case LocationCategory.LooseLoot:
                            // These objectives will have their wait timer cut if everyone arrived
                            AdjustDuration(squadObjective, squadObjective.Duration * _guardDurationCut.SampleGaussian());
                            DebugLog.Write($"{squad} adjusted {squadObjective.Location} wait duration to {squadObjective.Duration}");
                            break;
                        case LocationCategory.Quest:
                        case LocationCategory.Synthetic:
                            // These objectives simply reset the timer to a very short duration to give the bots chance to disperse
                            // NB: Here we also reset the start time otherwise it's almost guaranteed we'd trigger an immediate timout
                            AdjustDuration(squadObjective, _adjustedGuardDuration.SampleGaussian(), Time.time);
                            DebugLog.Write($"{squad} adjusted {squadObjective.Location} wait duration to {squadObjective.Duration}");
                            break;
                        case LocationCategory.Exfil:
                        default:
                            break;
                    }
                }
            }

            if (Time.time < squadObjective.StartTime + squadObjective.Duration)
            {
                continue;
            }

            DebugLog.Write($"{squad} wait timer ran out, requesting new assignment");
            AssignNewObjective(squad);
        }
    }

    private int UpdateAgents(Squad squad)
    {
        var squadObjective = squad.Objective;
        var finishedCount = 0;

        for (var i = 0; i < squad.Size; i++)
        {
            var agent = squad.Members[i];
            var agentObjective = agent.Objective;

            if (agentObjective.Location != squadObjective.Location)
            {
                agentObjective.Location = squadObjective.Location;
                DebugLog.Write($"{agent} assigned objective {squadObjective.Location}");
            }

            if (agentObjective.Location == null)
            {
                continue;
            }
            
            if ((agentObjective.Location.Position - agent.Player.Position).sqrMagnitude > agentObjective.Location.RadiusSqr)
            {
                continue;
            }

            finishedCount++;

            if (squadObjective.Status == ObjectiveState.Wait) continue;

            DebugLog.Write($"{agent} reached squad objective {squadObjective.Location}");
            var waitDuration = _guardDuration.SampleGaussian();
            squadObjective.Status = ObjectiveState.Wait;
            ResetDuration(squadObjective, waitDuration);
            DebugLog.Write($"{squad} engaging wait mode for {waitDuration} seconds");
        }

        return finishedCount;
    }

    private void AssignNewObjective(Squad squad)
    {
        var newLocation = assignmentSystem.RequestNear(squad, squad.Leader.Bot.Position, squad.Objective.LocationPrevious);

        if (newLocation == null)
        {
            DebugLog.Write($"{squad} received null objective location");
            return;
        }

        squad.Objective.LocationPrevious = squad.Objective.Location;
        squad.Objective.Location = newLocation;
        squad.Objective.Status = ObjectiveState.Active;
        ResetDuration(squad.Objective, _moveTimeout.SampleGaussian());

        DebugLog.Write($"{squad} assigned objective {squad.Objective.Location}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ResetDuration(SquadObjective objective, float duration)
    {
        objective.StartTime = Time.time;
        objective.Duration = duration;
        objective.DurationAdjusted = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AdjustDuration(SquadObjective objective, float duration)
    {
        objective.Duration = duration;
        objective.DurationAdjusted = true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AdjustDuration(SquadObjective objective, float duration, float startTime)
    {
        objective.StartTime = startTime;
        objective.Duration = duration;
        objective.DurationAdjusted = true;
    }
}