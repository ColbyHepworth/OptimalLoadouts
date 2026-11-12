using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// A rule that rules out apparel by one stat: values below (or above) a threshold, on a layer pick. It's written into
    /// the policy's checklist (<see cref="ApparelForbids"/>), so the game takes such apparel off by itself.
    /// </summary>
    public class ApparelForbid : IExposable
    {
        /// <summary>Values this close to the threshold count as on it, so a forbid at the lowest value catches nothing.</summary>
        private const float Epsilon = 0.0001f;

        public StatDef stat;

        /// <summary>True to forbid values above the threshold, false to forbid values below it.</summary>
        public bool forbidAbove;

        public float threshold;

        /// <summary>The layer this forbid covers, or null for every layer.</summary>
        public ApparelLayerDef layer;

        /// <summary>With a layer: cover every layer except it ("all but utility").</summary>
        public bool allButLayer;

        public ApparelForbid()
        {
        }

        public ApparelForbid(StatDef stat, float threshold)
        {
            this.stat = stat;
            this.threshold = threshold;
        }

        /// <summary>Whether this forbid rules out the item, judged in its usual material at normal quality.</summary>
        public bool Forbids(ThingDef apparel)
        {
            if (stat == null || !LayerScope.Covers(apparel, layer, allButLayer))
            {
                return false;
            }
            if (!ApparelForbids.TryValueOf(apparel, stat, out float value))
            {
                return false;
            }
            return forbidAbove ? value > threshold + Epsilon : value < threshold - Epsilon;
        }

        public ApparelForbid Copy()
        {
            return new ApparelForbid(stat, threshold) { forbidAbove = forbidAbove, layer = layer, allButLayer = allButLayer };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref forbidAbove, "forbidAbove", false);
            Scribe_Values.Look(ref threshold, "threshold", 0f);
            Scribe_Defs.Look(ref layer, "layer");
            Scribe_Values.Look(ref allButLayer, "allButLayer", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && layer == null)
            {
                allButLayer = false;
            }
        }
    }
}
