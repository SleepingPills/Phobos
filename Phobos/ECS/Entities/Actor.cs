using System;
using EFT;
using Phobos.ECS.Components;

namespace Phobos.ECS.Entities;

public class Actor(BotOwner bot) : IEquatable<Actor>
{
    public bool Suspended;
    public bool Paused;
    
    public readonly int SquadId = bot.BotsGroup.Id;
    public readonly BotOwner Bot = bot;
    public BotCurrentPathAbstractClass BotPath => Bot.Mover.ActualPathController.CurPath;
    
    public readonly ActorTask Task = new();
    public readonly Routing Routing = new();
    
    public bool IsActive => !Suspended && !Paused;
    
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