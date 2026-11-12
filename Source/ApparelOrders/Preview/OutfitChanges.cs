using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>What changed between two predictions for the same colonist, policy, tab and material.</summary>
    public sealed class OutfitChanges
    {
        private readonly BodyDef body;

        /// <summary>Pieces in the new outfit that weren't in the old one.</summary>
        public IReadOnlyList<Apparel> Added { get; }

        /// <summary>Pieces in the old outfit that aren't in the new one.</summary>
        public IReadOnlyList<Apparel> Removed { get; }

        public bool Any => Added.Count > 0 || Removed.Count > 0;

        private OutfitChanges(IReadOnlyList<Apparel> added, IReadOnlyList<Apparel> removed, BodyDef body)
        {
            Added = added;
            Removed = removed;
            this.body = body;
        }

        /// <summary>
        /// The changes from <paramref name="before"/> to <paramref name="after"/>. Imagined pieces are one per item,
        /// material and quality, so the same piece in both outfits is the same object.
        /// </summary>
        public static OutfitChanges Between(PredictedOutfit before, PredictedOutfit after, BodyDef body)
        {
            return new OutfitChanges(after.Worn.Except(before.Worn).ToList(), before.Worn.Except(after.Worn).ToList(), body);
        }

        /// <summary>The removed pieces the added one can't be worn with.</summary>
        public IReadOnlyList<Apparel> Replaced(Apparel added)
        {
            return Removed.Where(removed => !ApparelUtility.CanWearTogether(removed.def, added.def, body)).ToList();
        }
    }
}
