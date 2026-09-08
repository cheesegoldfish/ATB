using ff14bot;
using ff14bot.Managers;
using ff14bot.Objects;
using System;
using System.Collections.Generic;

namespace ATB.Utilities
{
    /// <summary>What we know about a mount's ability to carry a pillion passenger.</summary>
    internal enum MountSeats
    {
        /// <summary>The mount has extra seats - boarding it is worth attempting.</summary>
        Passengers,

        /// <summary>The mount is single-seat. Attempting to board it can only fail.</summary>
        SoloOnly,

        /// <summary>We could not read the mount, so we know nothing. Callers must not guess.</summary>
        Unknown
    }

    /// <summary>
    /// Reads which mount a character is actually riding.
    ///
    /// RB does not expose this: <see cref="BattleCharacter.Mount"/> resolves to null, the Mount-type
    /// actors in the object table come back with a zeroed LuaString and a bogus ObjectId, and
    /// <see cref="ActionManager.MountId"/> is a red herring - it returns CharacterSettings.MountId,
    /// the *preferred mount* configured in RB's own settings window, not what anyone is riding.
    ///
    /// The data is in the character struct, in the MountContainer the game keeps there:
    ///
    ///     Character + 0x680  GameObject*  the mount actor  (stale after dismount)
    ///     Character + 0x688  ushort       Mount sheet row id  (zeroed on dismount)
    ///
    /// Verified on RB 1.0.902 against 25 players in one sweep: every one of the 22 unmounted players
    /// read 0, and the mounted one read 217 = Ruby Gwiber. A pillion pair (host and passenger) both
    /// read the host's mount id.
    ///
    /// Offsets are patch-sensitive. Everything here is written so that a bad read reports
    /// <see cref="MountSeats.Unknown"/> rather than a wrong answer - see <see cref="ReadMountId"/>.
    /// </summary>
    internal static class MountReader
    {
        private const int MountObjectOffset = 0x680;
        private const int MountIdOffset = 0x688;

        /// <summary>Rejects reads that clearly are not a user-space pointer.</summary>
        private const long MinPlausiblePointer = 0x10000;
        private const long MaxPlausiblePointer = 0x7FFFFFFFFFFF;

        /// <summary>
        /// Every mount with ExtraSeats &gt; 0, from the Mount sheet. The value is the extra seat count,
        /// so a "+1" mount seats two people in total.
        ///
        /// Kept as a literal table rather than read from the client because RB's DataManager.MountCache
        /// carries names only - MountResult has no seat data. That cache does hold exactly one entry per
        /// Mount sheet row (451 on this patch, matching the sheet), so these ids line up with what the
        /// client actually loaded.
        ///
        /// Refresh after a patch that adds a multi-seat mount: ExtraSeats is column 40 of
        /// csv/en/Mount.csv in xivapi/ffxiv-datamining.
        /// </summary>
        private static readonly Dictionary<uint, int> ExtraSeats = new Dictionary<uint, int>
        {
            {  34, 1 },   // Draught Chocobo
            {  41, 1 },   // Ceremony Chocobo
            {  52, 1 },   // Amber Draught Chocobo
            {  83, 1 },   // Astrope
            {  84, 1 },   // Fat Moogle
            { 151, 3 },   // Regalia Type-G
            { 160, 1 },   // Indigo Whale
            { 188, 3 },   // Skyslipper
            { 222, 3 },   // Chocobo Carriage
            { 233, 7 },   // Lunar Whale
            { 235, 3 },   // Cerberus
            { 245, 1 },   // Landerwaffe
            { 246, 1 },   // Al-iklil
            { 247, 1 },   // Cruise Chaser
            { 271, 1 },   // unnamed in the sheet on this patch
            { 312, 3 },   // Blackjack
            { 318, 1 },   // Garlond GL-IS
            { 335, 1 },   // Garlond GL-IIT
            { 382, 3 },   // Air-wheeler C9
            { 383, 3 },   // Ancient Airship
        };

        private static bool _offsetWarningLogged;

        /// <summary>
        /// The Mount sheet row the character is riding, or 0 when they are not mounted or the read
        /// cannot be trusted.
        ///
        /// Three independent checks have to agree before we believe a value, because a patch that moves
        /// the struct turns this into a read of unrelated bytes:
        ///   - the character says it is mounted;
        ///   - the mount actor slot holds something shaped like a pointer;
        ///   - the id is a real row in RB's mount cache (451 valid values out of 65536, so noise fails here).
        /// </summary>
        internal static uint ReadMountId(BattleCharacter character)
        {
            if (character == null) return 0;

            try
            {
                if (!character.IsValid || !character.IsMounted) return 0;

                var mountActor = Core.Memory.Read<long>(character.Pointer + MountObjectOffset);
                if (mountActor < MinPlausiblePointer || mountActor > MaxPlausiblePointer)
                {
                    WarnOffsetOnce("mount actor slot held 0x" + mountActor.ToString("X"));
                    return 0;
                }

                uint mountId = Core.Memory.Read<ushort>(character.Pointer + MountIdOffset);
                if (mountId == 0 || DataManager.GetMountData(mountId) == null)
                {
                    WarnOffsetOnce("mount id read back as " + mountId);
                    return 0;
                }

                return mountId;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Whether this character's mount can take us as a passenger.</summary>
        internal static MountSeats GetSeats(BattleCharacter character)
        {
            var mountId = ReadMountId(character);
            if (mountId == 0) return MountSeats.Unknown;
            return ExtraSeats.ContainsKey(mountId) ? MountSeats.Passengers : MountSeats.SoloOnly;
        }

        /// <summary>
        /// True when both characters are sitting on the same mount actor - the exact test for
        /// "one of these two is riding pillion with the other".
        /// </summary>
        internal static bool ShareMountActor(BattleCharacter a, BattleCharacter b)
        {
            if (a == null || b == null) return false;

            try
            {
                if (!a.IsValid || !b.IsValid || !a.IsMounted || !b.IsMounted) return false;

                var actorA = Core.Memory.Read<long>(a.Pointer + MountObjectOffset);
                if (actorA < MinPlausiblePointer || actorA > MaxPlausiblePointer) return false;

                return actorA == Core.Memory.Read<long>(b.Pointer + MountObjectOffset);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Mount name for logging. Falls back to the raw id for rows the sheet leaves unnamed.</summary>
        internal static string MountName(uint mountId)
        {
            if (mountId == 0) return "unknown mount";

            try
            {
                var data = DataManager.GetMountData(mountId);
                var name = data == null ? null : data.CurrentLocaleName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch (Exception)
            {
            }

            return "mount #" + mountId;
        }

        /// <summary>
        /// A patch moving the struct silently disables every mount check we make, so it gets said out
        /// loud - once, not once per tick.
        /// </summary>
        private static void WarnOffsetOnce(string detail)
        {
            if (_offsetWarningLogged) return;
            _offsetWarningLogged = true;
            Logger.ATBLog(
                "[MountReader] Cannot read mount ids (" + detail + "). The 0x688 offset has probably moved - " +
                "AutoPillion will not board anyone until it is refreshed.");
        }
    }
}
