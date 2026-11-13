using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>How a piece's bonuses read: each in short, "+30 bulk capacity", a penalty in red.</summary>
    internal static class PieceBonusText
    {
        private const string Separator = ", ";

        public static string Of(StatDef stat, float value)
        {
            return "OL_Slot_GivesEntry".Translate(stat.ValueToString(value, ToStringNumberSense.Offset), stat.label).Resolve();
        }

        /// <summary>The bonuses in one line for a tooltip, a penalty in red.</summary>
        public static string List(IEnumerable<(StatDef stat, float value)> bonuses)
        {
            return string.Join(Separator, bonuses.Select(bonus =>
            {
                string text = Of(bonus.stat, bonus.value);
                return bonus.value < 0f ? text.Colorize(ColorLibrary.RedReadable) : text;
            }));
        }
    }
}
