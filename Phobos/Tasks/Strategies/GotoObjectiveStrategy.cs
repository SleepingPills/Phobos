using Phobos.Components;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
using UnityEngine;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData squadData, LocationSystem locationSystem, float hysteresis) : Task<Squad>(hysteresis)
{
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
        
            if (squad.Objective.Location == null)
            {
                // For now we just pick a random squad member as the origin.
                var agent = squad.Members[Random.Range(0, squad.Members.Count)];
                
                var newLocation = locationSystem.RequestNear(agent.Bot.Position, squad.Objective.LocationPrevious);

                if (newLocation == null)
                {
                    DebugLog.Write($"{squad} received null objective location");
                    continue;
                }
                
                squad.Objective.LocationPrevious = squad.Objective.Location;
                squad.Objective.Location = newLocation;
                
                DebugLog.Write($"{squad} assigned objective {squad.Objective.Location}");
            }
        
            for (var j = 0; j < squad.Size; j++)
            {
                var agent = squad.Members[j];
        
                if (squad.Objective.Location == agent.Objective.Location) continue;
        
                DebugLog.Write($"{agent} assigned objective {squad.Objective.Location}");
                
                agent.Objective.Location = squad.Objective.Location;
                agent.Objective.Status = ObjectiveStatus.Suspended;
            }
        }
    }
}