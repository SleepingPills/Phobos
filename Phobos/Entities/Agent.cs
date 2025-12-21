using System.Runtime.CompilerServices;
using EFT;

namespace Phobos.Entities;

public class Agent(int id, BotOwner bot, float[] taskScores) : Entity(id, taskScores)
{
    public readonly BotOwner Bot = bot;
    
    public bool IsLayerActive = false;
    public bool IsPhobosActive = true;
    
    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsLayerActive && IsPhobosActive;
    }

    public override string ToString()
    {
        return $"Agent(Id: {Id}, Name: {Bot.Profile.Nickname})";
    }
}