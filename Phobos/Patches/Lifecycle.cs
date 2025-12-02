using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using Phobos.Diag;
using Phobos.ECS;
using Phobos.Navigation;
using Phobos.Objectives;
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
        if (Singleton<SystemOrchestrator>.Instantiated)
            return;
        
        DebugLog.Write("Initializing Phobos");
        // Services
        var navJobExecutor = new NavJobExecutor();
        var objectiveQueue = new ObjectiveQueue();
        
        // Systems
        var systemOrchestrator = new SystemOrchestrator(navJobExecutor, objectiveQueue);
        
        // Registry
        Singleton<SystemOrchestrator>.Create(systemOrchestrator);
        Singleton<NavJobExecutor>.Create(navJobExecutor);
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

    [PatchPrefix]
    // ReSharper disable once InconsistentNaming
    public static void Prefix(AICoreControllerClass __instance)
    {
        // Bool_0 seems to be an IsActive flag
        if (!__instance.Bool_0)
            return;
        
        Singleton<SystemOrchestrator>.Instance.Update();
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
    public static void Prefix()
    {
        Plugin.Log.LogInfo("Disposing of static & long lived objects.");
        Singleton<SystemOrchestrator>.Release(Singleton<SystemOrchestrator>.Instance);
        Singleton<NavJobExecutor>.Release(Singleton<NavJobExecutor>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
    }
}

