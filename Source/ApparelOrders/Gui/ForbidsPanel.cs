using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// A policy's forbids, the Limits tab of its column in Manage apparel policies (each forbid shown as a minimum or
    /// maximum), under the same "Builds on" row as the Priorities tab. Each forbid reads as a sentence, the
    /// stat, below or above, the threshold, the layer and how many items it catches, with its slider under it. Then
    /// "Add forbid" and the forbids the policy gets from its base.
    /// </summary>
    public static class ForbidsPanel
    {
        private const float DirectionWidth = 80f;
        private const float LayerWidth = 120f;
        private const float ValueWidth = 70f;
        private const float CountWidth = 70f;
        private const float AddButtonWidth = 160f;
        private const float ForbidHeight = UiStyle.RowHeight * 2f + UiStyle.Gap;
        private const int ItemsListed = 20;

        private static Vector2 scroll;

        /// <summary>How many forbids reach the policy, its own and its base's, for the tab's label.</summary>
        public static int Count(ApparelPolicy policy)
        {
            return ApparelOrdersComponent.Current?.EffectiveForbids(policy).Count ?? 0;
        }

        public static void Draw(Rect rect, ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null || policy == null || rect.height <= 0f)
            {
                return;
            }
            BasePicker.Draw(new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight), orders, policy);
            DrawMaterial(new Rect(rect.x, rect.y + UiStyle.RowHeight + UiStyle.Gap / 2f, rect.width, UiStyle.RowHeight), orders, policy);
            float noteY = rect.y + 2f * UiStyle.RowHeight + UiStyle.Gap;
            string noteText = "OL_Forbid_Note".Translate();
            float noteHeight = Text.CalcHeight(noteText, rect.width);
            Rect note = new Rect(rect.x, noteY, rect.width, noteHeight);
            UiStyle.Note(note, noteText);
            TooltipHandler.TipRegion(note, "OL_Forbid_HeadingTip".Translate());
            float top = note.yMax + UiStyle.Gap;
            DrawList(new Rect(rect.x, top, rect.width, rect.yMax - top), orders, policy);
        }

        /// <summary>Which material the policy's limits judge apparel in, as a button opening the material list.</summary>
        private static void DrawMaterial(Rect row, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            string label = "OL_Forbid_Material".Translate(OrderLineText.MaterialLabel(orders.LimitMaterialOf(policy)));
            if (Widgets.ButtonText(row, label.Truncate(row.width)))
            {
                var general = new List<(string, Action)>
                {
                    ("OL_Forbid_MaterialUsual".Translate(), () => orders.SetLimitMaterial(policy, LimitMaterial.Usual)),
                    ("OL_Forbid_MaterialBest".Translate(), () => orders.SetLimitMaterial(policy, LimitMaterial.Best)),
                };
                Find.WindowStack.Add(new Dialog_PickMaterial("OL_Forbid_MaterialTitle".Translate(), general,
                    stuff => orders.SetLimitMaterial(policy, LimitMaterial.Of(stuff))));
            }
            TooltipHandler.TipRegion(row, "OL_Forbid_MaterialTip".Translate());
        }

        /// <summary>The policy's own forbids, the add button, then each base's heading and forbids, scrolling together.</summary>
        private static void DrawList(Rect listRect, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            List<ApparelForbid> own = orders.OrderFor(policy)?.forbids ?? new List<ApparelForbid>();
            List<(ApparelPolicy from, ApparelForbid forbid)> fromBase = BaseForbids(orders, policy);
            int baseHeadings = fromBase.Select(entry => entry.from).Distinct().Count();
            float viewHeight = (own.Count == 0 ? UiStyle.RowHeight : own.Count * ForbidHeight)
                + UiStyle.Gap + UiStyle.RowHeight + (fromBase.Count + baseHeadings) * UiStyle.RowHeight;
            bool scrolls = viewHeight > listRect.height;
            Rect viewRect = new Rect(0f, 0f, listRect.width - (scrolls ? UiStyle.ScrollbarWidth : 0f), viewHeight);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect, showScrollbars: scrolls);
            try
            {
                float y = 0f;
                if (own.Count == 0)
                {
                    using (new TextBlock(TextAnchor.MiddleLeft))
                    {
                        UiStyle.Note(new Rect(0f, y, viewRect.width, UiStyle.RowHeight), "OL_Forbid_Empty".Translate());
                    }
                    y += UiStyle.RowHeight;
                }
                for (int i = 0; i < own.Count; i++)
                {
                    if (DrawForbid(new Rect(0f, y, viewRect.width, ForbidHeight), orders, policy, own[i], i))
                    {
                        break;
                    }
                    y += ForbidHeight;
                }
                y += UiStyle.Gap;
                if (Widgets.ButtonText(new Rect(0f, y, AddButtonWidth, UiStyle.RowHeight), "OL_Forbid_Add".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_PickStat(stat => orders.AddForbid(policy, stat), Enumerable.Empty<StatDef>(),
                        "OL_Forbid_AddTitle".Translate()));
                }
                y += UiStyle.RowHeight;
                DrawBaseForbids(y, viewRect.width, fromBase);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// One forbid: the stat, below or above, the threshold, the layer, the count and remove on one row, the slider
        /// under them. Returns true when it was removed, so the list stops drawing the old one.
        /// </summary>
        private static bool DrawForbid(Rect rect, ApparelOrdersComponent orders, ApparelPolicy policy, ApparelForbid forbid, int index)
        {
            Widgets.DrawHighlightIfMouseover(new Rect(rect.x, rect.y, rect.width, rect.height - UiStyle.Gap));
            Rect row = new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight);
            float buttonY = row.y + UiStyle.IconGap / 2f;
            float buttonHeight = row.height - UiStyle.IconGap;
            Rect remove = new Rect(row.xMax - UiStyle.IconSize, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect count = new Rect(remove.x - UiStyle.IconGap - CountWidth, row.y, CountWidth, row.height);
            Rect layerButton = new Rect(count.x - UiStyle.IconGap - LayerWidth, buttonY, LayerWidth, buttonHeight);
            Rect value = new Rect(layerButton.x - UiStyle.IconGap - ValueWidth, row.y, ValueWidth, row.height);
            Rect direction = new Rect(value.x - UiStyle.IconGap - DirectionWidth, buttonY, DirectionWidth, buttonHeight);
            Rect label = new Rect(row.x, row.y, direction.x - UiStyle.Gap - row.x, row.height);

            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(label, forbid.stat.LabelCap.Resolve().Truncate(label.width));
            }
            if (!forbid.stat.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(label, forbid.stat.description);
            }
            LimitMaterial material = orders.LimitMaterialOf(policy);
            FloatRange range = ApparelForbids.RangeOf(forbid.stat, material);
            List<ThingDef> caught = ApparelForbids.Caught(policy, forbid);
            if (Widgets.ButtonText(direction, forbid.forbidAbove ? "OL_Forbid_AtMost".Translate() : "OL_Forbid_AtLeast".Translate()))
            {
                // A forbid that catches nothing keeps catching nothing when flipped, instead of catching everything at once.
                bool catchesAny = caught.Count > 0 || orders.LockedItems(policy).Any(def => forbid.Forbids(def, material));
                float threshold = catchesAny ? forbid.threshold : forbid.forbidAbove ? range.min : range.max;
                orders.SetForbid(policy, index, !forbid.forbidAbove, threshold);
            }
            TooltipHandler.TipRegion(direction, "OL_Forbid_DirectionTip".Translate());
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(value, OrderLineText.ValueText(forbid.stat, forbid.threshold));
            }
            if (Widgets.ButtonText(layerButton, OrderLineText.LayerLabel(forbid.layers, LayerWidth - 12f)))
            {
                Find.WindowStack.Add(new Dialog_PickLayers(forbid.layers, layers => orders.SetForbidLayers(policy, index, layers)));
            }
            TooltipHandler.TipRegion(layerButton, OrderLineText.LayerTip("OL_Forbid_LayerTip".Translate(), forbid.layers));
            using (new TextBlock(TextAnchor.MiddleRight, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(count, caught.Count == 1 ? "OL_Forbid_CountOne".Translate() : "OL_Forbid_Count".Translate(caught.Count));
            }
            if (caught.Count > 0)
            {
                TooltipHandler.TipRegion(count, () => CaughtTip(caught), Gen.HashCombineInt(policy.id, index));
            }
            if (Widgets.ButtonImage(remove, TexButton.Delete, true, "OL_Editor_Remove".Translate()))
            {
                orders.RemoveForbid(policy, index);
                return true;
            }

            Rect slider = new Rect(rect.x, row.yMax, rect.width - UiStyle.IconSize - UiStyle.IconGap, UiStyle.RowHeight);
            float moved = Widgets.HorizontalSlider(slider, forbid.threshold, range.min, range.max, middleAlignment: true);
            if (moved != forbid.threshold)
            {
                orders.SetForbid(policy, index, forbid.forbidAbove, moved);
            }
            return false;
        }

        private static void DrawBaseForbids(float y, float width, List<(ApparelPolicy from, ApparelForbid forbid)> fromBase)
        {
            ApparelPolicy currentSource = null;
            foreach ((ApparelPolicy from, ApparelForbid forbid) in fromBase)
            {
                if (from != currentSource)
                {
                    currentSource = from;
                    y += UiStyle.HeadingGap;
                    using (new TextBlock(TextAnchor.MiddleLeft))
                    {
                        UiStyle.Note(new Rect(0f, y, width, UiStyle.RowHeight - UiStyle.HeadingGap), "OL_Editor_FromBase".Translate(from.label));
                    }
                    y += UiStyle.RowHeight - UiStyle.HeadingGap;
                }
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(new Rect(UiStyle.Gap, y, width - UiStyle.Gap, UiStyle.RowHeight), OrderLineText.ForbidCap(forbid));
                }
                y += UiStyle.RowHeight;
            }
        }

        private static string CaughtTip(List<ThingDef> caught)
        {
            string listed = string.Join("\n", caught.Take(ItemsListed).Select(def => def.LabelCap.Resolve()));
            return caught.Count > ItemsListed ? listed + "\n" + "OL_Forbid_AndMore".Translate(caught.Count - ItemsListed) : listed;
        }

        /// <summary>The forbids the policy gets from its bases, each with the policy it comes from.</summary>
        private static List<(ApparelPolicy from, ApparelForbid forbid)> BaseForbids(ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            var result = new List<(ApparelPolicy, ApparelForbid)>();
            var visited = new HashSet<ApparelPolicy> { policy };
            for (ApparelPolicy basePolicy = orders.BaseOf(policy); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                List<ApparelForbid> forbids = orders.OrderFor(basePolicy)?.forbids;
                if (forbids == null)
                {
                    break;
                }
                foreach (ApparelForbid forbid in forbids)
                {
                    result.Add((basePolicy, forbid));
                }
            }
            return result;
        }
    }
}
