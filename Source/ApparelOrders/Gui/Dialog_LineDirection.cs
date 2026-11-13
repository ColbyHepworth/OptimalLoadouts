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
    /// A priority line's direction and the value it counts from, opened by its direction button like the layer picker:
    /// higher or lower first, then a box for the value, typed in the units the info card shows (a percent stat takes "10"
    /// for 10%, a temperature the player's own scale). Each change applies at once, and an empty box counts every amount.
    /// Under the box, the most any apparel has and how many items count as none.
    /// </summary>
    public class Dialog_LineDirection : Window
    {
        private const float Width = 300f;
        private const float BoxWidth = 70f;
        private const int ItemsListed = 20;

        /// <summary>Values closer to zero than this are none, so items without the stat aren't counted as under it.</summary>
        private const float Epsilon = 0.0001f;

        /// <summary>The number at the front of a formatted value, so what's left is its unit.</summary>
        private static readonly Regex LeadingNumber = new Regex(@"^[+\-−]?[\d.,]+\s*");

        private readonly OrderLine line;
        private readonly Action toggleDirection;
        private readonly Action<float> setCountsFrom;
        private readonly string highest;

        /// <summary>Each apparel item with a value on the stat, in its usual material.</summary>
        private readonly List<(ThingDef item, float value)> items;

        private string text;

        public Dialog_LineDirection(OrderLine line, Action toggleDirection, Action<float> setCountsFrom)
        {
            this.line = line;
            this.toggleDirection = toggleDirection;
            this.setCountsFrom = setCountsFrom;
            StatDef stat = line.stat;
            text = line.countsFrom > 0f ? ToShown(stat, line.countsFrom).ToString("0.###", CultureInfo.InvariantCulture) : "";
            items = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsApparel)
                .Select(def => (item: def, ok: ApparelForbids.TryValueOf(def, stat, LimitMaterial.Usual, false, out float value), value))
                .Where(entry => entry.ok && Mathf.Abs(entry.value) > Epsilon)
                .Select(entry => (entry.item, entry.value))
                .ToList();
            float most = items.Count == 0 ? 0f : items.Max(entry => Mathf.Abs(entry.value));
            highest = stat.ValueToString(most, ToStringNumberSense.Absolute);
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doWindowBackground = true;
        }

        public override Vector2 InitialSize => new Vector2(Width, 4f * UiStyle.RowHeight + UiStyle.Gap + 2f * Margin);

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
            DrawDirection(new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight), "OL_Order_HigherFirst".Translate(), !line.lowerIsBetter);
            y += UiStyle.RowHeight;
            DrawDirection(new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight), "OL_Order_LowerFirst".Translate(), line.lowerIsBetter);
            y += UiStyle.RowHeight + UiStyle.Gap;

            Rect row = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            string label = "OL_LineDirection_CountsFrom".Translate();
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
            TooltipHandler.TipRegion(row, "OL_CountsFrom_Tip".Translate());
            float? typed = Typed();
            if (typed.HasValue)
            {
                float stored = FromShown(line.stat, Mathf.Max(0f, typed.Value));
                if (Mathf.Abs(stored - line.countsFrom) > Epsilon)
                {
                    setCountsFrom(stored);
                }
            }
            y += UiStyle.RowHeight;

            string note = "OL_CountsFrom_Highest".Translate(highest);
            List<ThingDef> under = line.countsFrom > 0f ? Under() : new List<ThingDef>();
            if (under.Count > 0)
            {
                note += " " + (under.Count == 1 ? "OL_CountsFrom_CountOne".Translate() : "OL_CountsFrom_Count".Translate(under.Count));
            }
            Rect noteRect = new Rect(inRect.x, y, inRect.width, UiStyle.RowHeight);
            UiStyle.Note(noteRect, note.Truncate(noteRect.width));
            TooltipHandler.TipRegion(noteRect, () => under.Count == 0 ? note : note + "\n\n" + UnderTip(under), line.GetHashCode());
        }

        private void DrawDirection(Rect row, string label, bool chosen)
        {
            if (Widgets.RadioButtonLabeled(row, label, chosen) && !chosen)
            {
                toggleDirection();
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

        private static string UnderTip(List<ThingDef> under)
        {
            string listed = string.Join("\n", under.Take(ItemsListed).Select(def => def.LabelCap.Resolve()));
            return under.Count > ItemsListed ? listed + "\n" + "OL_Forbid_AndMore".Translate(under.Count - ItemsListed) : listed;
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
