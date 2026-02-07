using Phobos.Data;
using Phobos.Entities;
using Phobos.Systems;
using Phobos.Looting.Components;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.Tasks.Actions;

public class GuardAction(AgentData dataset, MovementSystem movementSystem, float hysteresis) : Task<Agent>(hysteresis)
{
    private const float UtilityBase = 0.65f;
    private const float InnerRadiusRatio = 0.8f * 0.8f;

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

            // The utility is 0 outside the radius, then quickly increases to 0.65f at 80% of the radius. 
            var distSqr = (location.Position - agent.Position).sqrMagnitude;
            var utilityScale = Mathf.InverseLerp(location.RadiusSqr, InnerRadiusRatio * location.RadiusSqr, distSqr);
            agent.TaskScores[ordinal] = utilityScale * UtilityBase;
        }
    }

    public override void Update()
    {
        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var agent = ActiveEntities[i];
            
            if (agent.Guard.CoverPoint == null)
            {
                continue;
            }
            
            var coverPoint = agent.Guard.CoverPoint.Value;
            
            // Target hysteresis: skip new move orders if the objective deviates from the target by less than the move system epsilon
            if (MovementSystem.IsMovementTargetCurrent(agent, coverPoint.Position))
            {
                continue;
            }
        
            movementSystem.MoveToByPath(agent, coverPoint.Position);
        }
    }

    public override void Activate(Agent entity)
    {
        base.Activate(entity);

        // Force a scan if we are arriving at a loot location
        if (entity.Objective?.Location?.Category == LocationCategory.ContainerLoot || entity.Objective?.Location?.Category == LocationCategory.LooseLoot)
        {
            var lootFinder = entity.Bot.GetPlayer.gameObject.GetComponent<LootFinder>();
            if (lootFinder != null)
            {
                lootFinder.ForceScan();
            }
            else
            {
                Plugin.Log.LogError($"[GuardAction] lootFinder is NULL for {entity.Bot.Profile.Nickname}!");
            }
        }

        if (entity.Guard.CoverPoint == null)
        {
            return;
        }
        
        var coverPoint = entity.Guard.CoverPoint.Value;
        
        // Check if we are already moving to our target
        if (entity.Movement.HasPath && MovementSystem.IsMovementTargetCurrent(entity, coverPoint.Position))
        {
            return;
        }

        movementSystem.MoveToByPath(entity, coverPoint.Position);
    }

}