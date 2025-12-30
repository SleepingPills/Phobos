using Phobos.Components;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData squadData, AgentData agentData, LocationQueue locationQueue, float hysteresis) : Task<Squad>(hysteresis)
{
    private readonly ComponentArray<SquadObjective> _squadObjectives = squadData.GetComponentArray<SquadObjective>();
    private readonly ComponentArray<Objective> _agentObjectives = agentData.GetComponentArray<Objective>();

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
            var squadObjective = _squadObjectives[squad.Id];
        
            if (squadObjective.Location == null)
            {
                squadObjective.Location = locationQueue.Next();
                DebugLog.Write($"{squad} assigned objective {squadObjective.Location}");
            }
        
            for (var j = 0; j < squad.Size; j++)
            {
                var agent = squad.Members[j];
                var agentObjective = _agentObjectives[agent.Id];
        
                if (squadObjective.Location == agentObjective.Location) continue;
        
                DebugLog.Write($"{agent} assigned objective {squadObjective.Location}");
                
                agentObjective.Location = squadObjective.Location;
                agentObjective.Status = ObjectiveStatus.Suspended;
            }
        }
    }
}