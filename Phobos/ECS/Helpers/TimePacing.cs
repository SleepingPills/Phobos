using System.Runtime.CompilerServices;
using UnityEngine;

namespace Phobos.ECS.Helpers;

public class TimePacing(float interval)
{
    private float _timeout = Time.time + interval;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Blocked()
    {
        if (Time.time < _timeout)
            return true;
        
        _timeout = Time.time + interval;
        return false;
    }
}


public class FramePacing(int interval)
{
    private int _counter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Blocked()
    {
        _counter++;
        return _counter % interval != 0;
    }
}