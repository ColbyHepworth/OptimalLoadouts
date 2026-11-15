using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>Why a piece that helps on no priority is still put on under "only add pieces that help".</summary>
    public enum ExtraNeed
    {
        None,

        /// <summary>It covers a bare torso or legs, the game's nudity test.</summary>
        Cover,

        /// <summary>It adds real warmth while what the colonist wears leaves them short this month.</summary>
        Warmth,
    }

    /// <summary>
    /// The rule behind a policy's "only add pieces that help": a piece for an empty slot that gains on no priority line is
    /// skipped, unless the colonist needs it for cover or warmth. Swaps of one piece for another stay with the game.
    /// </summary>
    public static class ExtraPieces
    {
        /// <summary>The game's margin under a race's comfortable minimum before a month counts as needing warmth.</summary>
        private const float WarmthMargin = 4f;

        /// <summary>
        /// The least cold insulation that counts as warmth. A cloth cowboy hat gives about 1.8 °C, steel beads 0.3 °C.
        /// </summary>
        private const float MinimumWarmth = 1f;

        /// <summary>
        /// Whether the check skips this piece under "only add pieces that help": the policy has a line a piece can gain
        /// on, the piece ties on every line, replaces nothing the pawn wears, and isn't needed for cover or warmth.
        /// </summary>
        public static bool Skipped(Pawn pawn, Apparel candidate, OrderVerdict verdict, IReadOnlyList<OrderLine> lines)
        {
            return IsExtra(pawn, candidate, verdict, lines) && NeededFor(pawn, candidate) == ExtraNeed.None;
        }

        /// <summary>
        /// Why the setting lets the piece through though it helps on no priority, or None when it gains on a priority,
        /// replaces something, or isn't let through.
        /// </summary>
        public static ExtraNeed LetThroughFor(Pawn pawn, Apparel candidate, OrderVerdict verdict, IReadOnlyList<OrderLine> lines)
        {
            return IsExtra(pawn, candidate, verdict, lines) ? NeededFor(pawn, candidate) : ExtraNeed.None;
        }

        /// <summary>A piece for an empty slot that ties on every line, under a policy with a line a piece can gain on.</summary>
        private static bool IsExtra(Pawn pawn, Apparel candidate, OrderVerdict verdict, IReadOnlyList<OrderLine> lines)
        {
            return verdict.IsTie && HasLineToGainOn(lines) && !ApparelComparison.ReplacesAny(pawn, candidate);
        }

        /// <summary>
        /// Whether any line ranks pieces. A Not allowed body part line only rules pieces out, so with nothing else every
        /// piece ties and the setting would add no hats or armor.
        /// </summary>
        private static bool HasLineToGainOn(IReadOnlyList<OrderLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].IsCoverage || lines[i].IsRequiredCoverage)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether the colonist needs the piece: it covers the torso or legs where no clothing worn does, or adds real
        /// warmth while what the colonist wears leaves them short of warmth this month.
        /// </summary>
        private static ExtraNeed NeededFor(Pawn pawn, Apparel candidate)
        {
            if (CoversBareTorsoOrLegs(pawn, candidate))
            {
                return ExtraNeed.Cover;
            }
            return WarmsShortColonist(pawn, candidate) ? ExtraNeed.Warmth : ExtraNeed.None;
        }

        /// <summary>
        /// The game's nudity test, the torso and the legs, counting only pieces that count as clothing: a backpack or a
        /// quiver on the torso isn't a shirt.
        /// </summary>
        private static bool CoversBareTorsoOrLegs(Pawn pawn, Apparel candidate)
        {
            if (!candidate.def.apparel.countsAsClothingForNudity)
            {
                return false;
            }
            bool hasShirt = false;
            bool hasPants = false;
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                ApparelProperties apparel = worn[i].def.apparel;
                if (apparel.countsAsClothingForNudity)
                {
                    hasShirt |= apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso);
                    hasPants |= apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs);
                }
            }
            List<BodyPartGroupDef> groups = candidate.def.apparel.bodyPartGroups;
            return (!hasShirt && groups.Contains(BodyPartGroupDefOf.Torso)) || (!hasPants && groups.Contains(BodyPartGroupDefOf.Legs));
        }

        /// <summary>
        /// The game's own warmth test, a month's average under the race's comfortable minimum less a margin, with the cold
        /// insulation already worn counted in, as the colonist's own comfort counts it. Only a piece with at least
        /// <see cref="MinimumWarmth"/> counts, so a metal trinket doesn't.
        /// </summary>
        private static bool WarmsShortColonist(Pawn pawn, Apparel candidate)
        {
            if (GameApparelRules.NeededWarmth != NeededWarmth.Warm || pawn.Map == null
                || PieceStats.Value(candidate, StatDefOf.Insulation_Cold) < MinimumWarmth)
            {
                return false;
            }
            float wornInsulation = 0f;
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                wornInsulation += PieceStats.Value(worn[i], StatDefOf.Insulation_Cold);
            }
            float monthAverage = GenTemperature.AverageTemperatureAtTileForTwelfth(pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
            float comfortable = pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin) - wornInsulation;
            return monthAverage < comfortable - WarmthMargin;
        }
    }
}
