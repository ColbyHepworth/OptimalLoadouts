using System;
using HarmonyLib;
using OptimalLoadouts.ApparelOrders.Gui;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>
    /// Widens Manage apparel policies and draws the outfit preview beside the policy's filter. The filter keeps its
    /// vanilla width, so other mods that draw on it (Apparel Policy Builder's button) still find it where they expect.
    /// Windows that replace this one with their own subclass (Choose Your Outfit) are left alone, since they lay out
    /// the space themselves.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_ManageApparelPolicies))]
    public static class ManageApparelPoliciesPatch
    {
        private const float PanelWidth = 380f;

        /// <summary>Choose Your Outfit's margin, so the window never runs off a small or high-UI-scale screen.</summary>
        private const float ScreenMargin = 80f;

        private static readonly Func<Dialog_ManagePolicies<ApparelPolicy>, ApparelPolicy> SelectedPolicy =
            AccessTools.MethodDelegate<Func<Dialog_ManagePolicies<ApparelPolicy>, ApparelPolicy>>(
                AccessTools.PropertyGetter(typeof(Dialog_ManagePolicies<ApparelPolicy>), "SelectedPolicy"));

        [HarmonyPatch(nameof(Dialog_ManageApparelPolicies.InitialSize), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Widen(Dialog_ManageApparelPolicies __instance, ref Vector2 __result)
        {
            if (IsVanillaWindow(__instance))
            {
                __result.x = Mathf.Min(__result.x + PanelWidth + UiStyle.Gap, UI.screenWidth - ScreenMargin);
            }
        }

        [HarmonyPatch("DoContentsRect")]
        [HarmonyPrefix]
        private static void MakeRoom(Dialog_ManageApparelPolicies __instance, ref Rect rect, out Rect __state)
        {
            __state = Rect.zero;
            if (!IsVanillaWindow(__instance))
            {
                return;
            }
            float panelWidth = Mathf.Min(PanelWidth, rect.width / 2f);
            __state = new Rect(rect.xMax - panelWidth, rect.y, panelWidth, rect.height);
            rect.width -= panelWidth + UiStyle.Gap;
        }

        [HarmonyPatch("DoContentsRect")]
        [HarmonyPostfix]
        private static void DrawPreview(Dialog_ManageApparelPolicies __instance, Rect __state)
        {
            if (__state.width > 0f)
            {
                OutfitPreviewPanel.Draw(__state, SelectedPolicy(__instance));
            }
        }

        private static bool IsVanillaWindow(Dialog_ManageApparelPolicies window)
        {
            return window.GetType() == typeof(Dialog_ManageApparelPolicies);
        }
    }
}
