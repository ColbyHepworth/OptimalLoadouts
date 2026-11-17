using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>Reads one number from a weapon's def and material, without making the weapon.</summary>
    public abstract class WeaponNumberWorker
    {
        public WeaponNumberDef def;

        /// <summary>
        /// The number for this weapon in this material (null for a weapon made of none), firing <paramref name="bullet"/>
        /// (null for the gun's first), or null when it has none.
        /// </summary>
        public abstract float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet);

        /// <summary>The bullet given, else the one a gun fires first, or null for a weapon that fires none.</summary>
        protected static ProjectileProperties Shot(ThingDef weapon, ThingDef bullet)
        {
            return weapon.IsRangedWeapon ? (bullet ?? CombatExtendedWeapons.FirstProjectile(weapon))?.projectile : null;
        }
    }

    /// <summary>A stat on the weapon, such as cooldown, mass, or CE's sway. Melee stats only count on melee weapons and ranged ones on guns.</summary>
    public class StatWeaponNumber : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            if ((def.group == WeaponNumberGroup.Ranged && !weapon.IsRangedWeapon) || (def.group == WeaponNumberGroup.Melee && !weapon.IsMeleeWeapon))
            {
                return null;
            }
            return weapon.GetStatValueAbstract(def.stat, stuff);
        }
    }

    /// <summary>Damage per shot: the bullet's damage with the weapon's damage multiplier, times its pellets under CE.</summary>
    public class DamagePerShot : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            ProjectileProperties shot = Shot(weapon, bullet);
            if (shot == null || shot.damageDef == null)
            {
                return null;
            }
            // Firefoam's extinguishing "damage" is 999999, and smoke and EMP hurt no one either.
            if (!shot.damageDef.harmsHealth)
            {
                return 0f;
            }
            return shot.GetDamageAmount(weapon, stuff) * CombatExtendedWeapons.Pellets(shot);
        }
    }

    /// <summary>
    /// Armor penetration of the bullet: CE's sharp penetration (mm RHA), or vanilla's penetration with the weapon's
    /// multiplier.
    /// </summary>
    public class ArmorPenetration : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            ProjectileProperties shot = Shot(weapon, bullet);
            if (shot == null || shot.damageDef == null)
            {
                return null;
            }
            float? sharp = CombatExtendedWeapons.SharpPenetration(shot);
            if (sharp != null)
            {
                return sharp;
            }
            return shot.GetArmorPenetration() * weapon.GetStatValueAbstract(StatDefOf.RangedWeapon_ArmorPenetrationMultiplier, stuff);
        }
    }

    /// <summary>CE's blunt penetration of the bullet (MPa).</summary>
    public class BluntPenetration : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            return CombatExtendedWeapons.BluntPenetration(Shot(weapon, bullet));
        }
    }

    public class FiringRange : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            return weapon.IsRangedWeapon ? CombatExtendedWeapons.PrimaryVerb(weapon)?.range : null;
        }
    }

    public class AimTime : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            return weapon.IsRangedWeapon ? CombatExtendedWeapons.PrimaryVerb(weapon)?.warmupTime : null;
        }
    }

    public class ShotsPerBurst : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            return weapon.IsRangedWeapon ? CombatExtendedWeapons.PrimaryVerb(weapon)?.burstShotCount : null;
        }
    }

    /// <summary>
    /// Melee armor penetration. Vanilla's stat follows the weapon's tools and material. CE's stat reads 0 outside its info
    /// card, so under CE it's the tools' sharp penetration weighted by how often each is used, times the material's
    /// penetration factor.
    /// </summary>
    public class MeleeArmorPenetration : WeaponNumberWorker
    {
        public override float? ValueFor(ThingDef weapon, ThingDef stuff, ThingDef bullet)
        {
            if (!weapon.IsMeleeWeapon || weapon.tools.NullOrEmpty())
            {
                return null;
            }
            if (!CombatExtendedWeapons.Active)
            {
                return def.stat == null ? (float?)null : weapon.GetStatValueAbstract(def.stat, stuff);
            }
            float weights = weapon.tools.Sum(tool => tool.chanceFactor);
            if (weights <= 0f)
            {
                return null;
            }
            float sum = 0f;
            foreach (Tool tool in weapon.tools)
            {
                float? sharp = CombatExtendedWeapons.ToolSharpPenetration(tool);
                if (sharp == null)
                {
                    return null;
                }
                sum += sharp.Value * tool.chanceFactor;
            }
            StatDef factor = DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");
            return sum / weights * (factor == null ? 1f : weapon.GetStatValueAbstract(factor, stuff));
        }
    }
}
