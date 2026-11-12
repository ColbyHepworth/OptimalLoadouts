using OptimalLoadouts.ApparelOrders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.Orders
{
    /// <summary>
    /// One line of a stat order: either a stat, with whether higher or lower values are better and, for apparel, the layer
    /// it counts on; or a locked item, worth 1 on a piece of that item and 0 on anything else.
    /// </summary>
    public class OrderLine : IExposable
    {
        /// <summary>The line's stat, or null on an item line.</summary>
        public StatDef stat;

        public bool lowerIsBetter;

        /// <summary>Apparel only: the layer this line counts on, or null for every layer.</summary>
        public ApparelLayerDef layer;

        /// <summary>With a layer: count every layer except it ("all but utility").</summary>
        public bool allButLayer;

        /// <summary>The locked item on an item line, or null on a stat line.</summary>
        public ThingDef item;

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
        /// Whether this line counts a piece of this item: every piece when it has no layer, otherwise a piece on its
        /// layer (or, for "all but", a piece on any other layer). A piece on several layers counts if any one matches.
        /// Item lines have no layer.
        /// </summary>
        public bool CountsOn(ThingDef apparel)
        {
            return IsItem || LayerScope.Covers(apparel, layer, allButLayer);
        }

        public OrderLine Copy()
        {
            return new OrderLine(stat, lowerIsBetter) { layer = layer, allButLayer = allButLayer, item = item };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
            Scribe_Defs.Look(ref layer, "layer");
            Scribe_Values.Look(ref allButLayer, "allButLayer", false);
            Scribe_Defs.Look(ref item, "item");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && layer == null)
            {
                allButLayer = false;
            }
        }
    }
}
