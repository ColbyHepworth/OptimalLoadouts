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
        /// <summary>
        /// Other mods mark a piece "never wear this" with -1000 or lower. Vanilla subtracts the replaced pieces' scores
        /// from it, which can lift it a little, and a real preference stays far above this. Under CE, a worn required
        /// piece can push a real gain lower, but required apparel is left to the game anyway.
        /// </summary>
        private const float NeverWear = -500f;

        [HarmonyPriority(Priority.First)]
        private static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (__result <= NeverWear || pawn?.outfits == null || pawn.apparel == null || ap == null)
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
                IReadOnlyList<OrderLine> lines = orders.EffectiveLines(pawn.outfits.CurrentApparelPolicy);
                if (lines.Count == 0)
                {
                    return;
                }
                __result = ApparelScoreBands.ScoreGain(ApparelComparison.Compare(pawn, ap, lines), lines.Count, __result);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't rank apparel by its order, so the game's own pick stands: " + e,
                    "OptimalLoadouts.ApparelScoreGain".GetHashCode());
            }
        }
    }
}
