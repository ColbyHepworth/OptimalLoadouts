using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.WeaponOrders.Gui
{
    /// <summary>
    /// How a weapon priority treats a weapon, opened by its direction button as an apparel priority's popup is: higher
    /// first, lower first, at least or at most, then its value, in the units the browser shows: the value it counts from
    /// when it ranks, its cutoff when it rules out. A note says the most any weapon has and how many count as none, or how
    /// many weapons the line rules out. Each change applies at once.
    /// </summary>
    public class Dialog_WeaponLineDirection : Window
    {
        private const float Width = 320f;
        private const float BoxWidth = 70f;
        private const int WeaponsListed = 20;

        private readonly WeaponLine line;
        private readonly Action<bool, bool> setChoice;
        private readonly Action<float> setCountsFrom;

        /// <summary>Each weapon with the number, in its usual material.</summary>
        private readonly Dictionary<ThingDef, float> values;

        /// <summary>The most any weapon has, where "at most" starts so it rules nothing out until it's lowered.</summary>
        private readonly float most;

        private string text = "";

        /// <param name="setChoice">Called with lower-is-better and rules-out for the choice picked.</param>
        public Dialog_WeaponLineDirection(WeaponLine line, Action<bool, bool> setChoice, Action<float> setCountsFrom)
        {
            this.line = line;
            this.setChoice = setChoice;
            this.setCountsFrom = setCountsFrom;
            values = new Dictionary<ThingDef, float>();
            foreach (ThingDef def in WeaponSources.WeaponDefs())
            {
                float? value = JudgedValue(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                if (value != null)
                {
                    values[def] = value.Value;
                }
            }
            most = values.Count == 0 ? 0f : values.Values.Max(Mathf.Abs);
            text = line.countsFrom > 0f ? ToShown(line.countsFrom).ToString("0.###", CultureInfo.InvariantCulture) : "";
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doWindowBackground = true;
        }

        /// <summary>
        /// A weapon's value on the line, from the ammo the ranking would judge it on there under Combat Extended: the
        /// highest of its ammo on a higher-first line, the lowest on a lower-first one.
        /// </summary>
        private float? JudgedValue(ThingDef weapon, ThingDef stuff)
        {
            List<float> byAmmo = CombatExtendedWeapons.AmmoFor(weapon)
                .Select(option => WeaponRanking.ValueOf(weapon, stuff, line.number, option.Bullet))
                .Where(value => value != null)
                .Select(value => value.Value)
                .ToList();
            if (byAmmo.Count == 0)
            {
                return WeaponRanking.ValueOf(weapon, stuff, line.number);
            }
            return line.lowerIsBetter ? byAmmo.Min() : byAmmo.Max();
        }

        public override Vector2 InitialSize => new Vector2(Width, 6f * UiStyle.RowHeight + UiStyle.Gap + 2f * Margin);

        /// <summary>Opens by the mouse, as a menu does, kept on screen.</summary>
        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            Vector2 at = UI.MousePositionOnUIInverted;
            at.x = Mathf.Min(at.x, UI.screenWidth - size.x);
            at.y = Mathf.Min(at.y, UI.screenHeight - size.y);
            windowRect = new Rect(at.x, at.y, size.x, size.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            y = DrawChoice(inRect, y, "OL_Order_HigherFirst".Translate(), false, false, null);
            y = DrawChoice(inRect, y, "OL_Order_LowerFirst".Translate(), true, false, null);
            y = DrawChoice(inRect, y, "OL_Rule_AtLeastChoice".Translate(), false, true, "OL_Weapon_RuleTip".Translate());
            y = DrawChoice(inRect, y, "OL_Rule_AtMostChoice".Translate(), true, true, "OL_Weapon_RuleTip".Translate());
            y += UiStyle.Gap;
            y = DrawValue(inRect, y);
            DrawNote(new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight));
        }

        /// <summary>
        /// One of the choices, as a radio button. "At most" starts at the most any weapon has, and leaving it clears the
        /// value, as on an apparel line.
        /// </summary>
        private float DrawChoice(Rect inRect, float y, string label, bool lowerIsBetter, bool rulesOut, string tip)
        {
            Rect row = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            bool chosen = line.lowerIsBetter == lowerIsBetter && line.rulesOut == rulesOut;
            if (Widgets.RadioButtonLabeled(row, label, chosen) && !chosen)
            {
                bool wasAtMost = line.lowerIsBetter && line.rulesOut;
                bool atMost = lowerIsBetter && rulesOut;
                if (atMost || wasAtMost)
                {
                    float value = atMost ? most : 0f;
                    setCountsFrom(value);
                    text = value > 0f ? ToShown(value).ToString("0.###", CultureInfo.InvariantCulture) : "";
                }
                setChoice(lowerIsBetter, rulesOut);
            }
            if (tip != null)
            {
                TooltipHandler.TipRegion(row, tip);
            }
            return y + UiStyle.RowHeight;
        }

        /// <summary>The value: what the line counts from while it ranks, its cutoff while it rules out.</summary>
        private float DrawValue(Rect inRect, float y)
        {
            Rect row = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            string label = !line.rulesOut ? "OL_LineDirection_CountsFrom".Translate()
                : line.lowerIsBetter ? "OL_Rule_AtMostChoice".Translate() : "OL_Rule_AtLeastChoice".Translate();
            float labelWidth = Text.CalcSize(label).x;
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            }
            Rect box = new Rect(row.x + labelWidth + UiStyle.Gap, row.y, BoxWidth, row.height);
            text = Widgets.TextField(box, text);
            if (ShowsPercent)
            {
                using (new TextBlock(TextAnchor.MiddleLeft))
                {
                    Widgets.Label(new Rect(box.xMax + UiStyle.Gap, row.y, row.xMax - box.xMax - UiStyle.Gap, row.height), "%");
                }
            }
            TooltipHandler.TipRegion(row, line.rulesOut ? "OL_Weapon_RuleValueTip".Translate() : "OL_Weapon_CountsFromTip".Translate());
            float? typed = Typed();
            if (typed.HasValue)
            {
                float stored = FromShown(Mathf.Max(0f, typed.Value));
                if (Mathf.Abs(stored - line.countsFrom) > 0.0001f)
                {
                    setCountsFrom(stored);
                }
            }
            return y + UiStyle.RowHeight;
        }

        /// <summary>How many weapons the line rules out, or the most any weapon has and how many count as none. Listed on hover.</summary>
        private void DrawNote(Rect noteRect)
        {
            string note;
            List<ThingDef> listed;
            if (line.rulesOut)
            {
                listed = WeaponSources.WeaponDefs().Where(def => line.RulesOut(values.TryGetValue(def, out float value) ? value : (float?)null)).ToList();
                note = listed.Count == 1 ? "OL_Weapon_RuledOutOne".Translate() : "OL_Weapon_RuledOut".Translate(listed.Count);
            }
            else
            {
                listed = line.countsFrom > 0f
                    ? values.Where(entry => line.Counted(entry.Value) == 0f && Mathf.Abs(entry.Value) > 0.0001f).Select(entry => entry.Key).ToList()
                    : new List<ThingDef>();
                note = "OL_Weapon_Highest".Translate(line.number.Format(most));
                if (listed.Count > 0)
                {
                    note += " " + (listed.Count == 1 ? "OL_Weapon_CountsAsNoneOne".Translate() : "OL_Weapon_CountsAsNone".Translate(listed.Count));
                }
            }
            UiStyle.Note(noteRect, note.Truncate(noteRect.width));
            if (listed.Count > 0)
            {
                string names = string.Join("\n", listed.Take(WeaponsListed).Select(def => def.LabelCap.Resolve()));
                string tip = listed.Count > WeaponsListed ? names + "\n" + "OL_Rule_AndMore".Translate(listed.Count - WeaponsListed) : names;
                TooltipHandler.TipRegion(noteRect, tip);
            }
        }

        /// <summary>The typed number, 0 for an empty box, or null for text that isn't a number yet.</summary>
        private float? Typed()
        {
            if (text.NullOrEmpty())
            {
                return 0f;
            }
            string trimmed = text.Trim().Replace(',', '.');
            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : (float?)null;
        }

        /// <summary>Whether the number shows as a percent, as vanilla's penetration and accuracy do.</summary>
        private bool ShowsPercent => line.number.percentWithoutCombatExtended && !CombatExtendedWeapons.Active;

        private float ToShown(float value)
        {
            return ShowsPercent ? value * 100f : value;
        }

        private float FromShown(float shown)
        {
            return ShowsPercent ? shown / 100f : shown;
        }
    }
}
