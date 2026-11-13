using System;
using System.Collections.Generic;
using OptimalLoadouts.ApparelOrders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.Orders
{
    /// <summary>
    /// One line of a stat order: either a stat, with whether higher or lower values are better, the value it counts from
    /// and, for apparel, the layers it counts on; or a locked item, worth 1 on a piece of that item and 0 on anything else.
    /// </summary>
    public class OrderLine : IExposable
    {
        /// <summary>The line's stat, or null on an item line.</summary>
        public StatDef stat;

        public bool lowerIsBetter;

        /// <summary>
        /// Apparel only: the layers this line counts on, or empty for every layer. A piece on several layers counts if any
        /// one is in it.
        /// </summary>
        public List<ApparelLayerDef> layers = new List<ApparelLayerDef>();

        /// <summary>The locked item on an item line, or null on a stat line.</summary>
        public ThingDef item;

        /// <summary>Values smaller than this, above or below zero, count as none on this line. 0 counts every amount.</summary>
        public float countsFrom;

        public OrderLine()
        {
        }

        public OrderLine(StatDef stat, bool lowerIsBetter = false)
        {
            this.stat = stat;
            this.lowerIsBetter = lowerIsBetter;
        }

        /// <summary>A line that locks one item: it beats everything not locked.</summary>
        public static OrderLine ForItem(ThingDef item)
        {
            return new OrderLine { item = item };
        }

        public bool IsItem => item != null;

        /// <summary>What the line is about, the item or the stat, so a policy and its base list each once.</summary>
        public Def Subject => IsItem ? item : (Def)stat;

        /// <summary>
        /// Whether this line counts a piece of this item: every piece when it has no layers, otherwise a piece on one of
        /// them. Item lines have no layers.
        /// </summary>
        public bool CountsOn(ThingDef apparel)
        {
            return IsItem || LayerScope.Covers(apparel, layers);
        }

        /// <summary>The value as this line counts it: none when it's smaller than <see cref="countsFrom"/>.</summary>
        public float Counted(float value)
        {
            // A value the game shows as the threshold counts, though the float stored for it may be a hair under.
            return Math.Abs(value) < countsFrom - CountsFromTolerance ? 0f : value;
        }

        private const float CountsFromTolerance = 0.0001f;

        public OrderLine Copy()
        {
            return new OrderLine(stat, lowerIsBetter) { layers = new List<ApparelLayerDef>(layers), item = item, countsFrom = countsFrom };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
            Scribe_Collections.Look(ref layers, "layers", LookMode.Def);
            Scribe_Defs.Look(ref item, "item");
            Scribe_Values.Look(ref countsFrom, "countsFrom", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                layers = layers ?? new List<ApparelLayerDef>();
                layers.RemoveAll(layer => layer == null);
            }
        }
    }
}
