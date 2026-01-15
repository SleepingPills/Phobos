using System.Runtime.CompilerServices;
using EFT;
using Phobos.Components;
using UnityEngine;

namespace Phobos.Entities;

public class Agent(int id, BotOwner bot, float[] taskScores) : Entity(id, taskScores)
{
    public bool IsActive;
    public bool IsLeader;
    public Squad Squad;
    
    public readonly BotOwner Bot = bot;
    public readonly Player Player = bot.Mover.Player;
    
    public readonly Movement Movement = new();
    public readonly Stuck Stuck = new();
    public readonly Look Look = new();
    
    public readonly Objective Objective = new();

    private readonly BifacialTransform _bodyTransform = bot.Mover.Player.PlayerBones.BodyTransform;

    public Vector3 Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bodyTransform.position;
    }

    
    public override string ToString()
    {
        return $"Agent(Id: {Id}, BsgId: {Bot.Id}, Name: {Bot.Profile.Nickname})";
    }
}