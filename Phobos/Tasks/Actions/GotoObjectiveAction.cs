using Phobos.Components;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Systems;
using UnityEngine;

namespace Phobos.Tasks.Actions;

public class GotoObjectiveAction(AgentData dataset, MovementSystem movementSystem, float hysteresis) : Task<Agent>(hysteresis)
{
    private const float UtilityBase = 0.5f;
    private const float UtilityBoost = 0.15f;
    private const float UtilityBoostMaxDistSqr = 50f * 50f;

    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var location = agent.Objective.Location;

            // If we don't have an objective or the movement failed
            if (location == null || agent.Objective.Status is ObjectiveStatus.Failed or ObjectiveStatus.Finished)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }

            // Baseline utility is 0.5f, boosted up to 0.65f as the bot gets nearer the objective. Once within the objective radius, the
            // utility falls off sharply.
            var distSqr = (location.Position - agent.Position).sqrMagnitude;

            var utilityBoostFactor = Mathf.InverseLerp(UtilityBoostMaxDistSqr, location.RadiusSqr, distSqr);
            var utilityDecay = Mathf.InverseLerp(0f, location.RadiusSqr, distSqr);

            agent.TaskScores[ordinal] = utilityDecay * (UtilityBase + utilityBoostFactor * UtilityBoost);
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            var objective = agent.Objective;

            if (objective.Location == null)
            {
                continue;
            }

            switch (objective.Status)
            {
                case ObjectiveStatus.None:
                    Log.Debug($"{agent} received new objective {agent.Objective.Location}, submitting move order");
                    movementSystem.MoveToByPath(agent, objective.Location.Position, sprint: true);
                    objective.Status = ObjectiveStatus.Moving;
                    break;
                case ObjectiveStatus.Moving:
                    if (agent.Movement.HasPath && objective.ArrivalPath != agent.Movement.Path)
                    {
                        objective.ArrivalPath = agent.Movement.Path;
                    }

                    var distanceSqr = (objective.Location.Position - agent.Position).sqrMagnitude;

                    // Stop sprinting within 2x the radius (2*2 when squared)
                    if (agent.Movement.Sprint && distanceSqr < 4 * agent.Objective.Location.RadiusSqr)
                    {
                        MovementSystem.ResetGait(agent);
                    }

                    // If we got within the objective radius, we don't care whether the movement failed, we consider it a success 
                    if (distanceSqr <= objective.Location.RadiusSqr)
                    {
                        objective.Status = ObjectiveStatus.Finished;
                        break;
                    }

                    if (agent.Movement.Status == MovementStatus.Failed)
                    {
                        objective.Status = ObjectiveStatus.Failed;
                    }

                    break;
                case ObjectiveStatus.Finished:
                case ObjectiveStatus.Failed:
                default:
                    break;
            }
        }
    }

    protected override void Deactivate(Agent entity)
    {
        if (entity.Objective.Status is ObjectiveStatus.Finished or ObjectiveStatus.Failed)
        {
            return;
        }
        
        // Reset the status if the bot was not failed/finished otherwise it'll not resubmit the move order the next time we are activated
        entity.Objective.Status =  ObjectiveStatus.None;
    }
}