using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Ranks the pieces that could fill one slot of an outfit. A slot is everything that can't be worn together with
    /// a given piece, by the game's own test. Under the order, pieces rank by the apparel check's own score for the
    /// slot left empty, so the first is what the pawn would put there.
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
        public static List<SlotChoice> Rank(Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, Apparel replacing,
            IEnumerable<Apparel> pieces, StatDef byStat, bool lowestFirst)
        {
            BodyDef body = pawn.RaceProps.body;
            List<Apparel> rest = outfit.Where(worn => ApparelUtility.CanWearTogether(worn.def, replacing.def, body)).ToList();
            List<(Apparel piece, int copies)> slot = SlotPieces(pawn, replacing, pieces, body);
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;

            var scored = new List<(SlotChoice choice, float raw)>();
            OutfitPredictor.SetNeededWarmth(pawn);
            using (new PreviewPolicy(pawn, policy))
            using (new ImaginedOutfit(pawn, rest))
            {
                List<float> wornScores = rest.Select(worn => JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, worn)).ToList();
                foreach ((Apparel piece, int copies) in slot)
                {
                    float score = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, piece, wornScores);
                    float raw = JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, piece);
                    List<Apparel> alsoReplaces = rest.Where(worn => !ApparelUtility.CanWearTogether(worn.def, piece.def, body)).ToList();
                    // The piece being replaced is already on the pawn, and imagined pieces are nowhere to reach.
                    bool stored = piece != replacing && piece.MapHeld != null;
                    bool reachable = !stored || OutfitPredictor.CanPutOn(pawn, piece, fromStorage: true);
                    var choice = new SlotChoice(piece, copies, policy.filter.Allows(piece.def), policy.filter.Allows(piece), reachable,
                        score, alsoReplaces);
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
        /// The pieces that can't be worn together with <paramref name="replacing"/> and that the pawn could wear, with
        /// identical stored copies (same item, material, quality and taint) counted as one.
        /// </summary>
        private static List<(Apparel piece, int copies)> SlotPieces(Pawn pawn, Apparel replacing, IEnumerable<Apparel> pieces, BodyDef body)
        {
            var slot = new List<(Apparel piece, int copies)> { (replacing, 1) };
            IEnumerable<IGrouping<(ThingDef, ThingDef, int, bool), Apparel>> groups = pieces
                .Where(piece => piece != replacing
                    && !ApparelUtility.CanWearTogether(piece.def, replacing.def, body)
                    && OutfitPredictor.CanPutOn(pawn, piece, fromStorage: false))
                .GroupBy(piece => (piece.def, piece.Stuff, piece.TryGetQuality(out QualityCategory quality) ? (int)quality : -1,
                    piece.WornByCorpse));
            foreach (IGrouping<(ThingDef, ThingDef, int, bool), Apparel> group in groups)
            {
                slot.Add((group.OrderByDescending(piece => piece.HitPoints).First(), group.Count()));
            }
            return slot;
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
                float delta = PieceStats.Value(a.Piece, line.stat) - PieceStats.Value(b.Piece, line.stat);
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
