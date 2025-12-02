using System.Diagnostics;
using UnityEngine;

namespace Phobos.Diag;

public static class DebugLog
{
    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        Plugin.Log.LogInfo($"F{Time.frameCount}: {message}");
    }
}