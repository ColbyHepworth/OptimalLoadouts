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
                float delta = PieceStats.Value(candidate, line.stat);
                for (int j = 0; j < replaced.Count; j++)
                {
                    delta -= PieceStats.Value(replaced[j], line.stat);
                }
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
                if (Math.Abs(delta) > Epsilon)
                {
                    return new OrderVerdict(i, delta);
                }
            }
            return OrderVerdict.Tie;
        }

        /// <summary>The first line a worn piece helps with, or null when it helps with none.</summary>
        public static OrderLine FirstLineServed(Apparel worn, IReadOnlyList<OrderLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                float value = PieceStats.Value(worn, lines[i].stat);
                bool helps = lines[i].lowerIsBetter ? value < -Epsilon : value > Epsilon;
                if (helps)
                {
                    return lines[i];
                }
            }
            return null;
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
