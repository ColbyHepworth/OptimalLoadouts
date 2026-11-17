using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.WeaponOrders.Gui
{
    /// <summary>A searchable list of the weapon numbers offered with the mods loaded, under Ranged, Melee and General.</summary>
    public class Dialog_PickWeaponNumber : Window
    {
        private static readonly WeaponNumberGroup[] Groups = { WeaponNumberGroup.Ranged, WeaponNumberGroup.Melee, WeaponNumberGroup.General };

        private readonly Action<WeaponNumberDef> onPick;
        private readonly HashSet<WeaponNumberDef> alreadyListed;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;

        public Dialog_PickWeaponNumber(Action<WeaponNumberDef> onPick, IEnumerable<WeaponNumberDef> alreadyListed)
        {
            this.onPick = onPick;
            this.alreadyListed = new HashSet<WeaponNumberDef>(alreadyListed);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            optionalTitle = "OL_Weapon_PickerTitle".Translate();
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
            List<(WeaponNumberGroup group, List<WeaponNumberDef> numbers)> shown = Groups
                .Select(group => (group, DefDatabase<WeaponNumberDef>.AllDefsListForReading
                    .Where(number => number.group == group && number.Offered && !alreadyListed.Contains(number) && search.filter.Matches(number.LabelCap))
                    .ToList()))
                .Where(entry => entry.Item2.Count > 0)
                .ToList();
            search.noResultsMatched = shown.Count == 0;
            int rows = shown.Sum(entry => entry.numbers.Count + 1);
            Rect view = new Rect(0f, 0f, listRect.width - GenUI.ScrollBarWidth, Mathf.Max(1, rows) * UiStyle.RowHeight);
            Widgets.BeginScrollView(listRect, ref scroll, view);
            try
            {
                float y = 0f;
                if (shown.Count == 0)
                {
                    UiStyle.Note(new Rect(0f, y, view.width, UiStyle.RowHeight), "OL_Weapon_PickerNoMatches".Translate());
                }
                foreach ((WeaponNumberGroup group, List<WeaponNumberDef> numbers) in shown)
                {
                    using (new TextBlock(TextAnchor.LowerLeft))
                    {
                        UiStyle.Note(new Rect(0f, y, view.width, UiStyle.RowHeight), ("OL_Weapon_Group_" + group).Translate());
                    }
                    y += UiStyle.RowHeight;
                    foreach (WeaponNumberDef number in numbers)
                    {
                        Rect row = new Rect(0f, y, view.width, UiStyle.RowHeight);
                        if (UiStyle.ClickableRow(row))
                        {
                            onPick(number);
                            Close();
                        }
                        TooltipHandler.TipRegion(row, number.description);
                        using (new TextBlock(TextAnchor.MiddleLeft))
                        {
                            Widgets.Label(new Rect(row.x + UiStyle.Gap, row.y, row.width - UiStyle.Gap, row.height), number.LabelCap);
                        }
                        y += UiStyle.RowHeight;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}
