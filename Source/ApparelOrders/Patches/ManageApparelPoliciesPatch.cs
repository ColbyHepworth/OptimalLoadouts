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
    /// Widens Manage apparel policies (and makes it taller where the screen has room) and draws the outfit preview beside
    /// the policy's filter, with a panel for a slot's pieces beside that where the screen is wide enough. The filter
    /// keeps its vanilla width, so other mods that draw on it (Apparel Policy Builder's button) still find it where they
    /// expect. Windows that replace this one with their own subclass (Choose Your Outfit) are left alone, since they lay
    /// out the space themselves.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_ManageApparelPolicies))]
    public static class ManageApparelPoliciesPatch
    {
        private const float PreviewWidth = 400f;

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

        /// <summary>The window's own width before it's widened, so the filter keeps exactly that much.</summary>
        private static float vanillaWidth = 700f;

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
                float wanted = __result.x + PreviewWidth + PreferredSlotWidth + 2f * UiStyle.Gap;
                __result.x = Mathf.Min(wanted, UI.screenWidth - ScreenMargin);
                __result.y = Mathf.Max(__result.y, Mathf.Min(PreferredHeight, UI.screenHeight - ScreenMargin));
            }
        }

        [HarmonyPatch("DoContentsRect")]
        [HarmonyPrefix]
        private static void MakeRoom(Dialog_ManageApparelPolicies __instance, ref Rect rect, out (Rect preview, Rect slot) __state)
        {
            __state = (Rect.zero, Rect.zero);
            if (!IsVanillaWindow(__instance))
            {
                return;
            }
            float room = __instance.windowRect.width - vanillaWidth - UiStyle.Gap;
            if (room >= PreviewWidth + UiStyle.Gap + MinSlotWidth)
            {
                float slotWidth = room - PreviewWidth - UiStyle.Gap;
                Rect slot = new Rect(rect.xMax - slotWidth, rect.y, slotWidth, rect.height);
                Rect preview = new Rect(slot.x - UiStyle.Gap - PreviewWidth, rect.y, PreviewWidth, rect.height);
                __state = (preview, slot);
                rect.width -= room + UiStyle.Gap;
                return;
            }
            float previewWidth = Mathf.Min(Mathf.Max(room, MinPreviewWidth), rect.width / 2f);
            __state = (new Rect(rect.xMax - previewWidth, rect.y, previewWidth, rect.height), Rect.zero);
            rect.width -= previewWidth + UiStyle.Gap;
        }

        [HarmonyPatch("DoContentsRect")]
        [HarmonyPostfix]
        private static void DrawPreview(Dialog_ManageApparelPolicies __instance, (Rect preview, Rect slot) __state)
        {
            if (__state.preview.width > 0f)
            {
                OutfitPreviewPanel.Draw(__state.preview, __state.slot, SelectedPolicy(__instance));
            }
        }

        private static bool IsVanillaWindow(Dialog_ManageApparelPolicies window)
        {
            return window.GetType() == typeof(Dialog_ManageApparelPolicies);
        }
    }
}
