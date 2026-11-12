using UnityEngine;
using Verse;

namespace OptimalLoadouts.Gui
{
    /// <summary>Spacing and small widgets shared by every panel, matched to the vanilla policy window.</summary>
    [StaticConstructorOnStartup]
    public static class UiStyle
    {
        /// <summary>The gap vanilla's policy window leaves between its parts.</summary>
        public const float Gap = 10f;

        public const float RowHeight = 28f;
        public const float IconSize = 24f;

        /// <summary>The space between icon buttons in a row.</summary>
        public const float IconGap = 4f;

        /// <summary>The space above a heading inside a list, such as the lines a policy gets from its base.</summary>
        public const float HeadingGap = 4f;

        /// <summary>A band across a list in the tiny font, such as a layer's name above its pieces.</summary>
        public const float BandHeight = 22f;

        /// <summary>How far a list's items sit in from the band they're under.</summary>
        public const float Indent = 12f;

        /// <summary>The width of one bracket beside a list, such as the layers a piece covers.</summary>
        public const float BracketWidth = 8f;

        /// <summary>The width a vertical scrollbar takes from a scroll view.</summary>
        public const float ScrollbarWidth = 16f;

        /// <summary>The icons vanilla's pawn tables show on the column they're sorted by.</summary>
        public static readonly Texture2D SortAscendingIcon = ContentFinder<Texture2D>.Get("UI/Icons/Sorting");

        public static readonly Texture2D SortDescendingIcon = ContentFinder<Texture2D>.Get("UI/Icons/SortingDescending");

        /// <summary>Vanilla's lock, as the ideoligion and gene screens mark what's locked. Drawn faint when unlocked.</summary>
        public static readonly Texture2D LockIcon = ContentFinder<Texture2D>.Get("UI/Overlays/Locked");

        /// <summary>How faint a lock is drawn on a row that isn't locked.</summary>
        public const float UnlockedAlpha = 0.25f;

        /// <summary>A band's fill: the window's darker background, against the lighter section the list sits on.</summary>
        public static readonly Color BandColor = Widgets.WindowBGFillColor;

        /// <summary>Faint backgrounds for a row that was just added or just taken away.</summary>
        public static readonly Color AddedColor = Faint(ColorLibrary.Green);

        public static readonly Color RemovedColor = Faint(ColorLibrary.Red);

        private static Color Faint(Color color)
        {
            return new Color(color.r, color.g, color.b, 0.15f);
        }

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
