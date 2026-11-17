using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>
    /// The weapons a role could carry, from the same sources as the outfit preview's pieces: every weapon, those the
    /// colony has researched, those it can make now, or the real ones in storage.
    /// </summary>
    public static class WeaponSources
    {
        private static List<ThingDef> weaponDefs;

        /// <summary>
        /// The candidates from a source. Weapons made new come in the first material the choice ranks for the lines (the
        /// first the colony can afford, for those it can make now). Real ones are in storage and not forbidden.
        /// </summary>
        public static List<ThingWithComps> For(Map map, PieceSource source, MaterialChoice material, IReadOnlyList<WeaponLine> lines)
        {
            if (source == PieceSource.InStorage)
            {
                return InStorage(map);
            }
            var weapons = new List<ThingWithComps>();
            foreach (ThingDef def in WeaponDefs())
            {
                if ((source == PieceSource.Researched && !CraftableApparel.IsResearched(def, map))
                    || (source == PieceSource.CraftableNow && !CraftableApparel.HasRecipeNow(def, map)))
                {
                    continue;
                }
                ThingWithComps weapon = Imagined(def, material, lines, source == PieceSource.CraftableNow ? map : null);
                if (weapon != null)
                {
                    weapons.Add(weapon);
                }
            }
            return weapons;
        }

        /// <summary>
        /// Weapons a colonist can carry: primary equipment that isn't a turret's gun, a mech's weapon that vanishes when
        /// dropped, a material or drink that can be swung (wood logs, beer), or a gun whose shots do less than 1 damage
        /// (firefoam, smoke, EMP, CE's binoculars).
        /// </summary>
        public static IReadOnlyList<ThingDef> WeaponDefs()
        {
            if (weaponDefs == null)
            {
                var turretGuns = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading
                    .Select(def => def.building?.turretGunDef).Where(gun => gun != null));
                weaponDefs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => def.IsWeapon && def.equipmentType == EquipmentType.Primary && !def.destroyOnDrop && !turretGuns.Contains(def)
                        && !def.IsStuff && !def.IsIngestible && !DoesNoDamage(def))
                    .ToList();
            }
            return weaponDefs;
        }

        /// <summary>
        /// The ammo a source offers, as its weapons: every ammo, ammo the colony has researched, ammo it can make now, or
        /// ammo in storage that isn't forbidden.
        /// </summary>
        public static AmmoPick AmmoFrom(PieceSource source, Map map, Func<ThingDef, ThingDef> pinned)
        {
            switch (source)
            {
                case PieceSource.Researched:
                    return new AmmoPick(pinned, ammo => CraftableApparel.IsResearched(ammo, map));
                case PieceSource.CraftableNow:
                    return new AmmoPick(pinned, ammo => CraftableApparel.HasRecipeNow(ammo, map));
                case PieceSource.InStorage:
                    return new AmmoPick(pinned, ammo => map.listerThings.ThingsOfDef(ammo).Any(thing => thing.IsInAnyStorage() && !thing.IsForbidden(Faction.OfPlayer)));
                default:
                    return new AmmoPick(pinned, null);
            }
        }

        /// <summary>
        /// A gun whose first shot does less than 1 damage: firefoam, smoke and EMP, which harm no one, and spotting tools
        /// such as CE's binoculars, whose dummy bullet does 0.
        /// </summary>
        private static bool DoesNoDamage(ThingDef def)
        {
            ProjectileProperties bullet = def.IsRangedWeapon ? CombatExtendedWeapons.FirstProjectile(def)?.projectile : null;
            if (bullet?.damageDef == null)
            {
                return false;
            }
            return !bullet.damageDef.harmsHealth || bullet.GetDamageAmount(def, null) * CombatExtendedWeapons.Pellets(bullet) < 1;
        }

        /// <summary>Each weapon's materials in order, worked out once per material choice and order change, since the preview gathers every second.</summary>
        private static readonly Dictionary<(ThingDef weapon, MaterialChoice choice), List<ThingDef>> materialOrders =
            new Dictionary<(ThingDef, MaterialChoice), List<ThingDef>>();

        private static (IReadOnlyList<WeaponLine> lines, int version) materialOrdersFor;

        private static List<ThingDef> MaterialsInOrder(ThingDef def, MaterialChoice material, IReadOnlyList<WeaponLine> lines)
        {
            int version = ApparelOrders.ApparelOrdersComponent.Current?.Version ?? 0;
            if (materialOrdersFor != (lines, version))
            {
                materialOrders.Clear();
                materialOrdersFor = (lines, version);
            }
            if (!materialOrders.TryGetValue((def, material), out List<ThingDef> ordered))
            {
                ordered = WeaponRanking.MaterialsInOrder(def, material, lines);
                materialOrders[(def, material)] = ordered;
            }
            return ordered;
        }

        private static ThingWithComps Imagined(ThingDef def, MaterialChoice material, IReadOnlyList<WeaponLine> lines, Map stockFrom)
        {
            if (!def.MadeFromStuff)
            {
                return stockFrom != null && !CraftableApparel.CanAfford(def, null, stockFrom) ? null : ImaginedWeapons.For(def, null);
            }
            foreach (ThingDef stuff in MaterialsInOrder(def, material, lines))
            {
                if (stockFrom == null || CraftableApparel.CanAfford(def, stuff, stockFrom))
                {
                    return ImaginedWeapons.For(def, stuff);
                }
            }
            return null;
        }

        private static List<ThingWithComps> InStorage(Map map)
        {
            var known = new HashSet<ThingDef>(WeaponDefs());
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .OfType<ThingWithComps>()
                .Where(weapon => known.Contains(weapon.def) && weapon.IsInAnyStorage() && !weapon.IsForbidden(Faction.OfPlayer) && !weapon.IsBurning())
                .ToList();
        }
    }
}
