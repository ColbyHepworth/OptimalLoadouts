using System.Collections.Generic;
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

        /// <summary>Whether this forbid rules out the item, judged in the material at normal quality.</summary>
        public bool Forbids(ThingDef apparel, LimitMaterial material)
        {
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

        public ApparelForbid Copy()
        {
            return new ApparelForbid(stat, threshold) { forbidAbove = forbidAbove, layers = new List<ApparelLayerDef>(layers) };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref forbidAbove, "forbidAbove", false);
            Scribe_Values.Look(ref threshold, "threshold", 0f);
            Scribe_Collections.Look(ref layers, "layers", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                layers = layers ?? new List<ApparelLayerDef>();
                layers.RemoveAll(layer => layer == null);
            }
        }
    }
}
