using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;
using Verse.AI;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Works out the outfit a pawn will end up in by playing the game's apparel check forward: the same candidates,
    /// the same score with every mod's changes, the same tests, one swap at a time on an imagined outfit.
    /// </summary>
    public static class OutfitPredictor
    {
        /// <summary>Enough for a naked pawn to dress fully. More than this means two pieces keep beating each other.</summary>
        public const int MaxSwaps = 30;

        /// <summary>The apparel check's threshold: a piece scoring below it is never put on.</summary>
        private const float MinimumGain = 0.05f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>The check's shared warmth need, which it sets for each pawn before scoring. Null if the game renames it.</summary>
        private static readonly AccessTools.FieldRef<NeededWarmth> NeededWarmthField = FindNeededWarmth();

        public static PredictedOutfit Predict(Pawn pawn)
        {
            ApparelPolicy policy = pawn.outfits.CurrentApparelPolicy;
            var now = new List<Apparel>(pawn.apparel.WornApparel);
            var outfit = new List<Apparel>(now);
            var notAllowed = now.Where(piece => MustTakeOff(pawn, policy, piece)).ToList();
            outfit.RemoveAll(notAllowed.Contains);

            List<Apparel> candidates = Candidates(pawn, policy);
            bool nothingToPick = candidates.Count == 0;
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;
            SetNeededWarmth(pawn);

            var swaps = new List<PredictedSwap>();
            bool settled = false;
            for (int i = 0; i < MaxSwaps; i++)
            {
                PredictedSwap swap;
                using (new ImaginedOutfit(pawn, outfit))
                {
                    swap = BestSwap(pawn, outfit, candidates, lines);
                }
                if (swap == null)
                {
                    settled = true;
                    break;
                }
                outfit.RemoveAll(swap.Replaced.Contains);
                outfit.Add(swap.PutOn);
                candidates.Remove(swap.PutOn);
                swaps.Add(swap);
            }
            return new PredictedOutfit(outfit, outfit.Except(now).ToList(), now.Except(outfit).ToList(), notAllowed, swaps,
                settled, nothingToPick);
        }

        /// <summary>
        /// The piece the check would put on next, scored against the imagined outfit already in place. On equal scores
        /// the later piece wins, as the check compares with "less than".
        /// </summary>
        private static PredictedSwap BestSwap(Pawn pawn, List<Apparel> outfit, List<Apparel> candidates, IReadOnlyList<OrderLine> lines)
        {
            List<float> wornScores = outfit.Select(worn => JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, worn)).ToList();
            Apparel best = null;
            float bestScore = 0f;
            Apparel runnerUp = null;
            float runnerUpScore = float.MinValue;
            foreach (Apparel candidate in candidates)
            {
                float score = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, candidate, wornScores);
                // The check's other tests, reach among them, run only for a piece that would win, as in the game.
                if (score >= MinimumGain && score >= bestScore && CanPutOn(pawn, candidate))
                {
                    if (best != null)
                    {
                        runnerUp = best;
                        runnerUpScore = bestScore;
                    }
                    best = candidate;
                    bestScore = score;
                }
                else if (score > runnerUpScore)
                {
                    runnerUp = candidate;
                    runnerUpScore = score;
                }
            }
            if (best == null)
            {
                return null;
            }
            BodyDef body = pawn.RaceProps.body;
            List<Apparel> replaced = outfit.Where(worn => !ApparelUtility.CanWearTogether(worn.def, best.def, body)).ToList();
            return new PredictedSwap(best, replaced, bestScore, VerdictFor(pawn, best, lines), runnerUp,
                runnerUp == null ? OrderVerdict.Tie : VerdictFor(pawn, runnerUp, lines));
        }

        private static OrderVerdict VerdictFor(Pawn pawn, Apparel piece, IReadOnlyList<OrderLine> lines)
        {
            return lines.Count == 0 ? OrderVerdict.Tie : ApparelComparison.Compare(pawn, piece, lines);
        }

        /// <summary>A worn piece the check takes off first: not allowed, not locked or forced, and not keeping them alive in vacuum.</summary>
        private static bool MustTakeOff(Pawn pawn, ApparelPolicy policy, Apparel piece)
        {
            Map map = pawn.MapHeld;
            if (map != null && map.Biome.inVacuum && pawn.Position.GetVacuum(map) >= 0.5f
                && piece.def.equippedStatOffsets.GetStatOffsetFromList(StatDefOf.VacuumResistance) > 0f)
            {
                return false;
            }
            return !policy.filter.Allows(piece)
                && pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(piece)
                && !pawn.apparel.IsLocked(piece);
        }

        /// <summary>
        /// The pieces the check scores, gathered as it gathers them (outfit stands included): allowed, stored, not
        /// forbidden, not burning and the right gender.
        /// </summary>
        private static List<Apparel> Candidates(Pawn pawn, ApparelPolicy policy)
        {
            var things = new List<Thing>();
            pawn.Map.listerThings.GetAllThings(in things, ThingRequestGroup.Apparel, null, true);
            foreach (IHaulSource source in pawn.Map.haulDestinationManager.AllHaulSourcesListForReading)
            {
                foreach (Thing held in source.GetDirectlyHeldThings())
                {
                    if (held is Apparel)
                    {
                        things.Add(held);
                    }
                }
            }
            var candidates = new List<Apparel>();
            foreach (Thing thing in things)
            {
                if (thing is Apparel piece
                    && !candidates.Contains(piece)
                    && policy.filter.Allows(piece)
                    && piece.IsInAnyStorage()
                    && !piece.IsForbidden(pawn)
                    && !piece.IsBurning()
                    && (piece.def.apparel.gender == Gender.None || piece.def.apparel.gender == pawn.gender))
                {
                    candidates.Add(piece);
                }
            }
            return candidates;
        }

        /// <summary>The check's tests on a piece that scored well enough: biocoding, body, outfit stand, life stage and reach.</summary>
        private static bool CanPutOn(Pawn pawn, Apparel piece)
        {
            if (CompBiocodable.IsBiocoded(piece) && !CompBiocodable.IsBiocodedFor(piece, pawn))
            {
                return false;
            }
            if (!ApparelUtility.HasPartsToWear(pawn, piece.def) || !piece.def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage))
            {
                return false;
            }
            LocalTargetInfo target = piece;
            if (piece.ParentHolder is IApparelSource source && source is Thing holder)
            {
                if (!source.ApparelSourceEnabled)
                {
                    return false;
                }
                target = holder;
            }
            return pawn.CanReserveAndReach(target, PathEndMode.OnCell, pawn.NormalMaxDanger());
        }

        private static void SetNeededWarmth(Pawn pawn)
        {
            if (NeededWarmthField == null || pawn.Map == null)
            {
                return;
            }
            NeededWarmthField() = PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
        }

        private static AccessTools.FieldRef<NeededWarmth> FindNeededWarmth()
        {
            try
            {
                return AccessTools.StaticFieldRefAccess<NeededWarmth>(AccessTools.Field(typeof(JobGiver_OptimizeApparel), "neededWarmth"));
            }
            catch (Exception e)
            {
                Log.Warning("[Optimal Loadouts] Couldn't find the apparel check's warmth need, so the outfit preview uses the last value it set: " + e.Message);
                return null;
            }
        }
    }
}
