using ATB.Models;
using ATB.Utilities;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Directors;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Objects;
using ff14bot.RemoteAgents;
using System;
using System.Linq;
using TreeSharp;
using Action = TreeSharp.Action;

namespace ATB.Logic
{
    internal static class DutyManagement
    {
        private static readonly Composite DutyManagementComposite;

        // Timer tracking for auto-leave delay
        private static DateTime? _autoLeaveExpiration;

        // Timer tracking for auto-register delay
        private static DateTime? _autoRegisterExpiration;

        // Timer tracking for auto-register throttling
        private static DateTime? _lastRegisterAttempt;

        // Track last queue state to avoid spam logging
        private static QueueState? _lastQueueState;

        // Track when the last treasure was opened (to allow time for loot window to appear)
        private static DateTime? _lastTreasureOpened;

        // Track if we've already logged that the instance ended (to prevent spam)
        private static bool _hasLoggedInstanceEnded;

        // Track last log message to prevent duplicate logs
        private static string _lastLogMessage = string.Empty;

        static DutyManagement()
        {
            DutyManagementComposite = new PrioritySelector(
                new Decorator(r => ShouldLeaveDuty(), new Action(r => LeaveDuty())),
                new Decorator(r => ShouldRegisterDuty(), new Action(r => RegisterDuty()))
            );
        }

        internal static Composite Execute()
        {
            return DutyManagementComposite;
        }

        private static bool ShouldLeaveDuty()
        {
            if (!MainSettingsModel.Instance.AutoLeaveDuty)
            {
                _autoLeaveExpiration = null;
                return false;
            }

            if (!DutyManager.InInstance)
            {
                _autoLeaveExpiration = null;
                return false;
            }

            var icDirector = DirectorManager.ActiveDirector as InstanceContentDirector;
            if (icDirector == null)
            {
                // Not an instance content director, can't check if ended
                _autoLeaveExpiration = null;
                _hasLoggedInstanceEnded = false;
                return false;
            }

            if (!icDirector.InstanceEnded)
            {
                // Reset timer when instance hasn't ended yet
                _autoLeaveExpiration = null;
                _hasLoggedInstanceEnded = false;
                return false;
            }

            // Instance has ended - log this only once
            if (!_hasLoggedInstanceEnded)
            {
                LogMessage("Instance has ended, checking conditions for auto-leave...");
                _hasLoggedInstanceEnded = true;
            }

            // Instance has ended, check conditions
            // Check if there are any treasures that haven't been opened
            var unopenedTreasures = GameObjectManager.GetObjectsOfType<Treasure>(true)
                .Where(t => t.IsTargetable && t.State == 0);
            if (unopenedTreasures.Any())
            {
                LogMessage($"Cannot leave: {unopenedTreasures.Count()} unopened treasure(s) remaining");
                _lastTreasureOpened = null; // Reset timer if there are still unopened treasures
                return false;
            }

            // Check for various loot-related window names
            // Common loot window names to check (trying multiple variations)
            string[] lootWindowNames = {
                "NeedGreed",
                "LootNotification",
                "Loot",
                "LootRoll",
                "NeedGreedDialog",
                "LootNotice",
                "LootDialog"
            };

            bool hasLootWindow = false;
            string foundWindowName = null;

            foreach (var windowName in lootWindowNames)
            {
                try
                {
                    var window = RaptureAtkUnitManager.GetWindowByName(windowName);
                    if (window != null && window.IsVisible)
                    {
                        hasLootWindow = true;
                        foundWindowName = windowName;
                        break;
                    }
                }
                catch
                {
                    // Window might not exist, continue checking others
                }
            }

            if (hasLootWindow)
            {
                // Reset timer when we detect a loot window (loot is still available)
                _lastTreasureOpened = null;
                LogMessage($"Cannot leave duty: Loot window is open ({foundWindowName})");
                return false;
            }

            // If we had opened a treasure recently, give it a short grace period for loot window to appear
            // But only if we're within 3 seconds of opening (not 10 seconds - that's too long)
            if (_lastTreasureOpened.HasValue)
            {
                var timeSinceTreasureOpened = DateTime.Now - _lastTreasureOpened.Value;
                if (timeSinceTreasureOpened.TotalSeconds < 3)
                {
                    // Just wait silently, don't log
                    return false;
                }
                else
                {
                    // More than 3 seconds have passed and no loot window, safe to clear
                    _lastTreasureOpened = null;
                }
            }

            // Track when treasures are opened (for the grace period above)
            var openedTreasures = GameObjectManager.GetObjectsOfType<Treasure>(true)
                .Where(t => t.IsTargetable && t.State > 0);
            if (openedTreasures.Any() && !_lastTreasureOpened.HasValue)
            {
                // A treasure was just opened, mark the time
                _lastTreasureOpened = DateTime.Now;
                LogMessage("Treasure opened, waiting for loot window...");
                return false; // Wait at least one tick to see if loot window appears
            }

            // Check if in combat
            // if (Core.Me.InCombat)
            // {
            //     LogMessage("Cannot leave: Still in combat");
            //     return false;
            // }

            // Check if we can leave
            if (!DutyManager.CanLeaveActiveDuty)
            {
                LogMessage("Cannot leave: CanLeaveActiveDuty is false");
                return false;
            }

            // Handle timing - if 0 seconds, leave immediately
            if (MainSettingsModel.Instance.SecondsToAutoLeaveDuty == 0)
            {
                LogMessage("All conditions met, leaving duty immediately (0 second delay)");
                _lastLogMessage = string.Empty; // Reset for next state
                return true;
            }

            // Initialize timer if not set
            if (!_autoLeaveExpiration.HasValue)
            {
                _autoLeaveExpiration = DateTime.Now.AddSeconds(MainSettingsModel.Instance.SecondsToAutoLeaveDuty);
                LogMessage($"All conditions met, starting {MainSettingsModel.Instance.SecondsToAutoLeaveDuty} second countdown...");
                _lastLogMessage = string.Empty; // Reset so countdown messages will log
                return false;
            }

            // Check if timer has expired
            if (DateTime.Now >= _autoLeaveExpiration.Value)
            {
                LogMessage("Countdown finished, leaving duty now");
                _autoLeaveExpiration = null;
                _lastLogMessage = string.Empty; // Reset so next state change will log
                return true;
            }
            else
            {
                // Just log that we're waiting (deduplication will prevent spam)
                LogMessage("Waiting to leave...");
            }

            return false;
        }

        private static void LogMessage(string message)
        {
            // Only log if the message is different from the last one (prevents spam)
            if (message != _lastLogMessage)
            {
                _lastLogMessage = message;
                Logger.ATBLog(message);
            }
        }

        private static RunStatus LeaveDuty()
        {
            Logger.ATBLog("Leaving Duty...");
            DutyManager.LeaveActiveDuty();
            _autoLeaveExpiration = null;
            return RunStatus.Success;
        }

        private static bool ShouldRegisterDuty()
        {
            if (!Core.IsInGame)
            {
                _autoRegisterExpiration = null;
                return false;
            }

            if (CommonBehaviors.IsLoading)
            {
                _autoRegisterExpiration = null;
                return false;
            }

            if (!MainSettingsModel.Instance.AutoRegisterDuties)
            {
                _autoRegisterExpiration = null;
                return false;
            }

            if (DutyManager.QueueState != QueueState.None)
            {
                // Reset timer when already in queue
                _autoRegisterExpiration = null;
                // Only log if state changed or if we're not in an instance (to avoid spam while in dungeon)
                if (_lastQueueState != DutyManager.QueueState && !DutyManager.InInstance)
                {
                    Logger.ATBLog($"Auto Register: Already in queue (State: {DutyManager.QueueState})");
                }
                _lastQueueState = DutyManager.QueueState;
                return false;
            }

            // Reset last queue state when queue is clear
            _lastQueueState = null;

            if (MainSettingsModel.Instance.DutyToRegister == 0)
            {
                Logger.ATBLog("Auto Register: No duty selected (DutyToRegister is 0)");
                return false;
            }

            // Check if duty is available (only register PvP duties)
            // Note: Some duties like "Daily Challenge: Frontline" have IsInDutyFinder: False but are still queueable
            var duty = DataManager.InstanceContentResults.Values
                .FirstOrDefault(d => d.Id == (uint)MainSettingsModel.Instance.DutyToRegister);

            if (duty == null)
            {
                Logger.ATBLog($"Auto Register: Duty with ID {MainSettingsModel.Instance.DutyToRegister} not found!");
                _autoRegisterExpiration = null;
                return false;
            }

            // Handle timing - if 0 seconds, register immediately
            if (MainSettingsModel.Instance.SecondsToAutoRegisterDuty == 0)
            {
                // Reset timer if delay is disabled
                _autoRegisterExpiration = null;
                // Throttle registration attempts (every 5 seconds)
                var now = DateTime.Now;
                if (!_lastRegisterAttempt.HasValue || now >= _lastRegisterAttempt.Value.AddSeconds(5))
                {
                    _lastRegisterAttempt = now;
                    Logger.ATBLog($"Auto Register: Attempting to queue '{duty.EnglishName}' (ID: {duty.Id}, IsInDutyFinder: {duty.IsInDutyFinder})");
                    return true;
                }
                return false;
            }

            // Initialize timer if not set
            if (!_autoRegisterExpiration.HasValue)
            {
                _autoRegisterExpiration = DateTime.Now.AddSeconds(MainSettingsModel.Instance.SecondsToAutoRegisterDuty);
                Logger.ATBLog($"Auto Register: Starting {MainSettingsModel.Instance.SecondsToAutoRegisterDuty} second countdown before queueing '{duty.EnglishName}'...");
                return false;
            }

            // Check if timer has expired
            if (DateTime.Now >= _autoRegisterExpiration.Value)
            {
                Logger.ATBLog($"Auto Register: Countdown finished, attempting to queue '{duty.EnglishName}'");
                _autoRegisterExpiration = null;
                return true;
            }
            else
            {
                // Still waiting, don't log every tick to avoid spam
                return false;
            }
        }

        private static RunStatus RegisterDuty()
        {
            try
            {
                var duty = DataManager.InstanceContentResults.Values
                    .FirstOrDefault(d => d.Id == (uint)MainSettingsModel.Instance.DutyToRegister);

                if (duty == null)
                {
                    Logger.ATBLog("Selected duty not found!");
                    _autoRegisterExpiration = null;
                    return RunStatus.Failure;
                }

                // Always set IsInDutyFinder to true when queueing, even if the duty has it as False
                // Some duties like "Daily Challenge: Frontline" have IsInDutyFinder: False but are still queueable
                DutyManager.Queue(new InstanceContentResult
                {
                    Id = duty.Id,
                    IsInDutyFinder = true,
                    ChnName = duty.ChnName,
                    EngName = duty.EnglishName
                });

                Logger.ATBLog($"Queued duty: {duty.EnglishName}");
                _autoRegisterExpiration = null;
                return RunStatus.Success;
            }
            catch (ArgumentException e)
            {
                Logger.ATBLog($"Error queuing duty: {e.Message}");
                _autoRegisterExpiration = null;
                return RunStatus.Failure;
            }
        }

    }
}

