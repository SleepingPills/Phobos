using System;
using System.Collections.Generic;
using Phobos.Components;

namespace Phobos.Entities;

public class Entity(int id) : IEquatable<Entity>
{
    public readonly int Id = id;
    
    public readonly List<Component> Components = new(32);
    
    public bool Equals(Entity other)
    {
        if (ReferenceEquals(other, null))
            return false;

        return Id == other.Id;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Agent)obj);
    }

    public override int GetHashCode()
    {
        return Id;
    }
    
    public static bool operator ==(Entity lhs, Entity rhs)
    {
        if (lhs is null)
        {
            return rhs is null;
            // null == null = true.
            // Only the left side is null.
        }
        // Equals handles the case of null on right side.
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Entity lhs, Entity rhs) => !(lhs == rhs);
}