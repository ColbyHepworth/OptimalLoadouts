using HarmonyLib;
using RimWorld;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>Duplicating an apparel policy copies its stat order, forbids and base too.</summary>
    [HarmonyPatch(typeof(ApparelPolicy), nameof(ApparelPolicy.CopyFrom))]
    public static class ApparelPolicyCopyFromPatch
    {
        private static void Postfix(ApparelPolicy __instance, Policy other)
        {
            if (other is ApparelPolicy from)
            {
                ApparelOrdersComponent.Current?.CopyOrder(from, __instance);
            }
        }
    }
}
