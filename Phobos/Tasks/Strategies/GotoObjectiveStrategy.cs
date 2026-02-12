using System;
using System.Runtime.CompilerServices;
using Phobos.Components;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
using UnityEngine;
using Random = UnityEngine.Random;
using Range = Phobos.Config.Range;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData squadData, LocationSystem locationSystem, float hysteresis) : Task<Squad>(hysteresis)
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
        var timeout = entity.Objective.Status == SquadObjectiveState.Wait
            ? _guardDuration.SampleGaussian()
            : _moveTimeout.SampleGaussian();

        ResetDuration(entity.Objective, timeout);
    }

    public override void Deactivate(Entity entity)
    {
        // Ensure that we return any assignments
        locationSystem.Return(entity);
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
                Log.Debug($"{squad} objective is null, requesting new assignment");
                AssignNewObjective(squad);
                continue;
            }

            var finishedCount = UpdateAgents(squad);

            if (finishedCount == squad.Size)
            {
                if (squadObjective.Status == SquadObjectiveState.Active)
                {
                    Log.Debug($"{squad} all members failed their objective en-route, requesting new assignment");
                    AssignNewObjective(squad);
                    continue;
                }

                if (!squadObjective.DurationAdjusted)
                {
                    switch (squadObjective.Location.Category)
                    {
                        case LocationCategory.ContainerLoot:
                        case LocationCategory.LooseLoot:
                        case LocationCategory.Quest:
                            // These objectives will have their wait timer cut if everyone arrived
                            AdjustDuration(squadObjective, squadObjective.Duration * _guardDurationCut.SampleGaussian());
                            Log.Debug($"{squad} adjusted {squadObjective.Location} wait duration to {squadObjective.Duration}");
                            break;
                        case LocationCategory.Synthetic:
                            // These objectives simply reset the timer to a very short duration to give the bots chance to disperse
                            // NB: Here we also reset the start time otherwise it's almost guaranteed we'd trigger an immediate timout
                            AdjustDuration(squadObjective, _adjustedGuardDuration.SampleGaussian(), Time.time);
                            Log.Debug($"{squad} adjusted {squadObjective.Location} wait duration to {squadObjective.Duration}");
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

            Log.Debug($"{squad} wait timer ran out, requesting new assignment");
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
                agentObjective.Status = ObjectiveStatus.None;
                
                // We ensure that we always pick a valid cover point even if the squad size has somehow grown since we generated the list 
                var coverPointIdx = i % squadObjective.CoverPoints.Count;
                
                if (squadObjective.Location != null)
                {
                    agent.Guard.CoverPoint = squadObjective.CoverPoints[coverPointIdx];
                }
                
                Log.Debug($"{agent} assigned objective {squadObjective.Location}");
            }

            if (agentObjective.Location == null)
            {
                continue;
            }
            
            switch (agent.Objective.Status)
            {
                case ObjectiveStatus.Failed:
                    finishedCount++;
                    break;
                case ObjectiveStatus.Finished:
                {
                    finishedCount++;

                    if (squadObjective.Status == SquadObjectiveState.Wait)
                    {
                        break;
                    }

                    Log.Debug($"{agent} reached squad objective {squadObjective.Location}");
                    var waitDuration = _guardDuration.SampleGaussian();
                    squadObjective.Status = SquadObjectiveState.Wait;
                    ResetDuration(squadObjective, waitDuration);
                    Log.Debug($"{squad} engaging wait mode for {waitDuration} seconds");
                    break;
                }
                case ObjectiveStatus.None:
                case ObjectiveStatus.Moving:
                default:
                    break;
            }
        }

        return finishedCount;
    }

    private void AssignNewObjective(Squad squad)
    {
        var objective = squad.Objective;
        
        var newLocation = locationSystem.RequestNear(squad, squad.Leader.Bot.Position, objective.LocationPrevious);

        if (newLocation == null)
        {
            Log.Debug($"{squad} received null objective location");
            return;
        }

        objective.LocationPrevious = objective.Location;
        objective.Location = newLocation;
        objective.Status = SquadObjectiveState.Active;
        
        ShufflePickCoverPoints(objective, Math.Max(squad.TargetMembersCount, squad.Size));
        
        ResetDuration(objective, _moveTimeout.SampleGaussian());

        Log.Debug($"{squad} assigned objective {objective.Location}");
    }
    
    private static void ShufflePickCoverPoints(SquadObjective objective, int count)
    {
        var location = objective.Location;
        
        objective.CoverPoints.Clear();

        var randIdx = Random.Range(0, location.CoverPoints.Count);
        
        for (var i = 0; i < count; i++)
        {
            objective.CoverPoints.Add(location.CoverPoints[randIdx]);
            randIdx = (randIdx + 1) % location.CoverPoints.Count;
            Log.Debug($"Getting cover point at {randIdx}/{location.CoverPoints.Count}");
        }
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