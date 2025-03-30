using ATB.Models;
using ATB.Utilities;
using ATB.Utilities.Extensions;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Objects;
using System.Linq;
using System.Threading.Tasks;
using TreeSharp;
using static ATB.Utilities.Constants;

namespace ATB.Logic
{
    public class Battle
    {
        private static readonly Composite BattleComposite;
        private static bool TargetConverted => TargetConverted(Target);

        static Battle()
        {
            BattleComposite = new Decorator(r => Core.Me.IsAlive, new ActionRunCoroutine(ctx => BattleTask()));
        }

        public static Composite Execute()
        {
            return BattleComposite;
        }

        private static async Task<bool> BattleTask()
        {
            if (Me.IsDead)
            {
                return false;
            }

            if (Me.InCombat  
                || (WorldManager.InPvP && WorldManager.ZoneId != 250) 
                // Sometimes striking dummies can have invalid TaggerTypes and look tapped but are not.
                || (Target != null && TargetConverted && ConvertedTarget().TaggerType != 0 && !Target.EnglishName.Contains("Dummy"))
                || PartyManager.VisibleMembers.Any(x => x.BattleCharacter != null && x.BattleCharacter.InCombat))
                return await BrainBehavior.CombatLogic.ExecuteCoroutine();

            if (Me.InCombat) return false;
            if (Me.IsMounted) return false;

            if (RoutineManager.Current.RestBehavior != null)
                await RoutineManager.Current.RestBehavior.ExecuteCoroutine();

            if (RoutineManager.Current.PreCombatBuffBehavior != null)
                await RoutineManager.Current.PreCombatBuffBehavior.ExecuteCoroutine();

            if (RoutineManager.Current.HealBehavior != null)
                await RoutineManager.Current.HealBehavior.ExecuteCoroutine();

            if (PartyManager.IsInParty && MainSettingsModel.Instance.UseSmartPull)
            {
                if (Me.CurrentTarget != null && TargetConverted && TargetingManager.IsValidEnemy(Core.Player.CurrentTarget))
                {
                    var targetsTarget = ConvertedTarget().TargetGameObject;
                    var party = PartyManager.VisibleMembers;
                    var tank = party.FirstOrDefault(x => PartyDescriptors.IsTank(x.Class))?.GameObject;
                    var targetingParty = ((tank != null && targetsTarget == tank) || party.Any(x => x.GameObject == targetsTarget));

                    if (Target != null && TargetConverted && targetingParty)
                    {
                        if (RoutineManager.Current.PullBuffBehavior != null && TargetingManager.IsValidEnemy(Core.Player.CurrentTarget))
                            await RoutineManager.Current.PullBuffBehavior.ExecuteCoroutine();

                        if (RoutineManager.Current.PullBehavior != null 
                            && MainSettingsModel.Instance.UseSmartPull 
                            && TargetingManager.IsValidEnemy(Core.Player.CurrentTarget) 
                            && Core.Player.CurrentTarget.Location.Distance3D(Core.Player.Location) <= RoutineManager.Current.PullRange + Core.Player.CurrentTarget.CombatReach)

                            return await RoutineManager.Current.PullBehavior.ExecuteCoroutine();
                    }
                }

                var tankCheck = PartyManager.VisibleMembers;
                if (tankCheck.Any(x => PartyDescriptors.IsTank(x.Class) && x.BattleCharacter != Core.Me))
                {
                    return false;
                }
            }

            if (RoutineManager.Current.PullBuffBehavior != null && TargetingManager.IsValidEnemy(Core.Player.CurrentTarget))
                await RoutineManager.Current.PullBuffBehavior.ExecuteCoroutine();

            if (Me.CurrentTarget == null) return false;

            if (RoutineManager.Current.PullBehavior != null && MainSettingsModel.Instance.UsePull && TargetingManager.IsValidEnemy(Core.Player.CurrentTarget) && Core.Player.CurrentTarget.Location.Distance3D(Core.Player.Location) <= RoutineManager.Current.PullRange + Core.Player.CurrentTarget.CombatReach)
                return await RoutineManager.Current.PullBehavior.ExecuteCoroutine();

            return false;
        }
    }
}
