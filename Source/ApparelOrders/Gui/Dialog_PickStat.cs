using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// A searchable list of the stats an order can use, folded into the info card's sections. Searching opens every
    /// section with a match.
    /// </summary>
    public class Dialog_PickStat : Window
    {
        private const float RowHeight = 26f;
        private const float HeadingHeight = 30f;
        private const float ArrowSize = 18f;
        private const float Indent = 24f;
        private const float CountWidth = 80f;

        private readonly Action<StatDef> onPick;
        private readonly HashSet<StatDef> alreadyListed;
        private readonly HashSet<StatSection> openSections = new HashSet<StatSection>();
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

        public override Vector2 InitialSize => new Vector2(460f, 600f);

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

            bool searching = search.filter.Active;
            var shown = new List<(StatSection section, List<OfferedStat> stats)>();
            foreach (StatSection section in StatChoices.Sections)
            {
                List<OfferedStat> matching = section.Stats
                    .Where(choice => !alreadyListed.Contains(choice.Stat) && search.filter.Matches(choice.Stat.LabelCap))
                    .ToList();
                if (matching.Count > 0)
                {
                    shown.Add((section, matching));
                }
            }
            search.noResultsMatched = shown.Count == 0;

            float viewHeight = HeadingHeight * 2f;
            foreach ((StatSection section, List<OfferedStat> stats) in shown)
            {
                viewHeight += RowHeight + (IsSectionOpen(section, searching) ? stats.Count * RowHeight : 0f);
            }
            Rect viewRect = new Rect(0f, 0f, listRect.width - UiStyle.ScrollbarWidth, viewHeight);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            if (search.noResultsMatched)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, RowHeight), "OL_StatPicker_NoMatches".Translate());
            }
            bool? headedPiece = null;
            foreach ((StatSection section, List<OfferedStat> stats) in shown)
            {
                if (headedPiece != section.OfPiece)
                {
                    headedPiece = section.OfPiece;
                    string heading = section.OfPiece ? "OL_StatPicker_PieceItself".Translate() : "OL_StatPicker_GivesWearer".Translate();
                    UiStyle.Note(new Rect(0f, y + 6f, viewRect.width, HeadingHeight - 6f), heading);
                    y += HeadingHeight;
                }
                y = DrawSection(section, stats, searching, viewRect.width, y);
            }
            Widgets.EndScrollView();
        }

        private bool IsSectionOpen(StatSection section, bool searching)
        {
            return searching || openSections.Contains(section);
        }

        private float DrawSection(StatSection section, List<OfferedStat> stats, bool searching, float width, float y)
        {
            Rect header = new Rect(0f, y, width, RowHeight);
            bool open = IsSectionOpen(section, searching);
            Widgets.DrawHighlightIfMouseover(header);
            GUI.DrawTexture(new Rect(header.x, header.y + (RowHeight - ArrowSize) / 2f, ArrowSize, ArrowSize),
                open ? TexButton.Collapse : TexButton.Reveal);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(header.x + Indent, header.y, header.width - Indent, header.height),
                    "OL_StatPicker_Section".Translate(section.Category.LabelCap, stats.Count));
            }
            if (!searching && Widgets.ButtonInvisible(header))
            {
                if (open)
                {
                    openSections.Remove(section);
                    SoundDefOf.TabClose.PlayOneShotOnCamera();
                }
                else
                {
                    openSections.Add(section);
                    SoundDefOf.TabOpen.PlayOneShotOnCamera();
                }
            }
            y += RowHeight;
            if (!open)
            {
                return y;
            }
            foreach (OfferedStat choice in stats)
            {
                Rect row = new Rect(Indent, y, width - Indent, RowHeight);
                Rect countRect = new Rect(row.xMax - CountWidth, row.y, CountWidth, row.height);
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(new Rect(row.x + 6f, row.y, row.width - CountWidth - 6f, row.height), choice.Stat.LabelCap);
                }
                using (new TextBlock(TextAnchor.MiddleRight, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(countRect.ContractedBy(6f, 0f), "OL_StatPicker_ItemCount".Translate(choice.ItemCount));
                }
                OfferedStat tipFor = choice;
                bool ofPiece = section.OfPiece;
                TooltipHandler.TipRegion(row, new TipSignal(() => Tooltip(tipFor, ofPiece), choice.Stat.shortHash));
                if (UiStyle.ClickableRow(row))
                {
                    onPick(choice.Stat);
                    Close();
                }
                y += RowHeight;
            }
            return y;
        }

        private static string Tooltip(OfferedStat choice, bool ofPiece)
        {
            var text = new StringBuilder();
            if (!choice.Stat.description.NullOrEmpty())
            {
                text.AppendLine(choice.Stat.description);
                text.AppendLine();
            }
            text.AppendLine(ofPiece
                ? "OL_StatPicker_HadBy".Translate(choice.ItemCount)
                : "OL_StatPicker_ChangedBy".Translate(choice.ItemCount));
            if (choice.Highest.Count > 0)
            {
                text.AppendLine("OL_StatPicker_Highest".Translate());
                ToStringNumberSense sense = ofPiece ? ToStringNumberSense.Absolute : ToStringNumberSense.Offset;
                foreach ((ThingDef item, float value) in choice.Highest)
                {
                    text.AppendLine("  " + "OL_StatPicker_HighestItem".Translate(item.LabelCap, choice.Stat.ValueToString(value, sense)));
                }
            }
            return text.ToString().TrimEnd();
        }
    }
}
