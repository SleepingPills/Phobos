using System.Collections.Generic;
using Phobos.Extensions;
using Phobos.Objectives;
using Phobos.Strategy;

namespace Phobos.ECS.Systems;

public class SquadManager(List<Location> objectives) : IPhobosSystem
{
    private readonly List<Squad> _squads = [];
    private readonly List<Squad> _emptySquads = [];
    private readonly Dictionary<int, Squad> _squadIdMap = new();


    public void Update()
    {
        for (var i = 0; i < _squads.Count; i++)
        {
            var squad = _squads[i];
            squad.Update();
        }
    }

    public void AddActor(Actor actor)
    {
        if (!_squadIdMap.TryGetValue(actor.SquadId, out var squad))
        {
            squad = new Squad(actor.SquadId, objectives);
            _squadIdMap.Add(actor.SquadId, squad);
            _squads.Add(squad);
        }
        else if(_emptySquads.RemoveSwap(squad))
        {
            // Move the empty squad back to the main list
            _squads.Add(squad);
        }
        
        squad.AddMember(actor);
    }

    public void RemoveActor(Actor actor)
    {
        if (!_squadIdMap.TryGetValue(actor.SquadId, out var squad)) return;
        
        squad.RemoveMember(actor);
        _squadIdMap.Remove(actor.SquadId);

        if (squad.Count != 0) return;
        
        _squads.RemoveSwap(squad);
        _emptySquads.Add(squad);
    }
}