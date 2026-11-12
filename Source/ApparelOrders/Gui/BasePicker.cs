using System.Collections.Generic;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// The "Builds on" row: the policy whose priorities and limits go under this one's. The same row tops the
    /// Priorities and Limits tabs, since the base covers both.
    /// </summary>
    public static class BasePicker
    {
        private const float ButtonWidth = 200f;

        public static void Draw(Rect row, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            string baseLabel = "OL_Editor_Base".Translate();
            float labelWidth = Text.CalcSize(baseLabel).x;
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), baseLabel);
            }
            Rect button = new Rect(row.x + labelWidth + UiStyle.Gap / 2f, row.y, Mathf.Min(ButtonWidth, row.width - labelWidth - UiStyle.Gap / 2f), row.height);
            ApparelPolicy basePolicy = orders.BaseOf(policy);
            string label = basePolicy?.label ?? "OL_Editor_NoBase".Translate();
            TooltipHandler.TipRegion(button, "OL_Editor_BaseTip".Translate());
            if (Widgets.ButtonText(button, label.Truncate(button.width - 12f)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("OL_Editor_NoBase".Translate(), () => orders.SetBase(policy, null)),
                };
                foreach (ApparelPolicy candidate in Current.Game.outfitDatabase.AllOutfits)
                {
                    if (orders.CanBeBase(policy, candidate))
                    {
                        options.Add(new FloatMenuOption(candidate.label, () => orders.SetBase(policy, candidate)));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}
