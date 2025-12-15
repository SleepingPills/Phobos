using Phobos.Navigation;

namespace Phobos.ECS.Components;

public enum ObjectiveStatus
{
    Suspended,
    Active,
    Success,
    Failed
}

public class ObjectiveComponent
{
    public Location Location;
    public float DistanceSqr;
    public ObjectiveStatus Status = ObjectiveStatus.Suspended;
}