using UnityEngine;
using Verse;

namespace OptimalLoadouts.Gui
{
    /// <summary>Spacing and small widgets shared by every panel, matched to the vanilla policy window.</summary>
    public static class UiStyle
    {
        /// <summary>The gap vanilla's policy window leaves between its parts.</summary>
        public const float Gap = 10f;

        public const float RowHeight = 28f;
        public const float IconSize = 24f;

        /// <summary>A line of grey explanatory text, the way vanilla writes hints.</summary>
        public static void Note(Rect rect, string text)
        {
            using (new TextBlock(ColoredText.SubtleGrayColor))
            {
                Widgets.Label(rect, text);
            }
        }

        /// <summary>A row that lights up under the mouse and reports a click.</summary>
        public static bool ClickableRow(Rect rect)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            return Widgets.ButtonInvisible(rect);
        }
    }
}
