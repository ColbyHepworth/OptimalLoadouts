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
    /// one-piece suit, and the suit never wins back against the same pair.
    /// </summary>
    public static class ApparelComparison
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller is a tie.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<Apparel> replaced = new List<Apparel>();
        private static readonly List<Apparel> candidateSide = new List<Apparel>();
        private static readonly List<Apparel> otherSide = new List<Apparel>();
        private static readonly List<Apparel> rest = new List<Apparel>();

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
            partners?.Clear();
            FindReplaced(pawn, candidate);
            if (GameApparelRules.Decides(pawn, candidate, replaced))
            {
                return OrderVerdict.LeftToGame;
            }
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
                    if (line.IsItem)
                    {
                        // Every copy of a locked item wins its line by the same 1, so the lines under it rank the copies.
                        delta += ItemTieBreak(lines, i + 1);
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
            rest.Clear();
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i] != candidate && !replaced.Contains(worn[i]))
                {
                    rest.Add(worn[i]);
                }
            }
            (List<Apparel> forCandidate, List<Apparel> forReplaced) = pool.Partners(candidate, replaced, rest, lines);
            candidateSide.AddRange(forCandidate);
            otherSide.AddRange(forReplaced);
        }

        /// <summary>How much better the candidate's side is on the line, with the line's direction applied.</summary>
        private static float Difference(OrderLine line)
        {
            float delta = 0f;
            for (int j = 0; j < candidateSide.Count; j++)
            {
                delta += ValueOnLine(candidateSide[j], line);
            }
            for (int j = 0; j < otherSide.Count; j++)
            {
                delta -= ValueOnLine(otherSide[j], line);
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

        /// <summary>
        /// A piece's value on a line: on an item line, 1 for a piece of the item and 0 for anything else; on a stat line,
        /// its value on the stat when the line counts its layer and the value reaches what the line counts from,
        /// otherwise 0.
        /// </summary>
        public static float ValueOnLine(Apparel piece, OrderLine line)
        {
            if (line.IsItem)
            {
                return piece.def == line.item ? 1f : 0f;
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
