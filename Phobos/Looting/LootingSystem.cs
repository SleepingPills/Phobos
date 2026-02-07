using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Looting.Components;
using Phobos.Looting.Patches;
using Phobos.Looting.Utilities;
using UnityEngine;

namespace Phobos.Looting
{
    public static class LootingSystem
    {
        public const string MOD_GUID = "me.phobos.lootingsystem";
        public const string MOD_NAME = "Phobos Looting System";
        public const string MOD_VERSION = "1.0.0";

        public const BotType SettingsDefaults = BotType.Scav | BotType.Pmc | BotType.PlayerScav | BotType.Raider;

        public const EquipmentType CanPickupEquipmentDefaults =
            EquipmentType.ArmoredRig
            | EquipmentType.ArmorVest
            | EquipmentType.Backpack
            | EquipmentType.Grenade
            | EquipmentType.Helmet
            | EquipmentType.TacticalRig
            | EquipmentType.Weapon
            | EquipmentType.Dogtag;

        // Settings
        public static ConfigEntry<BotType> CorpseLootingEnabled;
        public static ConfigEntry<BotType> ContainerLootingEnabled;
        public static ConfigEntry<BotType> LooseItemLootingEnabled;
        
        public static ConfigEntry<float> InitialStartTimer;
        public static ConfigEntry<float> LootScanInterval;
        
        public static ConfigEntry<float> DetectItemDistance;
        public static ConfigEntry<bool> DetectItemNeedsSight;
        public static ConfigEntry<float> DetectContainerDistance;
        public static ConfigEntry<bool> DetectContainerNeedsSight;
        public static ConfigEntry<float> DetectCorpseDistance;
        public static ConfigEntry<bool> DetectCorpseNeedsSight;
        
        // Notifications
        public static ConfigEntry<bool> ShowLootingNotifications;

        // Logging helpers (no longer configurable)
        public static Log LootLog;
        public static Log InteropLog;
        public static Log ItemAppraiserLog;

        // Loot Settings
        public static ConfigEntry<bool> BotsAlwaysCloseContainers;
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<int> TransactionDelay;
        public static ConfigEntry<bool> UseExamineTime;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<bool> CanStripAttachments;

        public static ConfigEntry<float> PMCMinLootThreshold;
        public static ConfigEntry<float> PMCMaxLootThreshold;
        public static ConfigEntry<float> ScavMinLootThreshold;
        public static ConfigEntry<float> ScavMaxLootThreshold;

        public static ConfigEntry<CanEquipEquipmentType> PMCGearToEquip;
        public static ConfigEntry<EquipmentType> PMCGearToPickup;
        public static ConfigEntry<CanEquipEquipmentType> ScavGearToEquip;
        public static ConfigEntry<EquipmentType> ScavGearToPickup;

        public static ItemAppraiser ItemAppraiser { get; private set; } = new ItemAppraiser();

        // Performance Settings
        public static ConfigEntry<int> MaxActiveLootingSystem;
        public static ConfigEntry<int> LimitDistanceFromPlayer;
        
        // Debug (Removed/Hardcoded)
        public static ConfigEntry<bool> DebugLootNavigation; // Kept as hidden or removed? User said remove unnecessary. Removing.

        public static List<LootFinder> ActiveLootFinders = new List<LootFinder>();

        public static void Init(ConfigFile config, ManualLogSource logger)
        {
            SetupConfig(config);

            // Defaults for logs (Error only to keep logs clean)
            LootLog = new Log(logger, Phobos.Looting.Utilities.LogLevel.Error | Phobos.Looting.Utilities.LogLevel.Warning | Phobos.Looting.Utilities.LogLevel.Info);
            logger.LogInfo("[LootingSystem] Init called. Registering layers...");
            InteropLog = new Log(logger, Phobos.Looting.Utilities.LogLevel.Error);
            ItemAppraiserLog = new Log(logger, Phobos.Looting.Utilities.LogLevel.Error);

            new RemoveLootingBrainPatch().Enable();
            new CleanCacheOnRaidEndPatch().Enable();
            // broken: new EnableWeaponSwitchingPatch().Enable();
            new LootOnDoorOpenPatch().Enable();

            BrainManager.RemoveLayer(
                "Utility peace",
                new List<string>
                {
                    "Assault",
                    "ExUsec",
                    "BossSanitar",
                    "CursAssault",
                    "PMC",
                    "PmcUsec",
                    "PmcBear",
                    "ExUsec",
                    "ArenaFighter",
                    "SectantWarrior",
                }
            );

            // Remove BSG's own looting layer
            BrainManager.RemoveLayer("LootPatrol", new List<string> { "Assault", "PMC" });

            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>
                {
                    "Assault",
                    "CursAssault",
                    "BossSanitar",
                    "BossKojaniy",
                    "BossGluhar",
                    "BossPartisan",
                    "BossKolontay",
                    "BirdEye",
                    "BigPipe",
                    "Knight",
                    "Tagilla",
                    "Killa",
                    "BossSanitar",
                    "BossBully",
                    "BossBoar",
                    "FollowerGluharScout",
                    "FollowerGluharProtect",
                    "FollowerGluharAssault",
                    "Fl_Zraychiy",
                    "TagillaFollower",
                    "KolonSec",
                    "FollowerSanitar",
                    "FollowerBully",
                    "FlBoar",
                },
                24
            );

            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string> { "PMC", "PmcUsec", "PmcBear", "ExUsec", "ArenaFighter" },
                25
            );

            BrainManager.AddCustomLayer(typeof(LootingLayer), new List<string> { "SectantWarrior" }, 13);
            BrainManager.AddCustomLayer(typeof(LootingLayer), new List<string> { "SectantPriest" }, 13);
            BrainManager.AddCustomLayer(typeof(LootingLayer), new List<string> { "Obdolbs" }, 11);
        }

        public static void OnUpdate()
        {
            if (UseMarketPrices == null) return;

            bool shoultInitAppraiser =
                (!UseMarketPrices.Value && ItemAppraiser.HandbookData == null)
                || (UseMarketPrices.Value && !ItemAppraiser.MarketInitialized);

            if (
                Singleton<ClientApplication<ISession>>.Instance != null
                && Singleton<HandbookClass>.Instance != null
                && shoultInitAppraiser
            )
            {
                LootLog.LogInfo($"Initializing item appraiser");
                ItemAppraiser.Init();
            }
        }

        private static void SetupConfig(ConfigFile Config)
        {
            // Reorganized Categories
            string catLootTypes = "1. Loot Detection";
            string catPerception = "2. Loot Perception";
            string catBehavior = "3. Bot Behavior";
            string catTiming = "4. Timing & Delays";
            string catLimits = "5. Limits & Thresholds";
            string catEquip = "6. Equipment";
            string catPerf = "7. Performance";

            // 1. Loot Detection
            CorpseLootingEnabled = Config.Bind(catLootTypes, "Enable Corpse Looting", SettingsDefaults, new ConfigDescription("Enables corpse looting for the selected bot types", null, new ConfigurationManagerAttributes { Order = 3 }));
            ContainerLootingEnabled = Config.Bind(catLootTypes, "Enable Container Looting", SettingsDefaults, new ConfigDescription("Enables container looting for the selected bot types", null, new ConfigurationManagerAttributes { Order = 2 }));
            LooseItemLootingEnabled = Config.Bind(catLootTypes, "Enable Loose Item Looting", SettingsDefaults, new ConfigDescription("Enables loose item looting for the selected bot types", null, new ConfigurationManagerAttributes { Order = 1 }));

            // 2. Perception
            DetectCorpseDistance = Config.Bind(catPerception, "Corpse Detection Dist", 80f, new ConfigDescription("Distance (meters) to detect corpses", null, new ConfigurationManagerAttributes { Order = 6 }));
            DetectCorpseNeedsSight = Config.Bind(catPerception, "Corpse Line of Sight", false, new ConfigDescription("Corpses ignored if not visible", null, new ConfigurationManagerAttributes { Order = 5 }));
            DetectContainerDistance = Config.Bind(catPerception, "Container Detection Dist", 80f, new ConfigDescription("Distance (meters) to detect containers", null, new ConfigurationManagerAttributes { Order = 4 }));
            DetectContainerNeedsSight = Config.Bind(catPerception, "Container Line of Sight", false, new ConfigDescription("Containers ignored if not visible", null, new ConfigurationManagerAttributes { Order = 3 }));
            DetectItemDistance = Config.Bind(catPerception, "Loose Item Detection Dist", 80f, new ConfigDescription("Distance (meters) to detect items", null, new ConfigurationManagerAttributes { Order = 2 }));
            DetectItemNeedsSight = Config.Bind(catPerception, "Loose Item Line of Sight", false, new ConfigDescription("Items ignored if not visible", null, new ConfigurationManagerAttributes { Order = 1 }));

            // Notifications
            ShowLootingNotifications = Config.Bind(catLootTypes, "Show Looting Logs", true, new ConfigDescription("Prints a log message when a bot successfully loots something", null, new ConfigurationManagerAttributes { Order = 0 }));

            // 3. Behavior
            BotsAlwaysCloseContainers = Config.Bind(catBehavior, "Always Close Containers", true, new ConfigDescription("Bots will try to close containers after looting", null, new ConfigurationManagerAttributes { Order = 4 }));
            CanStripAttachments = Config.Bind(catBehavior, "Strip Attachments", true, new ConfigDescription("Allow taking attachments off weapons they can't pick up", null, new ConfigurationManagerAttributes { Order = 3 }));
            UseMarketPrices = Config.Bind(catBehavior, "Use Flea Market Prices", false, new ConfigDescription("Query Ragfair for more accurate pricing (requires query on start)", null, new ConfigurationManagerAttributes { Order = 2 }));
            ValueFromMods = Config.Bind(catBehavior, "Value From Attachments", true, new ConfigDescription("Calculate weapon value by summing attachment values (more accurate, slightly heavier)", null, new ConfigurationManagerAttributes { Order = 1 }));

            // 4. Timing
            InitialStartTimer = Config.Bind(catTiming, "Raid Start Delay", 6f, new ConfigDescription("Seconds to wait after spawn before first scan", null, new ConfigurationManagerAttributes { Order = 4 }));
            LootScanInterval = Config.Bind(catTiming, "Scan Interval", 10f, new ConfigDescription("Seconds between loot scans", null, new ConfigurationManagerAttributes { Order = 3 }));
            TransactionDelay = Config.Bind(catTiming, "Transaction Delay", 500, new ConfigDescription("Delay (ms) after taking an item", null, new ConfigurationManagerAttributes { Order = 2 }));
            UseExamineTime = Config.Bind(catTiming, "Simulate Examine Time", true, new ConfigDescription("Add delay based on item examine time", null, new ConfigurationManagerAttributes { Order = 1 }));

            // 5. Limits
            PMCMinLootThreshold = Config.Bind(catLimits, "PMC Min Value", 12000f, new ConfigDescription("PMC bots ignore items below this value", null, new ConfigurationManagerAttributes { Order = 4 }));
            PMCMaxLootThreshold = Config.Bind(catLimits, "PMC Max Value", 0f, new ConfigDescription("PMC bots ignore items above this value (0=disabled)", null, new ConfigurationManagerAttributes { Order = 3 }));
            ScavMinLootThreshold = Config.Bind(catLimits, "Scav Min Value", 5000f, new ConfigDescription("Scav bots ignore items below this value", null, new ConfigurationManagerAttributes { Order = 2 }));
            ScavMaxLootThreshold = Config.Bind(catLimits, "Scav Max Value", 0f, new ConfigDescription("Scav bots ignore items above this value (0=disabled)", null, new ConfigurationManagerAttributes { Order = 1 }));

            // 6. Equipment
            PMCGearToEquip = Config.Bind(catEquip, "PMC Equip Allowed", CanEquipEquipmentType.All, new ConfigDescription("Gear PMCs can equip", null, new ConfigurationManagerAttributes { Order = 4 }));
            PMCGearToPickup = Config.Bind(catEquip, "PMC Bag Allowed", CanPickupEquipmentDefaults, new ConfigDescription("Gear PMCs can put in bag", null, new ConfigurationManagerAttributes { Order = 3 }));
            ScavGearToEquip = Config.Bind(catEquip, "Scav Equip Allowed", CanEquipEquipmentType.All, new ConfigDescription("Gear Scavs can equip", null, new ConfigurationManagerAttributes { Order = 2 }));
            ScavGearToPickup = Config.Bind(catEquip, "Scav Bag Allowed", CanPickupEquipmentDefaults, new ConfigDescription("Gear Scavs can put in bag", null, new ConfigurationManagerAttributes { Order = 1 }));

            // 7. Performance
            MaxActiveLootingSystem = Config.Bind(catPerf, "Max Looting Bots", 20, new ConfigDescription("Hard limit on simultaneous looting bots (0=unlimited)", null, new ConfigurationManagerAttributes { Order = 2 }));
            LimitDistanceFromPlayer = Config.Bind(catPerf, "Distance Limit", 0, new ConfigDescription("Bots beyond this distance (m) won't loot (0=unlimited)", null, new ConfigurationManagerAttributes { Order = 1 }));
            
            // DebugLootNavigation removed from config
            
            // Log loaded values
            Plugin.Log.LogError($"[LootingSystem] Config Loaded.");
            Plugin.Log.LogError($"[LootingSystem] CorpseLootingEnabled: {CorpseLootingEnabled.Value} ({(int)CorpseLootingEnabled.Value})");
            Plugin.Log.LogError($"[LootingSystem] ContainerLootingEnabled: {ContainerLootingEnabled.Value} ({(int)ContainerLootingEnabled.Value})");
            Plugin.Log.LogError($"[LootingSystem] LooseItemLootingEnabled: {LooseItemLootingEnabled.Value} ({(int)LooseItemLootingEnabled.Value})");
        }
    }
}
