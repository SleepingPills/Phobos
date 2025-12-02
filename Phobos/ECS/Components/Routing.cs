using EFT;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.ECS.Components;

public enum RoutingStatus
{
    Suspended,
    Active,
    Retry,
    Completed,
    Failed
}

public class Target
{
    public Vector3 Position;
    public Vector3[] Path;
}

public class Routing(BotOwner bot)
{
    public RoutingStatus Status = RoutingStatus.Suspended;
    public Target Target;
    public BotCurrentPathAbstractClass ActualPath => bot.Mover.ActualPathController.CurPath;
    public float SqrDistance;

    public void Set(NavJob job)
    {
        Target ??= new Target();
        Target.Position = job.Destination;
        Target.Path = job.Path;
    }

    public override string ToString()
    {
        return $"Routing(HasTarget: {Target!=null} SqrDistance: {SqrDistance}, Status: {Status} Path: {ActualPath?.CurIndex}/{ActualPath?.Length})";
    }
}