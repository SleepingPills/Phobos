using System.Runtime.CompilerServices;
using Phobos.Actions;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
using Phobos.Entities;
using UnityEngine;

namespace Phobos.ECS.Actions;

public class ObjectiveAction(MovementSystem movementSystem) : BaseAction(hysteresis: 0.25f)
{
    private const float ObjectiveReachedDistSqr = 10f * 10f;
    private const float ObjectiveVicinityDistSqr = 35f * 35f;

    public override void Activate(Agent agent)
    {
        base.Activate(agent);
        // var objective = agent.Task.Objective;
        //
        // objective.Location = location;
        // objective.DistanceSqr = (objective.Location.Position - agent.Bot.Position).sqrMagnitude;
        //
        // // Short circuit if we are already within the AO
        // if (objective.DistanceSqr <= ObjectiveReachedDistSqr)
        // {
        //     objective.Status = ObjectiveStatus.Success;
        //     return;
        // }
        //
        // objective.Status = ObjectiveStatus.Active;
        //
        // agent.Task.Current = objective;
        // agent.Movement.Speed = 1f;
        //
        // Activate(agent);
        //
        // movementSystem.MoveToDestination(agent, location.Position);
        // DebugLog.Write($"Assigned {objective} to {agent}");
    }

    // public void Update()
    // {
    //     for (var i = 0; i < Agents.Count; i++)
    //     {
    //         var actor = Agents[i];
    //         UpdateObjective(actor);
    //     }
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // private void UpdateObjective(Agent agent)
    // {
    //     var bot = agent.Bot;
    //     var objective = agent.Task.Objective;
    //
    //     if (agent.Movement.Status == MovementStatus.Failed)
    //     {
    //         objective.Status = ObjectiveStatus.Failed;
    //         agent.IsPhobosActive = false;
    //         DebugLog.Write($"{agent} {objective} failed, disengaging Phobos.");
    //     }
    //
    //     if (objective.Status != ObjectiveStatus.Active)
    //     {
    //         Deactivate(agent);
    //         return;
    //     }
    //
    //     objective.DistanceSqr = (objective.Location.Position - bot.Position).sqrMagnitude;
    //
    //     if (objective.DistanceSqr <= ObjectiveReachedDistSqr)
    //     {
    //         objective.Status = ObjectiveStatus.Success;
    //         Deactivate(agent);
    //     }
    //
    //     var targetSpeed = Mathf.Lerp(0.5f, 1f, Mathf.Pow(objective.DistanceSqr / ObjectiveVicinityDistSqr, 2));
    //     agent.Movement.Speed = targetSpeed;
    // }
}