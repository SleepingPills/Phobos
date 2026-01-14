using System.Collections.Generic;
using EFT;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;

namespace Phobos.Orchestration;

public class SquadRegistry(SquadData squadData, StrategyManager strategyManager)
{
    private readonly bool _scavSquadsEnabled = Plugin.ScavSquadsEnabled.Value;
    private readonly Dictionary<int, int> _squadIdMap = new(16);

    public void AddAgent(Agent agent)
    {
        var bsgSquadId = agent.Bot.BotsGroup.Id;

        Squad squad;

        var role = agent.Bot.Profile.Info.Settings.Role;

        if (role is WildSpawnType.assault or WildSpawnType.assaultGroup && !_scavSquadsEnabled)
        {
            squad = AddNewSquad(agent);
        }
        else
        {
            if (_squadIdMap.TryGetValue(bsgSquadId, out var squadId))
            {
                squad = squadData.Entities[squadId];
            }
            else
            {
                squad = AddNewSquad(agent);
                _squadIdMap.Add(bsgSquadId, squad.Id);
            }
        }
        
        squad.AddAgent(agent);
        agent.Squad = squad;
        DebugLog.Write($"Added {agent} to {squad} with {squad.Size} members");
    }
    
    public void RemoveAgent(Agent agent)
    {
        var squad = agent.Squad;
        squad.RemoveAgent(agent);
        DebugLog.Write($"Removed {agent} from {squad} with {squad.Size} members remaining");

        if (squad.Size > 0)
        {
            // Reassign squad leader if neccessary
            if (agent != squad.Leader) return;

            squad.Leader = squad.Members[^1];
            squad.Leader.IsLeader = true;
            DebugLog.Write($"{squad} assigned new leader {squad.Leader}");
            return;
        }

        DebugLog.Write($"Removing empty {squad}");
        _squadIdMap.Remove(agent.Bot.BotsGroup.Id);
        squadData.Entities.Remove(squad);
        strategyManager.RemoveEntity(squad);
    }
    
    private Squad AddNewSquad(Agent agent)
    {
        var squad = squadData.AddEntity(strategyManager.Tasks.Length);
        DebugLog.Write($"Registered new {squad}");
        squad.Leader = agent;
        squad.Leader.IsLeader = true;
        DebugLog.Write($"{squad} assigned new leader {squad.Leader}");
        return squad;
    }
}