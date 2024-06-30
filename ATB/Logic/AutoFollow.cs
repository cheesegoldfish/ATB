using ATB.Models;
using ATB.Utilities;
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
    public class AutoFollow
    {
        private static readonly Composite AutoFollowComposite;
        private static bool TargetConverted => TargetConverted(Target);

        static AutoFollow()
        {
            AutoFollowComposite = new Decorator(r =>
                MainSettingsModel.Instance.AutoFollowSelection == AutoFollowSelection.Smart,
                new ActionRunCoroutine(ctx => AutoFollowTask()));
        }

        public static Composite Execute()
        {
            return AutoFollowComposite;
        }

        private static async Task<bool> AutoFollowTask()
        {
            if (AvoidanceManager.IsRunningOutOfAvoid)
                return false;


            var result = false;

            switch (MainSettingsModel.Instance.AutoFollowSelection)
            {
                case AutoFollowSelection.Smart:
                    result = await FollowSmart();
                    break;
            }

            return result;
        }

        private static async Task<bool> FollowSmart()
        {
            // Perform AutoSprint
            // Perform MountDismount not combat
            // Perform Flight not combat


            // Find the location we will want to move depending on the 
            // environment 
            // in combat, center positions.
            // out of combat, party leader.

            // 1. Find location that looks good.
            // 2. If location is too far.
            // 3. Navigate to that location.



            return false;
        }
    }
}