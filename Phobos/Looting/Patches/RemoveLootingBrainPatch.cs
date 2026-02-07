using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
﻿using System.Reflection;
using EFT;
using Phobos.Looting.Components;
using Phobos.Looting.Utilities;
using SPT.Reflection.Patching;

namespace Phobos.Looting.Patches
{
    public class RemoveLootingBrainPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotOwner).GetMethod(nameof(BotOwner.Dispose), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix(BotOwner __instance)
        {
            if (__instance.GetPlayer.TryGetComponent<LootingBrain>(out var component))
            {
                UnityEngine.Object.Destroy(component);
            }

            if (LootingSystem.LootLog.DebugEnabled)
            {
                LootingSystem.LootLog.LogDebug("Cleanup on ActiveLootCache");
            }

            ActiveLootCache.Cleanup(__instance);
            ActiveBotCache.Remove(__instance);
        }
    }
}
