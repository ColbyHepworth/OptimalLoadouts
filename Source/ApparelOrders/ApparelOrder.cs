using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// An apparel policy's own priorities, the base policy whose priorities go under them, and the items its priorities
    /// that rule pieces out unticked in the policy's checklist.
    /// </summary>
    public class ApparelOrder : IExposable
    {
        public ApparelPolicy policy;
        public ApparelPolicy basePolicy;
        public List<OrderLine> lines = new List<OrderLine>();

        /// <summary>The items this policy's ruling-out priorities unticked, so they tick back when none catches them.</summary>
        public List<ThingDef> unticked = new List<ThingDef>();

        /// <summary>The material the policy's cutoffs judge in, or null for the usual one (or the best, with <see cref="limitBest"/>).</summary>
        public ThingDef limitStuff;

        public bool limitBest = true;

        /// <summary>Whether pawns on the policy add a piece to an empty slot only when it helps the priorities.</summary>
        public bool onlyWhatHelps;

        /// <summary>Which material the policy's cutoffs judge apparel in and its preview imagines pieces in.</summary>
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
            Scribe_Collections.Look(ref unticked, "unticked", LookMode.Def);
            Scribe_Defs.Look(ref limitStuff, "limitStuff");
            Scribe_Values.Look(ref limitBest, "limitBest", true);
            Scribe_Values.Look(ref onlyWhatHelps, "onlyWhatHelps", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null)
                {
                    lines = new List<OrderLine>();
                }
                lines.RemoveAll(line => line == null || line.Subject == null);
                // Locked items rank above every stat, so they're kept first, as moving lines keeps them.
                lines = lines.Where(line => line.IsItem).Concat(lines.Where(line => !line.IsItem)).ToList();
                if (unticked == null)
                {
                    unticked = new List<ThingDef>();
                }
                unticked.RemoveAll(def => def == null);
            }
        }
    }
}
