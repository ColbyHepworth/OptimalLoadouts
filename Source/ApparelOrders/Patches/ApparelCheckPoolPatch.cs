using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>
    /// While the game's apparel check runs for a colonist, its own list of stored apparel is the pool sets are completed
    /// from, so a colonist trades a one-piece suit for separate pieces only when they're all there to put on.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    public static class ApparelCheckPoolPatch
    {
        /// <summary>The check's list of every apparel on the map, filled and read inside it. Null if the game renames it.</summary>
        private static readonly AccessTools.FieldRef<List<Thing>> CheckedApparel = FindCheckedApparel();

        private static void Prefix(Pawn pawn, out PartnerPool __state)
        {
            __state = CheckedApparel == null || pawn == null ? null : PartnerPool.Use(pawn, () => CheckedApparel().OfType<Apparel>());
        }

        private static Exception Finalizer(Exception __exception, PartnerPool __state)
        {
            __state?.Dispose();
            return __exception;
        }

        private static AccessTools.FieldRef<List<Thing>> FindCheckedApparel()
        {
            try
            {
                return AccessTools.StaticFieldRefAccess<List<Thing>>(AccessTools.Field(typeof(JobGiver_OptimizeApparel), "tmpApparelList"));
            }
            catch (Exception e)
            {
                Log.Warning("[Optimal Loadouts] Couldn't find the apparel check's list of apparel, so colonists judge each piece alone, not as part of a set: " + e.Message);
                return null;
            }
        }
    }
}
