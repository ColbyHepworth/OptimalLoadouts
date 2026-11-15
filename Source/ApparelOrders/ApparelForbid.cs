using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// What a priority line that rules pieces out rules out: values below (or above) its value on a stat, or pieces that
    /// cover a body part, on the line's layers. It's written into the policy's checklist
    /// (<see cref="ApparelForbids"/>), so the game takes such apparel off by itself. Made from the line each time, not saved.
    /// </summary>
    public class ApparelForbid
    {
        /// <summary>Values this close to the threshold count as on it, so a forbid at the lowest value catches nothing.</summary>
        private const float Epsilon = 0.0001f;

        public StatDef stat;

        /// <summary>The body part on a coverage line's rule, or null on a stat line's.</summary>
        public BodyPartGroupDef covers;

        /// <summary>True to forbid values above the threshold, false to forbid values below it. Unused on a coverage rule.</summary>
        public bool forbidAbove;

        public float threshold;

        /// <summary>The layers this forbid covers, or empty for every layer.</summary>
        public List<ApparelLayerDef> layers = new List<ApparelLayerDef>();

        public ApparelForbid()
        {
        }

        public ApparelForbid(StatDef stat, float threshold)
        {
            this.stat = stat;
            this.threshold = threshold;
        }

        /// <summary>
        /// What the line rules out: on a stat, values below its value when higher is better, above it when lower is; on a
        /// body part line, pieces that cover the part.
        /// </summary>
        public static ApparelForbid From(OrderLine line)
        {
            var forbid = line.IsCoverage
                ? new ApparelForbid { covers = line.covers }
                : new ApparelForbid(line.stat, line.countsFrom) { forbidAbove = line.lowerIsBetter };
            forbid.layers = line.layers;
            return forbid;
        }

        public bool IsCoverage => covers != null;

        /// <summary>Whether this forbid rules out the item, judged in the material at normal quality.</summary>
        public bool Forbids(ThingDef apparel, LimitMaterial material)
        {
            if (IsCoverage)
            {
                return LayerScope.Covers(apparel, layers) && PieceCoverage.Covers(apparel, covers);
            }
            if (stat == null || !LayerScope.Covers(apparel, layers))
            {
                return false;
            }
            if (!ApparelForbids.TryValueOf(apparel, stat, material, forbidAbove, out float value))
            {
                return false;
            }
            return forbidAbove ? value > threshold + Epsilon : value < threshold - Epsilon;
        }

    }
}
