using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
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
        private static readonly Dictionary<(ThingDef def, StatDef stat), float?> values = new Dictionary<(ThingDef, StatDef), float?>();
        private static readonly Dictionary<StatDef, FloatRange> ranges = new Dictionary<StatDef, FloatRange>();
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
        /// Unticks what the policy's forbids catch and ticks back what they no longer catch. Returns whether the checklist
        /// changed.
        /// </summary>
        private static bool Apply(ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelOrder order = orders?.OrderFor(policy);
            if (order == null)
            {
                return false;
            }
            bool changed = false;
            List<ApparelForbid> forbids = orders.EffectiveForbids(policy);
            var caught = new HashSet<ThingDef>();
            if (forbids.Count > 0)
            {
                foreach (ThingDef def in Apparel)
                {
                    if (forbids.Any(forbid => forbid.Forbids(def)))
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

        /// <summary>The items a forbid catches, for its count and tooltip.</summary>
        public static List<ThingDef> Caught(ApparelForbid forbid)
        {
            return Apparel.Where(forbid.Forbids).ToList();
        }

        /// <summary>The first of the policy's forbids that catches the item, or null.</summary>
        public static ApparelForbid ForbiddenBy(ApparelPolicy policy, ThingDef def)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            return orders?.EffectiveForbids(policy).FirstOrDefault(forbid => forbid.Forbids(def));
        }

        /// <summary>The lowest and highest value any apparel has on the stat, for a forbid's slider.</summary>
        public static FloatRange RangeOf(StatDef stat)
        {
            if (ranges.TryGetValue(stat, out FloatRange known))
            {
                return known;
            }
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (ThingDef def in Apparel)
            {
                if (TryValueOf(def, stat, out float value))
                {
                    lowest = System.Math.Min(lowest, value);
                    highest = System.Math.Max(highest, value);
                }
            }
            FloatRange range = lowest > highest ? new FloatRange(0f, 0f) : new FloatRange(lowest, highest);
            ranges[stat] = range;
            return range;
        }

        /// <summary>
        /// An item's value on a stat, the way a forbid judges it: a piece in its usual material at normal quality, read
        /// as the order reads it. False when the item can't be imagined.
        /// </summary>
        public static bool TryValueOf(ThingDef def, StatDef stat, out float value)
        {
            if (!values.TryGetValue((def, stat), out float? known))
            {
                try
                {
                    Apparel piece = ImaginedPieces.For(def, GenStuff.DefaultStuffFor(def), QualityCategory.Normal);
                    known = piece == null ? (float?)null : PieceStats.Value(piece, stat);
                }
                catch (System.Exception e)
                {
                    // An item no installed material fits, or a mod's stat worker that can't read it, is never caught.
                    Log.WarningOnce($"[Optimal Loadouts] Couldn't read {stat.defName} on {def.defName}, so forbids leave it alone: {e.Message}",
                        ("OptimalLoadouts.ForbidValue" + def.defName + stat.defName).GetHashCode());
                    known = null;
                }
                values[(def, stat)] = known;
            }
            value = known ?? 0f;
            return known.HasValue;
        }

        /// <summary>Forgets the values, so a loaded game reads its own.</summary>
        public static void ClearCache()
        {
            values.Clear();
            ranges.Clear();
            apparel = null;
        }
    }
}
