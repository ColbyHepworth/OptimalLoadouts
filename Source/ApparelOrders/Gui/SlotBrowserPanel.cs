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
    /// vanilla's pawn tables:
    /// the pawn's rank, then the order's stats and one added by hand, sorted by clicking a header. A row can be tried on
    /// the pawn, and its checkbox allows or forbids the item in the policy. Rows keep their place while boxes are ticked,
    /// so the rank numbers and the highlight on the pawn's pick move instead.
    /// </summary>
    public static class SlotBrowserPanel
    {
        private const float RankWidth = 28f;
        private const float ValueWidth = 90f;

        /// <summary>On narrow screens, stat columns give way before a piece's name gets shorter than this.</summary>
        private const float MinNameWidth = 120f;

        /// <summary>Two lines of the small font, so stat names wrap instead of being cut off.</summary>
        private const float HeaderHeight = 34f;

        /// <summary>Vanilla's filter: Clear all and Allow all side by side, then the search box under them.</summary>
        private const float AllButtonHeight = 24f;

        private const float SearchGap = 3f;
        private const int OrderColumns = 2;

        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller shows as none.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        private static OutfitSlot slot;
        private static Vector2 scroll;
        private static readonly QuickSearchWidget search = new QuickSearchWidget();

        /// <summary>The column sorted by, or null to sort the way the pawn would pick.</summary>
        private static StatDef sortStat;

        private static bool lowestFirst;

        /// <summary>A stat shown as an extra column, kept across slots until it's removed.</summary>
        private static StatDef addedStat;

        private static List<SlotChoice> ranked = new List<SlotChoice>();
        private static (Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, List<Apparel> pieces, OutfitSlot slot,
            StatDef sortStat, bool lowestFirst, int version) rankedFor;

        /// <summary>The rows' order, set when the slot opens or the sort changes, and kept while boxes are ticked.</summary>
        private static List<object> rowOrder = new List<object>();
        private static (OutfitSlot slot, StatDef sortStat, bool lowestFirst) rowOrderFor;

        public static bool IsOpen => slot != null;

        /// <summary>The open slot, or null.</summary>
        public static OutfitSlot Slot => slot;

        /// <summary>The piece drawn on the pawn in place of the slot's, or null to draw the outfit as predicted.</summary>
        public static Apparel TriedOn { get; private set; }

        public static void Open(OutfitSlot opened)
        {
            slot = opened;
            TriedOn = null;
            scroll = Vector2.zero;
            search.Reset();
            rankedFor = default;
            rowOrder = new List<object>();
            rowOrderFor = default;
        }

        public static void Close()
        {
            slot = null;
            TriedOn = null;
            ranked = new List<SlotChoice>();
            rankedFor = default;
            rowOrder = new List<object>();
            rowOrderFor = default;
        }

        /// <summary>
        /// Draws the slot's pieces from <paramref name="pieces"/> (allowed or not), with a back arrow when
        /// <paramref name="showBack"/> (where the slot takes the outfit list's place). Returns true when a checkbox or an
        /// all button changed the policy's filter, so the preview gathers its pieces again.
        /// </summary>
        public static bool Draw(Rect rect, Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, List<Apparel> pieces, bool showBack)
        {
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;
            List<StatDef> columns = Columns(lines, rect.width - GenUI.ScrollBarWidth);
            if (sortStat != null && !columns.Contains(sortStat))
            {
                sortStat = null;
            }
            List<SlotChoice> choices = RankedFor(pawn, policy, outfit, pieces)
                .Where(choice => search.filter.Matches(PieceLabel.Of(choice.Piece)))
                .ToList();
            search.noResultsMatched = choices.Count == 0;

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
            DrawTitle(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), header, showBack);
            if (!IsOpen)
            {
                return false;
            }
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;
            bool filterChanged = DrawAllButtons(new Rect(rect.x, y, rect.width, AllButtonHeight), choices, policy);
            y += AllButtonHeight + SearchGap;
            search.OnGUI(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, QuickSearchWidget.WidgetHeight));
            y += QuickSearchWidget.WidgetHeight + SearchGap;
            DrawColumnHeaders(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, HeaderHeight), columns, lines);
            y += HeaderHeight;

            var picked = new HashSet<object>(outfit.Select(RowKey));
            Rect list = new Rect(rect.x, y, rect.width, rect.yMax - y);
            Rect view = new Rect(0f, 0f, list.width - GenUI.ScrollBarWidth, choices.Count * UiStyle.RowHeight);
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                float rowY = 0f;
                foreach (SlotChoice choice in choices)
                {
                    // Only rows in view are drawn: a headgear slot can hold a hundred pieces.
                    if (rowY + UiStyle.RowHeight >= scroll.y && rowY <= scroll.y + list.height)
                    {
                        Rect row = new Rect(0f, rowY, view.width, UiStyle.RowHeight);
                        filterChanged |= DrawRow(row, choice, pawn, policy, picked.Contains(RowKey(choice.Piece)), columns, lines);
                    }
                    rowY += UiStyle.RowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return filterChanged;
        }

        /// <summary>
        /// The order's first stats (item lines aren't columns), then the stat added by hand when it isn't one of them, as
        /// many as fit beside a readable name. A stat added by hand that has since joined the order is dropped, so "+"
        /// comes back.
        /// </summary>
        private static List<StatDef> Columns(IReadOnlyList<OrderLine> lines, float width)
        {
            List<StatDef> columns = lines.Where(line => !line.IsItem).Take(OrderColumns).Select(line => line.stat).ToList();
            if (columns.Contains(addedStat))
            {
                addedStat = null;
            }
            if (addedStat != null)
            {
                columns.Add(addedStat);
            }
            int fit = Mathf.Max(0, Mathf.FloorToInt((width - RowLayout.FixedWidth - MinNameWidth) / ValueWidth));
            return columns.Take(fit).ToList();
        }

        /// <summary>The ranked pieces, in the rows' kept order: pieces already listed keep their place, new ones join at the end.</summary>
        private static List<SlotChoice> RankedFor(Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, List<Apparel> pieces)
        {
            int version = ApparelOrdersComponent.Current?.Version ?? 0;
            var key = (pawn, policy, outfit, pieces, slot, sortStat, lowestFirst, version);
            if (key == rankedFor)
            {
                return ranked;
            }
            rankedFor = key;
            List<SlotChoice> fresh;
            try
            {
                fresh = SlotRanking.Rank(pawn, policy, outfit, slot, pieces, sortStat, lowestFirst);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't rank a slot's pieces for the preview: " + e, "OptimalLoadouts.RankSlot".GetHashCode());
                fresh = new List<SlotChoice>();
            }
            if (rowOrderFor != (slot, sortStat, lowestFirst))
            {
                rowOrderFor = (slot, sortStat, lowestFirst);
                rowOrder = new List<object>();
            }
            var place = new Dictionary<object, int>();
            for (int i = 0; i < rowOrder.Count; i++)
            {
                place[rowOrder[i]] = i;
            }
            ranked = fresh
                .Select((choice, rank) => (choice, at: place.TryGetValue(RowKey(choice.Piece), out int kept) ? kept : rowOrder.Count + rank))
                .OrderBy(entry => entry.at)
                .Select(entry => entry.choice)
                .ToList();
            rowOrder = ranked.Select(choice => RowKey(choice.Piece)).ToList();
            return ranked;
        }

        /// <summary>
        /// What makes a row the same row from one gathering to the next. Imagined pieces stand for their item type (the
        /// material can change when it's allowed or forbidden). Real pieces stand for their item, material, quality and
        /// taint, as identical stored copies share a row.
        /// </summary>
        private static object RowKey(Apparel piece)
        {
            if (piece.MapHeld == null)
            {
                return piece.def;
            }
            int quality = piece.TryGetQuality(out QualityCategory category) ? (int)category : -1;
            return (piece.def, piece.Stuff, quality, piece.WornByCorpse);
        }

        private static void DrawTitle(Rect row, string header, bool showBack)
        {
            float titleX = row.x;
            if (showBack)
            {
                Rect back = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
                titleX = back.xMax + UiStyle.Gap;
                if (Widgets.ButtonImage(back, TexUI.ArrowTexLeft, true, "OL_Slot_Back".Translate()))
                {
                    Close();
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

        /// <summary>Clear all and Allow all for every item listed, laid out as vanilla's filter does. Returns true when the filter changed.</summary>
        private static bool DrawAllButtons(Rect row, List<SlotChoice> listed, ApparelPolicy policy)
        {
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
            return changed;
        }

        private static bool SetAllowed(List<SlotChoice> listed, ApparelPolicy policy, bool allowed)
        {
            bool changed = false;
            foreach (ThingDef def in listed.Select(choice => choice.Piece.def).Distinct())
            {
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
        private static void DrawColumnHeaders(Rect row, List<StatDef> columns, IReadOnlyList<OrderLine> lines)
        {
            RowLayout layout = RowLayout.For(row, columns.Count);
            DrawRankHeader(layout.Rank);
            for (int i = 0; i < columns.Count; i++)
            {
                DrawStatHeader(layout.Value(i), columns[i], LineFor(lines, columns[i]), removable: columns[i] == addedStat);
            }
            if (addedStat == null && Widgets.ButtonImage(layout.Checkbox, TexButton.Plus, true, "OL_Slot_AddColumn".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickStat(stat =>
                {
                    addedStat = stat;
                    sortStat = stat;
                    lowestFirst = false;
                }, columns, "OL_Slot_AddColumnTitle".Translate()));
            }
        }

        private static void DrawRankHeader(Rect cell)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(cell, "OL_Slot_RankHeader".Translate());
            }
            if (sortStat == null)
            {
                DrawSortIcon(cell, descending: true);
            }
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, "OL_Slot_RankTip".Translate());
            }
            if (Widgets.ButtonInvisible(cell) && sortStat != null)
            {
                sortStat = null;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
        }

        private static void DrawStatHeader(Rect cell, StatDef stat, OrderLine line, bool removable)
        {
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, sortStat == stat ? Color.white : ColoredText.SubtleGrayColor))
            {
                Widgets.Label(cell, stat.LabelCap);
            }
            if (sortStat == stat)
            {
                DrawSortIcon(cell, !lowestFirst);
            }
            if (removable)
            {
                Rect remove = new Rect(cell.xMax - TexButton.CloseXSmall.width, cell.y, TexButton.CloseXSmall.width, TexButton.CloseXSmall.height);
                if (Widgets.ButtonImage(remove, TexButton.CloseXSmall, true, "OL_Slot_RemoveColumn".Translate()))
                {
                    addedStat = null;
                    if (sortStat == stat)
                    {
                        sortStat = null;
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
                CycleSort(stat);
            }
        }

        /// <summary>
        /// Vanilla's pawn table cycle: left click sorts highest first, then lowest first, then back to the pawn's pick.
        /// Right click runs the other way.
        /// </summary>
        private static void CycleSort(StatDef stat)
        {
            bool rightClick = Event.current.button == 1;
            if (sortStat != stat)
            {
                sortStat = stat;
                lowestFirst = rightClick;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            else if (lowestFirst != rightClick)
            {
                sortStat = null;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            else
            {
                lowestFirst = !lowestFirst;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
        }

        private static void DrawSortIcon(Rect cell, bool descending)
        {
            Texture2D icon = descending ? UiStyle.SortDescendingIcon : UiStyle.SortAscendingIcon;
            GUI.DrawTexture(new Rect(cell.xMax - icon.width - 1f, cell.yMax - icon.height - 1f, icon.width, icon.height), icon);
        }

        /// <summary>Draws one piece. Returns true when its checkbox changed the policy's filter.</summary>
        private static bool DrawRow(Rect row, SlotChoice choice, Pawn pawn, ApparelPolicy policy, bool isPick, List<StatDef> columns,
            IReadOnlyList<OrderLine> lines)
        {
            Apparel piece = choice.Piece;
            if (piece == TriedOn)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (isPick)
            {
                Widgets.DrawHighlight(row);
            }

            // Laid out like vanilla's filter: the info card button first, the checkbox at the far end.
            RowLayout layout = RowLayout.For(row, columns.Count);
            Widgets.InfoCardButton(layout.Info.x, layout.Info.y, piece);
            bool allowed = choice.TypeAllowed;
            Widgets.Checkbox(layout.Checkbox.x, layout.Checkbox.y, ref allowed, UiStyle.IconSize);
            bool filterChanged = allowed != choice.TypeAllowed;
            if (filterChanged)
            {
                policy.filter.SetAllow(piece.def, allowed);
            }
            DrawPin(layout.Pin, choice, pawn, policy);

            Rect body = layout.Body;
            if (UiStyle.ClickableRow(body))
            {
                TriedOn = piece == TriedOn ? null : piece;
            }
            // Imagined pieces may share an ID, so each row's place in the list keeps its tooltip apart.
            TooltipHandler.TipRegion(body, () => Tooltip(choice, isPick, lines, policy), Gen.HashCombineInt(slot.GetHashCode(), (int)row.y));

            bool dimmed = !choice.Pickable;
            using (new TextBlock(TextAnchor.MiddleCenter, dimmed ? ColoredText.SubtleGrayColor : Color.white))
            {
                Widgets.Label(layout.Rank, choice.PickRank > 0 ? choice.PickRank.ToString() : "OL_Slot_NoValue".Translate().ToString());
                for (int i = 0; i < columns.Count; i++)
                {
                    OrderLine line = LineFor(lines, columns[i]);
                    bool counts = line == null || line.CountsOn(piece.def);
                    Widgets.Label(layout.Value(i), counts ? ValueText(piece, columns[i]) : "OL_Slot_NoValue".Translate().Resolve());
                }
            }
            Widgets.ThingIcon(layout.Icon, piece, dimmed ? 0.5f : 1f);
            string name = choice.Copies > 1 ? "OL_Slot_Copies".Translate(PieceLabel.Of(piece), choice.Copies).ToString() : PieceLabel.Of(piece);
            using (new TextBlock(TextAnchor.MiddleLeft, dimmed ? ColoredText.SubtleGrayColor : Color.white))
            {
                Widgets.Label(layout.Name, name.Truncate(layout.Name.width));
            }
            return filterChanged;
        }

        private static string Tooltip(SlotChoice choice, bool isPick, IReadOnlyList<OrderLine> lines, ApparelPolicy policy)
        {
            var parts = new List<string> { choice.Piece.LabelCap };
            if (isPick)
            {
                parts.Add("OL_Slot_IsPick".Translate());
            }
            foreach (OrderLine line in lines.Where(line => !line.IsItem))
            {
                // A line limited to a layer the piece isn't on doesn't count it, so it shows no value there.
                string value = line.CountsOn(choice.Piece.def) ? ValueText(choice.Piece, line.stat) : "OL_Slot_NoValue".Translate().Resolve();
                parts.Add("OL_Slot_TipValue".Translate(OrderLineText.StatOnLayerCap(line), value));
            }
            if (choice.AlsoReplaces.Count > 0)
            {
                parts.Add("OL_Slot_AlsoReplaces".Translate(string.Join(", ", choice.AlsoReplaces.Select(worn => worn.LabelCap.ToString()))));
            }
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders != null && orders.Prefers(policy, choice.Piece.def))
            {
                parts.Add(choice.TypeAllowed ? "OL_Slot_IsPinned".Translate().Resolve() : "OL_Slot_PinnedNotAllowed".Translate().Resolve());
            }
            if (!choice.TypeAllowed)
            {
                ApparelForbid forbiddenBy = ApparelForbids.ForbiddenBy(policy, choice.Piece.def);
                parts.Add(forbiddenBy != null
                    ? "OL_Forbid_SlotTip".Translate(OrderLineText.ForbidCap(forbiddenBy).UncapitalizeFirst()).Resolve()
                    : "OL_Preview_NotAllowed".Translate().Resolve());
            }
            else if (!choice.Allowed)
            {
                parts.Add("OL_Slot_PieceNotAllowed".Translate());
            }
            if (!choice.Reachable)
            {
                parts.Add("OL_Slot_CantReach".Translate());
            }
            if (choice.GameWontWear)
            {
                parts.Add(choice.CantWearReason ?? "OL_Slot_WontWearLowScore".Translate().Resolve());
            }
            parts.Add("OL_Slot_ClickToTry".Translate());
            return string.Join("\n", parts);
        }

        /// <summary>
        /// The pin that makes the item a preferred item in the policy's priorities. Filled when pinned. A pin from the base
        /// is greyed, since it sits under this policy's own lines; a click pins the item here too, above them. Pinning a piece
        /// the game never puts on this pawn says why, since the pin can't change that.
        /// </summary>
        private static void DrawPin(Rect rect, SlotChoice choice, Pawn pawn, ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null)
            {
                return;
            }
            ThingDef item = choice.Piece.def;
            bool pinned = orders.Prefers(policy, item);
            bool own = orders.OrderFor(policy)?.lines.Any(line => line.item == item) ?? false;
            if (pinned && !own)
            {
                TooltipHandler.TipRegion(rect, "OL_Slot_PinnedInBase".Translate());
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    if (Widgets.ButtonImage(rect, UiStyle.PinIcon, GUI.color))
                    {
                        Pin(orders, choice, pawn, policy);
                    }
                }
                return;
            }
            TooltipHandler.TipRegion(rect, pinned ? "OL_Slot_Unpin".Translate() : "OL_Slot_Pin".Translate());
            if (Widgets.ButtonImage(rect, pinned ? UiStyle.PinIcon : UiStyle.PinOutlineIcon))
            {
                if (pinned)
                {
                    orders.StopPreferring(policy, item);
                }
                else
                {
                    Pin(orders, choice, pawn, policy);
                }
            }
        }

        private static void Pin(ApparelOrdersComponent orders, SlotChoice choice, Pawn pawn, ApparelPolicy policy)
        {
            orders.PreferItem(policy, choice.Piece.def);
            if (choice.CantWearReason != null)
            {
                Messages.Message("OL_Slot_PinWontWear".Translate(choice.CantWearReason, pawn.LabelShort), MessageTypeDefOf.RejectInput,
                    historical: false);
            }
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
            public Rect Checkbox { get; }
            public Rect Pin { get; }

            /// <summary>Everything between the info button and the pin, which a click tries on.</summary>
            public Rect Body { get; }

            private readonly float valuesX;
            private readonly float y;
            private readonly float height;

            private RowLayout(Rect row, int columnCount)
            {
                y = row.y;
                height = row.height;
                float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
                Info = new Rect(row.x, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f, Widgets.InfoCardButtonSize,
                    Widgets.InfoCardButtonSize);
                Checkbox = new Rect(row.xMax - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Pin = new Rect(Checkbox.x - UiStyle.IconGap - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Rank = new Rect(Info.xMax + UiStyle.Gap / 2f, row.y, RankWidth, row.height);
                Icon = new Rect(Rank.xMax + UiStyle.Gap / 2f, iconY, UiStyle.IconSize, UiStyle.IconSize);
                valuesX = Pin.x - UiStyle.Gap - columnCount * ValueWidth;
                Name = new Rect(Icon.xMax + UiStyle.Gap, row.y, valuesX - Icon.xMax - 2f * UiStyle.Gap, row.height);
                Body = new Rect(Rank.x, row.y, Pin.x - UiStyle.Gap - Rank.x, row.height);
            }

            /// <summary>Everything in a row but its name and value columns.</summary>
            public static float FixedWidth => Widgets.InfoCardButtonSize + RankWidth + 3f * UiStyle.IconSize + UiStyle.IconGap + 4f * UiStyle.Gap;

            public static RowLayout For(Rect row, int columnCount)
            {
                return new RowLayout(row, columnCount);
            }

            public Rect Value(int column)
            {
                return new Rect(valuesX + column * ValueWidth, y, ValueWidth, height);
            }
        }
    }
}
