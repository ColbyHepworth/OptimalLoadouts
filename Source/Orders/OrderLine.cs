using System;
using System.Collections.Generic;
using OptimalLoadouts.ApparelOrders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.Orders
{
    /// <summary>
    /// One line of a stat order: either a stat, with whether higher or lower values are better, the value it counts from
    /// and, for apparel, the layers it counts on; a locked item, worth 1 on a piece of that item and 0 on anything else;
    /// or a body part, worth 1 on a piece that covers it and 0 on one that doesn't: required of the outfit, or not allowed.
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

        /// <summary>
        /// The body part on a body part line, or null. Such a line is Required (higher first, not ruling out) or Not allowed
        /// (lower first, ruling out pieces that cover it).
        /// </summary>
        public BodyPartGroupDef covers;

        /// <summary>Values smaller than this, above or below zero, count as none on this line. 0 counts every amount.</summary>
        public float countsFrom;

        /// <summary>
        /// Also rules out pieces past the line's value: below <see cref="countsFrom"/> on a higher-first line (below 0 when
        /// it's 0), above it on a lower-first one; on a body part line, pieces that cover the part. Written into the
        /// policy's checklist, so the game takes such pieces off.
        /// </summary>
        public bool rulesOut;

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

        /// <summary>
        /// A line requiring the outfit to cover a body part: while nothing worn does, a piece that covers it wins the line.
        /// Made Not allowed, pieces that cover it are ruled out instead.
        /// </summary>
        public static OrderLine ForCoverage(BodyPartGroupDef part)
        {
            return new OrderLine { covers = part };
        }

        public bool IsItem => item != null;

        public bool IsCoverage => covers != null;

        /// <summary>A body part the outfit should cover, judged on everything worn rather than piece by piece.</summary>
        public bool IsRequiredCoverage => covers != null && !rulesOut;

        /// <summary>A line on a stat, which has an amount, a unit and a value it counts from.</summary>
        public bool IsStat => stat != null;

        /// <summary>What the line is about, the item, the body part or the stat, so a policy and its base list each once.</summary>
        public Def Subject => IsItem ? item : IsCoverage ? covers : (Def)stat;

        /// <summary>
        /// Whether this line counts a piece of this item: every piece when it has no layers, otherwise a piece on one of
        /// them. Item lines have no layers.
        /// </summary>
        public bool CountsOn(ThingDef apparel)
        {
            return IsItem || LayerScope.Covers(apparel, layers);
        }

        /// <summary>
        /// The value as this line counts it: none when it's smaller than <see cref="countsFrom"/>. A lower-first line that
        /// rules out uses its value as the most allowed, so it counts every amount under it: the lighter piece still wins.
        /// </summary>
        public float Counted(float value)
        {
            if (rulesOut && lowerIsBetter)
            {
                return value;
            }
            // A value the game shows as the threshold counts, though the float stored for it may be a hair under.
            return Math.Abs(value) < countsFrom - CountsFromTolerance ? 0f : value;
        }

        private const float CountsFromTolerance = 0.0001f;

        public OrderLine Copy()
        {
            return new OrderLine(stat, lowerIsBetter)
            {
                layers = new List<ApparelLayerDef>(layers), item = item, covers = covers, countsFrom = countsFrom, rulesOut = rulesOut,
            };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
            Scribe_Collections.Look(ref layers, "layers", LookMode.Def);
            Scribe_Defs.Look(ref item, "item");
            Scribe_Defs.Look(ref covers, "covers");
            Scribe_Values.Look(ref countsFrom, "countsFrom", 0f);
            Scribe_Values.Look(ref rulesOut, "rulesOut", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                layers = layers ?? new List<ApparelLayerDef>();
                layers.RemoveAll(layer => layer == null);
                if (IsCoverage)
                {
                    // A body part line is Required or Not allowed: it points lower first exactly when it rules out.
                    rulesOut = lowerIsBetter;
                }
            }
        }
    }
}
