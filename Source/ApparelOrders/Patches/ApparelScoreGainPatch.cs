using System;
using System.Collections.Generic;
using HarmonyLib;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>
    /// Ranks the pieces a pawn could put on by their policy's stat order. Runs before other mods' postfixes, so a
    /// mod that marks a piece "never wear this" afterwards still wins.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreGain))]
    public static class ApparelScoreGainPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (__result <= ApparelScoreBands.NeverWear || pawn?.outfits == null || pawn.apparel == null || ap == null)
            {
                return;
            }
            try
            {
                ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
                if (orders == null)
                {
                    return;
                }
                ApparelPolicy policy = PreviewPolicy.PolicyOf(pawn);
                IReadOnlyList<OrderLine> lines = orders.EffectiveLines(policy);
                if (lines.Count == 0)
                {
                    return;
                }
                OrderVerdict verdict = ApparelComparison.Compare(pawn, ap, lines);
                __result = orders.OnlyWhatHelps(policy) && ExtraPieces.Skipped(pawn, ap, verdict)
                    ? ApparelScoreBands.Worse
                    : ApparelScoreBands.ScoreGain(verdict, lines.Count, __result);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't rank apparel by its order, so the game's own pick stands: " + e,
                    "OptimalLoadouts.ApparelScoreGain".GetHashCode());
            }
        }
    }
}
