using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>A searchable list of the stats an order can use, grouped by what kind of stat they are.</summary>
    public class Dialog_PickStat : Window
    {
        private const float RowHeight = 26f;
        private const float HeaderHeight = 30f;

        private readonly Action<StatDef> onPick;
        private readonly HashSet<StatDef> alreadyListed;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;

        /// <param name="title">The window's title, "Add a stat" when null.</param>
        public Dialog_PickStat(Action<StatDef> onPick, IEnumerable<StatDef> alreadyListed, string title = null)
        {
            this.onPick = onPick;
            this.alreadyListed = new HashSet<StatDef>(alreadyListed);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            optionalTitle = title ?? "OL_StatPicker_Title".Translate();
        }

        public override Vector2 InitialSize => new Vector2(420f, 560f);

        public override void PostOpen()
        {
            base.PostOpen();
            search.Focus();
        }

        public override void DoWindowContents(Rect inRect)
        {
            search.OnGUI(new Rect(inRect.x, inRect.y, inRect.width, QuickSearchWidget.WidgetHeight));
            Rect listRect = new Rect(inRect.x, inRect.y + QuickSearchWidget.WidgetHeight + 8f, inRect.width,
                inRect.height - QuickSearchWidget.WidgetHeight - 8f);

            List<StatDef> pieceStats = Matching(StatChoices.WornPieceStats);
            List<StatDef> wearerStats = Matching(StatChoices.WearerBonuses);
            search.noResultsMatched = pieceStats.Count == 0 && wearerStats.Count == 0;

            float viewHeight = GroupHeight(pieceStats) + GroupHeight(wearerStats) + RowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            if (search.noResultsMatched)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, RowHeight), "OL_StatPicker_NoMatches".Translate());
            }
            y = DrawGroup("OL_StatPicker_PieceItself".Translate(), pieceStats, viewRect.width, y);
            DrawGroup("OL_StatPicker_GivesWearer".Translate(), wearerStats, viewRect.width, y);
            Widgets.EndScrollView();
        }

        private List<StatDef> Matching(IReadOnlyList<StatDef> stats)
        {
            var matching = new List<StatDef>();
            foreach (StatDef stat in stats)
            {
                if (!alreadyListed.Contains(stat) && search.filter.Matches(stat.LabelCap))
                {
                    matching.Add(stat);
                }
            }
            return matching;
        }

        private static float GroupHeight(List<StatDef> stats)
        {
            return stats.Count == 0 ? 0f : HeaderHeight + stats.Count * RowHeight;
        }

        private float DrawGroup(string header, List<StatDef> stats, float width, float y)
        {
            if (stats.Count == 0)
            {
                return y;
            }
            Text.Font = GameFont.Small;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 6f, width, HeaderHeight - 6f), header);
            GUI.color = Color.white;
            y += HeaderHeight;
            foreach (StatDef stat in stats)
            {
                Rect row = new Rect(0f, y, width, RowHeight);
                Widgets.DrawHighlightIfMouseover(row);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(row.ContractedBy(6f, 0f), stat.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;
                if (!stat.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(row, stat.description);
                }
                if (Widgets.ButtonInvisible(row))
                {
                    onPick(stat);
                    Close();
                }
                y += RowHeight;
            }
            return y;
        }
    }
}
