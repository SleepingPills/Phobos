using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

// Bypass the "AssaultEnemyFar" layer
public class BypassAssaultEnemyFarPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GClass45), nameof(GClass45.ShallUseNow));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static bool Patch()
    {
        return false;
    }
}

// Bypass the "Exfiltration" layer
public class BypassExfiltrationPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GClass75), nameof(GClass75.ShallUseNow));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static bool Patch()
    {
        return false;
    }
}