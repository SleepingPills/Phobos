using Phobos.Navigation;

namespace Phobos.Components;

public enum ObjectiveStatus
{
    Suspended,
    Active,
    Success,
    Failed
}

public class Objective()
{
    public Location Location;
    public float DistanceSqr;
    public ObjectiveStatus Status = ObjectiveStatus.Suspended;
}