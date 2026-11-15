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

        /// <summary>
        /// What a priority line rules out, as a rule on a line of its own: "Mass at most 5 kg on outer and middle", or
        /// "Must not cover eyes".
        /// </summary>
        public static string ForbidCap(ApparelForbid forbid)
        {
            string rule;
            if (forbid.IsCoverage)
            {
                rule = "OL_Rule_MustNotCover".Translate(forbid.covers.label);
            }
            else
            {
                string value = ValueText(forbid.stat, forbid.threshold);
                rule = forbid.forbidAbove
                    ? "OL_Rule_StatAtMost".Translate(forbid.stat.label, value)
                    : "OL_Rule_StatAtLeast".Translate(forbid.stat.label, value);
            }
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

        /// <summary>A threshold is a size, so it reads without a sign: "10%", not "+10%".</summary>
        public static string CountsFromValue(StatDef stat, float value)
        {
            return stat.ValueToString(value, ToStringNumberSense.Absolute);
        }

        /// <summary>
        /// The line's stat with its layers, lower case for use inside a sentence: "sharp armor on headgear and outer". An
        /// item line is its item: "cowboy hat". A body part line is what it asks for: "leaves neck free".
        /// </summary>
        public static string StatOnLayer(OrderLine line)
        {
            if (line.IsItem)
            {
                return line.item.label;
            }
            string subject = line.IsCoverage ? Coverage(line.covers, line.IsRequiredCoverage) : line.stat.label;
            return OnLayers(subject, line);
        }

        /// <summary>
        /// Whether a piece covers the line's part, with the line's layers: "covers neck on outer" or "leaves neck free".
        /// </summary>
        public static string CoverageOf(OrderLine line, bool covers)
        {
            return OnLayers(Coverage(line.covers, covers), line).CapitalizeFirst();
        }

        /// <summary>"covers neck", or "leaves neck free".</summary>
        private static string Coverage(BodyPartGroupDef part, bool covers)
        {
            return covers ? "OL_Editor_CoversPart".Translate(part.label).Resolve() : "OL_Editor_LeavesFree".Translate(part.label).Resolve();
        }

        private static string OnLayers(string subject, OrderLine line)
        {
            return line.layers.Count == 0 ? subject : "OL_Layer_StatOn".Translate(subject, LayerNames(line.layers)).Resolve();
        }

        /// <summary>The line's stat with its layers, capitalized, for a line of its own.</summary>
        public static string StatOnLayerCap(OrderLine line)
        {
            return StatOnLayer(line).CapitalizeFirst();
        }
    }
}
