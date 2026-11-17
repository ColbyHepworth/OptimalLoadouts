using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders;
using OptimalLoadouts.ApparelOrders.Gui;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OptimalLoadouts.WeaponOrders.Gui
{
    /// <summary>
    /// Every weapon the role could carry from the preview's source, ranked by its weapon priorities, as a table like the
    /// slot browser's: the rank, the weapon, then a column for each priority, sorted by clicking a header. Clicking a row
    /// tries that weapon in the preview.
    /// </summary>
    public static class WeaponBrowserPanel
    {
        private static readonly List<WeaponLine> NoLines = new List<WeaponLine>();

        /// <summary>
        /// Draws the browser, with a back arrow when <paramref name="showBack"/> (where it takes the outfit list's place),
        /// and the shown weapon's numbers under the list when <paramref name="statsBelow"/> (where they have no column of their own).
        /// </summary>
        public static void Draw(Rect rect, PreviewSession session, bool showBack, bool statsBelow)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            IReadOnlyList<WeaponLine> lines = orders?.EffectiveWeaponLines(session.Policy) ?? NoLines;
            WeaponKind kind = orders?.WeaponKindOf(session.Policy) ?? WeaponKind.Any;
            List<WeaponChoice> choices = session.WeaponRows()
                .Where(choice => session.WeaponSearch.filter.Matches(WeaponLabel.Of(choice.Weapon, choice.Ammo)))
                .ToList();
            session.WeaponSearch.noResultsMatched = choices.Count == 0;

            float y = rect.y;
            if (DrawTitle(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), "OL_Weapon_BrowserHeader".Translate(choices.Count), showBack, session))
            {
                return;
            }
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;
            if (kind == WeaponKind.None)
            {
                UiStyle.Note(new Rect(rect.x, y, rect.width, UiStyle.RowHeight * 2f), "OL_Weapon_KindNoneNote".Translate());
                return;
            }
            if (lines.Count == 0)
            {
                UiStyle.Note(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), "OL_Weapon_NoLinesNote".Translate());
                y += UiStyle.RowHeight;
            }
            DrawAllButtons(new Rect(rect.x, y, rect.width, SlotBrowserPanel.AllButtonHeight), choices, session);
            y += SlotBrowserPanel.AllButtonHeight + SlotBrowserPanel.SearchGap;
            session.WeaponSearch.OnGUI(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, QuickSearchWidget.WidgetHeight));
            y += QuickSearchWidget.WidgetHeight + SlotBrowserPanel.SearchGap;
            int shownColumns = ColumnsThatFit(rect.width - GenUI.ScrollBarWidth, lines.Count);
            DrawHeaders(new Rect(rect.x, y, rect.width - GenUI.ScrollBarWidth, SlotBrowserPanel.HeaderHeight), lines, shownColumns, session);
            y += SlotBrowserPanel.HeaderHeight;

            float listBottom = rect.yMax;
            if (statsBelow)
            {
                float statsHeight = (rect.yMax - y) * SlotBrowserPanel.StatsShare;
                Rect stats = new Rect(rect.x, rect.yMax - statsHeight, rect.width, statsHeight);
                using (new TextBlock(Widgets.SeparatorLineColor))
                {
                    Widgets.DrawLineHorizontal(stats.x, stats.y, stats.width);
                }
                WeaponStatsPanel.Draw(stats, session);
                listBottom = stats.y - UiStyle.Gap;
            }
            Rect list = new Rect(rect.x, y, rect.width, listBottom - y);
            Rect view = new Rect(0f, 0f, list.width - GenUI.ScrollBarWidth, choices.Count * UiStyle.RowHeight);
            Widgets.BeginScrollView(list, ref session.WeaponScroll, view);
            try
            {
                float rowY = 0f;
                foreach (WeaponChoice choice in choices)
                {
                    if (rowY + UiStyle.RowHeight >= session.WeaponScroll.y && rowY <= session.WeaponScroll.y + list.height)
                    {
                        DrawRow(new Rect(0f, rowY, view.width, UiStyle.RowHeight), choice, lines, shownColumns, session);
                    }
                    rowY += UiStyle.RowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }


        /// <summary>The title, with the back arrow. True when the arrow closed the browser.</summary>
        private static bool DrawTitle(Rect row, string header, bool showBack, PreviewSession session)
        {
            float titleX = row.x;
            if (showBack)
            {
                Rect back = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
                titleX = back.xMax + UiStyle.Gap;
                if (Widgets.ButtonImage(back, TexUI.ArrowTexLeft, true, "OL_Slot_Back".Translate()))
                {
                    session.CloseWeapons();
                    return true;
                }
            }
            Rect title = new Rect(titleX, row.y, row.xMax - titleX, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(title, header.Truncate(title.width));
            }
            return false;
        }

        private static int ColumnsThatFit(float width, int lineCount)
        {
            float forColumns = width - ControlsWidth - SlotBrowserPanel.RankWidth - UiStyle.IconSize - UiStyle.Gap - SlotBrowserPanel.MinNameWidth;
            return Mathf.Clamp(Mathf.FloorToInt(forColumns / SlotBrowserPanel.ValueWidth), 0, lineCount);
        }

        /// <summary>"#" over the rank, then a number over each value column. Clicking one sorts by it; "#" goes back to the ranking.</summary>
        private static void DrawHeaders(Rect row, IReadOnlyList<WeaponLine> lines, int shownColumns, PreviewSession session)
        {
            Rect rank = new Rect(row.x, row.y, SlotBrowserPanel.RankWidth, row.height);
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(rank, "OL_Slot_RankHeader".Translate());
            }
            if (Mouse.IsOver(rank))
            {
                Widgets.DrawHighlight(rank);
                TooltipHandler.TipRegion(rank, "OL_Weapon_RankTip".Translate());
            }
            if (Widgets.ButtonInvisible(rank) && session.WeaponSortNumber != null)
            {
                session.WeaponSortNumber = null;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            for (int i = 0; i < shownColumns; i++)
            {
                WeaponNumberDef number = lines[i].number;
                Rect cell = ValueCell(row, i, shownColumns);
                bool sorted = session.WeaponSortNumber == number;
                using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, sorted ? Color.white : ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(cell, number.LabelCap);
                }
                if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                    TooltipHandler.TipRegion(cell, number.LabelCap + "\n\n" + number.description + "\n\n" + "OL_Slot_SortHint".Translate());
                }
                if (Widgets.ButtonInvisible(cell))
                {
                    session.CycleWeaponSort(number, reverse: Event.current.button == 1);
                    (session.WeaponSortNumber == null ? SoundDefOf.Tick_Low : SoundDefOf.Tick_High).PlayOneShotOnCamera();
                }
            }
        }

        /// <summary>The allow box and the info card button at a row's right end, as on the slot browser's rows.</summary>
        private const float ControlsWidth = UiStyle.Gap + UiStyle.IconSize + UiStyle.Gap / 2f + Widgets.InfoCardButtonSize;

        /// <summary>The value columns end before the row's controls, the first priority leftmost.</summary>
        private static Rect ValueCell(Rect row, int index, int shownColumns)
        {
            float end = row.xMax - ControlsWidth;
            return new Rect(end - (shownColumns - index) * SlotBrowserPanel.ValueWidth, row.y, SlotBrowserPanel.ValueWidth, row.height);
        }

        /// <summary>Clear all and Allow all for every weapon listed, laid out as the slot browser's.</summary>
        private static void DrawAllButtons(Rect row, List<WeaponChoice> listed, PreviewSession session)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null)
            {
                return;
            }
            float half = (row.width - SlotBrowserPanel.SearchGap) / 2f;
            Rect clearAll = new Rect(row.x, row.y, half, row.height);
            Rect allowAll = new Rect(clearAll.xMax + SlotBrowserPanel.SearchGap, row.y, half, row.height);
            if (Widgets.ButtonText(clearAll, "ClearAll".Translate()))
            {
                SetAllowed(listed, orders, session, false);
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            if (Widgets.ButtonText(allowAll, "AllowAll".Translate()))
            {
                SetAllowed(listed, orders, session, true);
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(clearAll, "OL_Weapon_ClearAllTip".Translate());
            TooltipHandler.TipRegion(allowAll, "OL_Weapon_AllowAllTip".Translate());
        }

        private static void SetAllowed(List<WeaponChoice> listed, ApparelOrdersComponent orders, PreviewSession session, bool allowed)
        {
            foreach (ThingDef def in listed.Select(choice => choice.Weapon.def).Distinct())
            {
                orders.SetWeaponAllowed(session.Policy, def, allowed);
            }
        }

        /// <summary>
        /// One weapon: rank, icon and name, its value on each shown priority, then at the right end a box to allow or forbid
        /// it for the role and its info card, as on the slot browser's rows. The weapon shown in the preview is
        /// highlighted, and a forbidden one is grey.
        /// </summary>
        private static void DrawRow(Rect row, WeaponChoice choice, IReadOnlyList<WeaponLine> lines, int shownColumns, PreviewSession session)
        {
            ThingWithComps weapon = choice.Weapon;
            if (weapon == session.Weapon)
            {
                Widgets.DrawHighlightSelected(row);
            }
            Widgets.DrawHighlightIfMouseover(row);
            Rect info = new Rect(row.xMax - Widgets.InfoCardButtonSize, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f,
                Widgets.InfoCardButtonSize, Widgets.InfoCardButtonSize);
            Widgets.InfoCardButton(info.x, info.y, weapon);
            Rect box = new Rect(info.x - UiStyle.Gap / 2f - UiStyle.IconSize, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            bool allowed = choice.Allowed;
            Widgets.Checkbox(box.x, box.y, ref allowed, UiStyle.IconSize);
            TooltipHandler.TipRegion(box, "OL_Weapon_AllowTip".Translate());
            if (allowed != choice.Allowed)
            {
                ApparelOrdersComponent.Current?.SetWeaponAllowed(session.Policy, weapon.def, allowed);
            }
            Rect rank = new Rect(row.x, row.y, SlotBrowserPanel.RankWidth, row.height);
            using (new TextBlock(TextAnchor.MiddleCenter, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(rank, (choice.Rank + 1).ToString());
            }
            Rect icon = new Rect(rank.xMax, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.ThingIcon(icon, weapon);
            float valuesStart = row.xMax - ControlsWidth - shownColumns * SlotBrowserPanel.ValueWidth;
            Rect name = new Rect(icon.xMax + UiStyle.Gap, row.y, valuesStart - icon.xMax - UiStyle.Gap, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft, choice.Allowed ? Color.white : ColoredText.SubtleGrayColor))
            {
                Widgets.Label(name, WeaponLabel.Of(weapon, choice.Ammo).Truncate(name.width));
            }
            for (int i = 0; i < shownColumns; i++)
            {
                Rect cell = ValueCell(row, i, shownColumns);
                float? value = choice.Values[i];
                using (new TextBlock(TextAnchor.MiddleCenter, value == null ? ColoredText.SubtleGrayColor : Color.white))
                {
                    Widgets.Label(cell, value == null ? "–" : lines[i].number.Format(value.Value));
                }
            }
            // Up to the allow box, so a click on the box or the info card doesn't also try the weapon.
            Rect clickable = new Rect(rank.x, row.y, box.x - UiStyle.Gap / 2f - rank.x, row.height);
            TooltipHandler.TipRegion(clickable, Tooltip(choice, session));
            if (Widgets.ButtonInvisible(clickable))
            {
                session.TriedWeapon = session.TriedWeapon == weapon ? null : weapon;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        /// <summary>The weapon's name, then every number it has, one to a line, then what a click does.</summary>
        private static string Tooltip(WeaponChoice choice, PreviewSession session)
        {
            ThingWithComps weapon = choice.Weapon;
            IEnumerable<string> numbers = DefDatabase<WeaponNumberDef>.AllDefsListForReading
                .Where(number => number.Offered)
                .Select(number => (number, value: WeaponRanking.ValueOf(weapon.def, weapon.Stuff, number, choice.Bullet)))
                .Where(each => each.value != null)
                .Select(each => "OL_Weapon_NumberLine".Translate(each.number.LabelCap, each.number.Format(each.value.Value)).Resolve());
            string click = (session.TriedWeapon == weapon ? "OL_Weapon_ClickToUntry" : "OL_Weapon_ClickToTry").Translate();
            return WeaponLabel.Of(weapon, choice.Ammo) + "\n\n" + string.Join("\n", numbers) + "\n\n" + click;
        }
    }
}
