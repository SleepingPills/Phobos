using System.Diagnostics;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Phobos.Diag;
using Phobos.Orchestration;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Phobos.Patches;

// ReSharper disable once ClassNeverInstantiated.Global
public class BotMoverSoftTeleportLogPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotMover).GetMethod(nameof(BotMover.Teleport));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static void Patch(BotMover __instance, Vector3 rPosition)
    {
        if (__instance is GClass493)
            return;

        // var botPosition = __instance.BotOwner_0.GetPlayer.Position;
        // DebugGizmos.Line(botPosition, rPosition, color: Color.yellow, lineWidth: 0.5f, expiretime: 0f);
        // DebugGizmos.Line(botPosition - 500f * Vector3.up, botPosition + 500f * Vector3.up, color: Color.yellow, lineWidth: 0.1f, expiretime: 0f);
        // DebugGizmos.Line(rPosition - 500f * Vector3.up, rPosition + 500f * Vector3.up, color: Color.yellow, lineWidth: 0.1f, expiretime: 0f);

        Log.Debug(
            $"BotMover.teleport id: {__instance.BotOwner_0.Id} role: {__instance.BotOwner_0.GetPlayer.Profile?.Info?.Settings?.Role} name: {__instance.BotOwner_0.GetPlayer.Profile?.Nickname} {new StackTrace(true)}"
        );
    }
}

public class BotMoverHardTeleportLogPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotMover).GetMethod(nameof(BotMover.method_10));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static void Patch(BotMover __instance, Vector3 posiblePos)
    {
        if (__instance is GClass493)
            return;

        var botPosition = __instance.BotOwner_0.GetPlayer.Position;
        
        var sqrDist = (botPosition - posiblePos).sqrMagnitude;
        if (sqrDist < 4f)
            return;

        // DebugGizmos.Line(botPosition, posiblePos, color: Color.magenta, lineWidth: 0.5f, expiretime: 0f);
        // DebugGizmos.Line(botPosition - 500f * Vector3.up, botPosition + 500f * Vector3.up, color: Color.magenta, lineWidth: 0.1f, expiretime: 0f);
        // DebugGizmos.Line(posiblePos - 500f * Vector3.up, posiblePos + 500f * Vector3.up, color: Color.magenta, lineWidth: 0.1f, expiretime: 0f);

        Log.Debug(
            $"BotMover.method_10 distance: {sqrDist} id: {__instance.BotOwner_0.Id} role: {__instance.BotOwner_0.GetPlayer.Profile?.Info?.Settings?.Role} name: {__instance.BotOwner_0.GetPlayer.Profile?.Nickname} {new StackTrace(true)}"
        );
    }
}

// Stolen from Solarint's SAIN
// Disables the check for is ai in movement context. could break things in the future
public class MovementContextIsAIPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.IsAI));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static bool Patch(ref bool __result)
    {
        __result = false;
        return false;
    }
}

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
        return Singleton<BsgBotRegistry>.Instance == null || !Singleton<BsgBotRegistry>.Instance.IsPhobosActive(__instance.BotOwner_0);
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