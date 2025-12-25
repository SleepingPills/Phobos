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

    public override string ToString()
    {
        return $"{Location}";
    }
}