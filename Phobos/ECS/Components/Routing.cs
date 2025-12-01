using EFT;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.ECS.Components;

public enum RoutingStatus
{
    Inactive,
    Active,
    Completed,
    Failed
}

public class Routing
{
    public RoutingStatus Status = RoutingStatus.Inactive;
    public Vector3 Destination;
    public float SqrDistance;
    
    public void Set(NavJob job)
    {
        Status = RoutingStatus.Active;
        Destination = job.Destination;
    }

    public override string ToString()
    {
        return $"Routing(Corner: SqrDistance: {SqrDistance}, Status: {Status})";
    }
}