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
            // Only trigger if a door was opened or unlocked
            if (__instance is Door && (interactionResult.InteractionType == EInteractionType.Open || interactionResult.InteractionType == EInteractionType.Unlock))
            {
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
