using System;
using System.Collections.Generic;
using HarmonyLib;
using OptimalLoadouts.ApparelOrders.Gui;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>
    /// Widens Manage apparel policies (and makes it taller where the screen has room), gives the policy's column three
    /// tabs (vanilla's filter, its priorities and its limits), and draws the outfit preview beside them, with a panel for a slot's
    /// pieces beside that where the screen is wide enough. The filter is a little wider than vanilla's, which other
    /// mods that draw on it (Apparel Policy Builder's button, at its right edge) follow. Windows that replace this one
    /// with their own subclass (Choose Your Outfit) are left alone, since they lay out the space themselves.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_ManageApparelPolicies))]
    public static class ManageApparelPoliciesPatch
    {
        private const float PreviewWidth = 400f;

        /// <summary>Room added to the policy's column for each stat line's layer button.</summary>
        private const float PolicyColumnExtra = 100f;

        /// <summary>Wide enough for a slot's pieces with their names and two stat columns.</summary>
        private const float PreferredSlotWidth = 620f;

        /// <summary>The narrowest slot panel worth its own column. Narrower screens show the slot inside the preview.</summary>
        private const float MinSlotWidth = 420f;

        /// <summary>The narrowest the preview gets before the filter gives way instead, on small screens.</summary>
        private const float MinPreviewWidth = 380f;

        /// <summary>Choose Your Outfit's margin, so the window never runs off a small or high-UI-scale screen.</summary>
        private const float ScreenMargin = 80f;

        /// <summary>Tall enough for the preview's drawing and a list of pieces under it.</summary>
        private const float PreferredHeight = 900f;

        /// <summary>The window's own width before it's widened, which the policy's column keeps plus its extra room.</summary>
        private static float vanillaWidth = 700f;

        /// <summary>The policy column's open tab, and the window it's for, so each new window starts on the first tab.</summary>
        private static PolicyTab tab;

        private static Dialog_ManageApparelPolicies tabWindow;

        private static readonly Func<Dialog_ManagePolicies<ApparelPolicy>, ApparelPolicy> SelectedPolicy =
            AccessTools.MethodDelegate<Func<Dialog_ManagePolicies<ApparelPolicy>, ApparelPolicy>>(
                AccessTools.PropertyGetter(typeof(Dialog_ManagePolicies<ApparelPolicy>), "SelectedPolicy"));

        [HarmonyPatch(nameof(Dialog_ManageApparelPolicies.InitialSize), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Widen(Dialog_ManageApparelPolicies __instance, ref Vector2 __result)
        {
            if (IsVanillaWindow(__instance))
            {
                vanillaWidth = __result.x;
                float wanted = __result.x + PolicyColumnExtra + PreviewWidth + PreferredSlotWidth + 2f * UiStyle.Gap;
                __result.x = Mathf.Min(wanted, UI.screenWidth - ScreenMargin);
                __result.y = Mathf.Max(__result.y, Mathf.Min(PreferredHeight, UI.screenHeight - ScreenMargin));
            }
        }

        /// <summary>
        /// Lays out the window's columns and puts the tab row on the policy's. On the Stat order tab it skips vanilla's
        /// filter for this frame, since drawing over the filter would still let clicks through to it. Postfixes (ours,
        /// Apparel Policy Builder's button, [KV] Save Storage's buttons) still run.
        /// </summary>
        [HarmonyPatch("DoContentsRect")]
        [HarmonyPrefix]
        private static bool MakeRoom(Dialog_ManageApparelPolicies __instance, ref Rect rect, out PanelRects __state)
        {
            __state = default;
            if (!IsVanillaWindow(__instance))
            {
                return true;
            }
            if (__instance != tabWindow)
            {
                tabWindow = __instance;
                tab = PolicyTab.AllowedItems;
            }
            float room = __instance.windowRect.width - vanillaWidth - PolicyColumnExtra - UiStyle.Gap;
            if (room >= PreviewWidth + UiStyle.Gap + MinSlotWidth)
            {
                float slotWidth = room - PreviewWidth - UiStyle.Gap;
                __state.slot = new Rect(rect.xMax - slotWidth, rect.y, slotWidth, rect.height);
                __state.preview = new Rect(__state.slot.x - UiStyle.Gap - PreviewWidth, rect.y, PreviewWidth, rect.height);
                rect.width -= room + UiStyle.Gap;
            }
            else
            {
                float previewWidth = Mathf.Min(Mathf.Max(room, MinPreviewWidth), rect.width / 2f);
                __state.preview = new Rect(rect.xMax - previewWidth, rect.y, previewWidth, rect.height);
                rect.width -= previewWidth + UiStyle.Gap;
            }

            // The tab row sits where the preview's does, so the two columns' tabs line up.
            rect.yMin += TabDrawer.TabHeight;
            __state.policyColumn = rect;
            return tab == PolicyTab.AllowedItems;
        }

        [HarmonyPatch("DoContentsRect")]
        [HarmonyPostfix]
        private static void DrawPanels(Dialog_ManageApparelPolicies __instance, PanelRects __state)
        {
            if (__state.preview.width <= 0f)
            {
                return;
            }
            ApparelPolicy policy = SelectedPolicy(__instance);
            if (tab == PolicyTab.StatOrder)
            {
                Widgets.DrawMenuSection(__state.policyColumn);
                StatOrderPanel.Draw(__state.policyColumn.ContractedBy(UiStyle.Gap), policy);
            }
            else if (tab == PolicyTab.Forbids)
            {
                Widgets.DrawMenuSection(__state.policyColumn);
                ForbidsPanel.Draw(__state.policyColumn.ContractedBy(UiStyle.Gap), policy);
            }
            // After the section's frame, so the open tab covers the frame's top edge, as on vanilla's filter.
            DrawPolicyTabs(__state.policyColumn, policy);
            OutfitPreviewPanel.Draw(__state.preview, __state.slot, policy);
        }

        private static void DrawPolicyTabs(Rect column, ApparelPolicy policy)
        {
            int forbids = ForbidsPanel.Count(policy);
            string forbidsLabel = forbids > 0 ? "OL_Policy_LimitsTabCount".Translate(forbids) : "OL_Policy_LimitsTab".Translate();
            var tabs = new List<TabRecord>
            {
                new TabRecord("OL_Policy_AllowedItemsTab".Translate(), () => tab = PolicyTab.AllowedItems, tab == PolicyTab.AllowedItems),
                new TabRecord("OL_Policy_PrioritiesTab".Translate(), () => tab = PolicyTab.StatOrder, tab == PolicyTab.StatOrder),
                new TabRecord(forbidsLabel, () => tab = PolicyTab.Forbids, tab == PolicyTab.Forbids),
            };
            using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
            {
                TabDrawer.DrawTabs(column, tabs);
            }
        }

        private static bool IsVanillaWindow(Dialog_ManageApparelPolicies window)
        {
            return window.GetType() == typeof(Dialog_ManageApparelPolicies);
        }

        /// <summary>Where each of our panels goes this frame, worked out before vanilla draws the filter.</summary>
        private struct PanelRects
        {
            /// <summary>The policy's column under its tab row: the filter's rect, or the stat order's.</summary>
            public Rect policyColumn;
            public Rect preview;
            public Rect slot;
        }

        private enum PolicyTab
        {
            AllowedItems,
            StatOrder,
            Forbids,
        }
    }
}
