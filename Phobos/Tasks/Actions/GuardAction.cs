using System.Collections.Generic;
using Phobos.Components;
using Phobos.Data;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
using Phobos.Looting.Components;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.Tasks.Actions;

public class GuardAction(AgentData dataset, MovementSystem movementSystem, float hysteresis) : Task<Agent>(hysteresis)
{
    private const float UtilityBoost = 0.45f;
    private const float UtilityBase = 0.2f;
    private const float InnerRadiusRatio = 0.95f * 0.95f;

    private readonly List<Vector3> _watchCandidates = [];
    private readonly List<Vector3> _watchTargets = [];

    public override void UpdateScore(int ordinal)
    {
        var agents = dataset.Entities.Values;
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var location = agent.Objective.Location;
            
            // If we don't have an objective or cover point selected at all, bail out with zero
            if (location == null || agent.Guard.CoverPoint == null)
            {
                agent.TaskScores[ordinal] = 0;
                continue;
            }
            
            // The utility quickly increases to 0.65f
            var distSqr = (location.Position - agent.Position).sqrMagnitude;
            var utilityScale = Mathf.InverseLerp(location.RadiusSqr, InnerRadiusRatio * location.RadiusSqr, distSqr);
            agent.TaskScores[ordinal] = UtilityBase + utilityScale * UtilityBoost;
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
            
            if (MovementSystem.IsMovementTargetCurrent(agent, coverPoint.Position))
            {
                if (agent.Movement.Status == MovementStatus.Moving) continue;
                
                // If we are no longer moving, crouch
                if (agent.Movement.Pose > 0.3f && (coverPoint.Level != CoverLevel.Stay || Random.value > 0.5f))
                {
                    MovementSystem.ResetGait(agent, pose: 0.25f);
                }
            }
            else
            {
                movementSystem.MoveToByPath(agent, coverPoint.Position, sprint: true, urgency: MovementUrgency.Low);
            }
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

        movementSystem.MoveToByPath(entity, coverPoint.Position, sprint: true, urgency: MovementUrgency.Low);
    }

    private void PickObserveDirection(Agent agent, CoverPoint coverPoint)
    {
        _watchCandidates.Clear();
        
        if (coverPoint.Category == CoverCategory.Hard)
        {
            _watchCandidates.Add(-1 * coverPoint.Direction);

            // If it's not standing cover, also try to look in direction of the wall
            if (coverPoint.Level != CoverLevel.Stay)
            {
                _watchCandidates.Add(coverPoint.Direction);
            }
        }

        var objectiveVector = agent.Objective.Location.Position + Vector3.up - agent.Position;
        objectiveVector.Normalize();
        
        // At the objective
        _watchCandidates.Add(objectiveVector);
        
        // Away from the objective
        _watchCandidates.Add(-1 * objectiveVector);
        
        // Find all arrival path points with los > 25m
        if (agent.Objective.ArrivalPath != null)
        {
            
        }
        
        // Find all nearby doors
        for (var i = 0; i < agent.Objective.Location.Doors.Count; i++)
        {
            var door = agent.Objective.Location.Doors[i];
            var doorVector = door.transform.position - agent.Position;
            doorVector.Normalize();
            _watchCandidates.Add(doorVector);
        }
    }

    private static void SweepObserveDirection(Agent agent)
    {
        // Add a gentle random sweep with 35 deg horizontal and 10 deg vertial range
    }
}