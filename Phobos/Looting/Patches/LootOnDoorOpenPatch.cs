using System.Reflection;
using SPT.Reflection.Patching;
using EFT;
using EFT.Interactive;
using Phobos.Looting.Components;
using UnityEngine;
using System.Linq;

namespace Phobos.Looting.Patches
{
    public class LootOnDoorOpenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(WorldInteractiveObject).GetMethod("Interact", new[] { typeof(InteractionResult) });
        }

        [PatchPostfix]
        private static void Postfix(WorldInteractiveObject __instance, InteractionResult interactionResult)
        {
            // Only force a scan when a door is actually OPENED, not just unlocked.
            // If we scan on unlock, the door is still closed and line-of-sight checks will fail.
            if (__instance is Door door && interactionResult.InteractionType == EInteractionType.Open)
            {
                // Optional: Check if door was locked to be more specific, but generally any door open is a good time to re-scan.
                // Iterate through active loot finders and force scan if close
                foreach (var finder in LootingSystem.ActiveLootFinders)
                {
                   if (finder != null && Vector3.Distance(finder.transform.position, __instance.transform.position) < 25f)
                   {
                       finder.ForceScan();
                   }
                }
            }
        }
    }
}
