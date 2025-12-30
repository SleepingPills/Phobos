using Phobos.Navigation;

namespace Phobos.Components;

public enum ObjectiveStatus
{
    Suspended,
    Active,
    Failed
}

public class Objective
{
    public Location Location;
    public ObjectiveStatus Status = ObjectiveStatus.Suspended;

    public override string ToString()
    {
        return $"Objective({Location}, Status: {Status})";
    }
}