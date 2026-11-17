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
    /// The numbers of the weapon the preview shows (the one clicked in the weapon browser, else the role's pick), beside or
    /// under the browser as a piece's stats are beside the slot browser: under Ranged, Melee and General, each with its
    /// value. Clicking one sorts the browser by it. At the end, a priority's place with "−" to remove it, or "+" to make
    /// the number a priority.
    /// </summary>
    public static class WeaponStatsPanel
    {
        private const float StatHeight = UiStyle.BandHeight;

        /// <summary>The share of a row its name takes, as on the info card.</summary>
        private const float LabelShare = 0.55f;

        private const float NumberWidth = 26f;

        /// <summary>Big enough to see the weapon, as the outfit preview's portrait shows the pawn.</summary>
        private const float PictureSize = 64f;

        /// <summary>The least room an ammo's name ("Sabot", "Incendiary") keeps beside its numbers.</summary>
        private const float NameWidth = 70f;

        private static readonly WeaponNumberGroup[] Groups = { WeaponNumberGroup.Ranged, WeaponNumberGroup.Melee, WeaponNumberGroup.General };

        public static void Draw(Rect rect, PreviewSession session)
        {
            ThingWithComps weapon = session.Weapon;
            WeaponChoice judged = session.ChoiceFor(weapon);
            if (weapon == null)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    UiStyle.Note(new Rect(rect.x, rect.y + UiStyle.HeadingGap, rect.width, UiStyle.RowHeight), "OL_Weapon_StatsNoWeapon".Translate());
                }
                return;
            }
            // The weapon's picture, then its name with the info card button, as a heading.
            Rect picture = new Rect(rect.x, rect.y + UiStyle.HeadingGap, PictureSize, PictureSize);
            Widgets.ThingIcon(picture, weapon);
            Rect title = new Rect(picture.xMax + UiStyle.Gap, picture.y, rect.xMax - picture.xMax - UiStyle.Gap, PictureSize);
            Rect info = new Rect(title.xMax - Widgets.InfoCardButtonSize, title.y + (title.height - Widgets.InfoCardButtonSize) / 2f,
                Widgets.InfoCardButtonSize, Widgets.InfoCardButtonSize);
            Widgets.InfoCardButton(info.x, info.y, weapon);
            Rect nameRect = new Rect(title.x, title.y, info.x - UiStyle.Gap - title.x, title.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(nameRect, WeaponLabel.Of(weapon, judged?.Ammo).Truncate(nameRect.width));
            }

            List<(WeaponNumberGroup group, List<(WeaponNumberDef number, float value)> numbers)> runs = Groups
                .Select(group => (group, DefDatabase<WeaponNumberDef>.AllDefsListForReading
                    .Where(number => number.group == group && number.Offered)
                    .Select(number => (number, value: WeaponRanking.ValueOf(weapon.def, weapon.Stuff, number, judged?.Bullet)))
                    .Where(each => each.value != null)
                    .Select(each => (each.number, each.value.Value))
                    .ToList()))
                .Where(run => run.Item2.Count > 0)
                .ToList();
            IReadOnlyList<AmmoOption> ammo = CombatExtendedWeapons.AmmoFor(weapon.def);
            float ammoHeight = ammo.Count < 2 ? 0f : UiStyle.BandHeight + (ammo.Count + 1) * StatHeight;
            Rect list = new Rect(rect.x, title.yMax, rect.width, rect.yMax - title.yMax);
            Rect view = new Rect(0f, 0f, list.width - GenUI.ScrollBarWidth,
                ammoHeight + runs.Sum(run => UiStyle.BandHeight + run.numbers.Count * StatHeight));
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            List<WeaponLine> own = orders?.OrderFor(session.Policy)?.weaponLines ?? new List<WeaponLine>();
            IReadOnlyList<WeaponLine> lines = orders?.EffectiveWeaponLines(session.Policy) ?? new List<WeaponLine>();
            Widgets.BeginScrollView(list, ref session.WeaponStatsScroll, view);
            try
            {
                float y = 0f;
                if (ammoHeight > 0f)
                {
                    DrawAmmo(new Rect(0f, y, view.width, ammoHeight), weapon, ammo, judged, session, orders);
                    y += ammoHeight;
                }
                foreach ((WeaponNumberGroup group, List<(WeaponNumberDef number, float value)> numbers) in runs)
                {
                    Rect band = new Rect(0f, y, view.width, UiStyle.BandHeight);
                    Widgets.DrawBoxSolid(band, UiStyle.BandColor);
                    using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), ("OL_Weapon_Group_" + group).Translate());
                    }
                    y += UiStyle.BandHeight;
                    foreach ((WeaponNumberDef number, float value) in numbers)
                    {
                        DrawNumber(new Rect(0f, y, view.width, StatHeight), number, value, session, orders, own, lines);
                        y += StatHeight;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>The numbers that tell a gun's ammo apart, as the ammo table's columns, each with a header short enough to fit.</summary>
        private static readonly (string number, string header)[] AmmoColumns =
        {
            ("OL_DamagePerShot", "OL_Weapon_AmmoDamage"), ("OL_ArmorPenetration", "OL_Weapon_AmmoSharp"), ("OL_BluntPenetration", "OL_Weapon_AmmoBlunt"),
        };

        /// <summary>
        /// A Combat Extended gun's ammo, one row each with its damage and penetration. The ammo the gun is judged on is
        /// highlighted, and a pinned one wears a lock. Clicking a row pins that ammo on the gun for the policy; clicking the
        /// pinned row unpins it, back to the best for the priorities.
        /// </summary>
        private static void DrawAmmo(Rect area, ThingWithComps weapon, IReadOnlyList<AmmoOption> ammo, WeaponChoice judged,
            PreviewSession session, ApparelOrdersComponent orders)
        {
            Rect band = new Rect(area.x, area.y, area.width, UiStyle.BandHeight);
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), "OL_Weapon_AmmoBand".Translate());
            }
            var columns = AmmoColumns
                .Select(column => (number: DefDatabase<WeaponNumberDef>.GetNamedSilentFail(column.number), column.header))
                .Where(column => column.number != null && column.number.Offered)
                .ToList();
            float columnWidth = Mathf.Min(SlotBrowserPanel.ValueWidth, (area.width - UiStyle.IconSize - NameWidth) / Mathf.Max(1, columns.Count));
            Rect header = new Rect(area.x, band.yMax, area.width, StatHeight);
            using (new TextBlock(GameFont.Tiny, TextAnchor.LowerCenter, ColoredText.SubtleGrayColor))
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    Rect cell = AmmoCell(header, i, columns.Count, columnWidth);
                    Widgets.Label(cell, columns[i].header.Translate().Resolve().Truncate(columnWidth));
                    TooltipHandler.TipRegion(cell, columns[i].number.LabelCap);
                }
            }
            ThingDef pinned = orders?.PinnedAmmoOf(session.Policy, weapon.def);
            float y = header.yMax;
            foreach (AmmoOption option in ammo)
            {
                Rect row = new Rect(area.x, y, area.width, StatHeight);
                if (judged?.Ammo == option)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                Widgets.DrawHighlightIfMouseover(row);
                Rect lockRect = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
                if (option.Ammo == pinned)
                {
                    GUI.DrawTexture(lockRect, UiStyle.LockIcon);
                }
                Rect name = new Rect(lockRect.xMax + UiStyle.IconGap, row.y, AmmoCell(row, 0, columns.Count, columnWidth).x - lockRect.xMax - UiStyle.Gap, row.height);
                using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
                {
                    Widgets.Label(name, (option.KindLabel?.CapitalizeFirst() ?? option.Ammo.LabelCap.Resolve()).Truncate(name.width));
                }
                using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
                {
                    for (int i = 0; i < columns.Count; i++)
                    {
                        float? value = WeaponRanking.ValueOf(weapon.def, weapon.Stuff, columns[i].number, option.Bullet);
                        Widgets.Label(AmmoCell(row, i, columns.Count, columnWidth), value == null ? "–" : columns[i].number.Format(value.Value));
                    }
                }
                TooltipHandler.TipRegion(row, option.Ammo.LabelCap + "\n\n"
                    + (option.Ammo == pinned ? "OL_Weapon_AmmoUnpinTip" : "OL_Weapon_AmmoPinTip").Translate());
                if (orders != null && Widgets.ButtonInvisible(row))
                {
                    orders.SetPinnedAmmo(session.Policy, weapon.def, option.Ammo == pinned ? null : option.Ammo);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                y += StatHeight;
            }
        }

        /// <summary>The ammo table's columns sit at the row's right end.</summary>
        private static Rect AmmoCell(Rect row, int index, int count, float width)
        {
            return new Rect(row.xMax - (count - index) * width, row.y, width, row.height);
        }

        private static void DrawNumber(Rect row, WeaponNumberDef number, float value, PreviewSession session, ApparelOrdersComponent orders,
            List<WeaponLine> own, IReadOnlyList<WeaponLine> lines)
        {
            Rect button = new Rect(row.xMax - StatHeight, row.y, StatHeight, StatHeight);
            Rect place = new Rect(button.x - NumberWidth, row.y, NumberWidth, row.height);
            Rect body = new Rect(row.x, row.y, place.x - row.x - UiStyle.Gap / 2f, row.height);
            bool sorted = session.WeaponSortNumber == number;
            if (sorted)
            {
                Widgets.DrawHighlightSelected(body);
            }
            Widgets.DrawHighlightIfMouseover(body);
            Rect label = new Rect(body.x + UiStyle.Gap / 2f, body.y, body.width * LabelShare, body.height);
            Rect valueRect = new Rect(label.xMax, body.y, body.xMax - label.xMax, body.height);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(label, number.LabelCap.Resolve().Truncate(label.width));
                Widgets.Label(valueRect, number.Format(value).Truncate(valueRect.width));
            }
            TooltipHandler.TipRegion(body, number.description + "\n\n" + "OL_Slot_SortHint".Translate());
            if (Widgets.ButtonInvisible(body))
            {
                session.CycleWeaponSort(number, reverse: Event.current.button == 1);
                (session.WeaponSortNumber == null ? SoundDefOf.Tick_Low : SoundDefOf.Tick_High).PlayOneShotOnCamera();
            }
            if (orders == null)
            {
                return;
            }
            int ownIndex = own.FindIndex(line => line.number == number);
            int effective = lines.ToList().FindIndex(line => line.number == number);
            if (effective >= 0)
            {
                using (new TextBlock(TextAnchor.MiddleRight, ownIndex >= 0 ? Color.white : ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(place, "OL_Editor_LineNumber".Translate(effective + 1));
                }
            }
            if (ownIndex >= 0)
            {
                if (Widgets.ButtonImage(button.ContractedBy(2f), TexButton.Minus, true, "OL_Weapon_StatsRemoveTip".Translate()))
                {
                    orders.RemoveWeaponLine(session.Policy, ownIndex);
                }
            }
            else if (effective < 0 && Widgets.ButtonImage(button.ContractedBy(2f), TexButton.Plus, true, "OL_Weapon_StatsAddTip".Translate()))
            {
                orders.AddWeaponLine(session.Policy, number);
            }
        }
    }
}
