using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// A searchable list of the materials imagined pieces can be made of, grouped by kind (fabric, leather, metal), with
    /// "best for this order" and "typical" first.
    /// </summary>
    public class Dialog_PickMaterial : Window
    {
        private static List<IGrouping<StuffCategoryDef, ThingDef>> groups;

        private readonly Action<MaterialChoice> onPick;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;

        public Dialog_PickMaterial(Action<MaterialChoice> onPick)
        {
            this.onPick = onPick;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            optionalTitle = "OL_MaterialPicker_Title".Translate();
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
            float top = QuickSearchWidget.WidgetHeight + UiStyle.Gap;
            Rect listRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top);

            List<MaterialChoice> general = new List<MaterialChoice> { MaterialChoice.BestForOrder, MaterialChoice.Typical }
                .Where(choice => search.filter.Matches(choice.Label))
                .ToList();
            var matching = Groups()
                .Select(group => (category: group.Key, stuffs: group.Where(stuff => search.filter.Matches(stuff.LabelCap)).ToList()))
                .Where(group => group.stuffs.Count > 0)
                .ToList();
            search.noResultsMatched = general.Count == 0 && matching.Count == 0;

            int rows = general.Count + matching.Sum(group => group.stuffs.Count + 1) + (search.noResultsMatched ? 1 : 0);
            Rect view = new Rect(0f, 0f, listRect.width - GenUI.ScrollBarWidth, rows * UiStyle.RowHeight);
            Widgets.BeginScrollView(listRect, ref scroll, view);
            try
            {
                DrawRows(view.width, general, matching);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawRows(float width, List<MaterialChoice> general, List<(StuffCategoryDef category, List<ThingDef> stuffs)> matching)
        {
            float y = 0f;
            if (search.noResultsMatched)
            {
                UiStyle.Note(new Rect(0f, y, width, UiStyle.RowHeight), "OL_MaterialPicker_NoMatches".Translate());
                return;
            }
            foreach (MaterialChoice choice in general)
            {
                DrawRow(new Rect(0f, y, width, UiStyle.RowHeight), choice, null);
                y += UiStyle.RowHeight;
            }
            foreach ((StuffCategoryDef category, List<ThingDef> stuffs) in matching)
            {
                using (new TextBlock(TextAnchor.LowerLeft))
                {
                    UiStyle.Note(new Rect(0f, y, width, UiStyle.RowHeight), category.LabelCap);
                }
                y += UiStyle.RowHeight;
                foreach (ThingDef stuff in stuffs)
                {
                    DrawRow(new Rect(0f, y, width, UiStyle.RowHeight), MaterialChoice.Specific(stuff), stuff);
                    y += UiStyle.RowHeight;
                }
            }
        }

        private void DrawRow(Rect row, MaterialChoice choice, ThingDef stuff)
        {
            if (UiStyle.ClickableRow(row))
            {
                onPick(choice);
                Close();
            }
            float labelX = row.x + UiStyle.Gap;
            if (stuff != null)
            {
                Rect icon = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
                Widgets.ThingIcon(icon, stuff, null, null, 1f, null, null, 1f);
                labelX = icon.xMax + UiStyle.Gap;
                if (!stuff.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(row, stuff.description);
                }
            }
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(labelX, row.y, row.xMax - labelX, row.height), choice.Label);
            }
        }

        /// <summary>Every material some apparel can be made of, under its first kind, both sorted by name.</summary>
        private static List<IGrouping<StuffCategoryDef, ThingDef>> Groups()
        {
            if (groups == null)
            {
                List<ThingDef> apparel = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.IsApparel && def.MadeFromStuff).ToList();
                groups = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => def.IsStuff && !def.stuffProps.categories.NullOrEmpty() && apparel.Any(piece => def.stuffProps.CanMake(piece)))
                    .OrderBy(def => def.label)
                    .GroupBy(def => def.stuffProps.categories[0])
                    .OrderBy(group => group.Key.label)
                    .ToList();
            }
            return groups;
        }
    }
}
