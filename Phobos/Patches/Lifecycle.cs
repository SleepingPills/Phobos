using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using Phobos.Diag;
using Phobos.Navigation;
using Phobos.Orchestration;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

public class PhobosInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotsController).GetConstructor(Type.EmptyTypes);
    }

    [PatchPostfix]
    public static void Postfix()
    {
        // For some odd reason the constructor appears to be called twice. Prevent running the second time.
        if (Singleton<PhobosSystem>.Instantiated)
            return;
        
        DebugLog.Write("Initializing Phobos");
        
        // Services
        var navJobExecutor = new NavJobExecutor();
        
        // Systems
        var phobosSystem = new PhobosSystem();
        
        // Telemetry
        var telemetry = new Telemetry(phobosSystem);
        
        // Registry
        Singleton<NavJobExecutor>.Create(navJobExecutor);
        Singleton<PhobosSystem>.Create(phobosSystem);
        Singleton<Telemetry>.Create(telemetry);
    }
}

public class PhobosFrameUpdatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        // According to BotsController.method_0, this is where all the bot layer and action logic runs and where the AI decisions should be made.
        // We run before this method, so that any decision to activate/suspend our layer takes immediate effect.
        return typeof(AICoreControllerClass).GetMethod(nameof(AICoreControllerClass.Update));
    }

    // Has to be a postfix otherwise weird shit happens like the AI ActualPath gets nulled out by BSG code before our layer gets deactivated
    // causing path jobs to be resubmitted needlessly.
    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(AICoreControllerClass __instance)
    {
        // Bool_0 seems to be an IsActive flag
        if (!__instance.Bool_0)
            return;
        
        Singleton<PhobosSystem>.Instance.Update();
        Singleton<NavJobExecutor>.Instance.Update();
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
        Singleton<Telemetry>.Release(Singleton<Telemetry>.Instance);
        Singleton<NavJobExecutor>.Release(Singleton<NavJobExecutor>.Instance);
        Singleton<PhobosSystem>.Release(Singleton<PhobosSystem>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
    }
}

