using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Compares a piece a pawn could put on with what it would replace, line by line down an order. With a
    /// <see cref="PartnerPool"/> in use, a piece that takes off something covering another area of the body is judged
    /// as a set: each side gets the best pool pieces for the area the other covers, so a shirt and trousers can beat a
    /// one-piece suit, and the suit never wins back against the same pair. A body part the outfit must cover is judged on
    /// everything worn.
    /// </summary>
    public static class ApparelComparison
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller is a tie.</summary>
        private const float Epsilon = 0.0001f;

        /// <summary>One comparison's working lists, reused, so the comparison doesn't make garbage for every piece scored.</summary>
        private sealed class Frame
        {
            public readonly List<Apparel> Replaced = new List<Apparel>();
            public readonly List<Apparel> CandidateSide = new List<Apparel>();
            public readonly List<Apparel> OtherSide = new List<Apparel>();
            public readonly List<Apparel> Rest = new List<Apparel>();

            /// <summary>The body of the pawn being compared, for the totals worked out body part by body part.</summary>
            public BodyDef Body;

            /// <summary>What the pawn wears, for the totals worked out body part by body part.</summary>
            public List<Apparel> Worn;
        }

        /// <summary>
        /// A frame for each comparison under way. Choosing partners scores each one alone, which runs a comparison inside
        /// the one choosing them, so each gets its own lists.
        /// </summary>
        private static readonly List<Frame> frames = new List<Frame>();

        private static int depth;

        private static Frame Current => frames[depth - 1];
        private static List<Apparel> replaced => Current.Replaced;
        private static List<Apparel> candidateSide => Current.CandidateSide;
        private static List<Apparel> otherSide => Current.OtherSide;
        private static List<Apparel> rest => Current.Rest;

        public static OrderVerdict Compare(Pawn pawn, Apparel candidate, IReadOnlyList<OrderLine> lines)
        {
            return Compare(pawn, candidate, lines, null);
        }

        /// <summary>
        /// The same, filling <paramref name="partners"/> (when given) with the pieces the candidate's side was completed
        /// with, empty when it was judged alone.
        /// </summary>
        public static OrderVerdict Compare(Pawn pawn, Apparel candidate, IReadOnlyList<OrderLine> lines, List<Apparel> partners)
        {
            if (frames.Count == depth)
            {
                frames.Add(new Frame());
            }
            depth++;
            try
            {
                return CompareInFrame(pawn, candidate, lines, partners);
            }
            finally
            {
                depth--;
            }
        }

        private static OrderVerdict CompareInFrame(Pawn pawn, Apparel candidate, IReadOnlyList<OrderLine> lines, List<Apparel> partners)
        {
            partners?.Clear();
            FindReplaced(pawn, candidate);
            if (GameApparelRules.Decides(pawn, candidate, replaced))
            {
                return OrderVerdict.LeftToGame;
            }
            FindRest(pawn, candidate);
            Current.Body = pawn.RaceProps.body;
            Current.Worn = pawn.apparel.WornApparel;
            candidateSide.Clear();
            candidateSide.Add(candidate);
            otherSide.Clear();
            otherSide.AddRange(replaced);
            CompleteSides(pawn, candidate, lines);
            partners?.AddRange(candidateSide.Skip(1));

            for (int i = 0; i < lines.Count; i++)
            {
                OrderLine line = lines[i];
                float delta = Difference(line);
                if (Math.Abs(delta) > Epsilon)
                {
                    if (line.IsItem || line.IsCoverage)
                    {
                        // Every winner of an item or coverage line wins it by the same 1, so the lines under it rank them,
                        // and when none of them differs, the game's own score does.
                        float tieBreak = ItemTieBreak(lines, i + 1);
                        return new OrderVerdict(i, delta + tieBreak, tieBreak == 0f);
                    }
                    return new OrderVerdict(i, delta);
                }
            }
            return OrderVerdict.Tie;
        }

        /// <summary>
        /// With a pool in use, completes each side for the area at stake: what the candidate and what it takes off cover,
        /// and the slot's area when the pool ranks a slot. A piece that takes nothing off is compared with nothing, so
        /// only its own side is completed, and only for a slot.
        /// </summary>
        private static void CompleteSides(Pawn pawn, Apparel candidate, IReadOnlyList<OrderLine> lines)
        {
            PartnerPool pool = PartnerPool.For(pawn);
            if (pool == null || lines.Count == 0 || (replaced.Count == 0 && pool.Area?.Piece == null))
            {
                return;
            }
            (List<Apparel> forCandidate, List<Apparel> forReplaced) = pool.Partners(candidate, replaced, rest, lines);
            candidateSide.AddRange(forCandidate);
            otherSide.AddRange(forReplaced);
        }

        /// <summary>The worn pieces the candidate would keep on: neither the candidate nor anything it replaces.</summary>
        private static void FindRest(Pawn pawn, Apparel candidate)
        {
            rest.Clear();
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i] != candidate && !replaced.Contains(worn[i]))
                {
                    rest.Add(worn[i]);
                }
            }
        }

        /// <summary>
        /// How much better the colonist's outfit is on the line with the candidate's side than with the other, with the
        /// line's direction applied. A stat is totalled as the colonist ends up with it (<see cref="OutfitTotals"/>).
        /// </summary>
        private static float Difference(OrderLine line)
        {
            if (line.IsRequiredCoverage)
            {
                // The whole outfit counts, as the game's torso and legs cover does: covering a bare part wins, leaving it bare
                // loses, and covered either way is a tie, so the lines under it pick the cover.
                float kept = Covers(rest, line);
                return Math.Max(kept, Covers(candidateSide, line)) - Math.Max(kept, Covers(otherSide, line));
            }
            if (line.IsCoverage)
            {
                // A side covers a part once however many of its pieces cover it.
                float covered = Covers(candidateSide, line) - Covers(otherSide, line);
                return line.lowerIsBetter ? -covered : covered;
            }
            float delta;
            if (line.IsItem)
            {
                delta = 0f;
                for (int j = 0; j < candidateSide.Count; j++)
                {
                    delta += ValueOnLine(candidateSide[j], line);
                }
                for (int j = 0; j < otherSide.Count; j++)
                {
                    delta -= ValueOnLine(otherSide[j], line);
                }
            }
            else
            {
                // What the colonist ends up with, so more pieces don't win by adding up armor on parts they barely cover.
                delta = OutfitTotals.Change(Current.Body, Current.Worn, rest, candidateSide, otherSide, line);
            }
            return line.lowerIsBetter ? -delta : delta;
        }

        /// <summary>
        /// The first difference on the lines from <paramref name="from"/> on, squashed to under half a point either way, so
        /// a win or loss on an item line keeps its sign while better copies of the item score a little higher.
        /// </summary>
        private static float ItemTieBreak(IReadOnlyList<OrderLine> lines, int from)
        {
            for (int i = from; i < lines.Count; i++)
            {
                float delta = Difference(lines[i]);
                if (Math.Abs(delta) > Epsilon)
                {
                    return 0.49f * delta / (Math.Abs(delta) + 1f);
                }
            }
            return 0f;
        }

        private static float Covers(List<Apparel> side, OrderLine line)
        {
            for (int i = 0; i < side.Count; i++)
            {
                if (ValueOnLine(side[i], line) > 0f)
                {
                    return 1f;
                }
            }
            return 0f;
        }

        /// <summary>
        /// A piece's value on a line: on an item line, 1 for a piece of the item and 0 for anything else; on a coverage
        /// line, 1 when the piece covers the part on a layer the line counts; on a stat line, its value on the stat when
        /// the line counts its layer and the value reaches what the line counts from, otherwise 0.
        /// </summary>
        public static float ValueOnLine(Apparel piece, OrderLine line)
        {
            if (line.IsItem)
            {
                return piece.def == line.item ? 1f : 0f;
            }
            if (line.IsCoverage)
            {
                return line.CountsOn(piece.def) && PieceCoverage.Covers(piece.def, line.covers) ? 1f : 0f;
            }
            return line.CountsOn(piece.def) ? line.Counted(PieceStats.Value(piece, line.stat)) : 0f;
        }

        /// <summary>The worn pieces the candidate would replace, found the way the game's apparel check finds them.</summary>
        private static void FindReplaced(Pawn pawn, Apparel candidate)
        {
            replaced.Clear();
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                if (Replaces(pawn, candidate, worn[i]))
                {
                    replaced.Add(worn[i]);
                }
            }
        }

        /// <summary>Whether the candidate would take off anything the pawn wears.</summary>
        internal static bool ReplacesAny(Pawn pawn, Apparel candidate)
        {
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                if (Replaces(pawn, candidate, worn[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Replaces(Pawn pawn, Apparel candidate, Apparel worn)
        {
            return worn != candidate && !PieceCoverage.CanWearTogether(worn.def, candidate.def, pawn.RaceProps.body);
        }
    }
}
