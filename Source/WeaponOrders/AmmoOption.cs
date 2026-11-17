using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>One ammo a Combat Extended gun can load: the ammo item, the bullet it fires, and its kind's name (FMJ, AP, HP).</summary>
    public sealed class AmmoOption
    {
        /// <summary>The ammo item colonists carry and make.</summary>
        public ThingDef Ammo { get; }

        /// <summary>The bullet it fires, which the gun's damage and penetration come from.</summary>
        public ThingDef Bullet { get; }

        /// <summary>The kind's short name, "AP", or its name when it has none.</summary>
        public string KindLabel { get; }

        public AmmoOption(ThingDef ammo, ThingDef bullet, string kindLabel)
        {
            Ammo = ammo;
            Bullet = bullet;
            KindLabel = kindLabel;
        }
    }
}
