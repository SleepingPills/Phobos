using System.Reflection;
using Comfort.Common;
using EFT;
using Phobos.Diag;
using Phobos.Orchestration;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

public class GetBotsControllerPatch : ModulePatch
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
        // Registry
        Singleton<BotsController>.Create(__instance);
    }
}

public class PhobosInitPatch : ModulePatch
{
    private static readonly PhobosFrameUpdatePatch FrameUpdatePatch = new();

    protected override MethodBase GetTargetMethod()
    {
        // return typeof(BotsController).GetConstructor(Type.EmptyTypes);
        return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
    }

    // ReSharper disable once InconsistentNaming
    [PatchPrefix]
    public static void Prefix(GameWorld __instance)
    {
        DebugLog.Write("Initializing Phobos");

        // Core
        var bsgBotRegistry = new BsgBotRegistry();
        var phobosManager = new PhobosManager(Singleton<BotsController>.Instance, bsgBotRegistry);

        // Registry
        Singleton<PhobosManager>.Create(phobosManager);
        Singleton<BsgBotRegistry>.Create(bsgBotRegistry);

        // This needs to be patched in here because the frame updates start running way before OnGameStarted is called, but the exfils
        // aren't available earlier with vanilla SPT.
        if (!FrameUpdatePatch.IsActive)
        {
            FrameUpdatePatch.Enable();
        }
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

        DebugLog.Write("Phobos Frame Update");
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