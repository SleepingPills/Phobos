using System.Collections.Generic;
using Phobos.Navigation;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Phobos.Components;

public struct AreaSweepJob
{
    public JobHandle Handle;
    public NativeArray<RaycastCommand> Commands;
    public NativeArray<RaycastHit> Hits;
}

public enum GuardState
{
    None,
    Moving,
    Sweep,
    Watch,
}

public class Guard
{
    public GuardState State;
    public CoverPoint? CoverPoint;
    public AreaSweepJob? AreaSweepJob;
    public float WatchTimeout;
    public readonly List<Vector3> WatchDirections = [];

    public override string ToString()
    {
        return $"{nameof(Guard)}({CoverPoint}, state: {State} directions: {WatchDirections.Count})";
    }
}