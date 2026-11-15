using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using OptimalLoadouts.Gui;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// How a priority line treats a piece, opened by its direction button like the layer picker. A stat line: one choice
    /// of four, higher first, lower first, at least or at most, then its value, typed in the units the info card shows (a
    /// percent stat takes "10" for 10%, a temperature the player's own scale): the value it counts from when it ranks, its
    /// cutoff when it rules out. A body part line: required or not allowed. A note says the most any apparel has and how
    /// many items count as none, how many items cover the part, or how many items the line rules out. Each change applies
    /// at once.
    /// </summary>
    public class Dialog_LineDirection : Window
    {
        private const float Width = 320f;
        private const float BoxWidth = 70f;
        private const int ItemsListed = 20;

        /// <summary>Values closer to zero than this are none, so items without the stat aren't counted as under it.</summary>
        private const float Epsilon = 0.0001f;

        /// <summary>The number at the front of a formatted value, so what's left is its unit.</summary>
        private static readonly Regex LeadingNumber = new Regex(@"^[+\-−]?[\d.,]+\s*");

        private readonly OrderLine line;
        private readonly ApparelPolicy policy;
        private readonly Action<bool, bool> setChoice;
        private readonly Action<float> setCountsFrom;
        private readonly string highest;

        /// <summary>The most any apparel has, as stored, where "at most" starts so it rules nothing out until it's lowered.</summary>
        private readonly float most;

        /// <summary>Each apparel item with a value on the stat, in its usual material. Empty on a body part line.</summary>
        private readonly List<(ThingDef item, float value)> items = new List<(ThingDef item, float value)>();

        private string text = "";

        /// <summary>The items covering a body part line's part, worked out the first time the note is drawn.</summary>
        private List<ThingDef> covering;

        /// <param name="setChoice">Called with lower-is-better and rules-out for the choice picked.</param>
        public Dialog_LineDirection(OrderLine line, ApparelPolicy policy, Action<bool, bool> setChoice, Action<float> setCountsFrom)
        {
            this.line = line;
            this.policy = policy;
            this.setChoice = setChoice;
            this.setCountsFrom = setCountsFrom;
            if (line.IsStat)
            {
                StatDef stat = line.stat;
                text = line.countsFrom > 0f ? ToShown(stat, line.countsFrom).ToString("0.###", CultureInfo.InvariantCulture) : "";
                items = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => def.IsApparel)
                    .Select(def => (item: def, ok: ApparelForbids.TryValueOf(def, stat, LimitMaterial.Usual, false, out float value), value))
                    .Where(entry => entry.ok && Mathf.Abs(entry.value) > Epsilon)
                    .Select(entry => (entry.item, entry.value))
                    .ToList();
                most = items.Count == 0 ? 0f : items.Max(entry => Mathf.Abs(entry.value));
                highest = stat.ValueToString(most, ToStringNumberSense.Absolute);
            }
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doWindowBackground = true;
        }

        /// <summary>A stat's four choices, then its value, then a note; or a body part's two choices and a note.</summary>
        public override Vector2 InitialSize => new Vector2(Width, (line.IsStat ? 6f : 3f) * UiStyle.RowHeight + UiStyle.Gap + 2f * Margin);

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
            if (line.IsCoverage)
            {
                y = DrawChoice(inRect, y, "OL_Rule_Required".Translate(), false, false, "OL_Rule_RequiredTip".Translate(line.covers.label));
                y = DrawChoice(inRect, y, "OL_Rule_NotAllowed".Translate(), true, true, "OL_Rule_NotAllowedTip".Translate(line.covers.label));
                y += UiStyle.Gap;
                Rect partNote = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
                if (line.rulesOut)
                {
                    DrawRuledOut(partNote);
                }
                else
                {
                    DrawCovering(partNote);
                }
                return;
            }
            y = DrawChoice(inRect, y, "OL_Order_HigherFirst".Translate(), false, false, null);
            y = DrawChoice(inRect, y, "OL_Order_LowerFirst".Translate(), true, false, null);
            y = DrawChoice(inRect, y, "OL_Rule_AtLeastChoice".Translate(), false, true, "OL_Rule_Tip".Translate());
            y = DrawChoice(inRect, y, "OL_Rule_AtMostChoice".Translate(), true, true, "OL_Rule_Tip".Translate());
            y += UiStyle.Gap;
            y = DrawValue(inRect, y);
            Rect noteRect = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            if (line.rulesOut)
            {
                DrawRuledOut(noteRect);
            }
            else
            {
                DrawCountsAsNone(noteRect);
            }
        }

        /// <summary>
        /// One of the choices, as a radio button, with its tooltip when it has one. On a stat, "At most" starts at the most
        /// any apparel has, and leaving it clears the value, since an "at most" value read as the value a line counts from,
        /// or a value read as "at most", would rule out or count as none nearly everything.
        /// </summary>
        private float DrawChoice(Rect inRect, float y, string label, bool lowerIsBetter, bool rulesOut, string tip)
        {
            Rect row = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            bool chosen = line.lowerIsBetter == lowerIsBetter && line.rulesOut == rulesOut;
            if (Widgets.RadioButtonLabeled(row, label, chosen) && !chosen)
            {
                bool wasAtMost = line.lowerIsBetter && line.rulesOut;
                bool atMost = lowerIsBetter && rulesOut;
                if (line.IsStat && (atMost || wasAtMost))
                {
                    float value = atMost ? most : 0f;
                    setCountsFrom(value);
                    text = value > 0f ? ToShown(line.stat, value).ToString("0.###", CultureInfo.InvariantCulture) : "";
                }
                setChoice(lowerIsBetter, rulesOut);
            }
            if (tip != null)
            {
                TooltipHandler.TipRegion(row, tip);
            }
            return y + UiStyle.RowHeight;
        }

        /// <summary>How many items cover the body part on the line's layers, listed on hover.</summary>
        private void DrawCovering(Rect noteRect)
        {
            // The popup takes all input while it's open, so the line's layers can't change under it.
            if (covering == null)
            {
                covering = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => def.IsApparel && line.CountsOn(def) && PieceCoverage.Covers(def, line.covers))
                    .OrderBy(def => def.label)
                    .ToList();
            }
            string note = "OL_StatPicker_CoversTip".Translate(covering.Count, line.covers.label);
            UiStyle.Note(noteRect, note.Truncate(noteRect.width));
            if (covering.Count > 0)
            {
                TooltipHandler.TipRegion(noteRect, () => ItemsTip(covering), line.GetHashCode() + 2);
            }
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
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(box.xMax + UiStyle.Gap, row.y, row.xMax - box.xMax - UiStyle.Gap, row.height), Unit(line.stat));
            }
            TooltipHandler.TipRegion(row, line.rulesOut ? "OL_Rule_ValueTip".Translate() : "OL_CountsFrom_Tip".Translate());
            float? typed = Typed();
            if (typed.HasValue)
            {
                float stored = FromShown(line.stat, Mathf.Max(0f, typed.Value));
                if (Mathf.Abs(stored - line.countsFrom) > Epsilon)
                {
                    setCountsFrom(stored);
                }
            }
            return y + UiStyle.RowHeight;
        }

        /// <summary>The most any apparel has, and how many items count as none under the value, listed on hover.</summary>
        private void DrawCountsAsNone(Rect noteRect)
        {
            string note = "OL_CountsFrom_Highest".Translate(highest);
            List<ThingDef> under = line.countsFrom > 0f ? Under() : new List<ThingDef>();
            if (under.Count > 0)
            {
                note += " " + (under.Count == 1 ? "OL_CountsFrom_CountOne".Translate() : "OL_CountsFrom_Count".Translate(under.Count));
            }
            UiStyle.Note(noteRect, note.Truncate(noteRect.width));
            TooltipHandler.TipRegion(noteRect, () => under.Count == 0 ? note : note + "\n\n" + ItemsTip(under), line.GetHashCode());
        }

        /// <summary>How many items the line rules out, listed on hover.</summary>
        private void DrawRuledOut(Rect noteRect)
        {
            List<ThingDef> caught = ApparelForbids.Caught(policy, ApparelForbid.From(line));
            string note = caught.Count == 1 ? "OL_Rule_CountOne".Translate() : "OL_Rule_Count".Translate(caught.Count);
            UiStyle.Note(noteRect, note.Truncate(noteRect.width));
            if (caught.Count > 0)
            {
                TooltipHandler.TipRegion(noteRect, () => ItemsTip(caught), line.GetHashCode() + 1);
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

        private List<ThingDef> Under()
        {
            return items.Where(entry => Mathf.Abs(line.Counted(entry.value)) < Epsilon).Select(entry => entry.item).ToList();
        }

        private static string ItemsTip(List<ThingDef> listed)
        {
            string names = string.Join("\n", listed.Take(ItemsListed).Select(def => def.LabelCap.Resolve()));
            return listed.Count > ItemsListed ? names + "\n" + "OL_Rule_AndMore".Translate(listed.Count - ItemsListed) : names;
        }

        /// <summary>The stored value as the info card shows it: percent stats in percent, temperatures in the player's scale.</summary>
        public static float ToShown(StatDef stat, float value)
        {
            if (IsPercent(stat))
            {
                return value * 100f;
            }
            if (stat.toStringStyle == ToStringStyle.TemperatureOffset)
            {
                return GenTemperature.ConvertTemperatureOffset(value, TemperatureDisplayMode.Celsius, Prefs.TemperatureMode);
            }
            return value;
        }

        public static float FromShown(StatDef stat, float shown)
        {
            if (IsPercent(stat))
            {
                return shown / 100f;
            }
            if (stat.toStringStyle == ToStringStyle.TemperatureOffset)
            {
                return GenTemperature.ConvertTemperatureOffset(shown, Prefs.TemperatureMode, TemperatureDisplayMode.Celsius);
            }
            return shown;
        }

        /// <summary>The unit the info card puts after the stat's values: "%", "mm", "°C", or nothing.</summary>
        private static string Unit(StatDef stat)
        {
            return LeadingNumber.Replace(stat.ValueToString(FromShown(stat, 1f), ToStringNumberSense.Absolute), "").Trim();
        }

        private static bool IsPercent(StatDef stat)
        {
            return stat.toStringStyle == ToStringStyle.PercentZero || stat.toStringStyle == ToStringStyle.PercentOne
                || stat.toStringStyle == ToStringStyle.PercentTwo;
        }
    }
}
