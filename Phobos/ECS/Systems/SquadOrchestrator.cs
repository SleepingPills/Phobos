using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.ECS.Helpers;
using Phobos.Navigation;

namespace Phobos.ECS.Systems;

public class SquadOrchestrator(ObjectiveSystem objectiveSystem, LocationQueue locationQueue)
{
    private readonly FramePacing _pacing = new(10);
    
    private readonly SquadList _squads = new(16);
    private readonly SquadList _emptySquads = new(8);
    private readonly Dictionary<int, Squad> _squadIdMap = new(16);
    
    private readonly SquadTaskSystem _squadsTaskSystem = new(objectiveSystem, locationQueue);

    public Squad GetSquad(int squadId)
    {
        return _squadIdMap[squadId];
    }
    
    public void Update()
    {
        if (_pacing.Blocked())
            return;
        
        _squadsTaskSystem.Update(_squads);
    }

    public void AddActor(Actor actor)
    {
        if (!_squadIdMap.TryGetValue(actor.SquadId, out var squad))
        {
            squad = new Squad(actor.SquadId);
            _squadIdMap.Add(actor.SquadId, squad);
            _squads.Add(squad);
            DebugLog.Write($"Registered new {squad}");
        }
        else if(_emptySquads.SwapRemove(squad))
        {
            // Move the empty squad back to the main list
            _squads.Add(squad);
            DebugLog.Write($"Re-activated previously invactive {squad}");
        }
        
        squad.AddMember(actor);
        DebugLog.Write($"Added {actor} to {squad} with {squad.Count} members");
    }

    public void RemoveActor(Actor actor)
    {
        if (!_squadIdMap.TryGetValue(actor.SquadId, out var squad)) return;
        
        squad.RemoveMember(actor);
        DebugLog.Write($"Removed {actor} from {squad} with {squad.Count} members");
        
        if (squad.Count != 0) return;
        
        DebugLog.Write($"{squad} is empty and deactivated");

        _squads.SwapRemove(squad);
        _emptySquads.Add(squad);
    }
}