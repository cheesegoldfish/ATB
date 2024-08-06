using ff14bot.Enums;

namespace ATB.Utilities
{
    public static class PartyDescriptors
    {
        public static bool IsTank(ClassJobType c)
        {
            switch (c)
            {
                case ClassJobType.Marauder:
                case ClassJobType.Warrior:
                case ClassJobType.Paladin:
                case ClassJobType.Gladiator:
                case ClassJobType.DarkKnight:
                case ClassJobType.Gunbreaker:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsDps(ClassJobType c)
        {
            switch (c)
            {
                case ClassJobType.Adventurer:
                case ClassJobType.Archer:
                case ClassJobType.Arcanist:
                case ClassJobType.Bard:
                case ClassJobType.BlackMage:
                case ClassJobType.Dragoon:
                case ClassJobType.Lancer:
                case ClassJobType.Machinist:
                case ClassJobType.Monk:
                case ClassJobType.Ninja:
                case ClassJobType.Pugilist:
                case ClassJobType.Rogue:
                case ClassJobType.Summoner:
                case ClassJobType.Thaumaturge:
                case ClassJobType.Samurai:
                case ClassJobType.RedMage:
                case ClassJobType.Dancer:
                case ClassJobType.Reaper:
                case ClassJobType.Viper:
                case ClassJobType.Pictomancer:
                case ClassJobType.BlueMage:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsMelee(ClassJobType c)
        {
            if (IsTank(c))
                return true;

            switch (c)
            {
                case ClassJobType.Adventurer:
                case ClassJobType.Dragoon:
                case ClassJobType.Monk:
                case ClassJobType.Ninja:
                case ClassJobType.Samurai:
                case ClassJobType.Reaper:
                case ClassJobType.Viper:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsRanged(ClassJobType c)
        {
            if (IsHealer(c))
                return true;

            switch (c)
            {
                case ClassJobType.Adventurer:
                case ClassJobType.Bard:
                case ClassJobType.BlackMage:
                case ClassJobType.Machinist:
                case ClassJobType.Summoner:
                case ClassJobType.RedMage:
                case ClassJobType.Dancer:
                case ClassJobType.Pictomancer:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsHealer(ClassJobType c)
        {
            switch (c)
            {
                case ClassJobType.Scholar:
                case ClassJobType.WhiteMage:
                case ClassJobType.Conjurer:
                case ClassJobType.Astrologian:
                case ClassJobType.Sage:
                    return true;

                default:
                    return false;
            }
        }
    }
}