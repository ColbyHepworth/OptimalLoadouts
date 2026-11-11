using RimWorld;
using Verse;

namespace OptimalLoadouts.Orders
{
    /// <summary>One line of a stat order: a stat, and whether higher or lower values are better.</summary>
    public class OrderLine : IExposable
    {
        public StatDef stat;
        public bool lowerIsBetter;

        public OrderLine()
        {
        }

        public OrderLine(StatDef stat, bool lowerIsBetter = false)
        {
            this.stat = stat;
            this.lowerIsBetter = lowerIsBetter;
        }

        public OrderLine Copy()
        {
            return new OrderLine(stat, lowerIsBetter);
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
        }
    }
}
