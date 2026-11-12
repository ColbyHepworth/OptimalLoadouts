using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// An apparel policy's own stat order and forbids, the base policy whose order and forbids go under them, and the
    /// items its forbids unticked in the policy's checklist.
    /// </summary>
    public class ApparelOrder : IExposable
    {
        public ApparelPolicy policy;
        public ApparelPolicy basePolicy;
        public List<OrderLine> lines = new List<OrderLine>();
        public List<ApparelForbid> forbids = new List<ApparelForbid>();

        /// <summary>The items this policy's forbids unticked, so they tick back when no forbid catches them.</summary>
        public List<ThingDef> unticked = new List<ThingDef>();

        public void ExposeData()
        {
            Scribe_References.Look(ref policy, "policy");
            Scribe_References.Look(ref basePolicy, "base");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Collections.Look(ref forbids, "forbids", LookMode.Deep);
            Scribe_Collections.Look(ref unticked, "unticked", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null)
                {
                    lines = new List<OrderLine>();
                }
                lines.RemoveAll(line => line == null || line.stat == null);
                if (forbids == null)
                {
                    forbids = new List<ApparelForbid>();
                }
                forbids.RemoveAll(forbid => forbid == null || forbid.stat == null);
                if (unticked == null)
                {
                    unticked = new List<ThingDef>();
                }
                unticked.RemoveAll(def => def == null);
            }
        }
    }
}
