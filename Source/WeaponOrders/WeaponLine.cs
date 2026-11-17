using System;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>
    /// One weapon priority on a policy: a number, whether higher or lower is better, the value it counts from, and whether
    /// it rules out weapons past that value, as an apparel priority does.
    /// </summary>
    public sealed class WeaponLine : IExposable
    {
        /// <summary>Numbers closer than this are equal.</summary>
        private const float Epsilon = 0.0001f;

        public WeaponNumberDef number;

        public bool lowerIsBetter;

        /// <summary>While ranking, values smaller than this count as none. While ruling out, the cutoff. 0 counts every amount.</summary>
        public float countsFrom;

        /// <summary>
        /// Leaves out weapons past <see cref="countsFrom"/>: below it on a higher-first line ("at least"), above it on a
        /// lower-first one ("at most"). A weapon without the number fails "at least" and passes "at most".
        /// </summary>
        public bool rulesOut;

        public WeaponLine()
        {
        }

        public WeaponLine(WeaponNumberDef number, bool lowerIsBetter)
        {
            this.number = number;
            this.lowerIsBetter = lowerIsBetter;
        }

        public WeaponLine Copy()
        {
            return new WeaponLine(number, lowerIsBetter) { countsFrom = countsFrom, rulesOut = rulesOut };
        }

        /// <summary>The value as the line ranks it: none under <see cref="countsFrom"/> while it ranks, as it is otherwise.</summary>
        public float? Counted(float? value)
        {
            if (value == null || rulesOut || countsFrom <= 0f)
            {
                return value;
            }
            return Math.Abs(value.Value) < countsFrom - Epsilon ? 0f : value;
        }

        /// <summary>Whether the line leaves out a weapon with this value.</summary>
        public bool RulesOut(float? value)
        {
            if (!rulesOut)
            {
                return false;
            }
            if (lowerIsBetter)
            {
                return value != null && value.Value > countsFrom + Epsilon;
            }
            return value == null || value.Value < countsFrom - Epsilon;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref number, "number");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
            Scribe_Values.Look(ref countsFrom, "countsFrom", 0f);
            Scribe_Values.Look(ref rulesOut, "rulesOut", false);
        }
    }
}
