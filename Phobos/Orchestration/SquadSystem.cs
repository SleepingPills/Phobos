using System.Collections.Generic;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Helpers;

namespace Phobos.Orchestration;

public class SquadSystem(SquadData squadData, StrategySystem strategySystem, Telemetry telemetry)
{
    private readonly TimePacing _pacing = new(1);

    private readonly Dictionary<int, int> _squadIdMap = new(16);

    public Squad this[int bsgSquadId]
    {
        get
        {
            var squadId = _squadIdMap[bsgSquadId];
            return squadData.Entities[squadId];
        }
    }

    public void Update()
    {
        strategySystem.Update();

        // for (var i = 0; i < _squads.Count; i++)
        // {
        //     var squad = _squads[i];
        //
        //     if (squad.Objective != null) continue;
        //
        //     // If the squad does not have an objective yet, grab one.
        //     var location = locationQueue.Next();
        //     squad.Objective = location;
        //     DebugLog.Write($"Assigned {location} to {squad}");
        // }
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
            squad = squadData.AddEntity();
            _squadIdMap.Add(bsgSquadId, squad.Id);
            telemetry.AddEntity(squad);
            DebugLog.Write($"Registered new {squad}");
        }

        squad.AddAgent(agent);
        DebugLog.Write($"Added {agent} to {squad} with {squad.Count} members");
    }

    public void RemoveAgent(Agent agent)
    {
        if (!_squadIdMap.TryGetValue(agent.Bot.BotsGroup.Id, out var squadId)) return;

        var squad = squadData.Entities[squadId];
        squad.RemoveAgent(agent);
        DebugLog.Write($"Removed {agent} from {squad} with {squad.Count} members remaining");

        if (squad.Count != 0) return;

        DebugLog.Write($"Removing empty {squad}");
        squadData.Entities.Remove(squad);
        strategySystem.RemoveSquad(squad);
        telemetry.RemoveEntity(squad);
    }
}