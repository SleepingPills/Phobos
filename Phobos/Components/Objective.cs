using Phobos.Navigation;
using UnityEngine;

namespace Phobos.Components;

public enum ObjectiveStatus
{
    None,
    Moving,
    Finished,
    Failed
}

public class Objective
{
    public ObjectiveStatus Status;
    public Location Location;
    public Vector3[] ArrivalPath;

    public override string ToString()
    {
        return $"Objective({Location}, status: {Status})";
    }
}