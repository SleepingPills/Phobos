using System.Runtime.CompilerServices;
using EFT;
using UnityEngine;

namespace Phobos.Components;

public readonly struct MovementTarget(Vector3 position)
{
    public readonly Vector3 Position = position;
    public readonly bool Failed = false;

    public MovementTarget(Vector3 position, bool failed) : this(position)
    {
        Failed = failed;
    }
}

public class Movement(BotOwner bot)
{
    public MovementTarget? Target;

    public float Speed = 1f;
    public float Pose = 1f;
    public bool Sprint = false;
    public bool Prone = false;

    public int Retry = 0;

    public BotCurrentPathAbstractClass ActualPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => bot.Mover.ActualPathController.CurPath;
    }

    public override string ToString()
    {
        var dist = 0f;

        if (Target.HasValue)
        {
            dist = (Target.Value.Position - bot.Position).magnitude;
        }
        
        return $"Movement({Target} Try: {Retry} Path: {ActualPath?.CurIndex}/{ActualPath?.Length} Dist: {dist})";
    }
}