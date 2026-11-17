using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders;
using OptimalLoadouts.ApparelOrders.Gui;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.WeaponOrders.Gui
{
    /// <summary>
    /// The Weapon section under a policy's apparel priorities: what its colonists carry, its own weapon priorities to
    /// edit, the ones it gets from its base, and a button to add one.
    /// </summary>
    public static class WeaponOrderSection
    {
        private const float KindWidth = 150f;

        /// <summary>The section's height, so the priorities list scrolls over it.</summary>
        public static float Height(ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            int own = OwnLines(orders, policy).Count;
            List<(ApparelPolicy from, WeaponLine line)> fromBase = BaseLines(orders, policy);
            int headings = fromBase.Select(entry => entry.from).Distinct().Count();
            int rows = Mathf.Max(1, own + fromBase.Count + headings);
            return UiStyle.Gap + UiStyle.RowHeight + rows * UiStyle.RowHeight + UiStyle.Gap + UiStyle.RowHeight;
        }

        /// <summary>Draws the section from <paramref name="y"/> down, inside the priorities' scroll view.</summary>
        public static void Draw(float y, float width, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            y += UiStyle.Gap;
            DrawHeader(new Rect(0f, y, width, UiStyle.RowHeight), orders, policy);
            y += UiStyle.RowHeight;
            List<WeaponLine> own = OwnLines(orders, policy);
            List<(ApparelPolicy from, WeaponLine line)> fromBase = BaseLines(orders, policy);
            if (own.Count == 0 && fromBase.Count == 0)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(new Rect(0f, y, width, UiStyle.RowHeight), "OL_Weapon_NoLines".Translate());
                }
                y += UiStyle.RowHeight;
            }
            for (int i = 0; i < own.Count; i++)
            {
                DrawOwnLine(new Rect(0f, y, width, UiStyle.RowHeight), orders, policy, own[i], i);
                y += UiStyle.RowHeight;
            }
            ApparelPolicy currentSource = null;
            foreach ((ApparelPolicy from, WeaponLine line) in fromBase)
            {
                if (from != currentSource)
                {
                    currentSource = from;
                    using (new TextBlock(TextAnchor.MiddleLeft))
                    {
                        UiStyle.Note(new Rect(0f, y, width, UiStyle.RowHeight), "OL_Editor_FromBase".Translate(from.label));
                    }
                    y += UiStyle.RowHeight;
                }
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(new Rect(StatOrderPanel.NumberWidth, y, width - StatOrderPanel.NumberWidth, UiStyle.RowHeight),
                        "OL_Editor_BaseLine".Translate(line.number.LabelCap, DirectionLabel(line)));
                }
                y += UiStyle.RowHeight;
            }
            y += UiStyle.Gap;
            if (Widgets.ButtonText(new Rect(0f, y, StatOrderPanel.AddButtonWidth, UiStyle.RowHeight), "OL_Weapon_AddLine".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickWeaponNumber(number => orders.AddWeaponLine(policy, number),
                    own.Select(line => line.number)));
            }
        }

        /// <summary>"Weapon" on a band, with what the policy's colonists carry on the right.</summary>
        private static void DrawHeader(Rect band, ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            Rect kind = new Rect(band.xMax - KindWidth, band.y + 2f, KindWidth, band.height - 4f);
            Rect title = new Rect(band.x + UiStyle.Gap / 2f, band.y, kind.x - band.x - UiStyle.Gap, band.height);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(title, "OL_Weapon_Band".Translate());
            }
            WeaponKind own = orders.OrderFor(policy)?.weaponKind ?? WeaponKind.Any;
            WeaponKind effective = orders.WeaponKindOf(policy);
            string label = own == WeaponKind.Any && effective != WeaponKind.Any
                ? "OL_Weapon_KindFromBase".Translate(KindLabel(effective)).Resolve()
                : KindLabel(own);
            TooltipHandler.TipRegion(kind, "OL_Weapon_KindTip".Translate());
            if (Widgets.ButtonText(kind, label.Truncate(kind.width - 12f)))
            {
                var options = new List<FloatMenuOption>();
                foreach (WeaponKind each in new[] { WeaponKind.Any, WeaponKind.Ranged, WeaponKind.Melee, WeaponKind.None })
                {
                    options.Add(new FloatMenuOption(KindLabel(each), () => orders.SetWeaponKind(policy, each)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        /// <summary>One of the policy's own weapon lines: its number, its direction (opening the four choices), then move and remove.</summary>
        private static void DrawOwnLine(Rect row, ApparelOrdersComponent orders, ApparelPolicy policy, WeaponLine line, int index)
        {
            Widgets.DrawHighlightIfMouseover(row);
            float buttons = 3 * (UiStyle.IconSize + UiStyle.IconGap);
            float labelWidth = row.width - StatOrderPanel.NumberWidth - StatOrderPanel.DirectionWidth - UiStyle.IconGap - buttons - UiStyle.Gap;
            Rect labelRect = new Rect(row.x + StatOrderPanel.NumberWidth, row.y, labelWidth, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(row.x, row.y, StatOrderPanel.NumberWidth, row.height), "OL_Editor_LineNumber".Translate(index + 1));
                Widgets.Label(labelRect, line.number.LabelCap.Resolve().Truncate(labelWidth));
            }
            TooltipHandler.TipRegion(labelRect, line.number.description);
            float x = labelRect.xMax + UiStyle.Gap;
            Rect direction = new Rect(x, row.y + UiStyle.IconGap / 2f, StatOrderPanel.DirectionWidth, row.height - UiStyle.IconGap);
            TooltipHandler.TipRegion(direction, "OL_Weapon_DirectionTip".Translate());
            if (Widgets.ButtonText(direction, DirectionLabel(line).Truncate(StatOrderPanel.DirectionWidth - UiStyle.Gap)))
            {
                Find.WindowStack.Add(new Dialog_WeaponLineDirection(line,
                    (lower, rulesOut) => orders.SetWeaponLineChoice(policy, index, lower, rulesOut),
                    value => orders.SetWeaponLineCountsFrom(policy, index, value)));
            }
            x += StatOrderPanel.DirectionWidth + UiStyle.IconGap;
            float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
            DrawMove(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.ReorderUp, "OL_Editor_MoveUp".Translate(), orders, policy, index, -1);
            x += UiStyle.IconSize + UiStyle.IconGap;
            DrawMove(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.ReorderDown, "OL_Editor_MoveDown".Translate(), orders, policy, index, 1);
            x += UiStyle.IconSize + UiStyle.IconGap;
            if (Widgets.ButtonImage(new Rect(x, iconY, UiStyle.IconSize, UiStyle.IconSize), TexButton.Delete, true, "OL_Editor_Remove".Translate()))
            {
                orders.RemoveWeaponLine(policy, index);
            }
        }

        private static void DrawMove(Rect rect, Texture2D icon, string tooltip, ApparelOrdersComponent orders, ApparelPolicy policy, int index, int direction)
        {
            if (orders.CanMoveWeaponLine(policy, index, direction))
            {
                if (Widgets.ButtonImage(rect, icon, true, tooltip))
                {
                    orders.MoveWeaponLine(policy, index, direction);
                }
                return;
            }
            using (new TextBlock(ColoredText.SubtleGrayColor))
            {
                GUI.DrawTexture(rect, icon);
            }
        }

        /// <summary>"Higher first", "Higher, from 10" when the line counts from a value, or "At least 10" when it rules out what's below.</summary>
        private static string DirectionLabel(WeaponLine line)
        {
            string value = line.number.Format(line.countsFrom);
            if (line.rulesOut)
            {
                return (line.lowerIsBetter ? "OL_Rule_AtMost" : "OL_Rule_AtLeast").Translate(value);
            }
            if (line.countsFrom <= 0f)
            {
                return (line.lowerIsBetter ? "OL_Order_LowerFirst" : "OL_Order_HigherFirst").Translate();
            }
            return (line.lowerIsBetter ? "OL_Order_LowerFrom" : "OL_Order_HigherFrom").Translate(value);
        }

        private static string KindLabel(WeaponKind kind)
        {
            return ("OL_Weapon_Kind_" + kind).Translate();
        }

        private static List<WeaponLine> OwnLines(ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            return orders.OrderFor(policy)?.weaponLines ?? new List<WeaponLine>();
        }

        /// <summary>The weapon lines the policy gets from its bases, each with the policy it comes from.</summary>
        private static List<(ApparelPolicy from, WeaponLine line)> BaseLines(ApparelOrdersComponent orders, ApparelPolicy policy)
        {
            var result = new List<(ApparelPolicy, WeaponLine)>();
            var listed = new HashSet<WeaponNumberDef>(OwnLines(orders, policy).Select(line => line.number));
            var visited = new HashSet<ApparelPolicy> { policy };
            for (ApparelPolicy basePolicy = orders.BaseOf(policy); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                List<WeaponLine> baseLines = orders.OrderFor(basePolicy)?.weaponLines;
                if (baseLines == null)
                {
                    break;
                }
                foreach (WeaponLine line in baseLines)
                {
                    if (line.number != null && listed.Add(line.number))
                    {
                        result.Add((basePolicy, line));
                    }
                }
            }
            return result;
        }
    }
}
