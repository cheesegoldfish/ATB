using ATB.Models;
using ATB.Utilities;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.RemoteAgents;
using ff14bot.RemoteWindows;
using Microsoft.VisualBasic.Logging;
using System.Threading.Tasks;
using TreeSharp;
using System;
using System.Linq;
using ff14bot.Objects;
using ff14bot.Behavior;

namespace ATB.Logic
{
    public static class Helpers
    {
        private static readonly Composite HelpersComposite;

        private const int
            Jog = 4209,
            StellarSprint = 4398;

        private const uint
            SinusArdorumZoneId = 1237;

        static Helpers()
        {
            HelpersComposite = new Decorator(new PrioritySelector(new ActionRunCoroutine(r => HelpersMethod())));
        }

        public static Composite Execute()
        {
            return HelpersComposite;
        }

        private static async Task<bool> HelpersMethod()
        {
            // Auto Skip Cutscene
            if (MainSettingsModel.Instance.UseAutoCutscene)
            {
                if (await ExecuteAutoSkipCutscene())
                    return true;
            }

            // Auto Accept Revive
            if (MainSettingsModel.Instance.AutoAcceptRevive)
            {
                if (ExecuteAutoAcceptRevive())
                    return true;
            }

            // Auto Trade
            if (MainSettingsModel.Instance.AutoTrade)
            {
                if (await ExecuteAutoTrade())
                    return true;
            }

            // Auto Sprint
            if (MainSettingsModel.Instance.AutoSprint)
            {
                if (ExecuteAutoSprint())
                    return true;
            }

            // Auto Talk
            if (MainSettingsModel.Instance.UseAutoTalk)
            {
                if (ExecuteAutoTalk())
                    return true;
            }

            // Auto Handover Request Items
            if (MainSettingsModel.Instance.AutoHandoverRequestItems)
            {
                if (await ExecuteAutoHandoverRequestItems())
                    return true;
            }

            // Auto Quest
            if (MainSettingsModel.Instance.UseAutoQuest)
            {
                if (ExecuteAutoQuest())
                    return true;
            }

            return false;
        }

        private static bool ExecuteAutoAcceptRevive()
        {
            if (Core.Me.IsDead && Core.Me.HasAura(148) && SelectYesno.IsOpen)
            {
                var str = Core.Memory.ReadStringUTF8(new IntPtr(SelectYesno.___Elements[0].Data));
                if (str.Contains("的救助吗？") || str.Contains("Accept Raise from ") || str.Contains("からの蘇生を受けますか？"))
                {
                    ClientGameUiRevive.Revive();
                    Logger.ATBLog("Accepting Revive...");
                    return true;
                }
            }
            return false;
        }

        private static bool TradeOpen => RaptureAtkUnitManager.GetWindowByName("Trade") != null;
        private static bool ContextMenuOpened => RaptureAtkUnitManager.GetWindowByName("ContextMenu") != null;
        private static bool HasValidTradeTarget => Core.Me.HasTarget && Core.Target is Character c && !c.IsMe && c.Type == GameObjectType.Pc &&
                                                   !DutyManager.InInstance && c.IsWithinInteractRange;

        private static async Task<bool> ExecuteAutoTrade()
        {
            if (TradeOpen)
            {
                if (Request.IsOpen && Request.HandOverButtonClickable)
                {
                    Request.HandOver();
                    return true;
                }

                if (HasValidTradeTarget)
                {
                    if (InputNumeric.IsOpen)
                    {
                        InputNumeric.Ok((uint)InputNumeric.Field.MaxValue);
                        await Coroutine.Wait(1000, () => !InputNumeric.IsOpen);
                        RaptureAtkUnitManager.GetWindowByName("Trade").SendAction(1, 3, 0);
                        return true;
                    }

                    if (ContextMenuOpened)
                    {
                        RaptureAtkUnitManager.GetWindowByName("ContextMenu").SendAction(3, 3, 0, 3, 0, 3, 1);
                        return true;
                    }

                    if (SelectYesno.IsOpen)
                    {
                        SelectYesno.Yes();
                        return true;
                    }
                }
                else
                {
                    if (Trade.TradeStage == 3)
                    {
                        RaptureAtkUnitManager.GetWindowByName("Trade").SendAction(1, 3, 0);
                        return true;
                    }

                    if (SelectYesno.IsOpen)
                    {
                        SelectYesno.Yes();
                        return true;
                    }
                }
            }
            else
            {
                if (HasValidTradeTarget && ContextMenuOpened)
                {
                    RaptureAtkUnitManager.GetWindowByName("ContextMenu").SendAction(3, 3, 0, 3, 2, 4, 0);
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> ExecuteAutoHandoverRequestItems()
        {
            var ShopExchangeDialog = RaptureAtkUnitManager.GetWindowByName("ShopExchangeItemDialog");
            if (ShopExchangeDialog != null)
            {
                ShopExchangeDialog.SendAction(1, 3, 0);
                Logger.ATBLog("Click ShopExchangeItemDialog Yes");
                return true;
            }

            var GrandCompanySupplyReward = RaptureAtkUnitManager.GetWindowByName("GrandCompanySupplyReward");
            if (GrandCompanySupplyReward != null)
            {
                GrandCompanySupplyReward.SendAction(1, 3, 0);
                Logger.ATBLog("Click GrandCompanySupplyReward Yes");
                return true;
            }

            if (Request.IsOpen)
            {
                try
                {
                    if (await CommonTasks.HandOverRequestedItems(false))
                    {
                        Request.HandOver();
                        Logger.ATBLog("Handing over request items...");
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    Logger.ATBLog("We don't have the required amount of a requested item.");
                    return false;
                }
            }

            return false;
        }

        private static bool ExecuteAutoSprint()
        {
            if (MainSettingsModel.Instance.AutoSprint
                && WorldManager.ZoneId == SinusArdorumZoneId
                && ActionManager.IsSprintReady
                && MovementManager.IsMoving
                && !Core.Me.HasAura(StellarSprint)
                && !WorldManager.InPvP)
            {
                ActionManager.Sprint();
                return true;
            }
            else if (MainSettingsModel.Instance.AutoSprint
                && ActionManager.IsSprintReady
                && MovementManager.IsMoving
                && !Core.Me.HasAura(Jog)
                && !WorldManager.InPvP
                && (!MainSettingsModel.Instance.AutoSprintInSanctuaryOnly || WorldManager.InSanctuary)
                && WorldManager.ZoneId != SinusArdorumZoneId)
            {
                ActionManager.Sprint();
                return true;
            }
            return false;
        }

        private static bool ExecuteAutoTalk()
        {
            if (Core.Me.IsAlive)
            {
                if (SelectYesno.IsOpen)
                {
                    SelectYesno.ClickYes();
                    return true;
                }

                if (Talk.DialogOpen)
                {
                    Talk.Next();
                    return true;
                }
            }
            return false;
        }

        private static bool ExecuteAutoQuest()
        {
            if (JournalAccept.IsOpen)
            {
                JournalAccept.Accept();
                return true;
            }

            if (JournalResult.IsOpen && JournalResult.ButtonClickable)
            {
                JournalResult.Complete();
                return true;
            }

            return false;
        }

        private static async Task<bool> ExecuteAutoSkipCutscene()
        {
            if (QuestLogManager.InCutscene)
            {
                if (AgentCutScene.Instance.CanSkip && !SelectString.IsOpen)
                {
                    AgentCutScene.Instance.PromptSkip();
                    if (await Coroutine.Wait(600, () => SelectString.IsOpen))
                    {
                        SelectString.ClickSlot(0);
                        await Coroutine.Sleep(1000);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
