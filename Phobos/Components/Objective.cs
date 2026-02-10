using Phobos.Navigation;
using UnityEngine;

namespace Phobos.Components;

public enum ObjectiveState
{
    None,
    Moving,
    Finished,
    Failed
}

public class Objective
{
    public ObjectiveState State;
    public Location Location;
    public Vector3[] ArrivalPath;

    public override string ToString()
    {
        return $"Objective({Location})";
    }
}