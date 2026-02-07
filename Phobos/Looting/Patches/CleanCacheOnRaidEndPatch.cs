using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
﻿using System.Reflection;
using EFT;
using Phobos.Looting.Utilities;
using SPT.Reflection.Patching;

namespace Phobos.Looting.Patches
{
    public class CleanCacheOnRaidEndPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.Dispose), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            if (LootingSystem.LootLog.DebugEnabled)
            {
                LootingSystem.LootLog.LogDebug("Resetting Caches");
            }

            ActiveLootCache.Reset();
            ActiveBotCache.Reset();
        }
    }
}
