using System.Numerics;
using System.Runtime.CompilerServices;

namespace Phobos;

public enum PathComputeStatus
{
    Ready,
    Pending,
    Complete
}

public class NavPath
{
    public Vector3? Target;
    public Vector3[] Vertices;
    public PathComputeStatus ComputeStatus;
    public bool IsValid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete(Vector3[] vertices)
    {
        Vertices = vertices;
        IsValid = true;
        ComputeStatus = PathComputeStatus.Complete;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Submit()
    {
        IsValid = false;
        ComputeStatus = PathComputeStatus.Pending;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate()
    {
        IsValid = false;
        ComputeStatus = PathComputeStatus.Ready;
    }
}