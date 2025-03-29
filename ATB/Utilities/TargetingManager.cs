using ATB.Models;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ATB.Utilities.Extensions;
using TreeSharp;
using static ATB.Utilities.Constants;
using ff14bot.Helpers;

namespace ATB.Utilities
{
    public static class TargetingManager
    {
        private const int
            Invincibility0 = 981,
            Invincibility1 = 969,
            Invincibility2 = 895,
            Invincibility3 = 776,
            Invincibility4 = 775,
            Invincibility5 = 671,
            Invincibility6 = 656,
            Invincibility7 = 529,
            Invincibility8 = 325,
            Invincibility9 = 394;

        public static readonly List<uint> Invincibility = new List<uint>
        {
            Invincibility0,
            Invincibility1,
            Invincibility2,
            Invincibility3,
            Invincibility4,
            Invincibility5,
            Invincibility6,
            Invincibility7,
            Invincibility8,
            Invincibility9
        };

        public static readonly List<uint> Pvp_Guard = new List<uint>
        {
            3054, // guard
        };

        public static readonly List<uint> Pvp_Invuln = new List<uint>
        {
            1302, // hallowed
            3039, // undead redemption
        };

        public static readonly HashSet<string> StickyAuras = new HashSet<string> {
            "wildfire",
        };

        public static readonly HashSet<string> Pvp_StickyAuras = new HashSet<string> {
            "wildfire",
            "clawed muse",
            "fanged muse",
            "bind",
            "stun",
            "heavy",
            "silence",
            "deep freeze",
            "biolysis",
            "biolytic",
            "mummification"
        };

        public static readonly string StickyAurasString = string.Join(",", StickyAuras);
        public static readonly string Pvp_StickyAurasString = string.Join(",", Pvp_StickyAuras);

        private static readonly Composite TargetingManagerComposite;
        private static DateTime _pulseLimiter;
        private static DateTime lastTargetChange = DateTime.Now;

        static TargetingManager()
        {
            TargetingManagerComposite = new Decorator(r => true, new ActionRunCoroutine(ctx => TargetingManagerTask()));
        }

        public static Composite Execute()
        {
            return TargetingManagerComposite;
        }

        private static async Task<bool> TargetingManagerTask()
        {
            if (!MainSettingsModel.Instance.UseAutoTargeting || MainSettingsModel.Instance.AutoTargetSelection == AutoTargetSelection.None) return false;

            if (Core.Me.CurrentTarget.HasAnyAura(Invincibility) && Core.Me.InCombat)
                Core.Me.ClearTarget();

            if (MainSettingsModel.Instance.UseStickyTargeting && Core.Player.HasTarget)
                return false;

            if (WorldManager.InPvP)
            {
                if (MainSettingsModel.Instance.Pvp_DetargetInvuln && Core.Me.CurrentTarget.HasAnyAura(Pvp_Invuln) && Core.Me.InCombat)
                    Core.Me.ClearTarget();

                if (MainSettingsModel.Instance.Pvp_DetargetGuard && Core.Me.CurrentTarget.HasAnyAura(Pvp_Guard) && Core.Me.InCombat)
                    Core.Me.ClearTarget();

                if (MainSettingsModel.Instance.UseStickyAuraTargeting
                    && Core.Me.CurrentTarget.HasAnyAura(Pvp_StickyAuras, true))
                    return false;

                if (MainSettingsModel.Instance.Pvp_SmartTargeting)
                {
                    if (PartyDescriptors.IsMelee(Core.Me.CurrentJob) && Core.Me.HasTarget)
                    {
                        // Stick to current target if melee
                        if (Core.Me.CurrentTarget.Distance(Core.Me) < MainSettingsModel.Instance.MaxTargetDistance / 2
                            && IsValidEnemy(Core.Me.CurrentTarget)
                            && Core.Me.CurrentTarget.InLineOfSight()
                            && !(MainSettingsModel.Instance.Pvp_DetargetInvuln && Core.Me.CurrentTarget.HasAnyAura(Pvp_Invuln))
                            && !(MainSettingsModel.Instance.Pvp_DetargetGuard && Core.Me.CurrentTarget.HasAnyAura(Pvp_Guard)))
                        {
                            return false;
                        }
                    }

                    var objs = GameObjectManager.GameObjects.Where(o =>
                        IsValidEnemy(o)
                        && ((Character)o).InCombat
                        && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance
                        && o.InLineOfSight()
                        && !(MainSettingsModel.Instance.Pvp_DetargetInvuln && o.HasAnyAura(Pvp_Invuln))
                        && !(MainSettingsModel.Instance.Pvp_DetargetGuard && o.HasAnyAura(Pvp_Guard))
                    );

                    if (objs != null && objs.Any())
                    {
                        // Calculate vulnerability score with weighted mana importance
                        // Mana is weighted at 60%, HP at 40%
                        var lowestHpTargets = objs
                            .OrderByDescending(o => o.IsDps() || o.CurrentHealthPercent <= MainSettingsModel.Instance.Pvp_SmartTargetingHp)
                            .ThenBy(o => {
                                var char_o = (Character)o;
                                var hpVulnerability = 100 - o.CurrentHealthPercent;
                                var manaVulnerability = 100 - (char_o.CurrentMana * 100.0 / 10000);
                                return (hpVulnerability * 0.4) + (manaVulnerability * 0.6); // Weighted average
                            })
                            .Where(o => o.CurrentHealthPercent <= MainSettingsModel.Instance.Pvp_SmartTargetingHp);

                        GameObject newTarget;
                        String type;
                        int ChangeThreshold;

                        if (lowestHpTargets.Any())
                        {
                            newTarget = lowestHpTargets.First();
                            var char_target = (Character)newTarget;
                            var vulnScore = ((100 - newTarget.CurrentHealthPercent) * 0.4) + 
                                           ((100 - (char_target.CurrentMana * 100.0 / 10000)) * 0.6);
                            type = $"Lowest HP {newTarget.CurrentHealthPercent}% (Mana: {char_target.CurrentMana}, Vuln: {vulnScore:F1}%)";
                            ChangeThreshold = MainSettingsModel.Instance.Pvp_Stickiness / 2 * 1000;
                        }
                        else
                        {
                            var allies = _allianceMembers.Value;
                            var targetCounts = new Dictionary<uint, int>();

                            foreach (var ally in allies)
                            {
                                if (targetCounts.ContainsKey(ally.CurrentTargetId))
                                {
                                    targetCounts[ally.CurrentTargetId]++;
                                }
                                else
                                {
                                    targetCounts[ally.CurrentTargetId] = 1;
                                }
                            }

                            // prioritize things i've debuffed already
                            // then things the team has debuffed
                            // then by who has the most targets
                            // then by lowest hp
                            var mostTargetedTargets = objs
                                .OrderByDescending(o => o.CountDebuffs(true))
                                .ThenByDescending(o => o.CountDebuffs(false))
                                .ThenByDescending(o => targetCounts.TryGetValue(o.ObjectId, out var count) ? count : 0)
                                .ThenBy(o => {
                                    var char_o = (Character)o;
                                    var hpVulnerability = 100 - o.CurrentHealthPercent;
                                    var manaVulnerability = 100 - (char_o.CurrentMana * 100.0 / 10000);
                                    return (hpVulnerability * 0.4) + (manaVulnerability * 0.6);
                                });

                            newTarget = mostTargetedTargets.FirstOrDefault();
                            var char_target = (Character)newTarget;
                            var vulnScore = ((100 - newTarget.CurrentHealthPercent) * 0.4) + 
                                           ((100 - (char_target.CurrentMana * 100.0 / 10000)) * 0.6);
                            type = $"Most Targeted {(targetCounts.TryGetValue(newTarget.ObjectId, out var count) ? count : 0)} (Vuln: {vulnScore:F1}%)";
                            ChangeThreshold = MainSettingsModel.Instance.Pvp_Stickiness * 1000;
                        }

                        if (newTarget != null)
                        {
                            if (newTarget != Me.CurrentTarget
                                && (
                                    // Sticky for enough burst
                                    lastTargetChange.AddMilliseconds(ChangeThreshold) < DateTime.Now
                                    // Or I don't have a target
                                    || !Core.Me.HasTarget
                                    // Or my target is in sight anymore
                                    || !Me.CurrentTarget.InLineOfSight()
                                    // Or my target walked out of range
                                    || Core.Me.Distance(Me.CurrentTarget) >= MainSettingsModel.Instance.MaxTargetDistance + 1
                                )
                            )
                            {
                                newTarget.Target();
                                lastTargetChange = DateTime.Now;
                                //Logger.ATBLog("PvP Smart Targeting: " + type + " Target Change!");
                            }
                        }
                    }
                    
                    return false;
                }
            }

            if (MainSettingsModel.Instance.UseStickyAuraTargeting
                && Core.Me.CurrentTarget.HasAnyAura(StickyAuras, true))
                return false;

            switch (MainSettingsModel.Instance.AutoTargetSelection)
            {
                case AutoTargetSelection.NearestEnemy:
                    if (!Core.Player.HasTarget || (!Core.Me.CurrentTarget.IsPlayer() && !IsValidEnemy(Core.Me.CurrentTarget)) || PulseCheck())
                    {
                        var target = GetClosestEnemy();
                        if (target != null && target != Me.CurrentTarget)
                        {
                            Logger.ATBLog("Nearest Enemy Target Change!");
                            target.Target();
                        }
                    }

                    break;

                case AutoTargetSelection.BestClustered:

                    if (!Core.Player.HasTarget || (!Core.Me.CurrentTarget.IsPlayer() && !IsValidEnemy(Core.Me.CurrentTarget)) || PulseCheck())
                    {
                        var objs = GameObjectManager.GameObjects.Where(o =>
                            IsValidEnemy(o)
                            && ((Character)o).InCombat
                            && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance
                            && o.InLineOfSight()
                        );
                        if (objs != null && objs.Any())
                        {
                            var targets = objs.OrderBy(t =>
                                objs.Sum(ot => t.Distance(ot))
                            ).ThenBy(Core.Me.Distance);
                            var newTarget = targets.First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                newTarget.Target();
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.LowestCurrentHpTanked:
                    if (Me.IsTank())
                    {
                        Logger.ATBLog("Yer a tank, Harry! Can't assist yerself!");
                        MainSettingsModel.Instance.AutoTargetSelection = AutoTargetSelection.None;
                        break;
                    }

                    if (PartyManager.IsInParty && VisiblePartyMembers.Any(IsTank) && (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck()))
                    {
                        {
                            var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o) && ((Character)o).InCombat
                            && ((Character)o).CurrentTargetId == PartyTank.ObjectId && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                            if (objs != null && objs.Any())
                            {
                                var newTarget = objs.OrderBy(o => o.CurrentHealth).First();
                                if (newTarget != Me.CurrentTarget)
                                {
                                    Logger.ATBLog("Lowest Current HP Tanked Target Change!");
                                    newTarget.Target();
                                }
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.LowestCurrentHp:
                    if (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck())
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => 
                            IsValidEnemy(o) 
                            && ((Character)o).InCombat 
                            && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance
                            && o.InLineOfSight()
                        );
                        if (objs != null && objs.Any())
                        {
                            var targets = objs
                                .OrderByDescending(o => o.IsDps() || o.CurrentHealthPercent <= 25)
                                .ThenBy(o => o.CurrentHealthPercent);
                            var newTarget = targets
                                .First();
                            if (newTarget != Me.CurrentTarget
                                && (
                                    // Sticky for enough burst
                                    lastTargetChange.AddSeconds(7) < DateTime.Now
                                    // Or I don't have a target
                                    || !Core.Player.HasTarget
                                    // Or my target is in sight anymore
                                    || !Me.CurrentTarget.InLineOfSight()
                                    // Or my target walked out of range
                                    || Core.Me.Distance(Me.CurrentTarget) >= MainSettingsModel.Instance.MaxTargetDistance + 3
                                )
                            )
                            {
                                // Logger.ATBLog($"Lowest Current HP Target Change!");
                                /*if (Core.Me.Distance(Me.CurrentTarget.Location) >= MainSettingsModel.Instance.MaxTargetDistance + 3)
                                {
                                    Logger.ATBLog("Current target walked out of range.");
                                }*/
                                //foreach (var i in targets.ToArray()) {
                                //    Logger.ATBLog($"{i.Name}. DPS: {i.IsDps()}. HP: {i.CurrentHealthPercent}. LOS: {i.InLineOfSight()}. Dist: {Core.Me.Distance(i.Location)}");
                                //}
                                newTarget.Target();
                                lastTargetChange = DateTime.Now;
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.LowestTotalHpTanked:
                    if (Me.IsTank())
                    {
                        Logger.ATBLog("Yer a tank, Harry! Can't assist yerself!");
                        MainSettingsModel.Instance.AutoTargetSelection = AutoTargetSelection.None;
                        break;
                    }

                    if (PartyManager.IsInParty && VisiblePartyMembers.Any(IsTank) && (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck()))
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o)
                        && ((Character)o).InCombat
                        && ((Character)o).CurrentTargetId == PartyTank.ObjectId && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                        if (objs != null && objs.Any())
                        {
                            var newTarget = objs.OrderBy(o => o.MaxHealth).First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                Logger.ATBLog("Lowest Total HP Tanked Target Change!");
                                newTarget.Target();
                            }
                        }
                    }

                    break;

                case AutoTargetSelection.LowestTotalHp:
                    if (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck())
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o) && ((Character)o).InCombat && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                        if (objs != null && objs.Any())
                        {
                            var newTarget = objs.OrderBy(o => o.MaxHealth).First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                Logger.ATBLog("Lowest Total HP Target Change!");
                                newTarget.Target();
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.HighestCurrentHpTanked:
                    if (Me.IsTank())
                    {
                        Logger.ATBLog("Yer a tank, Harry! Can't assist yerself!");
                        MainSettingsModel.Instance.AutoTargetSelection = AutoTargetSelection.None;
                        break;
                    }

                    if (PartyManager.IsInParty && VisiblePartyMembers.Any(IsTank) && (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck()))
                    {
                        {
                            var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o) && ((Character)o).InCombat
                                                                                && ((Character)o).CurrentTargetId == PartyTank.ObjectId && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                            if (objs != null && objs.Any())
                            {
                                var newTarget = objs.OrderByDescending(o => o.CurrentHealth).First();
                                if (newTarget != Me.CurrentTarget)
                                {
                                    Logger.ATBLog("Highest Current HP Tanked Target Change!");
                                    newTarget.Target();
                                }
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.HighestCurrentHp:
                    if (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck())
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o) && ((Character)o).InCombat && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                        if (objs != null && objs.Any())
                        {
                            var newTarget = objs.OrderByDescending(o => o.CurrentHealth).First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                Logger.ATBLog("Highest Current HP Target Change!");
                                newTarget.Target();
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.HighestTotalHpTanked:
                    if (Me.IsTank())
                    {
                        Logger.ATBLog("Yer a tank, Harry! Can't assist yerself!");
                        MainSettingsModel.Instance.AutoTargetSelection = AutoTargetSelection.None;
                        break;
                    }

                    if (PartyManager.IsInParty && VisiblePartyMembers.Any(IsTank) && (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck()))
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o)
                                                                            && ((Character)o).InCombat
                                                                            && ((Character)o).CurrentTargetId == PartyTank.ObjectId && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                        if (objs != null && objs.Any())
                        {
                            var newTarget = objs.OrderByDescending(o => o.MaxHealth).First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                Logger.ATBLog("Highest Total HP Tanked Target Change!");
                                newTarget.Target();
                            }
                        }
                    }

                    break;

                case AutoTargetSelection.HighestTotalHp:
                    if (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || PulseCheck())
                    {
                        var objs = GameObjectManager.GameObjects.Where(o => IsValidEnemy(o) && ((Character)o).InCombat && Core.Me.Distance(o) <= MainSettingsModel.Instance.MaxTargetDistance);
                        if (objs != null && objs.Any())
                        {
                            var newTarget = objs.OrderByDescending(o => o.MaxHealth).First();
                            if (newTarget != Me.CurrentTarget)
                            {
                                Logger.ATBLog("Highest Total HP Target Change!");
                                newTarget.Target();
                            }
                        }
                    }
                    break;

                case AutoTargetSelection.TankAssist:
                    if (Me.IsTank())
                    {
                        Logger.ATBLog("Yer a tank, Harry! Can't assist yerself!");
                        MainSettingsModel.Instance.AutoTargetSelection = AutoTargetSelection.None;
                        break;
                    }

                    if (PartyManager.IsInParty && VisiblePartyMembers.Any(IsTank) && (!Core.Player.HasTarget || !Core.Player.CurrentTarget.CanAttack || Core.Player.CurrentTarget != PartyTank.TargetCharacter))
                    {
                        Assist(VisiblePartyMembers.First(x => !x.IsMe && x.IsTank()));
                    }
                    break;

                default:
                    return false;
            }
            return false;
        }

        public static GameObject GetClosestEnemy()
        {
            return GameObjectManager.GameObjects.Where(u =>
                    IsValidEnemy(u)
                    && Core.Me.Distance(u) <= MainSettingsModel.Instance.MaxTargetDistance)
                .OrderBy(u => Core.Me.Distance(u)).FirstOrDefault();
        }

        public static bool IsTank(Character c)
        {

            try
            {
                BattleCharacter bc = (BattleCharacter)c;

                return bc.IsTank();
            }
            catch (Exception ex)
            {
                //BadCasting this sux.
                return false;
            }
        }

        public static Character PartyTank
        {
            get
            {
                if (VisiblePartyMembers.Count <= 0) return null;
                try
                {
                    return VisiblePartyMembers.First(IsTank);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static List<Character> VisiblePartyMembers
        {
            get
            {
                var members = new List<Character>();
                if (!PartyManager.IsInParty)
                    members.Add(Core.Player);
                else
                    members.AddRange(from pm in PartyManager.AllMembers where pm.IsInObjectManager select (Character)GameObjectManager.GetObjectByObjectId(pm.ObjectId));
                return members;
            }
        }

        public static void Assist(Character c)
        {
            var target = GameObjectManager.GetObjectByObjectId(c.CurrentTargetId);
            if (target != null && target.IsTargetable && target.IsValid && target.CanAttack)
            {
                Logger.ATBLog(@"Assisting " + c.SafeName());
                target.Target();
            }
        }

        public static bool IsValidEnemy(GameObject obj)
        {
            if (!(obj is Character))
                return false;
            var c = (Character)obj;
            return !c.IsMe 
                && !c.IsDead
                && c.IsValid
                && c.IsTargetable
                && c.IsVisible
                && c.InLineOfSight()
                && c.CanAttack 
                && !c.HasAnyAura(Invincibility)
                && !c.Name.Contains("Raven")
                && !c.Name.Contains("Falcon") 
                && !c.Name.Contains("Striking Dummy")
                && !c.Name.Contains("Icebound Tomelith");
        }

        public static bool IsValidAlly(GameObject obj)
        {
            if (!(obj is Character))
                return false;
            var c = (Character)obj;
            return !c.IsMe 
                && !c.IsDead
                && c.IsValid
                && c.IsTargetable
                && c.IsVisible 
                && !c.CanAttack
                && c.InLineOfSight()
                && c.Type == GameObjectType.Pc;
        }

        private static readonly FrameCachedObject<IEnumerable<Character>> _allianceMembers = new(() => GameObjectManager.GetObjectsOfType<BattleCharacter>().Where(i => i != null && i.IsValid).Where(IsValidAlly));

        public static bool PulseCheck()
        {
            if (DateTime.Now < _pulseLimiter) return false;
            if (DateTime.Now > _pulseLimiter)
                if (MainSettingsModel.Instance.AutoTargetSelection == AutoTargetSelection.LowestCurrentHp)
                    _pulseLimiter = DateTime.Now.Add(TimeSpan.FromMilliseconds(250));
                else
                    _pulseLimiter = DateTime.Now.Add(TimeSpan.FromSeconds(3));
            return true;
        }
    }
}
