using ff14bot.Managers;
using System.Collections.Generic;
using System.Linq;

namespace ATB.Utilities
{
    /// <summary>
    /// Helper class for PvP duty selection
    /// Uses explicit name matching to filter supported PvP duties
    /// Supports: Daily Challenge: Frontline, Hidden Gorge, Crystalline Conflict (Casual Match), and Crystalline Conflict (Ranked Match)
    /// </summary>
    internal static class PvPDutyHelper
    {
        /// <summary>
        /// Gets a list of supported PvP duties using explicit name matching
        /// Only includes: Frontline, Crystalline Conflict (Casual Match), Crystalline Conflict (Ranked Match), Hidden Gorge, and Astragalos
        /// </summary>
        /// <returns>List of PvP duty IDs and names</returns>
        public static List<PvPDutyInfo> GetSupportedPvPDuties()
        {
            var duties = new List<PvPDutyInfo>();

            // Supported PvP duty names in desired order
            var supportedNames = new[]
            {
                "Daily Challenge: Frontline",
                "Hidden Gorge",
                "Crystalline Conflict (Casual Match)",
                "Crystalline Conflict (Ranked Match)"
            };

            // Get all duties that match our supported names, ordered by the list above
            var allDuties = DataManager.InstanceContentResults.Values
                .Where(d => !string.IsNullOrWhiteSpace(d.EnglishName))
                .Where(d => supportedNames.Any(supportedName =>
                    d.EnglishName.IndexOf(supportedName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            // Order by the position in supportedNames array
            var orderedDuties = supportedNames
                .SelectMany(supportedName => allDuties.Where(d =>
                    d.EnglishName.IndexOf(supportedName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var duty in orderedDuties)
            {
                duties.Add(new PvPDutyInfo
                {
                    Id = (int)duty.Id,
                    Name = duty.EnglishName
                });
            }

            return duties;
        }

        /// <summary>
        /// Gets a duty by ID (only if it's a supported PvP duty)
        /// </summary>
        public static PvPDutyInfo GetDutyById(int id)
        {
            var duty = DataManager.InstanceContentResults.Values
                .FirstOrDefault(d => d.Id == id);

            if (duty == null || string.IsNullOrWhiteSpace(duty.EnglishName))
                return null;

            // Supported PvP duty names (exact or partial matches)
            var supportedNames = new[]
            {
                "Daily Challenge: Frontline",
                "Hidden Gorge",
                "Crystalline Conflict (Casual Match)",
                "Crystalline Conflict (Ranked Match)"
            };

            var name = duty.EnglishName;
            bool isSupported = supportedNames.Any(supportedName =>
                name.IndexOf(supportedName, System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isSupported)
                return null;

            return new PvPDutyInfo
            {
                Id = (int)duty.Id,
                Name = duty.EnglishName
            };
        }
    }

    /// <summary>
    /// Information about a PvP duty
    /// </summary>
    public class PvPDutyInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}

