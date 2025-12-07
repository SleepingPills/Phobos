using System.Runtime.CompilerServices;
using EFT;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.ECS.Components;

public enum MovementStatus
{
    Suspended,
    Active,
    Failed
}

public class Target
{
    public Vector3 Position;
    public float DistanceSqr;

    public override string ToString()
    {
        return $"Target(Dist: {Mathf.Sqrt(DistanceSqr)})";
    }
}

public class Movement(BotOwner bot)
{
    public MovementStatus Status = MovementStatus.Suspended;
    public Target Target;
    public float Speed = 1f;
    
    public int Retry = 0;

    public BotCurrentPathAbstractClass ActualPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => bot.Mover.ActualPathController.CurPath;
    }

    public void Set(NavJob job)
    {
        Target ??= new Target();
        Target.Position = job.Destination;
    }

    public override string ToString()
    {
        return $"Movement({Target} Status: {Status} Try: {Retry} Path: {ActualPath?.CurIndex}/{ActualPath?.Length})";
    }
}