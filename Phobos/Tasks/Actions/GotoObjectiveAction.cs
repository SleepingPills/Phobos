using System.Runtime.CompilerServices;
using Phobos.Components;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
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
            if (location == null)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }

            // If the movement failed and the target is up to date, there's nothing for us to do.  
            if (agent.Movement.Status == MovementStatus.Failed && IsMovementTargetCurrent(agent, location))
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
            
            // Target hysteresis: skip new move orders if the objective deviates from the target by less than the move system epsilon
            if (IsMovementTargetCurrent(agent, objective.Location))
            {
                continue;
            }
            
            DebugLog.Write($"{agent} received new objective {agent.Objective.Location}, submitting move order");
            movementSystem.MoveToByPath(agent, objective.Location.Position);
        }
    }

    public override void Activate(Agent entity)
    {
        base.Activate(entity);

        var location = entity.Objective.Location;
        
        if (location == null)
        {
            return;
        }
        
        // Check if we are already moving to our target
        if (entity.Movement.HasPath)
        {
            if ((entity.Movement.Target - location.Position).sqrMagnitude <= location.RadiusSqr)
            {
                return;
            }
        }

        movementSystem.MoveToByPath(entity, location.Position);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMovementTargetCurrent(Agent agent, Location location)
    {
        return (agent.Movement.Target - location.Position).sqrMagnitude <= MovementSystem.TargetEpsSqr;
    }
}