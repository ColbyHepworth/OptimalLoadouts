using System.Collections.Generic;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>The one rule for which apparel a layer pick covers, shared by an order's lines and its forbids.</summary>
    public static class LayerScope
    {
        /// <summary>
        /// Whether an item is covered: every item when there's no layer, otherwise one on the layer (or, for "all but",
        /// on any other layer). An item on several layers is covered if any one of them matches.
        /// </summary>
        public static bool Covers(ThingDef apparel, ApparelLayerDef layer, bool allButLayer)
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
    }
}
