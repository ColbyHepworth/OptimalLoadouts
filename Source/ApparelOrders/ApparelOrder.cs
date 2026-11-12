using System.Collections.Generic;
using System.Linq;
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

        /// <summary>The material picked for the policy's limits, or null for the usual one (or the best, with <see cref="limitBest"/>).</summary>
        public ThingDef limitStuff;

        public bool limitBest;

        /// <summary>Which material the policy's limits judge apparel in.</summary>
        public LimitMaterial LimitMaterial
        {
            get => limitBest ? LimitMaterial.Best : LimitMaterial.Of(limitStuff);
            set
            {
                limitBest = value.IsBest;
                limitStuff = value.Stuff;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref policy, "policy");
            Scribe_References.Look(ref basePolicy, "base");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Collections.Look(ref forbids, "forbids", LookMode.Deep);
            Scribe_Collections.Look(ref unticked, "unticked", LookMode.Def);
            Scribe_Defs.Look(ref limitStuff, "limitStuff");
            Scribe_Values.Look(ref limitBest, "limitBest", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null)
                {
                    lines = new List<OrderLine>();
                }
                lines.RemoveAll(line => line == null || line.Subject == null);
                // Locked items rank above every stat, so they're kept first, as moving lines keeps them.
                lines = lines.Where(line => line.IsItem).Concat(lines.Where(line => !line.IsItem)).ToList();
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
