using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// The rule behind a policy's "only add pieces that help": a piece for an empty slot that gains on no priority line is
    /// skipped, unless the colonist needs it for cover or warmth. Swaps of one piece for another stay with the game.
    /// </summary>
    public static class ExtraPieces
    {
        /// <summary>The game's margin under a race's comfortable minimum before a month counts as needing warmth.</summary>
        private const float WarmthMargin = 4f;

        /// <summary>
        /// Whether the check skips this piece under "only add pieces that help": it ties on every line, replaces nothing
        /// the pawn wears, and isn't needed for cover or warmth.
        /// </summary>
        public static bool Skipped(Pawn pawn, Apparel candidate, OrderVerdict verdict)
        {
            return verdict.IsTie && !ApparelComparison.ReplacesAny(pawn, candidate) && !Needed(pawn, candidate);
        }

        /// <summary>
        /// Covers the torso or legs where no clothing worn does, or adds cold insulation while what the colonist wears
        /// leaves them short of warmth this month.
        /// </summary>
        public static bool Needed(Pawn pawn, Apparel candidate)
        {
            return CoversBareTorsoOrLegs(pawn, candidate) || WarmsShortColonist(pawn, candidate);
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
        /// insulation already worn counted in, as the colonist's own comfort counts it.
        /// </summary>
        private static bool WarmsShortColonist(Pawn pawn, Apparel candidate)
        {
            if (GameApparelRules.NeededWarmth != NeededWarmth.Warm || pawn.Map == null
                || PieceStats.Value(candidate, StatDefOf.Insulation_Cold) <= 0f)
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
