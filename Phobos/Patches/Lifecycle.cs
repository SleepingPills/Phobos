using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using Phobos.Diag;
using Phobos.Navigation;
using Phobos.Orchestration;
using Phobos.Systems;
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
        if (Singleton<PhobosManager>.Instantiated)
            return;
        
        DebugLog.Write("Initializing Phobos");
        
        // Services
        var navJobExecutor = new NavJobExecutor();
        
        // Systems
        var movementSystem = new MovementSystem(navJobExecutor);
        
        // Core
        var phobosManager = new PhobosManager(movementSystem);
        
        // Telemetry
        var telemetry = new Telemetry(phobosManager);
        
        // Registry
        Singleton<NavJobExecutor>.Create(navJobExecutor);
        Singleton<MovementSystem>.Create(movementSystem);
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
    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(AICoreControllerClass __instance)
    {
        // Bool_0 seems to be an IsActive flag
        if (!__instance.Bool_0)
            return;
        
        Singleton<PhobosManager>.Instance.Update();
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
        Singleton<PhobosManager>.Release(Singleton<PhobosManager>.Instance);
        Singleton<MovementSystem>.Release(Singleton<MovementSystem>.Instance);
        Singleton<NavJobExecutor>.Release(Singleton<NavJobExecutor>.Instance);
        Singleton<Telemetry>.Release(Singleton<Telemetry>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
    }
}

