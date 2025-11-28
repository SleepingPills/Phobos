using System.Reflection;
using Comfort.Common;
using EFT;
using Phobos.ECS.Systems;
using Phobos.Objectives;
using Phobos.Strategy;
using SPT.Reflection.Patching;

namespace Phobos.Patches;

public class PhobosInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
    }

    [PatchPrefix]
    // ReSharper disable once InconsistentNaming
    public static void Prefix(GameWorld __instance)
    {
        if (__instance is HideoutGameWorld)
        {
            Plugin.Log.LogInfo("Skipping Phobos in hideout");
            return;
        }

        var objectives = Location.Collect();
        Singleton<SquadManager>.Create(new SquadManager(objectives));
    }
}

public class PhobosDisposePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.Dispose));
    }

    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Prefix()
    {
        Plugin.Log.LogInfo("Disposing of static & long lived objects.");
        Singleton<SquadManager>.Release(Singleton<SquadManager>.Instance);
        Plugin.Log.LogInfo("Disposing complete.");
    }
}

