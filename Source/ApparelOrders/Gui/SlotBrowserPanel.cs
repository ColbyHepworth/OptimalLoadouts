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

        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller shows as none.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>
        /// Draws the session's open slot, with a back arrow when <paramref name="showBack"/> (where the slot takes the
        /// outfit list's place). A checkbox or an all button that changes the policy's filter tells the session, so it
        /// gathers its pieces again.
        /// </summary>
        public static void Draw(Rect rect, PreviewSession session, bool showBack)
        {
            SlotView open = session.Slot;
            OutfitSlot slot = open.Slot;
            Pawn pawn = session.Pawn;
            ApparelPolicy policy = session.Policy;
            IReadOnlyList<Apparel> outfit = session.Outfit.Worn;
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? NoLines;
            Columns columns = Columns.For(lines, rect.width - GenUI.ScrollBarWidth, session);
            if (session.SortStat != null && !columns.Stats.Contains(session.SortStat))
            {
                session.SortStat = null;
            }
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

            var picked = new HashSet<object>(outfit.Select(SlotView.RowKey));
            Rect list = new Rect(rect.x, y, rect.width, rect.yMax - y);
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
            for (int i = 0; i < columns.Stats.Count; i++)
            {
                StatDef stat = columns.Stats[i];
                DrawStatHeader(layout.Value(i), stat, LineFor(lines, stat), removable: stat == session.AddedStat, session);
            }
            if (session.AddedStat == null && Widgets.ButtonImage(layout.Checkbox, TexButton.Plus, true, "OL_Slot_AddColumn".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickStat(stat =>
                {
                    session.AddedStat = stat;
                    session.SortStat = stat;
                    session.LowestFirst = false;
                }, columns.Stats, "OL_Slot_AddColumnTitle".Translate()));
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
                Rect remove = new Rect(cell.xMax - TexButton.CloseXSmall.width, cell.y, TexButton.CloseXSmall.width, TexButton.CloseXSmall.height);
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

        /// <summary>
        /// Vanilla's pawn table cycle: left click sorts highest first, then lowest first, then back to the pawn's pick.
        /// Right click runs the other way.
        /// </summary>
        private static void CycleSort(StatDef stat, PreviewSession session)
        {
            bool rightClick = Event.current.button == 1;
            if (session.SortStat != stat)
            {
                session.SortStat = stat;
                session.LowestFirst = rightClick;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            else if (session.LowestFirst != rightClick)
            {
                session.SortStat = null;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            else
            {
                session.LowestFirst = !session.LowestFirst;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
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
            bool locked = ApparelOrdersComponent.Current?.IsLocked(policy, piece.def) ?? false;
            bool allowed = choice.TypeAllowed;
            Widgets.Checkbox(layout.Checkbox.x, layout.Checkbox.y, ref allowed, UiStyle.IconSize, disabled: locked);
            if (locked)
            {
                TooltipHandler.TipRegion(layout.Checkbox, "OL_Slot_LockedCheckbox".Translate());
            }
            if (allowed != choice.TypeAllowed)
            {
                policy.filter.SetAllow(piece.def, allowed);
                session.FilterChanged();
            }
            DrawLock(layout.Lock, choice, pawn, policy);

            Rect body = layout.Body;
            if (UiStyle.ClickableRow(body))
            {
                open.TryOn(piece);
            }
            // Imagined pieces may share an ID, so each row's place in the list keeps its tooltip apart.
            TooltipHandler.TipRegion(body, () => Tooltip(choice, isPick, lines, policy), Gen.HashCombineInt(open.Slot.GetHashCode(), (int)row.y));

            bool dimmed = !choice.Pickable;
            Color textColor = dimmed ? ColoredText.SubtleGrayColor : Color.white;
            using (new TextBlock(TextAnchor.MiddleCenter, textColor))
            {
                Widgets.Label(layout.Rank, choice.PickRank > 0 ? choice.PickRank.ToString() : "OL_Slot_NoValue".Translate().ToString());
                for (int i = 0; i < columns.Stats.Count; i++)
                {
                    StatDef stat = columns.Stats[i];
                    OrderLine line = LineFor(lines, stat);
                    bool counts = line == null || line.CountsOn(piece.def);
                    Widgets.Label(layout.Value(i), counts ? ValueText(piece, stat) : "OL_Slot_NoValue".Translate().Resolve());
                }
                if (columns.Hidden.Count > 0)
                {
                    int more = columns.Hidden.Count(line => line.CountsOn(piece.def) && Math.Abs(PieceStats.Value(piece, line.stat)) >= Epsilon);
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

        private static string Tooltip(SlotChoice choice, bool isPick, IReadOnlyList<OrderLine> lines, ApparelPolicy policy)
        {
            var parts = new List<string> { choice.Piece.LabelCap };
            if (isPick)
            {
                parts.Add("OL_Slot_IsPick".Translate());
            }
            var shown = new HashSet<StatDef>();
            var nothingFor = new List<string>();
            foreach (OrderLine line in lines.Where(line => !line.IsItem))
            {
                // A line limited to a layer the piece isn't on doesn't count it, so it has nothing for that line.
                if (line.CountsOn(choice.Piece.def) && Math.Abs(PieceStats.Value(choice.Piece, line.stat)) >= Epsilon)
                {
                    parts.Add("OL_Slot_TipValue".Translate(OrderLineText.StatOnLayerCap(line), ValueText(choice.Piece, line.stat)));
                    shown.Add(line.stat);
                }
                else
                {
                    nothingFor.Add(OrderLineText.StatOnLayer(line));
                }
            }
            if (nothingFor.Count > 0)
            {
                parts.Add("OL_Slot_NothingFor".Translate(nothingFor.ToCommaList()).Resolve().Colorize(ColoredText.SubtleGrayColor));
            }
            List<(StatDef stat, float value)> alsoGives = PieceBonuses.Of(choice.Piece, lines).Where(bonus => !shown.Contains(bonus.stat)).ToList();
            if (alsoGives.Count > 0)
            {
                parts.Add("OL_Slot_AlsoGives".Translate(PieceBonusText.List(alsoGives)));
            }
            if (choice.AlsoReplaces.Count > 0)
            {
                parts.Add("OL_Slot_AlsoReplaces".Translate(string.Join(", ", choice.AlsoReplaces.Select(worn => worn.LabelCap.ToString()))));
            }
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders != null && orders.IsLocked(policy, choice.Piece.def))
            {
                parts.Add("OL_Slot_IsLocked".Translate());
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
        /// The lock that keeps the item worn and allowed in the policy. Faint when unlocked. A base's lock is greyed and
        /// does nothing here, since it's unlocked in the base. Locking a piece the game never puts on this pawn says why,
        /// since the lock can't change that.
        /// </summary>
        private static void DrawLock(Rect rect, SlotChoice choice, Pawn pawn, ApparelPolicy policy)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null)
            {
                return;
            }
            ThingDef item = choice.Piece.def;
            bool locked = orders.IsLocked(policy, item);
            if (locked && !orders.LocksOwn(policy, item))
            {
                TooltipHandler.TipRegion(rect, "OL_Slot_LockedInBase".Translate(LockingBase(orders, policy, item)?.label ?? ""));
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    GUI.DrawTexture(rect, UiStyle.LockIcon);
                }
                return;
            }
            TooltipHandler.TipRegion(rect, locked ? "OL_Slot_Unlock".Translate() : "OL_Slot_Lock".Translate());
            Color color = locked ? Color.white : new Color(1f, 1f, 1f, UiStyle.UnlockedAlpha);
            if (Widgets.ButtonImage(rect, UiStyle.LockIcon, color))
            {
                if (locked)
                {
                    orders.Unlock(policy, item);
                }
                else
                {
                    orders.Lock(policy, item);
                    if (choice.CantWearReason != null)
                    {
                        Messages.Message("OL_Slot_LockWontWear".Translate(choice.CantWearReason, pawn.LabelShort), MessageTypeDefOf.RejectInput,
                            historical: false);
                    }
                }
            }
        }

        /// <summary>The nearest base whose own lines lock the item, or null.</summary>
        private static ApparelPolicy LockingBase(ApparelOrdersComponent orders, ApparelPolicy policy, ThingDef item)
        {
            var visited = new HashSet<ApparelPolicy> { policy };
            for (ApparelPolicy basePolicy = orders.BaseOf(policy); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                if (orders.LocksOwn(basePolicy, item))
                {
                    return basePolicy;
                }
            }
            return null;
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

            /// <summary>Everything between the info button and the lock, which a click tries on.</summary>
            public Rect Body { get; }

            private readonly float valuesX;
            private readonly float y;
            private readonly float height;

            private RowLayout(Rect row, Columns columns)
            {
                y = row.y;
                height = row.height;
                float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
                Info = new Rect(row.x, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f, Widgets.InfoCardButtonSize,
                    Widgets.InfoCardButtonSize);
                Checkbox = new Rect(row.xMax - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Lock = new Rect(Checkbox.x - UiStyle.IconGap - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Rank = new Rect(Info.xMax + UiStyle.Gap / 2f, row.y, RankWidth, row.height);
                Icon = new Rect(Rank.xMax + UiStyle.Gap / 2f, iconY, UiStyle.IconSize, UiStyle.IconSize);
                float moreWidth = columns.Hidden.Count > 0 ? MoreWidth : 0f;
                More = new Rect(Lock.x - UiStyle.Gap - moreWidth, row.y, moreWidth, row.height);
                valuesX = More.x - columns.Stats.Count * ValueWidth;
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
        /// The stat columns: every stat line of the order, a base's included (item lines aren't columns), then the stat
        /// added by hand, as many as fit beside a readable name.
        /// </summary>
        private sealed class Columns
        {
            private Columns(List<StatDef> stats, List<OrderLine> hidden)
            {
                Stats = stats;
                Hidden = hidden;
            }

            public List<StatDef> Stats { get; }

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
                List<StatDef> stats = statLines.Take(fit).Select(line => line.stat).ToList();
                if (added > 0)
                {
                    stats.Add(session.AddedStat);
                }
                bool roomForMore = room - (fit + added) * ValueWidth >= MoreWidth;
                List<OrderLine> hidden = roomForMore ? statLines.Skip(fit).ToList() : new List<OrderLine>();
                return new Columns(stats, hidden);
            }
        }
    }
}
