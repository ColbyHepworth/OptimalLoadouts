using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Pieces made only to be judged and drawn, never spawned. One per item type, material and quality, kept until the
    /// game changes, so turning the pawn or switching colonists reuses them.
    /// </summary>
    public static class ImaginedPieces
    {
        private static readonly Dictionary<(ThingDef def, ThingDef stuff, QualityCategory quality), Apparel> pieces =
            new Dictionary<(ThingDef, ThingDef, QualityCategory), Apparel>();

        /// <summary>A new piece at full hit points, or null when the item can't be made outside a map.</summary>
        public static Apparel For(ThingDef def, ThingDef stuff, QualityCategory quality)
        {
            var key = (def, stuff, quality);
            if (pieces.TryGetValue(key, out Apparel piece))
            {
                return piece;
            }
            try
            {
                piece = ThingMaker.MakeThing(def, stuff) as Apparel;
                piece?.TryGetComp<CompQuality>()?.SetQuality(quality, null);
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[Optimal Loadouts] Couldn't imagine a {def.defName} for the outfit preview, so it's left out: {e.Message}",
                    ("OptimalLoadouts.Imagine" + def.defName).GetHashCode());
                piece = null;
            }
            pieces[key] = piece;
            return piece;
        }

        /// <summary>Drops every piece, so a loaded game doesn't keep the previous one's.</summary>
        public static void Forget()
        {
            pieces.Clear();
        }
    }
}
