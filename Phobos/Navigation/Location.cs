using System;
using UnityEngine;

namespace Phobos.Navigation;

public enum LocationCategory
{
    ContainerLoot,
    LooseLoot,
    Quest,
    Exfil
}

public class Location(LocationCategory category, string name, Vector3 position) : IEquatable<Location>
{
    public readonly LocationCategory Category = category;
    public readonly string Name = name;
    public readonly Vector3 Position = position;

    public bool Equals(Location other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Category == other.Category && Name == other.Name && Position.Equals(other.Position);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Location)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Category, Name, Position);
    }
    
    public override string ToString()
    {
        return $"Location({Category}, {Name}, {Position})";
    }
}