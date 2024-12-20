﻿using Clio.Common;
using Clio.Utilities;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ATB.Utilities
{
    internal class ExtremeCaution
    {
        private static GameObject Target => Me.CurrentTarget;
        private static LocalPlayer Me => Core.Player;
        private static DateTime PulseLimiter;
        private static string AvertEyesSpellName = "a bad spell";
        private static string DebuffName = "debuff";
        private static string TargetName = "Your Target";

        private static bool InView(Vector3 playerLocation, float playerHeading, Vector3 targetLocation)
        {
            var d = Math.Abs(MathEx.NormalizeRadian(playerHeading - MathEx.NormalizeRadian(MathHelper.CalculateHeading(playerLocation, targetLocation) + (float)Math.PI)));

            if (d > Math.PI)
            {
                d = Math.Abs(d - 2 * (float)Math.PI);
            }
            return d < 0.78539f;
        }

        internal static bool ExtremeCautionTask()
        {
            if (Target != null)
                if (InView(Core.Player.Location, Core.Player.Heading, Target.Location) && CastingAvertEyesSpell(Target))
                {
                    if (PulseCheck())
                    {
                        Core.OverlayManager.AddToast(() => $"Danger! {TargetName} is casting {AvertEyesSpellName}!", TimeSpan.FromSeconds(2), Colors.Red, Colors.White, new FontFamily("High Tower Text Italic"), new FontWeight(), 52);
                        MovementManager.SetFacing(Me.Heading - 3);
                    }
                    return false;
                }

            var debuffAura = Me.CharacterAuras.FirstOrDefault(aura => StopActionSpellList.Contains(aura.Id) || StopActionSpellListNames.Contains(aura.Name.ToLower()));

            if (debuffAura != null && debuffAura.TimespanLeft.TotalMilliseconds <= 3000)
            {
                if (PulseCheck())
                {
                    DebuffName = DataManager.GetAuraResultById(debuffAura.Id).CurrentLocaleName;
                    Core.OverlayManager.AddToast(() => $"Danger! Stop Actions and Movement until {DebuffName} is cleared!", TimeSpan.FromSeconds(2), Colors.Red, Colors.White, new FontFamily("High Tower Text Italic"), new FontWeight(), 52);
                }
                return false;
            }

            return true;
        }

        private static bool CastingAvertEyesSpell(GameObject unit)
        {
            var unitAsCharacter = unit as Character;

            if (unitAsCharacter == null)
                return false;

            if (unitAsCharacter.IsCasting && AvertEyesSpellList.Contains(unitAsCharacter.CastingSpellId))
            {
                var avertEyesSpellId = AvertEyesSpellList.FirstOrDefault(x => x == unitAsCharacter.CastingSpellId);
                AvertEyesSpellName = DataManager.GetSpellData(avertEyesSpellId).Name;
                TargetName = Target.Name;
                return true;
            }

            if (unitAsCharacter.IsCasting && AvertEyesSpellListNames.Any(x => unitAsCharacter.SpellCastInfo.SpellData.Name.ToLower().Contains(x)))
            {
                AvertEyesSpellName = unitAsCharacter.SpellCastInfo.SpellData.Name;
                TargetName = Target.Name;
                return true;
            }

            return false;
        }

        private static readonly HashSet<uint> AvertEyesSpellList = new HashSet<uint>
        {
            934,    // Mortal Ray
            2832,   // Martal Ray
            4201,   // the Dragon's Gaze
            5154,   // Petrifaction
            5216,   // Frond Fatale
            5220,   // Frond Fatale
            5257,   // the Dragon's Gaze
            5374,   // Petrifaction
            5431,   // Petrifaction
            5788,   // Oogle
            6100,   // Mortal Ray
            6820,   // Gobsnick Leghops
            6821,   // Gobsnick Leghops
            7200,   // Hallow Nightmare
            7364,   // Flash Powder
            8951,   // Ribbet
            8951,   // Ribbet
            9318,   // Ribbet
            9485,   // Demon Eye
            9494,   // Demon Eye
            
            1969,    // Petrification
            1979,    // Petrification
            2516,    // Petrification
            2526,    // Petrification
            2824,    // Petrification
            4331,    // Petrification
            5028,    // Petrification
            5154,    // Petrification
            5374,    // Petrification
            5431,    // Petrification
            9649,    // Petrification

            934,     // Mortal Ray
            2832,    // Mortal Ray
            6100,    // Mortal Ray
        };

        private static readonly HashSet<string> AvertEyesSpellListNames = new HashSet<string>
        {
            "mortal ray",
            "petrifaction",
            "the dragon's gaze",
            "petrifaction"
        };

        private static readonly HashSet<uint> StopActionSpellList = new HashSet<uint>
        {
            639,  // Pyretic
            960,  // Pyretic
            1049, // Pyretic
            1072, // Acceleration Bomb
            1133, // Pyretic
            1147, // Shadow Links
            1132, // Extreme Caution
            1384,  // Acceleration Bomb
            3802,
        };

        private static readonly HashSet<string> StopActionSpellListNames = new HashSet<string>
        {
            "pyretic",
            "acceleration bomb",
            "shadow links",
            "extreme caution"
        };

        public static bool PulseCheck()
        {
            if (DateTime.Now < PulseLimiter) return false;
            if (DateTime.Now > PulseLimiter)
            {
                PulseLimiter = DateTime.Now.Add(TimeSpan.FromSeconds(1));
                FormManager.SaveFormInstances();
            }
            return true;
        }
    }
}