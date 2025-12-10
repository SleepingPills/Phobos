using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.ECS.Helpers;
using Phobos.ECS.Systems;
using Phobos.ECS.Systems.Objectives;
using Phobos.Navigation;

namespace Phobos.ECS;

public class SquadOrchestrator(
    QuestObjectiveSystem questObjectiveSystem,
    GuardObjectiveSystem guardObjectiveSystem,
    LocationQueue locationQueue
)
{
    private readonly FramePacing _pacing = new(10);

    private readonly SquadList _squads = new(16);
    private readonly SquadList _emptySquads = new(8);
    private readonly Dictionary<int, Squad> _squadIdMap = new(16);

    private readonly SquadStrategySystem _squadsStrategySystem = new(
        questObjectiveSystem, guardObjectiveSystem, locationQueue
    );

    public Squad GetSquad(int squadId)
    {
        return _squadIdMap[squadId];
    }

    public void Update()
    {
        if (_pacing.Blocked())
            return;

        _squadsStrategySystem.Update(_squads);
    }

    public void AddAgent(Agent agent)
    {
        if (!_squadIdMap.TryGetValue(agent.SquadId, out var squad))
        {
            squad = new Squad(agent.SquadId);
            _squadIdMap.Add(agent.SquadId, squad);
            _squads.Add(squad);
            DebugLog.Write($"Registered new {squad}");
        }
        else if (_emptySquads.SwapRemove(squad))
        {
            // Move the empty squad back to the main list
            _squads.Add(squad);
            DebugLog.Write($"Re-activated previously invactive {squad}");
        }

        squad.AddMember(agent);
        DebugLog.Write($"Added {agent} to {squad} with {squad.Count} members");
    }

    public void RemoveAgent(Agent agent)
    {
        if (!_squadIdMap.TryGetValue(agent.SquadId, out var squad)) return;

        squad.RemoveMember(agent);
        DebugLog.Write($"Removed {agent} from {squad} with {squad.Count} members");

        if (squad.Count != 0) return;

        DebugLog.Write($"{squad} is empty and deactivated");

        _squads.SwapRemove(squad);
        _emptySquads.Add(squad);
    }
}