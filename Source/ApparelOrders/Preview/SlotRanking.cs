using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Ranks the pieces that could fill one slot of an outfit (<see cref="OutfitSlot"/>). Under the order, pieces rank by
    /// the apparel check's own score for the slot left empty, so the first is what the pawn would put there.
    /// </summary>
    public static class SlotRanking
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller is a tie.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>
        /// The slot's pieces ranked the way the pawn would pick, or by one stat when <paramref name="byStat"/> is set
        /// (highest first unless <paramref name="lowestFirst"/>). Pieces the pawn wouldn't consider (not allowed, out of
        /// reach, or scored too low) always come after the rest, so the first is what the pawn would put there.
        /// </summary>
        public static List<SlotChoice> Rank(Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, OutfitSlot slot,
            IEnumerable<Apparel> pieces, StatDef byStat, bool lowestFirst)
        {
            BodyDef body = pawn.RaceProps.body;
            List<Apparel> rest = outfit.Where(worn => !slot.Holds(worn.def, body)).ToList();
            // Worn pieces aren't in storage, so the ones in the slot join the list by hand. A layer can hold several.
            List<Apparel> inSlot = slot.Piece != null
                ? new List<Apparel> { slot.Piece }
                : outfit.Where(worn => slot.Holds(worn.def, body)).ToList();
            List<(Apparel piece, int copies)> slotPieces = SlotPieces(pawn, slot, inSlot, pieces, body);
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;

            var scored = new List<(SlotChoice choice, float raw)>();
            OutfitPredictor.SetNeededWarmth(pawn);
            using (new PreviewPolicy(pawn, policy))
            using (new ImaginedOutfit(pawn, rest))
            {
                // The check reads the scores by the pawn's worn list, which the imagined outfit keeps in draw order.
                List<float> wornScores = pawn.apparel.WornApparel.Select(worn => JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, worn)).ToList();
                foreach ((Apparel piece, int copies) in slotPieces)
                {
                    float score = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, piece, wornScores);
                    float raw = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, piece);
                    List<Apparel> alsoReplaces = rest.Where(worn => !ApparelUtility.CanWearTogether(worn.def, piece.def, body)).ToList();
                    // Pieces in the slot are already on the pawn, and imagined pieces are nowhere to reach.
                    bool stored = !inSlot.Contains(piece) && piece.MapHeld != null;
                    bool reachable = !stored || OutfitPredictor.CanPutOn(pawn, piece, fromStorage: true);
                    string cantWear = GameApparelRules.WhyCantWear(pawn, piece) ?? GameApparelRules.WhyModRulesOut(piece, score);
                    string skipped = cantWear == null ? WhySkippedAsExtra(pawn, policy, piece, lines, score) : null;
                    var choice = new SlotChoice(piece, copies, policy.filter.Allows(piece.def), policy.filter.Allows(piece), reachable,
                        score, cantWear, skipped, alsoReplaces);
                    scored.Add((choice, raw));
                }
            }

            List<SlotChoice> byPick = Ordered(scored, null, false, lines);
            int rank = 0;
            foreach (SlotChoice choice in byPick)
            {
                choice.PickRank = choice.Pickable ? ++rank : 0;
            }
            return byStat == null ? byPick : Ordered(scored, byStat, lowestFirst, lines);
        }

        /// <summary>Why a piece the policy's "only add pieces that help" skips isn't put on, or null when it isn't skipped.</summary>
        private static string WhySkippedAsExtra(Pawn pawn, ApparelPolicy policy, Apparel piece, IReadOnlyList<OrderLine> lines, float score)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (score >= OutfitPredictor.MinimumGain || lines.Count == 0 || orders == null || !orders.OnlyWhatHelps(policy))
            {
                return null;
            }
            return ExtraPieces.Skipped(pawn, piece, ApparelComparison.Compare(pawn, piece, lines))
                ? "OL_Slot_WontWearExtra".Translate().Resolve()
                : null;
        }

        /// <summary>Pickable pieces first, then by the stat when one is given, then the way the pawn would pick.</summary>
        private static List<SlotChoice> Ordered(List<(SlotChoice choice, float raw)> scored, StatDef byStat, bool lowestFirst,
            IReadOnlyList<OrderLine> lines)
        {
            IComparer<SlotChoice> byOrder = Comparer<SlotChoice>.Create((a, b) => CompareByOrder(a, b, lines));
            IOrderedEnumerable<(SlotChoice choice, float raw)> ordered = scored.OrderBy(entry => entry.choice.Pickable ? 0 : 1);
            if (byStat != null)
            {
                ordered = lowestFirst
                    ? ordered.ThenBy(entry => PieceStats.Value(entry.choice.Piece, byStat))
                    : ordered.ThenByDescending(entry => PieceStats.Value(entry.choice.Piece, byStat));
            }
            return ordered
                .ThenBy(entry => entry.choice, byOrder)
                .ThenByDescending(entry => entry.raw)
                .ThenBy(entry => entry.choice.Piece.LabelCap.ToString())
                .Select(entry => entry.choice)
                .ToList();
        }

        /// <summary>
        /// The pieces in the slot, then the pieces that compete for it and that the pawn could wear, with identical stored
        /// copies (same item, material, quality and taint) counted as one.
        /// </summary>
        private static List<(Apparel piece, int copies)> SlotPieces(Pawn pawn, OutfitSlot slot, List<Apparel> inSlot, IEnumerable<Apparel> pieces,
            BodyDef body)
        {
            var listed = inSlot.Select(worn => (worn, 1)).ToList();
            IEnumerable<IGrouping<(ThingDef, ThingDef, int, bool), Apparel>> groups = pieces
                .Where(piece => !inSlot.Contains(piece)
                    && slot.Holds(piece.def, body)
                    && OutfitPredictor.CanPutOn(pawn, piece, fromStorage: false))
                .GroupBy(piece => (piece.def, piece.Stuff, piece.TryGetQuality(out QualityCategory quality) ? (int)quality : -1,
                    piece.WornByCorpse));
            foreach (IGrouping<(ThingDef, ThingDef, int, bool), Apparel> group in groups)
            {
                listed.Add((group.OrderByDescending(piece => piece.HitPoints).First(), group.Count()));
            }
            return listed;
        }

        /// <summary>The apparel check's score first, then the pieces' own values down the order, so ties never shuffle.</summary>
        private static int CompareByOrder(SlotChoice a, SlotChoice b, IReadOnlyList<OrderLine> lines)
        {
            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0)
            {
                return byScore;
            }
            foreach (OrderLine line in lines)
            {
                float delta = ApparelComparison.ValueOnLine(a.Piece, line) - ApparelComparison.ValueOnLine(b.Piece, line);
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
                if (Math.Abs(delta) > Epsilon)
                {
                    return delta > 0f ? -1 : 1;
                }
            }
            return 0;
        }
    }
}
