using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Phobos.Diag;

public static class Log
{
    [Conditional("DEBUG")]
    public static void Debug(string message)
    {
        Plugin.Log.LogInfo($"F{Time.frameCount}: {message}");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info(string message)
    {
        Plugin.Log.LogInfo($"F{Time.frameCount}: {message}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warning(string message)
    {
        Plugin.Log.LogWarning($"F{Time.frameCount}: {message}");
    }
}