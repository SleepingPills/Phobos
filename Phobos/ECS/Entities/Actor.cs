using System;
using EFT;
using Phobos.ECS.Components;

namespace Phobos.ECS.Entities;

public class Actor(BotOwner bot) : IEquatable<Actor>
{
    public bool IsLayerActive = false;
    public bool IsPhobosActive = true;
    
    public readonly int SquadId = bot.BotsGroup.Id;
    public readonly BotOwner Bot = bot;
    
    
    public readonly ActorTask Task = new();
    public readonly Movement Movement = new(bot);
    
    public bool IsActive => IsLayerActive && IsPhobosActive;
    
    private readonly int _id = bot.Id;

    public bool Equals(Actor other)
    {
        if (ReferenceEquals(other, null))
            return false;

        return _id == other._id;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Actor)obj);
    }

    public override int GetHashCode()
    {
        return _id;
    }

    public override string ToString()
    {
        return $"Actor(id: {_id}, name: {Bot.Profile.Nickname})";
    }
}