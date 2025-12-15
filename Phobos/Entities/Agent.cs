using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Actions;
using Phobos.Components;

namespace Phobos.Entities;

public class Agent(BotOwner bot, int id) : IEquatable<Agent>
{
    public readonly int Id = id;
    public readonly int BotId = bot.Id;
    public readonly int SquadId = bot.BotsGroup.Id;
    public readonly BotOwner Bot = bot;
    
    public bool IsLayerActive = false;
    public bool IsPhobosActive = true;

    public List<IComponent> Components = new(32);
    public List<UtilityScore> UtilityScores = new(16);
    public BaseAction CurrentAction;
    
    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsLayerActive && IsPhobosActive;
    }

    public bool Equals(Agent other)
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

    public override string ToString()
    {
        return $"Actor(Id: {Id}, Name: {Bot.Profile.Nickname}, LayerActive: {IsLayerActive}, PhobosActive: {IsPhobosActive})";
    }
}