using System.Collections.Generic;
using System.Linq;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>The one rule for which apparel a set of layers covers, shared by an order's lines and its forbids.</summary>
    public static class LayerScope
    {
        /// <summary>
        /// Whether an item is covered: every item when the set is empty, otherwise one on any layer in it. An item on
        /// several layers is covered if any one of them is in the set.
        /// </summary>
        public static bool Covers(ThingDef apparel, List<ApparelLayerDef> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return true;
            }
            List<ApparelLayerDef> onLayers = apparel.apparel?.layers;
            if (onLayers == null)
            {
                return false;
            }
            for (int i = 0; i < onLayers.Count; i++)
            {
                if (layers.Contains(onLayers[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The set to store for the layers picked: empty when none or every worn layer is picked, since both mean every
        /// layer. Otherwise each layer once, in the order the game draws them.
        /// </summary>
        public static List<ApparelLayerDef> Normalized(IEnumerable<ApparelLayerDef> picked)
        {
            var set = new HashSet<ApparelLayerDef>(picked.Where(layer => layer != null));
            IReadOnlyList<ApparelLayerDef> worn = ApparelLayers.Worn;
            if (set.Count == 0 || worn.All(set.Contains))
            {
                return new List<ApparelLayerDef>();
            }
            return set.OrderBy(layer => layer.drawOrder).ThenBy(layer => layer.label).ToList();
        }
    }
}
