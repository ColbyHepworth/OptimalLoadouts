using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders.Gui
{
    /// <summary>A weapon's name as the weapon screens show it, with the ammo it was judged on under Combat Extended: "Assault rifle (AP)".</summary>
    public static class WeaponLabel
    {
        public static string Of(ThingWithComps weapon, AmmoOption ammo)
        {
            // One made new for the preview has no quality worth showing, and CE's ammo set would crowd out the ammo.
            string name = (weapon.MapHeld != null ? weapon.LabelNoCount : GenLabel.ThingLabel(weapon.def, weapon.Stuff, 1)).CapitalizeFirst();
            return ammo?.KindLabel == null ? name : "OL_Weapon_WithAmmo".Translate(name, ammo.KindLabel).Resolve();
        }
    }
}
