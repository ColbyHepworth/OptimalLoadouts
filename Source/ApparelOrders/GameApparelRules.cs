using System.Collections.Generic;
using OptimalLoadouts.ApparelOrders.Patches;
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
            return CantWearKey(pawn, piece) != null;
        }

        /// <summary>Why the game never puts the piece on this pawn by itself, or null when it would.</summary>
        public static string WhyCantWear(Pawn pawn, Apparel piece)
        {
            return CantWearKey(pawn, piece)?.Translate().Resolve();
        }

        /// <summary>
        /// Why another mod rules the piece out, when its apparel check score is down where mods mark "never wear this",
        /// or null. Combat Extended's shield rule is named, matched by type name so the mod doesn't need CE.
        /// </summary>
        public static string WhyModRulesOut(Apparel piece, float score)
        {
            if (score > ApparelScoreGainPatch.NeverWear)
            {
                return null;
            }
            return IsCEShield(piece)
                ? "OL_Slot_WontWearCEShield".Translate().Resolve()
                : "OL_Slot_WontWearModRule".Translate().Resolve();
        }

        /// <summary>Whether the piece is Combat Extended's shield or built on it, as CE's own rule tests.</summary>
        private static bool IsCEShield(Apparel piece)
        {
            for (System.Type type = piece.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName == "CombatExtended.Apparel_Shield")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The reason's translation key, so the apparel check itself never looks up text.</summary>
        private static string CantWearKey(Pawn pawn, Apparel piece)
        {
            ApparelProperties apparel = piece.def.apparel;
            if (!piece.PawnCanWear(pawn, ignoreGender: true))
            {
                return "OL_Slot_WontWearPawn";
            }
            if (apparel.blocksVision)
            {
                return "OL_Slot_WontWearBlocksVision";
            }
            if (apparel.slaveApparel && !pawn.IsSlave)
            {
                return "OL_Slot_WontWearSlaves";
            }
            if (apparel.mechanitorApparel && pawn.mechanitor == null)
            {
                return "OL_Slot_WontWearMechanitors";
            }
            return null;
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
