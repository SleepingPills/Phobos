using System.Reflection;
using Comfort.Common;
using EFT;
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
        DebugLog.Write("Initializing Phobos");
        
        // Core
        var bsgBotRegistry = new BsgBotRegistry();
        var phobosManager = new PhobosManager(__instance, bsgBotRegistry);

        // Registry
        Singleton<PhobosManager>.Create(phobosManager);
        Singleton<BsgBotRegistry>.Create(bsgBotRegistry);
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
        Singleton<BsgBotRegistry>.Release(Singleton<BsgBotRegistry>.Instance);
        Singleton<BotsController>.Release(Singleton<BotsController>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
    }
}