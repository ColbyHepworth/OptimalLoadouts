using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// The stats of the piece tried on in the slot browser, or of the piece filling the slot, one to a row as its info card
    /// lists them, beside the slot's list or under it. Clicking a stat an order can use sorts the list by it, "+" makes it
    /// one of the policy's own priorities, and "−" on such a priority removes it again.
    /// </summary>
    public static class PieceStatsPanel
    {
        /// <summary>As tall as a band, so the stats stay compact.</summary>
        private const float StatHeight = UiStyle.BandHeight;

        /// <summary>The share of a stat's cell its name takes, as on the info card.</summary>
        private const float LabelShare = 0.55f;

        /// <summary>Room for a priority's place in the order ("12."), beside its button.</summary>
        private const float NumberWidth = 26f;

        /// <summary>The height the panel needs to show the stats of the piece it's for without scrolling.</summary>
        public static float HeightFor(PreviewSession session)
        {
            Apparel piece = session.Slot?.StatsPiece;
            float title = UiStyle.HeadingGap + UiStyle.RowHeight;
            return piece == null ? title : title + Runs(PieceStatSheet.For(piece)).Sum(RunHeight);
        }

        public static void Draw(Rect rect, PreviewSession session)
        {
            SlotView open = session.Slot;
            Apparel piece = open.StatsPiece;
            Rect title = new Rect(rect.x, rect.y + UiStyle.HeadingGap, rect.width, UiStyle.RowHeight);
            if (piece == null)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(title, "OL_Stats_NoPiece".Translate());
                }
                return;
            }
            // "Priority" heads the numbers and buttons at the rows' end, which sit left of the scrollbar.
            float priorityWidth = NumberWidth + StatHeight;
            Rect priorityHeading = new Rect(title.xMax - GenUI.ScrollBarWidth - priorityWidth - UiStyle.Gap, title.y,
                priorityWidth + UiStyle.Gap, title.height);
            Rect nameRect = new Rect(title.x, title.y, priorityHeading.x - title.x, title.height);
            string name = PieceLabel.Of(piece);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(nameRect, name.Truncate(nameRect.width));
            }
            TooltipHandler.TipRegion(nameRect, name);
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerRight, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(priorityHeading, "OL_Stats_PriorityHeader".Translate());
            }
            TooltipHandler.TipRegion(priorityHeading, "OL_Stats_PriorityHeaderTip".Translate());

            List<List<StatDrawEntry>> runs = Runs(PieceStatSheet.For(piece));
            Rect list = new Rect(rect.x, title.yMax, rect.width, rect.yMax - title.yMax);
            Rect view = new Rect(0f, 0f, list.width - GenUI.ScrollBarWidth, runs.Sum(RunHeight));
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelOrder own = orders?.OrderFor(session.Policy);
            IReadOnlyList<OrderLine> lines = orders?.EffectiveLines(session.Policy) ?? new List<OrderLine>();
            bool mouseInList = Mouse.IsOver(list);
            Widgets.BeginScrollView(list, ref open.StatsScroll, view);
            try
            {
                float y = 0f;
                foreach (List<StatDrawEntry> run in runs)
                {
                    DrawBand(new Rect(0f, y, view.width, UiStyle.BandHeight), run[0].category);
                    y += UiStyle.BandHeight;
                    for (int i = 0; i < run.Count; i++)
                    {
                        float rowY = y + i * StatHeight;
                        // Only stats in view are drawn: a piece can list fifty.
                        if (rowY + StatHeight < open.StatsScroll.y || rowY > open.StatsScroll.y + list.height)
                        {
                            continue;
                        }
                        DrawStat(new Rect(0f, rowY, view.width, StatHeight), run[i], piece, session, own, lines, mouseInList);
                    }
                    y += run.Count * StatHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>The sheet split where the category changes. Categories can share a display order and take turns.</summary>
        private static List<List<StatDrawEntry>> Runs(IReadOnlyList<StatDrawEntry> entries)
        {
            var runs = new List<List<StatDrawEntry>>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == 0 || entries[i].category != entries[i - 1].category)
                {
                    runs.Add(new List<StatDrawEntry>());
                }
                runs[runs.Count - 1].Add(entries[i]);
            }
            return runs;
        }

        private static float RunHeight(List<StatDrawEntry> run)
        {
            return UiStyle.BandHeight + run.Count * StatHeight;
        }

        private static void DrawBand(Rect band, StatCategoryDef category)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), category.LabelCap);
            }
        }

        /// <summary>
        /// One stat: its name, its value, the sort icon when the list is sorted by it, and at the end its place in the order
        /// when it's a priority, with "−" to remove it, or "+" to make it one. A priority from a base shows its place in grey
        /// with no button, since it's removed in the base.
        /// </summary>
        private static void DrawStat(Rect cell, StatDrawEntry entry, Apparel piece, PreviewSession session, ApparelOrder own,
            IReadOnlyList<OrderLine> lines, bool mouseInList)
        {
            StatDef stat = entry.stat;
            bool sortable = StatChoices.IsOffered(stat);
            float icon = cell.height;
            Rect button = new Rect(cell.xMax - icon, cell.y, icon, icon);
            Rect number = new Rect(button.x - NumberWidth, cell.y, NumberWidth, icon);
            Rect sortIcon = new Rect(number.x - icon, cell.y, icon, icon);
            Rect body = new Rect(cell.x, cell.y, sortIcon.x - cell.x, cell.height);
            Rect label = new Rect(body.x + UiStyle.Gap / 2f, cell.y, body.width * LabelShare, cell.height);
            Rect value = new Rect(label.xMax + UiStyle.Gap / 2f, cell.y, body.xMax - label.xMax - UiStyle.Gap / 2f, cell.height);

            if (sortable && mouseInList)
            {
                Widgets.DrawHighlightIfMouseover(body);
            }
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(label, entry.LabelCap.Truncate(label.width));
                Widgets.Label(value, entry.ValueString.Truncate(value.width));
            }
            TooltipHandler.TipRegion(body, new TipSignal(() => Tooltip(entry, piece, sortable), Gen.HashCombineInt(entry.LabelCap.GetHashCode(), (int)cell.y)));

            if (stat != null && session.SortStat == stat)
            {
                Texture2D sorted = session.LowestFirst ? UiStyle.SortAscendingIcon : UiStyle.SortDescendingIcon;
                GUI.DrawTexture(new Rect(sortIcon.center.x - sorted.width / 2f, sortIcon.center.y - sorted.height / 2f, sorted.width, sorted.height), sorted);
            }
            // A row half out of view mustn't take a click meant for the piece's name above it.
            if (sortable && mouseInList && Widgets.ButtonInvisible(body))
            {
                // Sorted by it already, it cycles as its column header does. Otherwise it starts highest first.
                if (session.SortStat == stat)
                {
                    SlotBrowserPanel.CycleSort(stat, session);
                }
                else
                {
                    session.ShowColumn(stat);
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
            }
            if (!sortable)
            {
                return;
            }
            // The place matches the Priorities tab's numbers, which follow the order the lines rank in.
            int place = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].IsItem && lines[i].stat == stat)
                {
                    place = i + 1;
                    break;
                }
            }
            int ownIndex = own?.lines.FindIndex(line => line.stat == stat) ?? -1;
            if (place > 0)
            {
                using (new TextBlock(TextAnchor.MiddleRight, ownIndex >= 0 ? Color.white : ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(number, "OL_Editor_LineNumber".Translate(place));
                }
            }
            if (ownIndex >= 0)
            {
                if (Widgets.ButtonImage(button.ContractedBy(2f), TexButton.Minus, true, "OL_Stats_RemovePriority".Translate()))
                {
                    // The list stays sorted by it, as the added column it was before it became a priority.
                    if (session.SortStat == stat)
                    {
                        session.AddedStat = stat;
                    }
                    ApparelOrdersComponent.Current.RemoveLine(session.Policy, ownIndex);
                }
            }
            else if (place < 0 && Widgets.ButtonImage(button.ContractedBy(2f), TexButton.Plus, true, "OL_Stats_AddPriority".Translate()))
            {
                ApparelOrdersComponent.Current?.AddLine(session.Policy, stat);
            }
        }

        /// <summary>What the info card says about the value (its base, material and quality), then how to sort by it.</summary>
        private static string Tooltip(StatDrawEntry entry, Apparel piece, bool sortable)
        {
            string explanation;
            try
            {
                explanation = entry.GetExplanationText(entry.hasOptionalReq ? entry.optionalReq : StatRequest.For(piece));
            }
            catch (Exception)
            {
                // A mod's stat worker that can't explain a piece it never expected still shows the value.
                explanation = entry.LabelCap;
            }
            return sortable ? explanation.TrimEnd() + "\n\n" + "OL_Stats_SortHint".Translate() : explanation;
        }
    }
}
