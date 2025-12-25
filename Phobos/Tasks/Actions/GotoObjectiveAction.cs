using Phobos.Components;
using Phobos.Data;
using Phobos.Entities;
using UnityEngine;

namespace Phobos.Tasks.Actions;

public class GotoObjectiveAction(AgentData dataset, float hysteresis) : Task<Agent>(hysteresis)
{
    private const float UtilityBase = 0.5f;
    private const float UtilityBoost = 0.15f;
    private const float UtilityBoostMaxDistSqr = 50f * 50f;
    private const float UtilityBoostMinDistSqr = 5f * 5f;

    private readonly ComponentArray<Objective> _objectiveComponents = dataset.GetComponentArray<Objective>();
    
    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var objective = _objectiveComponents[agent.Id];

            if (objective.Location == null)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }
            
            // Baseline utility is 0.5f, boosted up to 0.65f as the bot gets nearer the objective. Once within the objective radius, the
            // utility falls off sharply.
            var distSqr = (objective.Location.Position - agent.Bot.Position).sqrMagnitude;
            
            var utilityBoostFactor = Mathf.InverseLerp(UtilityBoostMaxDistSqr, UtilityBoostMinDistSqr, distSqr);
            var utilityDecay = Mathf.InverseLerp(0f, UtilityBoostMinDistSqr, distSqr);
            
            agent.TaskScores[ordinal] = utilityDecay * (UtilityBase + utilityBoostFactor * UtilityBoost);
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            var objective = _objectiveComponents[agent.Id];
            
            if (objective.Location == null)
                continue;

            // TODO: We need some movement state handling here to avoid resubmitting the move job repeatedly in case it takes multiple frames to calc.
            //       We stash away the nav job and only submit a new one once it has been cleared?
            // if (agent.Movement.Target != objective.Location.Position)
            // {
            //     agent.Movement.Target = objective.Location.Position;
            //     movementSystem.MoveToDestination(agent, objective.Location.Position);
            // }
        }
    }

    public override void Activate(Agent entity)
    {
        // TODO: Check if we are currently moving, and whether the move target is very close to our objective, if yes, leave it be. 
        base.Activate(entity);
    }

    public override void Deactivate(Entity entity)
    {
        var objective = _objectiveComponents[entity.Id];
        // TODO: Set the status to suspended
        base.Deactivate(entity);
    }
}