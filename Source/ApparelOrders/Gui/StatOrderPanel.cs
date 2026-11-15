using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Gui;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// A policy's priorities (its stat order), the Priorities tab of its column in Manage apparel policies: its base, its
    /// own lines to edit (any of which can also rule pieces out), the lines it gets from its base, and a button to add a
    /// priority.
    /// </summary>
    public static class StatOrderPanel
    {
        private const float NumberWidth = 28f;
        private const float DirectionWidth = 110f;
        private const float LayerWidth = 120f;
        private const float AddButtonWidth = 160f;

        private static Vector2 linesScroll;

        public static void Draw(Rect rect, ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null || policy == null || rect.height <= 0f)
            {
                return;
            }
            float y = rect.y;
            BasePicker.Draw(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), orders, policy);
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;

            string noteText = "OL_Editor_Note".Translate();
            float noteHeight = Text.CalcHeight(noteText, rect.width);
            Rect note = new Rect(rect.x, y, rect.width, noteHeight);
            UiStyle.Note(note, noteText);
            TooltipHandler.TipRegion(note, "OL_Editor_Help".Translate());
            y += noteHeight + UiStyle.Gap;

            DrawLines(new Rect(rect.x, y, rect.width, rect.yMax - y), orders, policy);
        }

        /// <summary>
        /// The lines in the order they rank: the policy's own locked items, then its bases' locked items, then its own
        /// stats, then each base's heading and stats. Then the add button, scrolling with them.
        /// </summary>
        private static void DrawLines(Rect listRect, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            List<OrderLine> ownLines = OwnLines(orders, policy);
            List<(ApparelPolicy from, OrderLine line)> fromBase = BaseLines(orders, policy, ownLines);
            List<(ApparelPolicy from, OrderLine line)> baseLocks = fromBase.Where(entry => entry.line.IsItem).ToList();
            List<(ApparelPolicy from, OrderLine line)> baseStats = fromBase.Where(entry => !entry.line.IsItem).ToList();
            int baseHeadings = baseStats.Select(entry => entry.from).Distinct().Count();
            int rows = ownLines.Count == 0 && fromBase.Count == 0 ? 1 : ownLines.Count + fromBase.Count + baseHeadings;
            float viewHeight = rows * UiStyle.RowHeight + UiStyle.Gap + UiStyle.RowHeight;
            bool scrolls = viewHeight > listRect.height;
            Rect viewRect = new Rect(0f, 0f, listRect.width - (scrolls ? UiStyle.ScrollbarWidth : 0f), viewHeight);
            Widgets.BeginScrollView(listRect, ref linesScroll, viewRect, showScrollbars: scrolls);
            float y = 0f;
            if (ownLines.Count == 0 && fromBase.Count == 0)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    string empty = orders.OnlyWhatHelps(policy) ? "OL_Editor_EmptyNothingNew" : "OL_Editor_Empty";
                    UiStyle.Note(new Rect(0f, y, viewRect.width, UiStyle.RowHeight), empty.Translate());
                }
                y += UiStyle.RowHeight;
            }
            int number = 0;
            for (int i = 0; i < ownLines.Count; i++)
            {
                if (ownLines[i].IsItem)
                {
                    DrawOwnLine(new Rect(0f, y, viewRect.width, UiStyle.RowHeight), orders, policy, ownLines, i, ++number);
                    y += UiStyle.RowHeight;
                }
            }
            foreach ((ApparelPolicy from, OrderLine line) in baseLocks)
            {
                Rect row = new Rect(0f, y, viewRect.width, UiStyle.RowHeight);
                // Numbered, since a base's locked items rank above this policy's own stats.
                using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(new Rect(row.x, row.y, NumberWidth, row.height), "OL_Editor_LineNumber".Translate(++number));
                }
                DrawBaseLine(row, line, from);
                y += UiStyle.RowHeight;
            }
            for (int i = 0; i < ownLines.Count; i++)
            {
                if (!ownLines[i].IsItem)
                {
                    DrawOwnLine(new Rect(0f, y, viewRect.width, UiStyle.RowHeight), orders, policy, ownLines, i, ++number);
                    y += UiStyle.RowHeight;
                }
            }
            ApparelPolicy currentSource = null;
            foreach ((ApparelPolicy from, OrderLine line) in baseStats)
            {
                if (from != currentSource)
                {
                    currentSource = from;
                    y += UiStyle.HeadingGap;
                    using (new TextBlock(TextAnchor.MiddleLeft))
                    {
                        UiStyle.Note(new Rect(0f, y, viewRect.width, UiStyle.RowHeight - UiStyle.HeadingGap), "OL_Editor_FromBase".Translate(from.label));
                    }
                    y += UiStyle.RowHeight - UiStyle.HeadingGap;
                }
                DrawBaseLine(new Rect(0f, y, viewRect.width, UiStyle.RowHeight), line, from);
                y += UiStyle.RowHeight;
            }
            y += UiStyle.Gap;
            if (Widgets.ButtonText(new Rect(0f, y, AddButtonWidth, UiStyle.RowHeight), "OL_Editor_AddStat".Translate()))
            {
                // Only the policy's own stats are hidden, so a base's stat can be added here to move it up or flip it.
                IEnumerable<StatDef> listed = ownLines.Where(line => line.IsStat).Select(line => line.stat);
                IEnumerable<BodyPartGroupDef> listedParts = ownLines.Where(line => line.IsCoverage).Select(line => line.covers);
                Find.WindowStack.Add(new Dialog_PickStat(stat => orders.AddLine(policy, stat), listed, null,
                    part => orders.AddCoverageLine(policy, part), listedParts));
            }
            DrawOnlyWhatHelps(new Rect(AddButtonWidth + UiStyle.Gap, y, viewRect.width - AddButtonWidth - UiStyle.Gap, UiStyle.RowHeight),
                orders, policy);
            Widgets.EndScrollView();
        }

        /// <summary>The "only add pieces that help" box with its words right after it, so the two read as one. Either toggles it.</summary>
        private static void DrawOnlyWhatHelps(Rect row, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            string label = "OL_Editor_OnlyWhatHelps".Translate();
            float boxY = row.y + (row.height - UiStyle.IconSize) / 2f;
            float labelWidth = Mathf.Min(Text.CalcSize(label).x, row.width - UiStyle.IconSize - UiStyle.IconGap);
            Rect hit = new Rect(row.x, row.y, UiStyle.IconSize + UiStyle.IconGap + labelWidth, row.height);
            bool on = orders.OnlyWhatHelps(policy);
            Widgets.DrawHighlightIfMouseover(hit);
            Widgets.CheckboxDraw(row.x, boxY, on, false, UiStyle.IconSize);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(row.x + UiStyle.IconSize + UiStyle.IconGap, row.y, labelWidth, row.height), label.Truncate(labelWidth));
            }
            TooltipHandler.TipRegion(hit, "OL_Editor_OnlyWhatHelpsTip".Translate());
            if (Widgets.ButtonInvisible(hit))
            {
                orders.SetOnlyWhatHelps(policy, !on);
                (on ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn).PlayOneShotOnCamera();
            }
        }

        /// <summary>One of the policy's own lines: <paramref name="index"/> is its place in the order, <paramref name="number"/> its place on screen.</summary>
        private static void DrawOwnLine(Rect row, ApparelOrdersComponent orders, ApparelPolicy policy, List<OrderLine> lines, int index, int number)
        {
            OrderLine line = lines[index];
            Widgets.DrawHighlightIfMouseover(row);
            float x = row.x;
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(x, row.y, NumberWidth, row.height), "OL_Editor_LineNumber".Translate(number));
                x += NumberWidth;
                float labelWidth = row.width - NumberWidth - LayerWidth - UiStyle.IconGap - DirectionWidth
                    - 3 * (UiStyle.IconSize + UiStyle.IconGap) - UiStyle.Gap;
                Rect labelRect = new Rect(x, row.y, labelWidth, row.height);
                if (line.IsItem)
                {
                    DrawItemLabel(labelRect, line.item);
                }
                else if (line.IsCoverage)
                {
                    string label = "OL_Editor_CoversPart".Translate(line.covers.label).CapitalizeFirst();
                    Widgets.Label(labelRect, label.Truncate(labelWidth));
                    TooltipHandler.TipRegion(labelRect, "OL_Editor_CoversTip".Translate(line.covers.label));
                }
                else
                {
                    Widgets.Label(labelRect, line.stat.LabelCap.Resolve().Truncate(labelWidth));
                    if (!line.stat.description.NullOrEmpty())
                    {
                        TooltipHandler.TipRegion(labelRect, line.stat.description);
                    }
                }
                x += labelWidth + UiStyle.Gap;
            }

            if (line.IsItem)
            {
                // An item line has no layer or direction, so its row keeps their space empty and its buttons line up.
                x += LayerWidth + UiStyle.IconGap + DirectionWidth + UiStyle.IconGap;
                DrawMoveAndRemove(x, row, orders, policy, index);
                return;
            }
            Rect layerButton = new Rect(x, row.y + UiStyle.IconGap / 2f, LayerWidth, row.height - UiStyle.IconGap);
            TooltipHandler.TipRegion(layerButton, OrderLineText.LayerTip("OL_Layer_Tip".Translate(), line.layers));
            if (Widgets.ButtonText(layerButton, OrderLineText.LayerLabel(line.layers, LayerWidth - 12f)))
            {
                Find.WindowStack.Add(new Dialog_PickLayers(line.layers, layers => orders.SetLayers(policy, index, layers)));
            }
            x += LayerWidth + UiStyle.IconGap;

            Rect direction = new Rect(x, row.y + UiStyle.IconGap / 2f, DirectionWidth, row.height - UiStyle.IconGap);
            TooltipHandler.TipRegion(direction, line.IsCoverage ? "OL_Editor_CoverageDirectionTip".Translate() : "OL_Editor_DirectionTip".Translate());
            if (Widgets.ButtonText(direction, DirectionButtonLabel(line).Truncate(DirectionWidth - 12f)))
            {
                Find.WindowStack.Add(new Dialog_LineDirection(line, policy, (lower, rulesOut) => orders.SetLineChoice(policy, index, lower, rulesOut),
                    value => orders.SetCountsFrom(policy, index, value)));
            }
            x += DirectionWidth + UiStyle.IconGap;
            DrawMoveAndRemove(x, row, orders, policy, index);
        }

        private static void DrawMoveAndRemove(float x, Rect row, ApparelOrdersComponent orders, ApparelPolicy policy, int index)
        {
            float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
            DrawMoveButton(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.ReorderUp, "OL_Editor_MoveUp".Translate(), orders,
                policy, index, -1);
            x += UiStyle.IconSize + UiStyle.IconGap;
            DrawMoveButton(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.ReorderDown, "OL_Editor_MoveDown".Translate(), orders,
                policy, index, 1);
            x += UiStyle.IconSize + UiStyle.IconGap;
            if (Widgets.ButtonImage(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.Delete, true, "OL_Editor_Remove".Translate()))
            {
                orders.RemoveLine(policy, index);
            }
        }

        /// <summary>
        /// A move button, greyed where the line can't move: at the end of the list, or where a stat would pass a locked item.
        /// </summary>
        private static void DrawMoveButton(Rect rect, Texture2D icon, string tooltip, ApparelOrdersComponent orders, ApparelPolicy policy,
            int index, int direction)
        {
            if (orders.CanMoveLine(policy, index, direction))
            {
                if (Widgets.ButtonImage(rect, icon, true, tooltip))
                {
                    orders.MoveLine(policy, index, direction);
                }
                return;
            }
            List<OrderLine> lines = orders.OrderFor(policy).lines;
            int target = index + direction;
            if (target >= 0 && target < lines.Count)
            {
                TooltipHandler.TipRegion(rect, "OL_Editor_LocksFirst".Translate());
            }
            using (new TextBlock(ColoredText.SubtleGrayColor))
            {
                GUI.DrawTexture(rect, icon);
            }
        }

        /// <summary>A locked item: its icon, its name and "locked" in grey. Its info card opens on a click.</summary>
        private static void DrawItemLabel(Rect rect, ThingDef item)
        {
            Rect icon = new Rect(rect.x, rect.y + (rect.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.DefIcon(icon, item);
            Rect text = new Rect(icon.xMax + UiStyle.IconGap, rect.y, rect.xMax - icon.xMax - UiStyle.IconGap, rect.height);
            string name = item.LabelCap.Resolve();
            string tag = "OL_Editor_LockedItem".Translate();
            float nameWidth = Mathf.Min(Text.CalcSize(name).x, text.width);
            Widgets.Label(new Rect(text.x, text.y, nameWidth, text.height), name.Truncate(nameWidth));
            UiStyle.Note(new Rect(text.x + nameWidth + UiStyle.IconGap, text.y, text.width - nameWidth - UiStyle.IconGap, text.height),
                tag.Truncate(text.width - nameWidth - UiStyle.IconGap));
            TooltipHandler.TipRegion(rect, "OL_Editor_LockedItemTip".Translate(item.label));
            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new Dialog_InfoCard(item, GenStuff.DefaultStuffFor(item)));
            }
        }

        private static void DrawBaseLine(Rect row, OrderLine line, ApparelPolicy from)
        {
            if (line.IsItem)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(new Rect(row.x + NumberWidth, row.y, row.width - NumberWidth, row.height),
                        "OL_Editor_BaseItemLine".Translate(line.item.LabelCap, from.label));
                }
                return;
            }
            string subject = line.IsCoverage ? OrderLineText.CoverageOf(line, true) : OrderLineText.StatOnLayerCap(line);
            string text = "OL_Editor_BaseLine".Translate(subject, DirectionButtonLabel(line)).Resolve();
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                UiStyle.Note(new Rect(row.x + NumberWidth, row.y, row.width - NumberWidth, row.height), text);
            }
        }

        /// <summary>
        /// "Higher first", "Higher, from 3 mm" when the line counts from a value, or "At least 3 mm" when it rules out what's
        /// below. A body part line: "Required" or "Not allowed".
        /// </summary>
        private static string DirectionButtonLabel(OrderLine line)
        {
            if (line.IsCoverage)
            {
                return line.rulesOut ? "OL_Rule_NotAllowed".Translate() : "OL_Rule_Required".Translate();
            }
            string value = OrderLineText.CountsFromValue(line.stat, line.countsFrom);
            if (line.rulesOut)
            {
                return line.lowerIsBetter ? "OL_Rule_AtMost".Translate(value) : "OL_Rule_AtLeast".Translate(value);
            }
            if (line.countsFrom <= 0f)
            {
                return DirectionLabel(line);
            }
            return line.lowerIsBetter ? "OL_Order_LowerFrom".Translate(value) : "OL_Order_HigherFrom".Translate(value);
        }

        private static string DirectionLabel(OrderLine line)
        {
            return line.lowerIsBetter ? "OL_Order_LowerFirst".Translate() : "OL_Order_HigherFirst".Translate();
        }

        private static List<OrderLine> OwnLines(ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            return orders.OrderFor(policy)?.lines ?? new List<OrderLine>();
        }

        /// <summary>The lines the policy gets from its bases, each with the policy it comes from.</summary>
        private static List<(ApparelPolicy from, OrderLine line)> BaseLines(ApparelOrdersComponent orders, ApparelPolicy policy, List<OrderLine> ownLines)
        {
            var result = new List<(ApparelPolicy, OrderLine)>();
            var listed = new HashSet<Def>(ownLines.Select(line => line.Subject));
            var visited = new HashSet<ApparelPolicy> { policy };
            for (ApparelPolicy basePolicy = orders.BaseOf(policy); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                List<OrderLine> baseLines = orders.OrderFor(basePolicy)?.lines;
                if (baseLines == null)
                {
                    break;
                }
                foreach (OrderLine line in baseLines)
                {
                    if (line.Subject != null && listed.Add(line.Subject))
                    {
                        result.Add((basePolicy, line));
                    }
                }
            }
            return result;
        }
    }
}
