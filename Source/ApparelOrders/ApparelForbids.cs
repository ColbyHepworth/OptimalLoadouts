using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Writes each policy's forbids into its checklist. Only what a forbid catches is unticked, and each policy remembers
    /// what its forbids unticked, so an item ticks back when no forbid catches it any more and an item the player
    /// unticked stays unticked.
    /// </summary>
    public static class ApparelForbids
    {
        private static readonly Dictionary<(ThingDef def, StatDef stat, ThingDef stuff), float?> values =
            new Dictionary<(ThingDef, StatDef, ThingDef), float?>();
        private static readonly Dictionary<(ThingDef def, StatDef stat, bool forbidAbove), float?> bestValues =
            new Dictionary<(ThingDef, StatDef, bool), float?>();

        private static readonly Dictionary<(StatDef stat, LimitMaterial material), FloatRange> ranges =
            new Dictionary<(StatDef, LimitMaterial), FloatRange>();
        private static List<ThingDef> apparel;

        /// <summary>Every item an apparel policy's checklist lists.</summary>
        private static IReadOnlyList<ThingDef> Apparel
        {
            get
            {
                if (apparel == null)
                {
                    apparel = ThingCategoryDefOf.Apparel.DescendantThingDefs.Distinct().ToList();
                }
                return apparel;
            }
        }

        /// <summary>
        /// Unticks what the policy's forbids catch and ticks back what they no longer catch. A locked item is never caught,
        /// and is ticked back if anything unticked it. Returns whether the checklist changed.
        /// </summary>
        private static bool Apply(ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelOrder order = orders?.OrderFor(policy);
            if (order == null)
            {
                return false;
            }
            bool changed = orders.KeepLockedAllowed(policy).Count > 0;
            List<ApparelForbid> forbids = orders.EffectiveForbids(policy);
            LimitMaterial material = order.LimitMaterial;
            var locked = new HashSet<ThingDef>(orders.LockedItems(policy));
            var caught = new HashSet<ThingDef>();
            if (forbids.Count > 0)
            {
                foreach (ThingDef def in Apparel)
                {
                    if (!locked.Contains(def) && forbids.Any(forbid => forbid.Forbids(def, material)))
                    {
                        caught.Add(def);
                    }
                }
            }
            ThingFilter filter = policy.filter;
            foreach (ThingDef def in caught)
            {
                if (filter.Allows(def))
                {
                    filter.SetAllow(def, false);
                    changed = true;
                    if (!order.unticked.Contains(def))
                    {
                        order.unticked.Add(def);
                    }
                }
            }
            for (int i = order.unticked.Count - 1; i >= 0; i--)
            {
                ThingDef def = order.unticked[i];
                if (!caught.Contains(def))
                {
                    changed |= !filter.Allows(def);
                    filter.SetAllow(def, true);
                    order.unticked.RemoveAt(i);
                }
            }
            return changed;
        }

        /// <summary>Applies every policy's forbids. Returns whether any checklist changed.</summary>
        public static bool ApplyAll()
        {
            bool changed = false;
            foreach (ApparelPolicy policy in Current.Game.outfitDatabase.AllOutfits)
            {
                changed |= Apply(policy);
            }
            return changed;
        }

        /// <summary>
        /// The items a forbid catches in the policy, for its count and tooltip: all but its locked items, judged in the
        /// policy's material. With no policy, nothing is locked and the usual material is used.
        /// </summary>
        public static List<ThingDef> Caught(ApparelPolicy policy, ApparelForbid forbid)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            var locked = new HashSet<ThingDef>(orders?.LockedItems(policy) ?? Enumerable.Empty<ThingDef>());
            LimitMaterial material = orders?.LimitMaterialOf(policy) ?? LimitMaterial.Usual;
            return Apparel.Where(def => !locked.Contains(def) && forbid.Forbids(def, material)).ToList();
        }

        /// <summary>The first of the policy's forbids that catches the item, or null. A locked item is never caught.</summary>
        public static ApparelForbid ForbiddenBy(ApparelPolicy policy, ThingDef def)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null || orders.IsLocked(policy, def))
            {
                return null;
            }
            LimitMaterial material = orders.LimitMaterialOf(policy);
            return orders.EffectiveForbids(policy).FirstOrDefault(forbid => forbid.Forbids(def, material));
        }

        /// <summary>
        /// The lowest and highest value any apparel has on the stat in the material a policy judges in, for a forbid's
        /// slider. With the best material, every material each item can be made of counts.
        /// </summary>
        public static FloatRange RangeOf(StatDef stat, LimitMaterial material)
        {
            if (ranges.TryGetValue((stat, material), out FloatRange known))
            {
                return known;
            }
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (ThingDef def in Apparel)
            {
                if (TryValueOf(def, stat, material, false, out float most))
                {
                    highest = System.Math.Max(highest, most);
                }
                if (TryValueOf(def, stat, material, true, out float least))
                {
                    lowest = System.Math.Min(lowest, least);
                }
            }
            FloatRange range = lowest > highest ? new FloatRange(0f, 0f) : new FloatRange(lowest, highest);
            ranges[(stat, material)] = range;
            return range;
        }

        /// <summary>
        /// An item's value on a stat, the way a forbid judges it: a piece in the material at normal quality, read as the
        /// order reads it. With <see cref="LimitMaterial.Best"/>, the best value any material gives for the forbid's
        /// direction: the lowest for an "at most" (<paramref name="forbidAbove"/>), the highest otherwise. False when the
        /// item can't be imagined in any.
        /// </summary>
        public static bool TryValueOf(ThingDef def, StatDef stat, LimitMaterial material, bool forbidAbove, out float value)
        {
            if (!material.IsBest || !def.MadeFromStuff)
            {
                return TryValueIn(def, stat, material.StuffFor(def), out value);
            }
            // The Limits tab asks every frame, and trying every material is slow, so the answer is kept.
            if (!bestValues.TryGetValue((def, stat, forbidAbove), out float? best))
            {
                foreach (ThingDef stuff in GenStuff.AllowedStuffsFor(def))
                {
                    if (TryValueIn(def, stat, stuff, out float inStuff) && (best == null || (forbidAbove ? inStuff < best : inStuff > best)))
                    {
                        best = inStuff;
                    }
                }
                bestValues[(def, stat, forbidAbove)] = best;
            }
            value = best ?? 0f;
            return best.HasValue;
        }

        private static bool TryValueIn(ThingDef def, StatDef stat, ThingDef stuff, out float value)
        {
            if (!values.TryGetValue((def, stat, stuff), out float? known))
            {
                try
                {
                    Apparel piece = ImaginedPieces.For(def, stuff, QualityCategory.Normal);
                    known = piece == null ? (float?)null : PieceStats.Value(piece, stat);
                }
                catch (System.Exception e)
                {
                    // An item no installed material fits, or a mod's stat worker that can't read it, is never caught.
                    Log.WarningOnce($"[Optimal Loadouts] Couldn't read {stat.defName} on {def.defName}, so forbids leave it alone: {e.Message}",
                        ("OptimalLoadouts.ForbidValue" + def.defName + stat.defName).GetHashCode());
                    known = null;
                }
                values[(def, stat, stuff)] = known;
            }
            value = known ?? 0f;
            return known.HasValue;
        }

        /// <summary>Forgets the values, so a loaded game reads its own.</summary>
        public static void ClearCache()
        {
            values.Clear();
            bestValues.Clear();
            ranges.Clear();
            apparel = null;
        }
    }
}
