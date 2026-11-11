using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>An apparel policy's own stat order, and the base policy whose order goes under it.</summary>
    public class ApparelOrder : IExposable
    {
        public ApparelPolicy policy;
        public ApparelPolicy basePolicy;
        public List<OrderLine> lines = new List<OrderLine>();

        public void ExposeData()
        {
            Scribe_References.Look(ref policy, "policy");
            Scribe_References.Look(ref basePolicy, "base");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null)
                {
                    lines = new List<OrderLine>();
                }
                lines.RemoveAll(line => line == null || line.stat == null);
            }
        }
    }
}
