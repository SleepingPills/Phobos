using Phobos.Components;
using Phobos.Data;
using Phobos.Entities;

namespace Phobos.Tasks.Actions;

public class GotoObjectiveAction(AgentData dataset, float hysteresis) : Task<Agent>(hysteresis)
{
    private readonly ComponentArray<Objective> _objectiveComponents = dataset.GetComponentArray<Objective>();
    
    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var objective = _objectiveComponents[agent.Id];

            // We only participate in the scoring if we have an objective
            if (objective.Location != null)
            {
                agent.TaskScores[ordinal] = 0.5f;
            }
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            var objective = _objectiveComponents[agent.Id];
        }
    }
}

// public class ObjectiveAction(MovementSystem movementSystem) : BaseAction(hysteresis: 0.25f)
// {
//     private const float ObjectiveReachedDistSqr = 10f * 10f;
//     private const float ObjectiveVicinityDistSqr = 35f * 35f;
//
//     public override void Activate(Agent agent)
//     {
//         base.Activate(agent);
//         var objective = agent.Task.Objective;
//         
//         objective.Location = location;
//         objective.DistanceSqr = (objective.Location.Position - agent.Bot.Position).sqrMagnitude;
//         
//         // Short circuit if we are already within the AO
//         if (objective.DistanceSqr <= ObjectiveReachedDistSqr)
//         {
//             objective.Status = ObjectiveStatus.Success;
//             return;
//         }
//         
//         objective.Status = ObjectiveStatus.Active;
//         
//         agent.Task.Current = objective;
//         agent.Movement.Speed = 1f;
//         
//         Activate(agent);
//         
//         movementSystem.MoveToDestination(agent, location.Position);
//         DebugLog.Write($"Assigned {objective} to {agent}");
//     }
//
//     public void Update()
//     {
//         for (var i = 0; i < Agents.Count; i++)
//         {
//             var actor = Agents[i];
//             UpdateObjective(actor);
//         }
//     }
//
//     [MethodImpl(MethodImplOptions.AggressiveInlining)]
//     private void UpdateObjective(Agent agent)
//     {
//         var bot = agent.Bot;
//         var objective = agent.Task.Objective;
//     
//         if (agent.Movement.Status == MovementStatus.Failed)
//         {
//             objective.Status = ObjectiveStatus.Failed;
//             agent.IsPhobosActive = false;
//             DebugLog.Write($"{agent} {objective} failed, disengaging Phobos.");
//         }
//     
//         if (objective.Status != ObjectiveStatus.Active)
//         {
//             Deactivate(agent);
//             return;
//         }
//     
//         objective.DistanceSqr = (objective.Location.Position - bot.Position).sqrMagnitude;
//     
//         if (objective.DistanceSqr <= ObjectiveReachedDistSqr)
//         {
//             objective.Status = ObjectiveStatus.Success;
//             Deactivate(agent);
//         }
//     
//         var targetSpeed = Mathf.Lerp(0.5f, 1f, Mathf.Pow(objective.DistanceSqr / ObjectiveVicinityDistSqr, 2));
//         agent.Movement.Speed = targetSpeed;
//     }
// }