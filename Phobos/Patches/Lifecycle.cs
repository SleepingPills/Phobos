using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Phobos.Diag;
using Phobos.Orchestration;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

public class PhobosInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        // return typeof(BotsController).GetConstructor(Type.EmptyTypes);
        return typeof(BotsController).GetMethod(nameof(BotsController.Init));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPostfix]
    public static void Postfix(BotsController __instance)
    {
        // For some odd reason the constructor appears to be called twice. Prevent running the second time.
        if (Singleton<PhobosManager>.Instantiated)
            return;

        DebugLog.Write("Initializing Phobos");

        // Core
        var phobosManager = new PhobosManager(__instance);

        // Telemetry
        var telemetry = new Telemetry(phobosManager);

        // Registry
        Singleton<PhobosManager>.Create(phobosManager);
        Singleton<Telemetry>.Create(telemetry);
    }
}

public class PhobosFrameUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        // According to BotsController.method_0, this is where all the bot layer and action logic runs and where the AI decisions should be made.
        return typeof(AICoreControllerClass).GetMethod(nameof(AICoreControllerClass.Update));
    }

    // Has to be a postfix otherwise weird shit happens like the AI ActualPath gets nulled out by BSG code before our layer gets deactivated
    // causing path jobs to be resubmitted needlessly.
    // ReSharper disable once InconsistentNaming
    [PatchPostfix]
    public static void Postfix(AICoreControllerClass __instance)
    {
        // Bool_0 seems to be an IsActive flag
        if (!__instance.Bool_0)
            return;

        Singleton<PhobosManager>.Instance.Update();
    }
}

public class PhobosDisposePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.Dispose));
    }

    [PatchPostfix]
    public static void Postfix()
    {
        Plugin.Log.LogInfo("Disposing of static & long lived objects.");
        Singleton<PhobosManager>.Release(Singleton<PhobosManager>.Instance);
        Singleton<Telemetry>.Release(Singleton<Telemetry>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
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
public class TestBotMoverManualFixedUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotMover), nameof(BotMover.ManualFixedUpdate));
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
public class TestBotMoverManualUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotMover), nameof(BotMover.ManualUpdate));
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