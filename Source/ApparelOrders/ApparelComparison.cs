using System;
using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>Compares a piece a pawn could put on with what it would replace, line by line down an order.</summary>
    public static class ApparelComparison
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller is a tie.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<Apparel> replaced = new List<Apparel>();

        public static OrderVerdict Compare(Pawn pawn, Apparel candidate, IReadOnlyList<OrderLine> lines)
        {
            FindReplaced(pawn, candidate);
            if (GameApparelRules.Decides(pawn, candidate, replaced))
            {
                return OrderVerdict.LeftToGame;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                OrderLine line = lines[i];
                float delta = ValueOnLine(candidate, line);
                for (int j = 0; j < replaced.Count; j++)
                {
                    delta -= ValueOnLine(replaced[j], line);
                }
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
                if (Math.Abs(delta) > Epsilon)
                {
                    if (line.IsItem)
                    {
                        // Every copy of a locked item wins its line by the same 1, so the lines under it rank the copies.
                        delta += ItemTieBreak(candidate, lines, i + 1);
                    }
                    return new OrderVerdict(i, delta);
                }
            }
            return OrderVerdict.Tie;
        }

        /// <summary>
        /// The first difference on the lines from <paramref name="from"/> on, squashed to under half a point either way, so
        /// a win or loss on an item line keeps its sign while better copies of the item score a little higher.
        /// </summary>
        private static float ItemTieBreak(Apparel candidate, IReadOnlyList<OrderLine> lines, int from)
        {
            for (int i = from; i < lines.Count; i++)
            {
                OrderLine line = lines[i];
                float delta = ValueOnLine(candidate, line);
                for (int j = 0; j < replaced.Count; j++)
                {
                    delta -= ValueOnLine(replaced[j], line);
                }
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
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
            BodyDef body = pawn.RaceProps.body;
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i] != candidate && !ApparelUtility.CanWearTogether(worn[i].def, candidate.def, body))
                {
                    replaced.Add(worn[i]);
                }
            }
        }
    }
}
