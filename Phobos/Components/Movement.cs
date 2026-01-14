using System.Runtime.CompilerServices;
using Phobos.Helpers;
using UnityEngine;

namespace Phobos.Components;

public enum MovementStatus
{
    Stopped,
    Moving,
    Failed
}

public class Movement
{
    public Vector3 Target;
    public Vector3[] Path;
    public MovementStatus Status = MovementStatus.Stopped;

    public int CurrentCorner;
    public int Retry;

    public float Speed = 1f;
    public float Pose = 1f;
    public bool Sprint = false;
    public bool Prone = false;

    public bool HasPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Path is { Length: > 0 };
    }

    public readonly TimePacing VoxelUpdatePacing = new(0.25f);

    public override string ToString()
    {
        return
            $"Movement(Path: {CurrentCorner}/{Path?.Length}, Status: {Status} Retry: {Retry} Speed: {Speed}, Pose: {Pose} Sprint {Sprint}, Prone: {Prone})";
    }
}