using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Objects;
using System.Collections.Generic;
using System.Linq;

namespace ATB.Utilities.Extensions
{
    internal static class GameObjectExtensions
    {
        private static GameObject Target => Me.CurrentTarget;
        private static LocalPlayer Me => Core.Player;

        #region SafeNames

        public static bool ShowPlayerNames = false;

        public static string SafeDisplayName(this GameObject obj)
        {
            if (obj.IsMe)
            {
                return "Me";
            }

            string name;
            var character = obj as BattleCharacter;
            if (character != null)
            {
                name = character.CanAttack ? "Enemy -> " : "Ally -> ";
                if (ShowPlayerNames) name += character.Name;
                //else name += character.CurrentJob.ToString();
            }
            else
            {
                name = obj.Name;
            }

            return name + obj.Name;
        }

        #endregion SafeNames

        #region Name Helpers

        /// <summary>
        /// Gets the name of the object with fallback to Name if EnglishName is null.
        /// Returns empty string if both are null to avoid null reference exceptions.
        /// </summary>
        public static string SafeName(this GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            return !string.IsNullOrEmpty(obj.EnglishName) ? obj.EnglishName : (obj.Name ?? string.Empty);
        }

        #endregion Name Helpers

        internal static bool HasAura(this GameObject unit, uint spell, bool isMyAura = false, double msLeft = 0)
        {
            var unitasc = unit as Character;
            if (unit == null || unitasc == null || !unitasc.IsValid)
            {
                return false;
            }
            var auras = isMyAura
                ? unitasc.CharacterAuras.Where(r => r.CasterId == Me.ObjectId && r.Id == spell)
                : unitasc.CharacterAuras.Where(r => r.Id == spell);

            return auras.Any(aura => aura.TimespanLeft.TotalMilliseconds >= msLeft);
        }

        internal static bool HasAnyAuraOfMine(this GameObject unit)
        {
            var unitasc = unit as Character;

            if (unit == null || unitasc == null || !unitasc.IsValid)
            {
                return false;
            }

            var auras = unitasc.CharacterAuras.Where(r => r.CasterId == Me.ObjectId);

            return auras.Any();
        }

        internal static bool HasAnyAura(this GameObject unit, List<uint> auras, bool isMyAura = false)
        {
            var unitasc = unit as Character;

            if (unit == null || unitasc == null || !unitasc.IsValid)
            {
                return false;
            }

            return isMyAura
                ? unitasc.CharacterAuras.Any(r => auras.Contains(r.Id) && r.CasterId == Me.ObjectId)
                : unitasc.CharacterAuras.Any(r => auras.Contains(r.Id));
        }

        internal static bool HasAnyAura(this GameObject unit, HashSet<string> auras, bool isMyAura = false)
        {
            var unitasc = unit as Character;

            if (unit == null || unitasc == null || !unitasc.IsValid)
            {
                return false;
            }

            return isMyAura
                ? unitasc.CharacterAuras.Any(r => auras.Contains(r.Name.ToLower()) && r.CasterId == Me.ObjectId)
                : unitasc.CharacterAuras.Any(r => auras.Contains(r.Name.ToLower()));
        }

        internal static int CountDebuffs(this GameObject unit, bool isMyAura = false)
        {
            var unitasc = unit as Character;

            if (unit == null || unitasc == null || !unitasc.IsValid)
            {
                return 0;
            }

            return isMyAura
                ? unitasc.CharacterAuras.Count(r => r.IsDebuff && r.CasterId == Me.ObjectId)
                : unitasc.CharacterAuras.Count(r => r.IsDebuff);
        }

        public static bool IsPlayer(this GameObject tar)
        {
            var gameObject = tar as Character;
            return gameObject != null && (IsTank(tar) || IsHealer(tar) || IsDps(tar));
        }

        public static bool IsTank(this GameObject tar)
        {
            var gameObject = tar as Character;
            return gameObject != null && Tanks.Contains(gameObject.CurrentJob);
        }

        public static bool IsHealer(this GameObject tar)
        {
            var gameObject = tar as Character;
            return gameObject != null && Healers.Contains(gameObject.CurrentJob);
        }

        public static bool IsDps(this GameObject tar)
        {
            var gameObject = tar as Character;
            return gameObject != null && Dps.Contains(gameObject.CurrentJob);
        }

        public static bool IsWarMachina(this GameObject unit)
        {
            if (unit == null)
                return false;

            var name = unit.SafeName();
            if (string.IsNullOrEmpty(name))
                return false;

            return name.Contains("Raven")
                || name.Contains("Falcon")
                || name.Contains("Striking Dummy")
                || name.Contains("Icebound Tomelith")
                || name.Contains("Interceptor");
        }

        public static bool HealthCheck(this GameObject tar, int healthInt, float healthPercent)
        {
            if (tar == null)
                return false;

            // If our target has more health than our setting and more health percent than our health percent setting, return true, else, return false
            return tar.CurrentHealth > healthInt && tar.CurrentHealthPercent > healthPercent;

            //// If our target has more hp percent than our hp percent setting but has less health than our health setting, return false
            //if (tar.CurrentHealthPercent > healthPercent && tar.CurrentHealth < healthInt)
            //    return false;

            //// if our target has more health than our setting but less health percent than our hp percent setting, return false
            //if (tar.CurrentHealth > healthInt && tar.CurrentHealthPercent < healthPercent)
            //    return false;

            //// if our target has less health than our setting and less health than our percent setting, return false
            //return tar.CurrentHealth >= healthInt || !(tar.CurrentHealthPercent < healthPercent);
        }

        #region Helpers

        private static readonly List<ClassJobType> Tanks = new List<ClassJobType>()
        {
            ClassJobType.Gladiator,
            ClassJobType.Marauder,
            ClassJobType.Paladin,
            ClassJobType.Warrior,
            ClassJobType.DarkKnight,
            ClassJobType.Gunbreaker
        };

        private static readonly List<ClassJobType> Healers = new List<ClassJobType>()
        {
            ClassJobType.Conjurer,
            ClassJobType.Scholar,
            ClassJobType.WhiteMage,
            ClassJobType.Astrologian,
            ClassJobType.Sage
        };

        private static readonly List<ClassJobType> Dps = new List<ClassJobType>()
        {
            ClassJobType.Arcanist,
            ClassJobType.Archer,
            ClassJobType.Bard,
            ClassJobType.Thaumaturge,
            ClassJobType.BlackMage,
            ClassJobType.Lancer,
            ClassJobType.Dragoon,
            ClassJobType.Pugilist,
            ClassJobType.Monk,
            ClassJobType.Ninja,
            ClassJobType.Machinist,
            ClassJobType.Rogue,
            ClassJobType.Reaper,
            ClassJobType.RedMage,
            ClassJobType.Dancer,
            ClassJobType.Samurai,
            ClassJobType.Viper,
            ClassJobType.Pictomancer
        };

        public static IEnumerable<BattleCharacter> PartyMembers
        {
            get
            {
                return
                    PartyManager.VisibleMembers
                    .Select(pm => pm.GameObject as BattleCharacter)
                    .Where(pm => pm.IsTargetable);
            }
        }

        public static IEnumerable<BattleCharacter> HealManager
        {
            get
            {
                return
                    GameObjectManager.GetObjectsOfType<BattleCharacter>(true, true)
                    .Where(hm => hm.IsAlive && (PartyMembers.Contains(hm) || hm == Core.Player))
                    .OrderBy(HpScore);
            }
        }

        private static float HpScore(BattleCharacter c)
        {
            var score = c.CurrentHealthPercent;

            if (c.IsTank())
            {
                score -= 5f;
            }
            if (c.IsHealer())
            {
                score -= 3f;
            }
            return score;
        }

        /// <summary>
        /// Calculates the effective combat distance between this GameObject and another GameObject.
        /// The effective combat distance is the actual distance minus the combat reach of both objects.
        /// </summary>
        /// <param name="source">The source GameObject</param>
        /// <param name="target">The target GameObject</param>
        /// <returns>The effective combat distance</returns>
        public static float EffectiveCombatDistance(this GameObject source, GameObject target)
        {
            if (source == null || target == null)
                return float.MaxValue;

            return source.Distance(target) - source.CombatReach - target.CombatReach;
        }

        /// <summary>
        /// Checks if the target is within the specified combat distance from this GameObject.
        /// </summary>
        /// <param name="source">The source GameObject</param>
        /// <param name="target">The target GameObject</param>
        /// <param name="distance">The maximum effective combat distance</param>
        /// <returns>True if the target is within the specified combat distance</returns>
        public static bool WithinCombatReach(this GameObject source, GameObject target, float distance)
        {
            return source.EffectiveCombatDistance(target) <= distance;
        }

        /// <summary>
        /// Checks if this GameObject is within the specified combat distance from the player (Core.Me).
        /// </summary>
        /// <param name="source">The source GameObject</param>
        /// <param name="distance">The maximum effective combat distance</param>
        /// <returns>True if this GameObject is within the specified combat distance from the player</returns>
        public static bool WithinCombatReach(this GameObject source, float distance)
        {
            return source.EffectiveCombatDistance(Core.Me) <= distance;
        }

        #endregion Helpers
    }
}