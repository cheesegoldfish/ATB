using ATB.Models;
using ATB.Utilities;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TreeSharp;

namespace ATB.Logic
{
    /// <summary>
    /// Automatically rides pillion on a party member's multi-seat mount.
    ///
    /// If a party member is sitting on a mount that takes passengers and we are close enough to
    /// board it, we board it.
    ///
    /// Implementation notes (verified against the live client):
    ///   - The ride is issued with the game's own "/ridepillion &lt;t&gt; [seat]" text command rather than
    ///     by calling into the client. Dalamud plugins that do this call ExecuteCommandManager
    ///     directly through FFXIVClientStructs signatures, which is what makes them crash the game
    ///     when a patch moves things around. A text command is parsed exactly as if it were typed,
    ///     so the worst case here is a harmless error line in the chat log.
    ///   - Seats fall through: asking for seat 1 on an occupied seat places us in the next free one.
    ///   - RB exposes no accessor for the mount someone is riding, but the id is in the character
    ///     struct and <see cref="MountReader"/> reads it, so hosts on single-seat mounts are filtered
    ///     out before we ever target them. Only when that read cannot be trusted do we fall back to
    ///     attempting and observing, backing off per <see cref="HostState"/>.
    ///   - A refusal from the client arrives as a SystemErrorMessages line in the game log, which is
    ///     both faster and more informative than watching for a seat that never fills.
    /// </summary>
    public static class AutoPillion
    {
        private static readonly Composite AutoPillionComposite;

        /// <summary>Seat to ask for. The client automatically slides us to the next free seat if taken.</summary>
        private const int PreferredSeat = 1;

        /// <summary>Consecutive failures at a given distance before we stop pestering a host.</summary>
        private const int MaxAttemptsPerEpisode = 2;

        /// <summary>Hard cap of attempts for one mount episode, however much closer we get.</summary>
        private const int MaxTotalAttemptsPerEpisode = 5;

        /// <summary>Getting this much closer than our best failed attempt earns a fresh try.</summary>
        private const float DistanceImprovementForRetry = 3f;

        /// <summary>
        /// Game log kinds carry flags in their high bits - a Damage line arrives as 8873, not 41 - so the
        /// kind has to be masked out before it can be compared to <see cref="ff14bot.Enums.MessageType"/>.
        /// </summary>
        private const int MessageKindMask = 0x7F;

        /// <summary>How far back through the game log to look for our own refusal. A busy fight fills it fast.</summary>
        private const int MaxLogScanback = 80;

        /// <summary>
        /// Refusals we can fix by walking closer. Everything else the client says is treated as final for
        /// the current mount episode, which is the safe way round: an unrecognised refusal costs us one
        /// ride we might have got, whereas a misread distance error puts us back in a retry loop.
        ///
        /// These are matched against the client's own wording, so they are English-client strings. On a
        /// client in another language nothing matches, every refusal reads as final, and the bot simply
        /// stops asking - the same fail-closed behaviour the mount read uses.
        /// </summary>
        private static readonly string[] DistanceRefusalHints = { "too far", "out of range", "not close enough" };

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

        private static readonly Dictionary<uint, HostState> HostStates = new Dictionary<uint, HostState>();
        private static DateTime _nextAttemptAllowed = DateTime.MinValue;

        // Hopping off a mount is a deliberate act - we stay off for a while rather than immediately
        // climbing back on. Tracked here rather than per-host because the intent is "leave me alone",
        // not "leave me alone about that one person".
        private static DateTime _dismountCooldownUntil = DateTime.MinValue;
        private static bool _wasPassenger;
        private static uint _currentHostId;

        /// <summary>Per-host bookkeeping for the current "mount episode" (one continuous mounted period).</summary>
        private class HostState
        {
            public int Failures;
            public int TotalAttempts;
            public float ClosestFailedDistance = float.MaxValue;
            public DateTime LastAttempt = DateTime.MinValue;
            public bool WasMounted;

            /// <summary>
            /// The client refused for a reason that closing the distance cannot fix. Unlike
            /// <see cref="Failures"/> this survives the distance-improvement rule - the whole point is
            /// that no amount of walking closer will change the answer.
            /// </summary>
            public bool HardRefused;

            public void ResetEpisode()
            {
                Failures = 0;
                TotalAttempts = 0;
                ClosestFailedDistance = float.MaxValue;
                HardRefused = false;
            }
        }

        static AutoPillion()
        {
            AutoPillionComposite = new Decorator(
                r => MainSettingsModel.Instance.AutoPillion,
                new ActionRunCoroutine(r => PillionTask())
            );
        }

        public static Composite Execute()
        {
            return AutoPillionComposite;
        }

        /// <summary>
        /// True when we are a passenger on someone else's mount (as opposed to riding our own).
        ///
        /// Two independent signals, either of which is sufficient:
        ///   - We and a party member reference the same mount actor, which is only true of a shared mount.
        ///   - We are welded to a mounted party member's exact coordinates, ditto.
        /// The second covers us when the mount offsets cannot be read, so this keeps working on the
        /// patch that moves them.
        ///
        /// <c>ActionManager.MountId</c> is deliberately not consulted: it returns CharacterSettings.MountId,
        /// RB's *preferred mount* setting (-1 while the chocobo is stabled), not what anyone is riding.
        /// </summary>
        public static bool IsPillionPassenger
        {
            get
            {
                try
                {
                    var me = Core.Me;
                    if (me == null || !me.IsValid || !me.IsMounted)
                        return false;

                    if (!PartyManager.IsInParty)
                        return false;

                    foreach (var member in PartyManager.VisibleMembers)
                    {
                        try
                        {
                            var bc = member?.BattleCharacter;
                            if (bc == null || !bc.IsValid || bc.ObjectId == me.ObjectId) continue;
                            if (!bc.IsMounted) continue;
                            if (MountReader.ShareMountActor(me, bc)) return true;
                            if (me.Location.Distance(bc.Location) < 0.1f) return true;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
                catch (Exception)
                {
                }

                return false;
            }
        }

        private static async Task<bool> PillionTask()
        {
            try
            {
                var me = Core.Me;
                if (me == null || !me.IsValid || !me.IsAlive)
                    return false;

                // Keep episode bookkeeping current even while we are riding, so a host who hops off
                // and back on gets a clean slate.
                UpdateEpisodes();

                if (TrackPassengerTransition())
                    return false;

                if (!BoardingConditionsMet(me)) return false;
                if (DateTime.Now < _nextAttemptAllowed) return false;

                var host = FindPillionHost();
                if (host == null) return false;

                // Can't board theirs while sitting on our own - get off first.
                if (me.IsMounted)
                {
                    Logger.ATBLog($"[AutoPillion] Dismounting to ride with {host.Name}");
                    ActionManager.Dismount();
                    _nextAttemptAllowed = DateTime.Now.AddSeconds(1);
                    await Coroutine.Sleep(500);
                    return true;
                }

                return await AttemptRide(host);
            }
            catch (Exception ex)
            {
                Logger.ATBLog($"[AutoPillion] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Watches for the moment we stop being a passenger and decides whether that was our doing.
        ///
        /// If the mount is still running when we come off it, we jumped - the rider wants to go do
        /// something, so we stay off for <see cref="MainSettingsModel.PillionCooldown"/> seconds instead
        /// of instantly climbing back on. If the mount is gone, the host dismounted and dropped us,
        /// which carries no such intent and earns no cooldown.
        /// </summary>
        /// <returns>True while we are aboard, meaning there is nothing else to do this tick.</returns>
        private static bool TrackPassengerTransition()
        {
            if (IsPillionPassenger)
            {
                _wasPassenger = true;
                if (_currentHostId == 0)
                    _currentHostId = FindCurrentHostId();
                return true;
            }

            if (_wasPassenger)
            {
                _wasPassenger = false;

                var host = GetPartyMember(_currentHostId);
                if (host != null && host.IsMounted)
                {
                    var cooldown = MainSettingsModel.Instance.PillionCooldown;
                    _dismountCooldownUntil = DateTime.Now.AddSeconds(cooldown);
                    Logger.ATBLog($"[AutoPillion] Hopped off {host.Name}'s mount - staying off for {cooldown}s");
                }

                _currentHostId = 0;
            }

            return false;
        }

        /// <summary>Identifies whose mount we are sitting on by exact position match.</summary>
        private static uint FindCurrentHostId()
        {
            try
            {
                foreach (var member in PartyManager.VisibleMembers)
                {
                    try
                    {
                        var bc = member?.BattleCharacter;
                        if (bc == null || !bc.IsValid || bc.IsMe) continue;
                        if (!bc.IsMounted) continue;
                        if (Core.Me.Location.Distance(bc.Location) < 0.1f) return bc.ObjectId;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (Exception)
            {
            }

            return 0;
        }

        private static BattleCharacter GetPartyMember(uint objectId)
        {
            if (objectId == 0) return null;

            try
            {
                foreach (var member in PartyManager.VisibleMembers)
                {
                    try
                    {
                        var bc = member?.BattleCharacter;
                        if (bc != null && bc.IsValid && bc.ObjectId == objectId) return bc;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>State gates for attempting to board. Excludes the <see cref="_nextAttemptAllowed"/> throttle.</summary>
        private static bool BoardingConditionsMet(BattleCharacter me)
        {
            if (me == null) return false;
            if (!PartyManager.IsInParty) return false;
            if (DateTime.Now < _dismountCooldownUntil) return false;
            if (me.InCombat || me.IsCasting) return false;
            if (WorldManager.InPvP) return false;
            if (MovementManager.IsFlying || MovementManager.IsDiving) return false;
            if (MovementManager.MovementLocked) return false;

            return true;
        }

        /// <summary>
        /// Picks the nearest party member worth trying. Hosts we have already failed on are skipped
        /// until they remount or we close a meaningful amount of distance.
        /// </summary>
        private static BattleCharacter FindPillionHost()
        {
            BattleCharacter best = null;
            var bestDistance = float.MaxValue;
            var range = MainSettingsModel.Instance.PillionRange;

            foreach (var member in PartyManager.VisibleMembers)
            {
                try
                {
                    var bc = member?.BattleCharacter;
                    if (bc == null || !bc.IsValid || bc.IsMe) continue;
                    if (!bc.IsMounted || !bc.IsAlive) continue;

                    var distance = Core.Me.Distance(bc);
                    if (distance > range) continue;

                    // A single-seat mount can never take us, and we can tell before touching the target.
                    // Unknown counts as no - a mount we cannot identify is a mount we do not board.
                    if (MountReader.GetSeats(bc) != MountSeats.Passengers) continue;

                    if (!ShouldAttemptHost(GetState(bc.ObjectId), distance)) continue;

                    if (distance < bestDistance)
                    {
                        best = bc;
                        bestDistance = distance;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return best;
        }

        /// <summary>
        /// Backoff policy for the failures we cannot attribute. A refusal the client explained sets
        /// <see cref="HostState.HardRefused"/> and never reaches the rest of this; what is left is
        /// silence, which usually means distance, so distance is what earns a retry: closing
        /// <see cref="DistanceImprovementForRetry"/> yalms on our best failed attempt is a material
        /// change in conditions and resets the failure count.
        /// </summary>
        private static bool ShouldAttemptHost(HostState state, float currentDistance)
        {
            if (DateTime.Now - state.LastAttempt < RetryDelay)
                return false;

            // The client told us why, and it was not distance. Done with this mount.
            if (state.HardRefused)
                return false;

            if (state.TotalAttempts >= MaxTotalAttemptsPerEpisode)
                return false;

            if (state.Failures < MaxAttemptsPerEpisode)
                return true;

            // Given up at the old distance - only a significantly closer approach reopens it.
            if (currentDistance <= state.ClosestFailedDistance - DistanceImprovementForRetry)
            {
                state.Failures = 0;
                return true;
            }

            return false;
        }

        private static async Task<bool> AttemptRide(BattleCharacter host)
        {
            var state = GetState(host.ObjectId);
            var distance = Core.Me.Distance(host);
            var previousTarget = Core.Me.CurrentTarget;

            state.LastAttempt = DateTime.Now;
            state.TotalAttempts++;
            _nextAttemptAllowed = DateTime.Now.AddSeconds(3);

            try
            {
                // "/ridepillion" resolves <t>, so the host has to be targeted for the duration of the
                // command. Party slot placeholders (<1>-<8>) would avoid this, but RB exposes no party
                // list index to derive them from, and guessing wrong boards the wrong person.
                host.Target();
                await Coroutine.Sleep(150);

                var target = Core.Me.CurrentTarget;
                if (target == null || target.ObjectId != host.ObjectId)
                {
                    state.Failures++;
                    state.ClosestFailedDistance = Math.Min(state.ClosestFailedDistance, distance);
                    return false;
                }

                var mountName = MountReader.MountName(MountReader.ReadMountId(host));
                Logger.ATBLog($"[AutoPillion] Riding pillion with {host.Name} on {mountName} ({distance:F1}y)");

                // Mark our place in the game log before speaking, so we can pick our own refusal out of
                // whatever else is scrolling past.
                var logMarker = NewestLogEntry();
                ChatManager.SendChat($"/ridepillion <t> {PreferredSeat}");

                // Give the client time to seat us, bailing the moment it does - or the moment it tells
                // us it will not, which is the common case.
                string refusal = null;
                var refusalKind = 0;
                for (var i = 0; i < 12 && !IsPillionPassenger; i++)
                {
                    await Coroutine.Sleep(125);
                    refusal = FindRefusalSince(logMarker, out refusalKind);
                    if (refusal != null) break;
                }

                // Death and revival notices share the channel we read refusals from, and both mean combat
                // started under us. Anything caught then says nothing about the mount, so it is discarded
                // and the attempt falls back to being an ordinary timeout.
                if (refusal != null && Core.Me.InCombat)
                    refusal = null;

                if (IsPillionPassenger)
                {
                    state.ResetEpisode();
                    MovementManager.MoveStop();
                    Logger.ATBLog($"[AutoPillion] Aboard {host.Name}'s mount");
                    return true;
                }

                state.Failures++;
                state.ClosestFailedDistance = Math.Min(state.ClosestFailedDistance, distance);

                if (refusal != null)
                {
                    if (IsDistanceRefusal(refusal))
                    {
                        Logger.ATBLog($"[AutoPillion] Too far from {host.Name} at {distance:F1}y - will retry closer (\"{refusal}\")");
                    }
                    else
                    {
                        state.HardRefused = true;
                        Logger.ATBLog($"[AutoPillion] {host.Name}'s {mountName} will not take us: \"{refusal}\" (log kind {refusalKind}) - dropping it for this mount");
                    }
                }
                else if (state.Failures >= MaxAttemptsPerEpisode)
                {
                    Logger.ATBLog($"[AutoPillion] {host.Name}'s mount refused a passenger at {distance:F1}y - waiting for a remount or a closer approach");
                }

                return false;
            }
            finally
            {
                try
                {
                    if (previousTarget != null && previousTarget.IsValid)
                        previousTarget.Target();
                    else
                        Core.Me.ClearTarget();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>Where the game log ends right now, used as a "read from here" bookmark.</summary>
        private static ChatLogEntry NewestLogEntry()
        {
            try
            {
                var buffer = GamelogManager.CurrentBuffer;
                return buffer != null && buffer.Count > 0 ? buffer[buffer.Count - 1] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The client's objection to our command, if it has arrived yet.
        ///
        /// Walks back from the end of the game log to <paramref name="marker"/> by reference identity
        /// rather than by timestamp: game log entries are stamped in server time (five hours off local
        /// here) and only to the second, so a clock comparison would be both wrong and too coarse.
        ///
        /// The kind we match on is RB's SystemErrorMessages, but that name oversells it - "You are
        /// defeated by the trade tortoise." and "You are revived." arrive on the same channel. Those only
        /// happen in combat, and the caller drops anything it caught while combat was running.
        /// <paramref name="rawKind"/> is the unmasked kind, logged so a real refusal can be pinned to an
        /// exact value later.
        /// </summary>
        private static string FindRefusalSince(ChatLogEntry marker, out int rawKind)
        {
            rawKind = 0;

            try
            {
                var buffer = GamelogManager.CurrentBuffer;
                if (buffer == null) return null;

                var floor = Math.Max(0, buffer.Count - MaxLogScanback);
                for (var i = buffer.Count - 1; i >= floor; i--)
                {
                    var entry = buffer[i];
                    if (ReferenceEquals(entry, marker)) break;
                    if (entry == null) continue;

                    var kind = (int)entry.MessageType;
                    if ((kind & MessageKindMask) != (int)ff14bot.Enums.MessageType.SystemErrorMessages)
                        continue;

                    var text = string.IsNullOrEmpty(entry.Contents) ? entry.FullLine : entry.Contents;
                    if (string.IsNullOrEmpty(text)) continue;

                    rawKind = kind;
                    return text;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static bool IsDistanceRefusal(string refusal)
        {
            if (string.IsNullOrEmpty(refusal)) return false;

            foreach (var hint in DistanceRefusalHints)
            {
                if (refusal.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A mount episode is one continuous mounted period. When a host dismounts, whatever we learned
        /// about their mount stops applying - the next one may well take passengers.
        /// </summary>
        private static void UpdateEpisodes()
        {
            var live = new HashSet<uint>();

            foreach (var member in PartyManager.VisibleMembers)
            {
                try
                {
                    var bc = member?.BattleCharacter;
                    if (bc == null || !bc.IsValid || bc.IsMe) continue;

                    live.Add(bc.ObjectId);
                    var state = GetState(bc.ObjectId);
                    var mounted = bc.IsMounted;

                    if (state.WasMounted && !mounted)
                        state.ResetEpisode();

                    state.WasMounted = mounted;
                }
                catch (Exception)
                {
                    continue;
                }
            }

            if (HostStates.Count > live.Count)
            {
                foreach (var stale in HostStates.Keys.Where(id => !live.Contains(id)).ToList())
                    HostStates.Remove(stale);
            }
        }

        private static HostState GetState(uint objectId)
        {
            HostState state;
            if (!HostStates.TryGetValue(objectId, out state))
            {
                state = new HostState();
                HostStates[objectId] = state;
            }
            return state;
        }
    }
}
