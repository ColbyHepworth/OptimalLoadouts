using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>Weapons made only to be ranked and shown, never spawned. One per weapon and material, kept until the game changes.</summary>
    public static class ImaginedWeapons
    {
        private static readonly Dictionary<(ThingDef def, ThingDef stuff), ThingWithComps> weapons =
            new Dictionary<(ThingDef, ThingDef), ThingWithComps>();

        /// <summary>A new weapon at normal quality and full hit points, or null when it can't be made outside a map.</summary>
        public static ThingWithComps For(ThingDef def, ThingDef stuff)
        {
            var key = (def, stuff);
            if (weapons.TryGetValue(key, out ThingWithComps weapon))
            {
                return weapon;
            }
            try
            {
                weapon = ThingMaker.MakeThing(def, stuff) as ThingWithComps;
                weapon?.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, null);
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[Optimal Loadouts] Couldn't imagine a {def.defName} for the weapon finder, so it's left out: {e.Message}",
                    ("OptimalLoadouts.ImagineWeapon" + def.defName).GetHashCode());
                weapon = null;
            }
            weapons[key] = weapon;
            return weapon;
        }

        public static void ClearCache()
        {
            weapons.Clear();
        }
    }
}
