using System;
using UnityEngine;

namespace Phobos.Objectives;

public enum LocationCategory
{
    ContainerLoot,
    LooseLoot,
    Quest,
    Exfil
}

public class Objective(ValueTuple<LocationCategory, string> id, Vector3 position) : IEquatable<Objective>
{
    private readonly ValueTuple<LocationCategory, string> _id = id;
    public readonly Vector3 Position = position;

    public override string ToString()
    {
        return $"Location({_id})";
    }

    public bool Equals(Objective other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _id == other._id;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Objective)obj);
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }
}