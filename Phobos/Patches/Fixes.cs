using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

// Stolen from Solarint's SAIN
// Disable specific functions in Manual Update that might be causing erratic movement in sain bots if they are in combat.
public class BotMoverManualFixedUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotMover).GetMethod(nameof(BotMover.ManualFixedUpdate));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static bool PatchPrefix(BotMover __instance)
    {
        return false;
    }
}

// Stolen from Solarint's SAIN
// Disable specific functions in Manual Update that might be causing erratic movement in sain bots.
// NB: Currently unused as it seems unneeded.
// ReSharper disable once ClassNeverInstantiated.Global
public class BotMoverManualUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotMover).GetMethod(nameof(BotMover.ManualUpdate));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static bool PatchPrefix(BotMover __instance)
    {
        __instance.LocalAvoidance.DropOffset();
        __instance.PositionOnWayInner = __instance.BotOwner_0.Position;

        //__instance.method_16();
        //__instance.method_15();
        __instance.method_14();
        //__instance.LocalAvoidance.ManualUpdate();
        return false;
    }
}

// Stolen from Solarint's SAIN
public class EnableVaultPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Player).GetMethod(nameof(Player.InitVaultingComponent));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static void Patch(Player __instance, ref bool aiControlled)
    {
        if (__instance.UsedSimplifiedSkeleton)
        {
            return;
        }

        aiControlled = false;
    }
}