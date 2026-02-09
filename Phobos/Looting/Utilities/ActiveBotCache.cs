using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;

namespace Phobos.Looting.Utilities
{
    // Cached used to keep track of what lootable are currently being targeted by a bot so that multiple bots
    // dont try and path to the same lootable
    public static class ActiveBotCache
    {
        public static List<BotOwner> ActiveBots = [];

        public static bool IsCacheActive
        {
            get { return LootingSystem.MaxActiveLootingSystem.Value > 0; }
        }

        public static bool IsAbleToCache
        {
            get { return GetSize() < LootingSystem.MaxActiveLootingSystem.Value; }
        }

        public static bool IsOverCapacity
        {
            get { return GetSize() > LootingSystem.MaxActiveLootingSystem.Value; }
        }

        public static void Reset()
        {
            ActiveBots = [];
        }

        public static void Add(BotOwner botOwner)
        {
            ActiveBots.Add(botOwner);

            if (LootingSystem.LootLog.DebugEnabled)
                LootingSystem.LootLog.LogDebug(
                    $"{botOwner.name.Localized()} looting enabled  (total: {ActiveBots.Count})"
                );
        }

        public static bool Has(BotOwner botOwner)
        {
            return ActiveBots.Contains(botOwner);
        }

        public static void Remove(BotOwner botOwner)
        {
            ActiveBots.Remove(botOwner);

            if (LootingSystem.LootLog.DebugEnabled)
                LootingSystem.LootLog.LogDebug(
                    $"{botOwner.name.Localized()} looting disabled (total: {ActiveBots.Count})"
                );
        }

        public static int GetSize()
        {
            return ActiveBots.Count;
        }
    }
}
