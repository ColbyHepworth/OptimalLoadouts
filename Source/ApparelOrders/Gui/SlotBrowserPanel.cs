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
    /// Every piece that could go in one slot of the previewed outfit (a worn piece's place, or a layer), as a table like
    /// vanilla's pawn tables: the pawn's rank, then every stat of the order and one added by hand, sorted by clicking a
    /// header. A row can be tried on the pawn, and its checkbox allows or forbids the item in
    /// the policy. Rows keep their place while boxes are ticked, so the rank numbers and the highlight on the pawn's pick
    /// move instead. What it shows is the session's open <see cref="SlotView"/>.
    /// </summary>
    public static class SlotBrowserPanel
    {
        private const float RankWidth = 28f;
        private const float ValueWidth = 64f;

        /// <summary>The "+2" at a row's end for bonuses that have no column.</summary>
        private const float MoreWidth = 28f;

        /// <summary>On narrow screens, stat columns give way before a piece's name gets shorter than this.</summary>
        private const float MinNameWidth = 120f;

        /// <summary>Two lines of the small font, so stat names wrap instead of being cut off.</summary>
        private const float HeaderHeight = 34f;

        /// <summary>Vanilla's filter: Clear all and Allow all side by side, then the search box under them.</summary>
        private const float AllButtonHeight = 24f;

        private const float SearchGap = 3f;

        /// <summary>The most of the room under the headers a piece's stats take when they go under the list.</summary>
        private const float StatsShare = 0.5f;

        /// <summary>The × on the added column's header, small enough to leave its name readable.</summary>
        private const float RemoveColumnSize = 12f;

        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller shows as none.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>
        /// Draws the session's open slot, with a back arrow when <paramref name="showBack"/> (where the slot takes the
        /// outfit list's place), and with a piece's stats under the list when <paramref name="statsBelow"/> (where they have no
        /// column of their own). A checkbox or an all button that changes the policy's filter tells the session, so it
        /// gathers its pieces again.
        /// </summary>
        public static void Draw(Rect rect, PreviewSession session, bool showBack, bool statsBelow)
        {
            SlotView open = session.Slot;
            OutfitSlot slot = open.Slot;
            Pawn pawn = session.Pawn;
            ApparelPolicy policy = session.Policy;
            IReadOnlyList<Apparel> outfit = session.Outfit.Worn;
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;
            Columns columns = Columns.For(lines, rect.width - GenUI.ScrollBarWidth, session);
            List<SlotChoice> choices = open.Ranked()
                .Where(choice => open.Search.filter.Matches(PieceLabel.Of(choice.Piece)))
                .ToList();
            open.Search.noResultsMatched = choices.Count == 0;
            open.Hovered = null;

            float y = rect.y;
            // Named by what fills the slot now, which changes when the piece it opened on is forbidden or a layer fills.
            BodyDef body = pawn.RaceProps.body;
            List<Apparel> filling = outfit.Where(worn => slot.Holds(worn.def, body)).ToList();
            string header;
            if (slot.Layer == null)
            {
                header = "OL_Slot_Header".Translate(PieceLabel.Of(filling.FirstOrDefault() ?? slot.Piece), choices.Count).Resolve();
            }
            else if (filling.Count > 0)
            {
                string worn = filling.Select(piece => PieceLabel.Of(piece)).ToCommaList(useAnd: true);
                header = "OL_Slot_HeaderLayerWorn".Translate(slot.Layer.LabelCap, worn, choices.Count).Resolve();
            }
            else
            {
                header = "OL_Slot_HeaderLayer".Translate(slot.Layer.LabelCap, choices.Count).Resolve();
            }
            DrawTitle(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), header, showBack, session);
            if (session.Slot == null)
            {
                return;
            }
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;
            DrawAllButtons(new Rect(rect.x, y, rect.width, AllButtonHeight), choices, session);
            y += AllButtonHeight + SearchGap;
            open.Search.OnGUI(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, QuickSearchWidget.WidgetHeight));
            y += QuickSearchWidget.WidgetHeight + SearchGap;
            DrawColumnHeaders(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, HeaderHeight), columns, lines, session);
            y += HeaderHeight;

            // Without a column of their own, the stats of the piece tried on (or the slot's own) go under the list, taking
            // what they need up to their share, so a short sheet leaves the list more room.
            float listBottom = rect.yMax;
            if (statsBelow)
            {
                float statsHeight = Mathf.Min(PieceStatsPanel.HeightFor(session), (rect.yMax - y) * StatsShare);
                Rect stats = new Rect(rect.x, rect.yMax - statsHeight, rect.width, statsHeight);
                using (new TextBlock(Widgets.SeparatorLineColor))
                {
                    Widgets.DrawLineHorizontal(stats.x, stats.y, stats.width);
                }
                PieceStatsPanel.Draw(stats, session);
                listBottom = stats.y - UiStyle.Gap;
            }

            var picked = new HashSet<object>(outfit.Select(SlotView.RowKey));
            Rect list = new Rect(rect.x, y, rect.width, listBottom - y);
            Rect view = new Rect(0f, 0f, list.width - GenUI.ScrollBarWidth, choices.Count * UiStyle.RowHeight);
            // A row half out of view mustn't count as hovered while the mouse is on the headers above it.
            bool mouseInList = Mouse.IsOver(list);
            Widgets.BeginScrollView(list, ref open.Scroll, view);
            try
            {
                float rowY = 0f;
                foreach (SlotChoice choice in choices)
                {
                    // Only rows in view are drawn: a headgear slot can hold a hundred pieces.
                    if (rowY + UiStyle.RowHeight >= open.Scroll.y && rowY <= open.Scroll.y + list.height)
                    {
                        Rect row = new Rect(0f, rowY, view.width, UiStyle.RowHeight);
                        if (mouseInList && Mouse.IsOver(row))
                        {
                            open.Hovered = choice;
                        }
                        DrawRow(row, choice, session, picked.Contains(SlotView.RowKey(choice.Piece)), columns, lines);
                    }
                    rowY += UiStyle.RowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawTitle(Rect row, string header, bool showBack, PreviewSession session)
        {
            float titleX = row.x;
            if (showBack)
            {
                Rect back = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
                titleX = back.xMax + UiStyle.Gap;
                if (Widgets.ButtonImage(back, TexUI.ArrowTexLeft, true, "OL_Slot_Back".Translate()))
                {
                    session.CloseSlot();
                    return;
                }
            }
            Rect title = new Rect(titleX, row.y, row.xMax - titleX, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(title, header.Truncate(title.width));
            }
            TooltipHandler.TipRegion(title, header);
        }

        /// <summary>Clear all and Allow all for every item listed, laid out as vanilla's filter does.</summary>
        private static void DrawAllButtons(Rect row, List<SlotChoice> listed, PreviewSession session)
        {
            ApparelPolicy policy = session.Policy;
            float half = (row.width - SearchGap) / 2f;
            Rect clearAll = new Rect(row.x, row.y, half, row.height);
            Rect allowAll = new Rect(clearAll.xMax + SearchGap, row.y, half, row.height);
            bool changed = false;
            if (Widgets.ButtonText(clearAll, "ClearAll".Translate()))
            {
                changed = SetAllowed(listed, policy, false);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(allowAll, "AllowAll".Translate()))
            {
                changed = SetAllowed(listed, policy, true);
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(clearAll, "OL_Slot_ClearAllTip".Translate());
            TooltipHandler.TipRegion(allowAll, "OL_Slot_AllowAllTip".Translate());
            if (changed)
            {
                session.FilterChanged();
            }
        }

        /// <summary>Allows or forbids every listed item. Locked items stay allowed.</summary>
        private static bool SetAllowed(List<SlotChoice> listed, ApparelPolicy policy, bool allowed)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            bool changed = false;
            foreach (ThingDef def in listed.Select(choice => choice.Piece.def).Distinct())
            {
                if (!allowed && orders != null && orders.IsLocked(policy, def))
                {
                    continue;
                }
                if (policy.filter.Allows(def) != allowed)
                {
                    policy.filter.SetAllow(def, allowed);
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// The headers, laid out like the rows: "#" after the info button, a stat over each value column, and "+" over the
        /// checkboxes to add a column. Clicking a header sorts by it the way vanilla's pawn tables do.
        /// </summary>
        private static void DrawColumnHeaders(Rect row, Columns columns, IReadOnlyList<OrderLine> lines, PreviewSession session)
        {
            RowLayout layout = RowLayout.For(row, columns);
            DrawRankHeader(layout.Rank, session);
            for (int i = 0; i < columns.Shown.Count; i++)
            {
                OrderLine column = columns.Shown[i];
                if (column.IsCoverage)
                {
                    DrawCoverageHeader(layout.Value(i), column);
                    continue;
                }
                DrawStatHeader(layout.Value(i), column.stat, LineFor(lines, column.stat), removable: column.stat == session.AddedStat, session);
            }
            if (session.AddedStat == null && Widgets.ButtonImage(layout.Checkbox, TexButton.Plus, true, "OL_Slot_AddColumn".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickStat(session.ShowColumn, columns.Stats, "OL_Slot_AddColumnTitle".Translate()));
            }
        }

        /// <summary>A coverage priority's header: the body part. It isn't sorted by, since every piece has 1 or 0.</summary>
        private static void DrawCoverageHeader(Rect cell, OrderLine line)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(cell, line.covers.LabelCap);
            }
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, OrderLineText.CoverageOf(line, covers: true));
            }
        }

        private static void DrawRankHeader(Rect cell, PreviewSession session)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(cell, "OL_Slot_RankHeader".Translate());
            }
            if (session.SortStat == null)
            {
                DrawSortIcon(cell, descending: true);
            }
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, "OL_Slot_RankTip".Translate());
            }
            if (Widgets.ButtonInvisible(cell) && session.SortStat != null)
            {
                session.SortStat = null;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
        }

        private static void DrawStatHeader(Rect cell, StatDef stat, OrderLine line, bool removable, PreviewSession session)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, session.SortStat == stat ? Color.white : ColoredText.SubtleGrayColor))
            {
                Widgets.Label(cell, stat.LabelCap);
            }
            if (session.SortStat == stat)
            {
                DrawSortIcon(cell, !session.LowestFirst);
            }
            if (removable)
            {
                Rect remove = new Rect(cell.xMax - RemoveColumnSize, cell.y, RemoveColumnSize, RemoveColumnSize);
                if (Widgets.ButtonImage(remove, TexButton.CloseXSmall, true, "OL_Slot_RemoveColumn".Translate()))
                {
                    session.AddedStat = null;
                    if (session.SortStat == stat)
                    {
                        session.SortStat = null;
                    }
                    return;
                }
            }
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                string description = stat.description.NullOrEmpty() ? "" : "\n\n" + stat.description;
                // A column from a line limited to a layer says so, since pieces off that layer show "–" in it.
                string title = line != null ? OrderLineText.StatOnLayerCap(line) : stat.LabelCap.Resolve();
                TooltipHandler.TipRegion(cell, title + description + "\n\n" + "OL_Slot_SortHint".Translate());
            }
            if (Widgets.ButtonInvisible(cell))
            {
                CycleSort(stat, session);
            }
        }

        /// <summary>Sorts by the column the way vanilla's pawn tables do, with their sounds. Right click runs the other way.</summary>
        internal static void CycleSort(StatDef stat, PreviewSession session)
        {
            session.CycleSort(stat, reverse: Event.current.button == 1);
            (session.SortStat == null ? SoundDefOf.Tick_Low : SoundDefOf.Tick_High).PlayOneShotOnCamera();
        }

        private static void DrawSortIcon(Rect cell, bool descending)
        {
            Texture2D icon = descending ? UiStyle.SortDescendingIcon : UiStyle.SortAscendingIcon;
            GUI.DrawTexture(new Rect(cell.xMax - icon.width - 1f, cell.yMax - icon.height - 1f, icon.width, icon.height), icon);
        }

        /// <summary>Draws one piece.</summary>
        private static void DrawRow(Rect row, SlotChoice choice, PreviewSession session, bool isPick, Columns columns,
            IReadOnlyList<OrderLine> lines)
        {
            Apparel piece = choice.Piece;
            Pawn pawn = session.Pawn;
            ApparelPolicy policy = session.Policy;
            SlotView open = session.Slot;
            if (choice.TakesOffMore)
            {
                Widgets.DrawBoxSolid(row, UiStyle.TakesOffMoreColor);
            }
            if (piece == open.TriedOn)
            {
                // An outline, so the row's amber still shows while it's tried on.
                Widgets.DrawBox(row, UiStyle.TriedOnOutline);
            }
            else if (isPick)
            {
                Widgets.DrawHighlight(row);
            }

            // Laid out like vanilla's filter: the info card button first, the checkbox at the far end.
            RowLayout layout = RowLayout.For(row, columns);
            Widgets.InfoCardButton(layout.Info.x, layout.Info.y, piece);
            ItemControls.DrawAllowed(layout.Checkbox, piece.def, policy, session);
            ItemControls.DrawLock(layout.Lock, piece.def, pawn, policy, choice.CantWearReason);

            Rect body = layout.Body;
            if (UiStyle.ClickableRow(body))
            {
                open.TryOn(piece);
            }
            // Imagined pieces may share an ID, so each row's place in the list keeps its tooltip apart.
            bool triedOn = piece == open.TriedOn;
            TooltipHandler.TipRegion(body, () => Tooltip(choice, isPick, triedOn, lines, policy), Gen.HashCombineInt(open.Slot.GetHashCode(), (int)row.y));

            bool dimmed = !choice.Pickable;
            Color textColor = dimmed ? ColoredText.SubtleGrayColor : Color.white;
            using (new TextBlock(TextAnchor.MiddleCenter, textColor))
            {
                Widgets.Label(layout.Rank, choice.PickRank > 0 ? choice.PickRank.ToString() : "OL_Slot_NoValue".Translate().ToString());
                for (int i = 0; i < columns.Shown.Count; i++)
                {
                    if (columns.Shown[i].IsCoverage)
                    {
                        bool covers = ApparelComparison.ValueOnLine(piece, columns.Shown[i]) > 0f;
                        Widgets.Label(layout.Value(i), covers ? "OL_Slot_Covers".Translate() : "OL_Slot_NoValue".Translate());
                        continue;
                    }
                    StatDef stat = columns.Shown[i].stat;
                    OrderLine line = LineFor(lines, stat);
                    bool counts = line == null || line.CountsOn(piece.def);
                    // A value under what the line counts from is grey, since it counts as none.
                    float value = PieceStats.Value(piece, stat);
                    bool underThreshold = line != null && Math.Abs(value) >= Epsilon && Math.Abs(line.Counted(value)) < Epsilon;
                    // One line, so a long unit (CE's "mm RHA") doesn't wrap and lift the value.
                    using (new TextBlock(null, null, false, underThreshold ? ColoredText.SubtleGrayColor : textColor))
                    {
                        Rect cell = layout.Value(i);
                        Widgets.Label(cell, (counts ? ValueText(piece, stat) : "OL_Slot_NoValue".Translate().Resolve()).Truncate(cell.width));
                    }
                }
                if (columns.Hidden.Count > 0)
                {
                    // Every piece has a value on a body part line, 1 or 0, so only stat lines are counted.
                    int more = columns.Hidden.Count(line => line.IsStat && Math.Abs(ApparelComparison.ValueOnLine(piece, line)) >= Epsilon);
                    if (more > 0)
                    {
                        Widgets.Label(layout.More, "OL_Slot_MorePriorities".Translate(more));
                    }
                }
            }
            Widgets.ThingIcon(layout.Icon, piece, dimmed ? 0.5f : 1f);
            string name = choice.Copies > 1 ? "OL_Slot_Copies".Translate(PieceLabel.Of(piece), choice.Copies).ToString() : PieceLabel.Of(piece);
            using (new TextBlock(TextAnchor.MiddleLeft, textColor))
            {
                Widgets.Label(layout.Name, name.Truncate(layout.Name.width));
            }
        }

        /// <summary>
        /// A row's tooltip in groups: its name, then why it is or isn't picked, then its value on each priority and what
        /// else it gives, then what else it takes off, then how to try it on.
        /// </summary>
        private static string Tooltip(SlotChoice choice, bool isPick, bool triedOn, IReadOnlyList<OrderLine> lines, ApparelPolicy policy)
        {
            var groups = new List<string>
            {
                PieceLabel.Of(choice.Piece),
                Joined(Status(choice, isPick, lines, policy)),
                Joined(Priorities(choice, lines)),
                Joined(TakesOff(choice)),
                (triedOn ? "OL_Slot_ClickToGoBack" : "OL_Slot_ClickToTry").Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
            };
            return string.Join("\n\n", groups.Where(group => !group.NullOrEmpty()));
        }

        private static string Joined(List<string> lines)
        {
            return string.Join("\n", lines);
        }

        /// <summary>Whether it's the pick, locked, or kept from being put on, and why.</summary>
        private static List<string> Status(SlotChoice choice, bool isPick, IReadOnlyList<OrderLine> lines, ApparelPolicy policy)
        {
            var status = new List<string>();
            if (isPick)
            {
                status.Add("OL_Slot_IsPick".Translate());
                // A pick with nothing on any priority can only have won on the game's own score.
                if (!lines.Any(line => !line.IsItem))
                {
                    status.Add("OL_Preview_WhyNoOrder".Translate());
                }
                else if (!HasValueOnAnyLine(choice, lines) && !(ApparelOrdersComponent.Current?.IsLocked(policy, choice.Piece.def) ?? false))
                {
                    status.Add("OL_Preview_WhyTie".Translate());
                }
            }
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders != null && orders.IsLocked(policy, choice.Piece.def))
            {
                status.Add("OL_Slot_IsLocked".Translate());
            }
            if (!choice.TypeAllowed)
            {
                ApparelForbid forbiddenBy = ApparelForbids.ForbiddenBy(policy, choice.Piece.def);
                status.Add(forbiddenBy != null
                    ? "OL_Slot_RuledOut".Translate(OrderLineText.ForbidCap(forbiddenBy).UncapitalizeFirst()).Resolve()
                    : "OL_Preview_NotAllowed".Translate().Resolve());
            }
            else if (!choice.Allowed)
            {
                status.Add("OL_Slot_PieceNotAllowed".Translate());
            }
            if (!choice.Reachable)
            {
                status.Add("OL_Slot_CantReach".Translate());
            }
            if (choice.GameWontWear)
            {
                status.Add(choice.CantWearReason ?? choice.SkippedReason ?? "OL_Slot_WontWearLowScore".Translate().Resolve());
            }
            return status;
        }

        private static bool HasValueOnAnyLine(SlotChoice choice, IReadOnlyList<OrderLine> lines)
        {
            return lines.Any(line => !line.IsItem && line.CountsOn(choice.Piece.def)
                && Math.Abs(ApparelComparison.ValueOnLine(choice.Piece, line)) >= Epsilon);
        }

        /// <summary>Its value on each priority, one to a line, then what else it gives.</summary>
        private static List<string> Priorities(SlotChoice choice, IReadOnlyList<OrderLine> lines)
        {
            var values = new List<string>();
            var shown = new HashSet<StatDef>();
            foreach (OrderLine line in lines.Where(line => !line.IsItem))
            {
                // A line limited to a layer the piece isn't on doesn't count it, and one counting from more than the
                // piece has counts it as none, so the piece has nothing for those lines.
                if (line.IsCoverage)
                {
                    // Said by what the piece does. A piece off the line's layers isn't judged by it.
                    if (line.CountsOn(choice.Piece.def))
                    {
                        bool covers = ApparelComparison.ValueOnLine(choice.Piece, line) > 0f;
                        string coverage = OrderLineText.CoverageOf(line, covers);
                        values.Add(covers ? coverage : coverage.Colorize(ColoredText.SubtleGrayColor));
                    }
                }
                else if (Math.Abs(ApparelComparison.ValueOnLine(choice.Piece, line)) >= Epsilon)
                {
                    values.Add("OL_Slot_TipValue".Translate(OrderLineText.StatOnLayerCap(line), ValueText(choice.Piece, line.stat)).Resolve());
                }
                else
                {
                    values.Add("OL_Slot_TipValue".Translate(OrderLineText.StatOnLayerCap(line), "OL_Slot_None".Translate()).Resolve()
                        .Colorize(ColoredText.SubtleGrayColor));
                }
                if (line.IsStat && line.CountsOn(choice.Piece.def))
                {
                    // A bonus under the line's threshold isn't listed again under "Also gives".
                    shown.Add(line.stat);
                }
            }
            List<(StatDef stat, float value)> alsoGives = PieceBonuses.Of(choice.Piece, lines).Where(bonus => !shown.Contains(bonus.stat)).ToList();
            if (alsoGives.Count > 0)
            {
                values.Add("OL_Slot_AlsoGives".Translate() + "\n" + PieceBonusText.Lines(alsoGives));
            }
            return values;
        }

        /// <summary>What else putting it on takes off, and the partners it's ranked with.</summary>
        private static List<string> TakesOff(SlotChoice choice)
        {
            var effects = new List<string>();
            if (choice.AlsoReplaces.Count > 0)
            {
                effects.Add("OL_Slot_AlsoReplaces".Translate(choice.AlsoReplaces.Select(worn => PieceLabel.Of(worn).UncapitalizeFirst()).ToCommaList(useAnd: true)));
            }
            if (choice.Partners.Count > 0)
            {
                effects.Add("OL_Slot_WithPartners".Translate(choice.Partners.Select(partner => partner.LabelNoParenthesis).ToCommaList(useAnd: true)));
            }
            return effects;
        }

        /// <summary>The order's line for a column's stat, or null for a column added by hand.</summary>
        private static OrderLine LineFor(IReadOnlyList<OrderLine> lines, StatDef stat)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].stat == stat)
                {
                    return lines[i];
                }
            }
            return null;
        }

        /// <summary>The piece's value on the stat, or a dash when it has none, so real bonuses stand out.</summary>
        private static string ValueText(Apparel piece, StatDef stat)
        {
            float value = PieceStats.Value(piece, stat);
            if (Math.Abs(value) < Epsilon)
            {
                return "OL_Slot_NoValue".Translate();
            }
            ToStringNumberSense sense = StatChoices.IsWornPieceStat(stat) ? ToStringNumberSense.Absolute : ToStringNumberSense.Offset;
            return stat.ValueToString(value, sense);
        }

        /// <summary>Where each cell of a row (or the header row) goes, so headers line up with their values.</summary>
        private readonly struct RowLayout
        {
            public Rect Info { get; }
            public Rect Rank { get; }
            public Rect Icon { get; }
            public Rect Name { get; }

            /// <summary>After the stat columns, the "+2" for priorities with no column. Zero wide when none are hidden.</summary>
            public Rect More { get; }

            public Rect Checkbox { get; }
            public Rect Lock { get; }

            /// <summary>Everything from the rank to the lock, which a click tries on.</summary>
            public Rect Body { get; }

            private readonly float valuesX;
            private readonly float y;
            private readonly float height;

            private RowLayout(Rect row, Columns columns)
            {
                y = row.y;
                height = row.height;
                float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
                Info = new Rect(row.xMax - Widgets.InfoCardButtonSize, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f,
                    Widgets.InfoCardButtonSize, Widgets.InfoCardButtonSize);
                Checkbox = new Rect(Info.x - UiStyle.Gap / 2f - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Lock = new Rect(Checkbox.x - UiStyle.IconGap - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Rank = new Rect(row.x, row.y, RankWidth, row.height);
                Icon = new Rect(Rank.xMax + UiStyle.Gap / 2f, iconY, UiStyle.IconSize, UiStyle.IconSize);
                float moreWidth = columns.Hidden.Count > 0 ? MoreWidth : 0f;
                More = new Rect(Lock.x - UiStyle.Gap - moreWidth, row.y, moreWidth, row.height);
                valuesX = More.x - columns.Shown.Count * ValueWidth;
                Name = new Rect(Icon.xMax + UiStyle.Gap, row.y, valuesX - Icon.xMax - 2f * UiStyle.Gap, row.height);
                Body = new Rect(Rank.x, row.y, Lock.x - UiStyle.Gap - Rank.x, row.height);
            }

            /// <summary>Everything in a row but its name, value columns and "+2".</summary>
            public static float FixedWidth => Widgets.InfoCardButtonSize + RankWidth + 3f * UiStyle.IconSize + UiStyle.IconGap + 4f * UiStyle.Gap;

            public static RowLayout For(Rect row, Columns columns)
            {
                return new RowLayout(row, columns);
            }

            public Rect Value(int column)
            {
                return new Rect(valuesX + column * ValueWidth, y, ValueWidth, height);
            }
        }

        /// <summary>
        /// The columns: every stat and coverage line of the order, a base's included (item lines aren't columns), then the
        /// stat added by hand, as many as fit beside a readable name.
        /// </summary>
        private sealed class Columns
        {
            private Columns(List<OrderLine> shown, List<OrderLine> hidden)
            {
                Shown = shown;
                Hidden = hidden;
            }

            /// <summary>The lines shown as columns. The stat added by hand is a line of its own, not one of the order's.</summary>
            public List<OrderLine> Shown { get; }

            /// <summary>The stats shown as columns, which the "+" picker leaves out.</summary>
            public List<StatDef> Stats => Shown.Where(line => line.IsStat).Select(line => line.stat).ToList();

            /// <summary>The order's lines that didn't fit, so a row says how many of them the piece has a value on.</summary>
            public List<OrderLine> Hidden { get; }

            /// <summary>
            /// The columns that fit in <paramref name="width"/>. The added one keeps its room, and the order's last lines
            /// give way first. A stat added by hand that has since joined the order is dropped, so "+" comes back.
            /// </summary>
            public static Columns For(IReadOnlyList<OrderLine> lines, float width, PreviewSession session)
            {
                List<OrderLine> statLines = lines.Where(line => !line.IsItem).ToList();
                if (statLines.Any(line => line.stat == session.AddedStat))
                {
                    session.AddedStat = null;
                }
                float room = width - RowLayout.FixedWidth - MinNameWidth;
                int capacity = Mathf.Max(0, Mathf.FloorToInt(room / ValueWidth));
                int added = session.AddedStat != null && capacity > 0 ? 1 : 0;
                int fit = capacity - added;
                if (statLines.Count > fit)
                {
                    // Room for "+2" at the row's end, when there's room for it at all.
                    fit = Mathf.Max(0, Mathf.FloorToInt((room - MoreWidth) / ValueWidth) - added);
                }
                List<OrderLine> shown = statLines.Take(fit).ToList();
                if (added > 0)
                {
                    shown.Add(new OrderLine(session.AddedStat));
                }
                bool roomForMore = room - (fit + added) * ValueWidth >= MoreWidth;
                List<OrderLine> hidden = roomForMore ? statLines.Skip(fit).ToList() : new List<OrderLine>();
                return new Columns(shown, hidden);
            }
        }
    }
}
