using System.Collections.Generic;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;

namespace Phobos.Orchestration;

public class SquadRegistry(SquadData squadData, StrategyManager strategyManager)
{
    private readonly Dictionary<int, int> _squadIdMap = new(16);

    public Squad this[int bsgSquadId]
    {
        get
        {
            var squadId = _squadIdMap[bsgSquadId];
            return squadData.Entities[squadId];
        }
    }

    public void AddAgent(Agent agent)
    {
        var bsgSquadId = agent.Bot.BotsGroup.Id;
        Squad squad;
        if (_squadIdMap.TryGetValue(bsgSquadId, out var squadId))
        {
            squad = squadData.Entities[squadId];
        }
        else
        {
            squad = squadData.AddEntity(strategyManager.Tasks.Length);
            _squadIdMap.Add(bsgSquadId, squad.Id);
            DebugLog.Write($"Registered new {squad}");
        }

        squad.AddAgent(agent);
        DebugLog.Write($"Added {agent} to {squad} with {squad.Size} members");
    }

    public void RemoveAgent(Agent agent)
    {
        if (!_squadIdMap.TryGetValue(agent.Bot.BotsGroup.Id, out var squadId)) return;

        var squad = squadData.Entities[squadId];
        squad.RemoveAgent(agent);
        DebugLog.Write($"Removed {agent} from {squad} with {squad.Size} members remaining");

        if (squad.Size > 0) return;

        DebugLog.Write($"Removing empty {squad}");
        _squadIdMap.Remove(agent.Bot.BotsGroup.Id);
        squadData.Entities.Remove(squad);
        strategyManager.RemoveEntity(squad);
    }
}