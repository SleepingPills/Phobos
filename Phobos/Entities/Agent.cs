using System.Runtime.CompilerServices;
using EFT;
using Phobos.Components;

namespace Phobos.Entities;

public class Agent(int id, BotOwner bot, float[] taskScores) : Entity(id, taskScores)
{
    public bool IsActive;
    
    public readonly BotOwner Bot = bot;
    public readonly Movement Movement = new(bot);

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