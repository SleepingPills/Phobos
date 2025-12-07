using Phobos.Navigation;
using UnityEngine;

namespace Phobos.ECS.Components;

public enum ObjectiveStatus
{
    Suspended,
    Active,
    Completed,
    Failed
}

public class Objective
{
    public Location Location;
    public ObjectiveStatus Status = ObjectiveStatus.Suspended;
    public float DistanceSqr;
    
    public override string ToString()
    {
        return $"ActorTask({Location} Status: {Status} Dist: {Mathf.Sqrt(DistanceSqr)})";
    }
}