using System;
using System.Collections.Generic;
using Phobos.Extensions;
using Phobos.Objectives;
using Random = UnityEngine.Random;

namespace Phobos.Strategy;

public class Squad : IEquatable<Squad>
{
    public Location? Objective;

    private readonly int _id;
    private readonly List<Actor> _members = [];
    private readonly List<Location> _objectives;
    
    public int Count => _members.Count;

    public Squad(int id, List<Location> objectives)
    {
        _id = id;
        _objectives = [..objectives];

        // Fisher-Yates in-place shuffle
        for (var i = 0; i < _objectives.Count; i++)
        {
            var randomIndex = Random.Range(i, _objectives.Count);
            (_objectives[i], _objectives[randomIndex]) = (_objectives[randomIndex], _objectives[i]);
        }
        
        Objective = FindObjective();
    }

    public void Update()
    {
    }

    public void AddMember(Actor member)
    {
        if (_members.Contains(member))
        {
            throw new PhobosError(
                $"Failed to add bot to squad: bot is already in the members list {member.BotOwner.name} | {member.BotOwner.Profile.Nickname}"
            );
        }

        _members.Add(member);
    }

    public void RemoveMember(Actor member)
    {
        if (!_members.RemoveSwap(member))
            throw new PhobosError(
                $"Failed to remove bot from squad: bot is not in the members list {member.BotOwner.name} | {member.BotOwner.Profile.Nickname}"
            );
    }

    private Location? FindObjective()
    {
        if (_objectives.Count == 0)
            return null;
        
        var lastIndex = _objectives.Count - 1;
        Objective = _objectives[0];
        _objectives.RemoveAt(lastIndex);
        return Objective;
    }

    public bool Equals(Squad other)
    {
        if (ReferenceEquals(other, null))
            return false;
        
        return _id == other._id;
    }

    public override int GetHashCode()
    {
        return _id;
    }
}