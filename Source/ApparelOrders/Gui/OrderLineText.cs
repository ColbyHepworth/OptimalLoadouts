using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>How a line's or a forbid's layers read, the same on every screen that shows them.</summary>
    public static class OrderLineText
    {
        /// <summary>
        /// The layers on their own, for the layer button: "All layers", their names when they fit the width
        /// ("Headgear, outer"), or "3 layers". One layer keeps its name, cut short if it must be.
        /// </summary>
        public static string LayerLabel(List<ApparelLayerDef> layers, float width)
        {
            if (layers.Count == 0)
            {
                return "OL_Layer_All".Translate();
            }
            string names = string.Join(", ", layers.Select(layer => layer.label)).CapitalizeFirst();
            if (layers.Count == 1 || Text.CalcSize(names).x <= width)
            {
                return names.Truncate(width);
            }
            return "OL_Layer_Count".Translate(layers.Count).Resolve();
        }

        /// <summary>A layer button's tooltip: what it does, then the layers picked when there are any.</summary>
        public static string LayerTip(string what, List<ApparelLayerDef> layers)
        {
            return layers.Count == 0 ? what : what + "\n\n" + LayerNames(layers).CapitalizeFirst() + ".";
        }

        /// <summary>The layers in a sentence: "headgear and outer". Empty for every layer.</summary>
        public static string LayerNames(List<ApparelLayerDef> layers)
        {
            return layers.Select(layer => layer.label).ToCommaList(useAnd: true);
        }

        /// <summary>A forbid as the limit it sets, on a line of its own: "Mass at most 5 kg on outer and middle".</summary>
        public static string ForbidCap(ApparelForbid forbid)
        {
            string value = ValueText(forbid.stat, forbid.threshold);
            string rule = forbid.forbidAbove
                ? "OL_Forbid_RuleAtMost".Translate(forbid.stat.label, value)
                : "OL_Forbid_RuleAtLeast".Translate(forbid.stat.label, value);
            if (forbid.layers.Count > 0)
            {
                rule = "OL_Layer_StatOn".Translate(rule, LayerNames(forbid.layers));
            }
            return rule.CapitalizeFirst();
        }

        /// <summary>A value of the stat in its own units: the piece's own stats as they are, a wearer bonus as an offset.</summary>
        public static string ValueText(StatDef stat, float value)
        {
            ToStringNumberSense sense = StatChoices.IsWornPieceStat(stat) ? ToStringNumberSense.Absolute : ToStringNumberSense.Offset;
            return stat.ValueToString(value, sense);
        }

        /// <summary>
        /// The line's stat with its layers, lower case for use inside a sentence: "sharp armor on headgear and outer". An
        /// item line is its item: "cowboy hat".
        /// </summary>
        public static string StatOnLayer(OrderLine line)
        {
            if (line.IsItem)
            {
                return line.item.label;
            }
            if (line.layers.Count == 0)
            {
                return line.stat.label;
            }
            return "OL_Layer_StatOn".Translate(line.stat.label, LayerNames(line.layers));
        }

        /// <summary>The line's stat with its layers, capitalized, for a line of its own.</summary>
        public static string StatOnLayerCap(OrderLine line)
        {
            return StatOnLayer(line).CapitalizeFirst();
        }
    }
}
