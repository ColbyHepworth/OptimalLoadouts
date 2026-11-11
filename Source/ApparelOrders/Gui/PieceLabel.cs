using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>Short names for pieces in the preview's lists.</summary>
    public static class PieceLabel
    {
        /// <summary>
        /// The piece's name and material, with its condition when worn down, and its quality only when it isn't normal:
        /// imagined pieces are all normal, so "(normal)" on every row would only crowd the list.
        /// </summary>
        public static string Of(Apparel piece)
        {
            bool showQuality = piece.TryGetQuality(out QualityCategory quality) && quality != QualityCategory.Normal;
            return GenLabel.ThingLabel(piece, 1, includeHp: true, includeQuality: showQuality).CapitalizeFirst();
        }
    }
}
