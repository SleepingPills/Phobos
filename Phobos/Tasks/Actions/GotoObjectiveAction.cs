using Phobos.Components;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Systems;
using UnityEngine;
using UnityEngine.AI;

namespace Phobos.Tasks.Actions;

public class GotoObjectiveAction(AgentData dataset, MovementSystem movementSystem, float hysteresis) : Task<Agent>(hysteresis)
{
    public const float ObjectiveEpsDistSqr = 10f * 10f;
    
    private const float UtilityBase = 0.5f;
    private const float UtilityBoost = 0.15f;
    private const float UtilityBoostMaxDistSqr = 50f * 50f;

    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var objective = agent.Objective;

            if (objective.Status == ObjectiveStatus.Failed || objective.Location == null)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }

            // Baseline utility is 0.5f, boosted up to 0.65f as the bot gets nearer the objective. Once within the objective radius, the
            // utility falls off sharply.
            var distSqr = (objective.Location.Position - agent.Position).sqrMagnitude;

            var utilityBoostFactor = Mathf.InverseLerp(UtilityBoostMaxDistSqr, ObjectiveEpsDistSqr, distSqr);
            var utilityDecay = Mathf.InverseLerp(0f, ObjectiveEpsDistSqr, distSqr);

            agent.TaskScores[ordinal] = utilityDecay * (UtilityBase + utilityBoostFactor * UtilityBoost);
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            var objective = agent.Objective;
            
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (objective.Status == ObjectiveStatus.Failed)
            {
                continue;
            }

            if (objective.Status == ObjectiveStatus.Suspended)
            {
                DebugLog.Write($"{agent} objective {agent.Objective.Location} is suspended, submitting move order");
                objective.Status = ObjectiveStatus.Active;
                movementSystem.MoveToByPath(agent, objective.Location.Position);
                continue;
            }

            // Only fail the objective if the movement fails outside the objective zone.  
            if (agent.Movement.Status == MovementStatus.Failed &&
                (objective.Location.Position - agent.Position).sqrMagnitude > ObjectiveEpsDistSqr)
            {
                DebugLog.Write($"{agent} objective {agent.Objective.Location} is failed");
                objective.Status = ObjectiveStatus.Failed;
            }
        }
    }

    public override void Activate(Agent entity)
    {
        base.Activate(entity);

        var objective = entity.Objective;

        // If the current objective failed bail out. 
        if (objective.Status == ObjectiveStatus.Failed)
        {
            return;
        }

        objective.Status = ObjectiveStatus.Active;

        // Check if we are already moving to our target
        if (entity.Movement.HasPath)
        {
            if ((entity.Movement.Target - objective.Location.Position).sqrMagnitude <= ObjectiveEpsDistSqr)
            {
                return;
            }
        }

        movementSystem.MoveToByPath(entity, objective.Location.Position);
    }
}