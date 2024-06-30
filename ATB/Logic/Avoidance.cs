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
    public class Avoidance
    {
        private static readonly Composite AvoidanceComposite;
        private static bool TargetConverted => TargetConverted(Target);

        static Avoidance()
        {
            AvoidanceComposite = new Decorator(r =>
                false,
                new ActionRunCoroutine(ctx => AvoidanceTask()));
        }

        public static Composite Execute()
        {
            return AvoidanceComposite;
        }

        private static async Task<bool> AvoidanceTask()
        {
            return false;
        }
    }
}