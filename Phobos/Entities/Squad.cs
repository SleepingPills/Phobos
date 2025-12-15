using System;
using Phobos.Data;
using Phobos.ECS.Components;
using Phobos.Entities;
using Phobos.Navigation;

namespace Phobos.ECS.Entities;

public class SquadList(int capacity) : ExtendedList<Squad>(capacity);

public class Squad(int id) : IEquatable<Squad>
{
    public Location Objective;
    public readonly AgentList Members = new(6);
    
    private readonly int _id = id;
    
    public int Count => Members.Count;

    public void AddMember(Agent member)
    {
        Members.Add(member);
    }

    public void RemoveMember(Agent member)
    {
        Members.SwapRemove(member);
    }
    
    public bool Equals(Squad other)
    {
        if (ReferenceEquals(other, null))
            return false;

        return _id == other._id;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Squad)obj);
    }

    public override int GetHashCode()
    {
        return _id;
    }

    public override string ToString()
    {
        return $"Squad(id: {_id})";
    }
}