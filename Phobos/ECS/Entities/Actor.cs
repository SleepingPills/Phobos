using System;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Data;
using Phobos.ECS.Components;

namespace Phobos.ECS.Entities;

public class ActorList(int capacity) : ExtendedList<Actor>(capacity);

public class Actor(BotOwner bot) : IEquatable<Actor>
{
    public bool IsLayerActive = false;
    public bool IsPhobosActive = true;
    
    public readonly int SquadId = bot.BotsGroup.Id;
    public readonly BotOwner Bot = bot;
    
    public readonly Objective Objective = new();
    public readonly Movement Movement = new(bot);
    public readonly Guard Guard = new();
    
    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsLayerActive && IsPhobosActive;
    }

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
        return $"Actor(Id: {_id}, Name: {Bot.Profile.Nickname}, LayerActive: {IsLayerActive}, PhobosActive: {IsPhobosActive})";
    }
}