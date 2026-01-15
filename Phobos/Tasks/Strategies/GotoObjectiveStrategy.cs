using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Systems;
using Phobos.Tasks.Actions;
using UnityEngine;
using Range = Phobos.Config.Range;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData squadData, AssignmentSystem assignmentSystem, float hysteresis) : Task<Squad>(hysteresis)
{
    private static Range _moveTimeoutRange = new(300, 600);
    private static Range _waitTimeoutRange = new(100, 200);

    public override void UpdateScore(int ordinal)
    {
        var squads = squadData.Entities.Values;
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            squad.TaskScores[ordinal] = 0.5f;
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var squad = ActiveEntities[i];

            var startWait = false;

            for (var j = 0; j < squad.Size; j++)
            {
                var agent = squad.Members[j];

                if (agent.Objective.Location != squad.Objective.Location)
                {
                    agent.Objective.Location = squad.Objective.Location;
                    DebugLog.Write($"{agent} assigned objective {squad.Objective.Location}");
                }

                if (agent.Objective.Location == null || squad.Objective.Status == ObjectiveState.Wait)
                {
                    continue;
                }

                if ((agent.Objective.Location.Position - agent.Player.Position).sqrMagnitude > GotoObjectiveAction.ObjectiveEpsDistSqr)
                {
                    continue;
                }

                DebugLog.Write($"{agent} reached squad objective {squad.Objective.Location}");
                startWait = true;
            }

            if (squad.Objective.Status != ObjectiveState.Wait && startWait)
            {
                var waitTime = _waitTimeoutRange.SampleGaussian();
                squad.Objective.Status = ObjectiveState.Wait;
                squad.Objective.Timeout = Time.time + waitTime;
                DebugLog.Write($"{squad} engaging wait mode for {waitTime} seconds");
            }

            if (squad.Objective.Location != null && Time.time < squad.Objective.Timeout) continue;

            AssignNewObjective(squad);
        }
    }

    public override void Activate(Squad entity)
    {
        // If we have an objective, reset the timer on activation
        if (entity.Objective.Location != null)
        {
            entity.Objective.Timeout = entity.Objective.Status == ObjectiveState.Wait
                ? _waitTimeoutRange.SampleGaussian()
                : _moveTimeoutRange.SampleGaussian();
        }
        
        base.Activate(entity);
    }

    public override void Deactivate(Entity entity)
    {
        // Ensure that we return any assignments
        assignmentSystem.Return(entity);
        base.Deactivate(entity);
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
        squad.Objective.Timeout = Time.time + _moveTimeoutRange.SampleGaussian();

        DebugLog.Write($"{squad} assigned objective {squad.Objective.Location}");
    }
}