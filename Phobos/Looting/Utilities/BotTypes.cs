using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;

using Phobos.Looting.Components;

namespace Phobos.Looting.Utilities
{
    [Flags]
    public enum BotType
    {
        Scav = 1,
        Pmc = 2,
        PlayerScav = 4,
        Raider = 8,
        Cultist = 16,
        Boss = 32,
        Follower = 64,
        Bloodhound = 128,

        All = Scav | Pmc | PlayerScav | Raider | Cultist | Boss | Follower | Bloodhound,
    }

    public static class BotTypeUtils
    {
        public static bool HasScav(this BotType botType)
        {
            return botType.HasFlag(BotType.Scav);
        }

        public static bool HasPmc(this BotType botType)
        {
            return botType.HasFlag(BotType.Pmc);
        }

        public static bool HasPlayerScav(this BotType botType)
        {
            return botType.HasFlag(BotType.PlayerScav);
        }

        public static bool HasRaider(this BotType botType)
        {
            return botType.HasFlag(BotType.Raider);
        }

        public static bool HasCultist(this BotType botType)
        {
            return botType.HasFlag(BotType.Cultist);
        }

        public static bool HasBoss(this BotType botType)
        {
            return botType.HasFlag(BotType.Boss);
        }

        public static bool HasFollower(this BotType botType)
        {
            return botType.HasFlag(BotType.Follower);
        }

        public static bool HasBloodhound(this BotType botType)
        {
            return botType.HasFlag(BotType.Bloodhound);
        }

        public static bool IsBotEnabled(this BotType enabledTypes, LootingBrain brain)
        {
            if (brain.IsPlayerScav)
            {
                return enabledTypes.HasPlayerScav();
            }
            WildSpawnType role = brain.BotOwner.Profile.Info.Settings.Role;
            return IsBotEnabled(enabledTypes, role);
        }

        public static bool IsBotEnabled(this BotType enabledTypes, WildSpawnType botType)
        {
            bool isPmc = IsPMC(botType);
            bool isBoss = IsBoss(botType);
            bool result = false;

            if (isPmc)
            {
                result = enabledTypes.HasPmc();
            }
            else if (isBoss)
            {
                result = enabledTypes.HasBoss();
            }
            else 
            {
                switch (botType)
                {
                    case WildSpawnType.assault:
                    case WildSpawnType.assaultGroup:
                        result = enabledTypes.HasScav();
                        break;
                    case WildSpawnType.followerBigPipe:
                    case WildSpawnType.followerBirdEye:
                    case WildSpawnType.followerBully:
                    case WildSpawnType.followerGluharAssault:
                    case WildSpawnType.followerGluharScout:
                    case WildSpawnType.followerGluharSecurity:
                    case WildSpawnType.followerGluharSnipe:
                    case WildSpawnType.followerKojaniy:
                    case WildSpawnType.followerSanitar:
                    case WildSpawnType.followerTagilla:
                    case WildSpawnType.followerTest:
                    case WildSpawnType.followerZryachiy:
                    case WildSpawnType.followerKolontayAssault:
                    case WildSpawnType.followerKolontaySecurity:
                    case WildSpawnType.bossBoarSniper:
                    case WildSpawnType.followerBoarClose1:
                    case WildSpawnType.followerBoarClose2:
                    case WildSpawnType.followerBoar:
                        result = enabledTypes.HasFollower();
                        break;
                    case WildSpawnType.exUsec:
                    case WildSpawnType.pmcBot:
                        result = enabledTypes.HasRaider();
                        break;
                    case WildSpawnType.sectantPriest:
                    case WildSpawnType.sectantWarrior:
                    case WildSpawnType.cursedAssault:
                        result = enabledTypes.HasCultist();
                        break;
                    case WildSpawnType.arenaFighter:
                    case WildSpawnType.arenaFighterEvent:
                    case WildSpawnType.crazyAssaultEvent:
                        result = enabledTypes.HasBloodhound();
                        break;
                    default:
                        result = false;
                        break;
                }
            }

            return result;
        }

        public static bool IsPMC(WildSpawnType wildSpawnType)
        {
            return wildSpawnType == WildSpawnType.pmcBEAR || wildSpawnType == WildSpawnType.pmcUSEC;
        }

        public static bool IsScav(WildSpawnType wildSpawnType)
        {
            return wildSpawnType == WildSpawnType.assault || wildSpawnType == WildSpawnType.assaultGroup;
        }

        public static bool IsBoss(WildSpawnType wildSpawnType)
        {
            List<WildSpawnType> bosses = new List<WildSpawnType>
            {
                WildSpawnType.bossBully,
                WildSpawnType.bossGluhar,
                WildSpawnType.bossKilla,
                WildSpawnType.bossKnight,
                WildSpawnType.bossKojaniy,
                WildSpawnType.bossSanitar,
                WildSpawnType.bossTagilla,
                WildSpawnType.bossTest,
                WildSpawnType.bossZryachiy,
                WildSpawnType.bossBoar,
                WildSpawnType.bossKolontay,
                WildSpawnType.bossPartisan,
            };
            return bosses.Contains(wildSpawnType);
        }
    }
}
