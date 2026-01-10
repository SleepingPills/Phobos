using System.Runtime.CompilerServices;
using EFT;
using Phobos.Components;

namespace Phobos.Entities;

public class Agent(int id, BotOwner bot, float[] taskScores) : Entity(id, taskScores)
{
    public bool IsActive;
    public bool IsLeader;
    public Squad Squad;
    
    public readonly BotOwner Bot = bot;
    
    public readonly Movement Movement = new();
    public readonly Stuck Stuck = new();
    public readonly Look Look = new();
    
    public readonly Objective Objective = new();

    public Player Player
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Bot.Mover.Player;
    }
    
    public override string ToString()
    {
        return $"Agent(Id: {Id}, Name: {Bot.Profile.Nickname})";
    }
}