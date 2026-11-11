using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// The game's own apparel rules that an order never overrules. They mirror the cases where
    /// <c>JobGiver_OptimizeApparel.ApparelScoreRaw</c> refuses a piece or weights it for a title or role.
    /// </summary>
    public static class GameApparelRules
    {
        /// <summary>
        /// Whether the game should decide this swap: the pawn can't wear the piece, or the swap puts on or takes off
        /// apparel a title or ideoligion role requires, or the piece is below a title's minimum quality.
        /// </summary>
        public static bool Decides(Pawn pawn, Apparel candidate, List<Apparel> replaced)
        {
            if (CantWear(pawn, candidate) || BelowTitleQuality(pawn, candidate) || IsRequired(pawn, candidate.def))
            {
                return true;
            }
            for (int i = 0; i < replaced.Count; i++)
            {
                if (IsRequired(pawn, replaced[i].def))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CantWear(Pawn pawn, Apparel piece)
        {
            ApparelProperties apparel = piece.def.apparel;
            return !piece.PawnCanWear(pawn, ignoreGender: true)
                || apparel.blocksVision
                || (apparel.slaveApparel && !pawn.IsSlave)
                || (apparel.mechanitorApparel && pawn.mechanitor == null);
        }

        private static bool IsRequired(Pawn pawn, ThingDef def)
        {
            foreach (ApparelRequirementWithSource requirement in pawn.apparel.AllRequirements)
            {
                if (requirement.requirement.RequiredForPawn(pawn, def))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool BelowTitleQuality(Pawn pawn, Apparel piece)
        {
            if (pawn.royalty == null || !piece.TryGetQuality(out QualityCategory quality))
            {
                return false;
            }
            foreach (RoyalTitle title in pawn.royalty.AllTitlesInEffectForReading)
            {
                if (quality < title.def.requiredMinimumApparelQuality)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
