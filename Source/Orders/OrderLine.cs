using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.Orders
{
    /// <summary>
    /// One line of a stat order: a stat, whether higher or lower values are better, and for apparel, the layer it counts
    /// on.
    /// </summary>
    public class OrderLine : IExposable
    {
        public StatDef stat;
        public bool lowerIsBetter;

        /// <summary>Apparel only: the layer this line counts on, or null for every layer.</summary>
        public ApparelLayerDef layer;

        /// <summary>With a layer: count every layer except it ("all but utility").</summary>
        public bool allButLayer;

        public OrderLine()
        {
        }

        public OrderLine(StatDef stat, bool lowerIsBetter = false)
        {
            this.stat = stat;
            this.lowerIsBetter = lowerIsBetter;
        }

        /// <summary>
        /// Whether this line counts a piece of this item: every piece when it has no layer, otherwise a piece on its
        /// layer (or, for "all but", a piece on any other layer). A piece on several layers counts if any one matches.
        /// </summary>
        public bool CountsOn(ThingDef apparel)
        {
            if (layer == null)
            {
                return true;
            }
            List<ApparelLayerDef> layers = apparel.apparel?.layers;
            if (layers == null)
            {
                return false;
            }
            for (int i = 0; i < layers.Count; i++)
            {
                if ((layers[i] == layer) != allButLayer)
                {
                    return true;
                }
            }
            return false;
        }

        public OrderLine Copy()
        {
            return new OrderLine(stat, lowerIsBetter) { layer = layer, allButLayer = allButLayer };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref lowerIsBetter, "lowerIsBetter", false);
            Scribe_Defs.Look(ref layer, "layer");
            Scribe_Values.Look(ref allButLayer, "allButLayer", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && layer == null)
            {
                allButLayer = false;
            }
        }
    }
}
