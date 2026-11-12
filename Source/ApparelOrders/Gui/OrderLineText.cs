using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>How a line's layer pick reads, the same on every screen that shows a line.</summary>
    public static class OrderLineText
    {
        private static List<ApparelLayerDef> wornLayers;

        /// <summary>The pick on its own, for the layer button: "All layers", "Outer" or "All but utility".</summary>
        public static string LayerLabel(OrderLine line)
        {
            if (line.layer == null)
            {
                return "OL_Layer_All".Translate();
            }
            return line.allButLayer ? "OL_Layer_AllBut".Translate(line.layer.label) : line.layer.LabelCap.Resolve();
        }

        /// <summary>The line's stat with its layer, lower case for use inside a sentence: "sharp armor on outer".</summary>
        public static string StatOnLayer(OrderLine line)
        {
            if (line.layer == null)
            {
                return line.stat.label;
            }
            return line.allButLayer
                ? "OL_Layer_StatAllBut".Translate(line.stat.label, line.layer.label)
                : "OL_Layer_StatOn".Translate(line.stat.label, line.layer.label);
        }

        /// <summary>The line's stat with its layer, capitalized, for a line of its own.</summary>
        public static string StatOnLayerCap(OrderLine line)
        {
            return StatOnLayer(line).CapitalizeFirst();
        }

        /// <summary>The layers some apparel is worn on, in the order the game draws them.</summary>
        public static IReadOnlyList<ApparelLayerDef> WornLayers
        {
            get
            {
                if (wornLayers == null)
                {
                    var used = new HashSet<ApparelLayerDef>();
                    foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.IsApparel && def.apparel.layers != null)
                        {
                            used.AddRange(def.apparel.layers);
                        }
                    }
                    wornLayers = used.OrderBy(layer => layer.drawOrder).ThenBy(layer => layer.label).ToList();
                }
                return wornLayers;
            }
        }
    }
}
