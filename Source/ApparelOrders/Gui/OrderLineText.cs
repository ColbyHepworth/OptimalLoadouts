using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>How a line's or a forbid's layer pick reads, the same on every screen that shows one.</summary>
    public static class OrderLineText
    {
        private static List<ApparelLayerDef> wornLayers;

        /// <summary>The pick on its own, for the layer button: "All layers", "Outer" or "All but utility".</summary>
        public static string LayerLabel(OrderLine line)
        {
            return LayerLabel(line.layer, line.allButLayer);
        }

        public static string LayerLabel(ApparelLayerDef layer, bool allButLayer)
        {
            if (layer == null)
            {
                return "OL_Layer_All".Translate();
            }
            return allButLayer ? "OL_Layer_AllBut".Translate(layer.label) : layer.LabelCap.Resolve();
        }

        /// <summary>Every layer, every layer but utility, then each layer some apparel is worn on.</summary>
        public static List<FloatMenuOption> LayerOptions(Action<ApparelLayerDef, bool> pick)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("OL_Layer_All".Translate(), () => pick(null, false)),
                new FloatMenuOption("OL_Layer_AllBut".Translate(ApparelLayerDefOf.Belt.label), () => pick(ApparelLayerDefOf.Belt, true)),
            };
            foreach (ApparelLayerDef layer in WornLayers)
            {
                options.Add(new FloatMenuOption(layer.LabelCap, () => pick(layer, false)));
            }
            return options;
        }

        /// <summary>A forbid as the limit it sets, on a line of its own: "Mass at most 5 kg on all but utility".</summary>
        public static string ForbidCap(ApparelForbid forbid)
        {
            string value = ValueText(forbid.stat, forbid.threshold);
            string rule = forbid.forbidAbove
                ? "OL_Forbid_RuleAtMost".Translate(forbid.stat.label, value)
                : "OL_Forbid_RuleAtLeast".Translate(forbid.stat.label, value);
            if (forbid.layer != null)
            {
                rule = forbid.allButLayer
                    ? "OL_Layer_StatAllBut".Translate(rule, forbid.layer.label)
                    : "OL_Layer_StatOn".Translate(rule, forbid.layer.label);
            }
            return rule.CapitalizeFirst();
        }

        /// <summary>A value of the stat in its own units: the piece's own stats as they are, a wearer bonus as an offset.</summary>
        public static string ValueText(StatDef stat, float value)
        {
            ToStringNumberSense sense = StatChoices.IsWornPieceStat(stat) ? ToStringNumberSense.Absolute : ToStringNumberSense.Offset;
            return stat.ValueToString(value, sense);
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
