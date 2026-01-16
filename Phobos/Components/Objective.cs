using Phobos.Navigation;

namespace Phobos.Components;


public class Objective
{
    public Location Location;

    public override string ToString()
    {
        return $"Objective({Location})";
    }
}