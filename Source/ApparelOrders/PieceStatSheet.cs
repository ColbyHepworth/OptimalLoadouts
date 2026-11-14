using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// A piece's stats as its info card lists them, without the description. The game's own list is private and shared by
    /// whichever info card is open, so this builds its own the same way.
    /// </summary>
    public static class PieceStatSheet
    {
        private static Apparel lastPiece;
        private static ThingDef lastStuff;
        private static int lastHitPoints;
        private static List<StatDrawEntry> lastSheet = new List<StatDrawEntry>();

        /// <summary>
        /// The piece's stats: the def's and the piece's special stats, its hit points, then every stat the game shows for
        /// it (hidden ones and stats at their default left out, as the card does). Sorted by category display order, then
        /// the card's priority within it, then name. Remembered for the last piece.
        /// </summary>
        public static IReadOnlyList<StatDrawEntry> For(Apparel piece)
        {
            // A worn piece's condition changes while it's shown.
            if (piece == lastPiece && piece.Stuff == lastStuff && piece.HitPoints == lastHitPoints)
            {
                return lastSheet;
            }
            lastPiece = piece;
            lastStuff = piece.Stuff;
            lastHitPoints = piece.HitPoints;
            lastSheet = Build(piece);
            return lastSheet;
        }

        private static List<StatDrawEntry> Build(Apparel piece)
        {
            StatRequest request = StatRequest.For(piece);
            var entries = new List<StatDrawEntry>();
            entries.AddRange(Safely(() => piece.def.SpecialDisplayStats(request)));
            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                StatDrawEntry entry = StatEntry(piece, stat);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            if (piece.def.useHitPoints)
            {
                entries.Add(new StatDrawEntry(StatCategoryDefOf.BasicsImportant, "HitPointsBasic".Translate().CapitalizeFirst(),
                    piece.HitPoints + " / " + piece.MaxHitPoints, "Stat_HitPoints_Desc".Translate(), 99998));
            }
            entries.AddRange(Safely(piece.SpecialDisplayStats));
            return entries
                .Where(entry => entry?.category != null && (entry.stat == null || entry.stat.showNonAbstract) && entry.ShouldDisplay(piece))
                .OrderBy(entry => entry.category.displayOrder)
                .ThenByDescending(entry => entry.DisplayPriorityWithinCategory)
                .ThenBy(entry => entry.LabelCap)
                .ToList();
        }

        /// <summary>The stat's entry, or null when the card wouldn't show it.</summary>
        private static StatDrawEntry StatEntry(Apparel piece, StatDef stat)
        {
            try
            {
                if (!stat.Worker.ShouldShowFor(StatRequest.For(piece)))
                {
                    return null;
                }
                if (stat.Worker.IsDisabledFor(piece))
                {
                    return new StatDrawEntry(stat.category, stat);
                }
                float value = piece.GetStatValue(stat);
                if (!stat.showOnDefaultValue && value == stat.defaultBaseValue)
                {
                    return null;
                }
                return new StatDrawEntry(stat.category, stat, value, StatRequest.For(piece));
            }
            catch (Exception)
            {
                // A mod's stat worker that can't read a piece it never expected is left out, not shown as an error.
                return null;
            }
        }

        private static List<StatDrawEntry> Safely(Func<IEnumerable<StatDrawEntry>> entries)
        {
            try
            {
                return entries().ToList();
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't read some of a piece's stats: " + e, "OptimalLoadouts.StatSheet".GetHashCode());
                return new List<StatDrawEntry>();
            }
        }
    }
}
