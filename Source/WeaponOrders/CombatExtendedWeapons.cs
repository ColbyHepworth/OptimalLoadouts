using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>
    /// What a weapon's numbers need from Combat Extended, read by type and field name so the mod keeps no reference to its
    /// DLL: a gun's first ammo, its bullet's penetration and pellets, and a melee tool's penetration. A field that's
    /// missing (a CE update renamed it) logs one warning, and its numbers read as unknown.
    /// </summary>
    public static class CombatExtendedWeapons
    {
        private static bool? active;
        private static bool looked;
        private static Type ammoUserType;
        private static FieldInfo ammoSetField;
        private static FieldInfo ammoTypesField;
        private static FieldInfo linkProjectileField;
        private static FieldInfo sharpField;
        private static FieldInfo bluntField;
        private static FieldInfo pelletsField;
        private static FieldInfo toolSharpField;
        private static FieldInfo linkAmmoField;
        private static FieldInfo ammoClassField;
        private static FieldInfo labelShortField;
        private static readonly List<AmmoOption> NoAmmo = new List<AmmoOption>();
        private static readonly Dictionary<ThingDef, List<AmmoOption>> ammoFor = new Dictionary<ThingDef, List<AmmoOption>>();

        public static bool Active
        {
            get
            {
                if (active == null)
                {
                    active = ModsConfig.IsActive("CETeam.CombatExtended");
                }
                return active.Value;
            }
        }

        /// <summary>The bullet a gun fires first: its ammo set's first ammo under CE, else its primary verb's projectile. Null for a weapon that fires none.</summary>
        public static ThingDef FirstProjectile(ThingDef weapon)
        {
            ThingDef fromVerb = PrimaryVerb(weapon)?.defaultProjectile;
            if (!Active || !Look())
            {
                return fromVerb;
            }
            object ammoUser = weapon.comps?.Find(comp => ammoUserType.IsInstanceOfType(comp));
            object ammoSet = ammoUser == null ? null : ammoSetField.GetValue(ammoUser);
            if (ammoSet == null)
            {
                return fromVerb;
            }
            var ammoTypes = ammoTypesField.GetValue(ammoSet) as IList;
            if (ammoTypes == null || ammoTypes.Count == 0 || ammoTypes[0] == null)
            {
                return fromVerb;
            }
            return linkProjectileField.GetValue(ammoTypes[0]) as ThingDef ?? fromVerb;
        }

        /// <summary>
        /// Every ammo a CE gun can load, in its ammo set's order (the first is the one CE loads first), each with its bullet
        /// and its kind (FMJ, AP, HP). Empty outside CE or for a gun without an ammo set. Worked out once per gun.
        /// </summary>
        public static IReadOnlyList<AmmoOption> AmmoFor(ThingDef weapon)
        {
            if (weapon == null || !Active || !Look())
            {
                return NoAmmo;
            }
            if (ammoFor.TryGetValue(weapon, out List<AmmoOption> options))
            {
                return options;
            }
            options = new List<AmmoOption>();
            object ammoUser = weapon.comps?.Find(comp => ammoUserType.IsInstanceOfType(comp));
            object ammoSet = ammoUser == null ? null : ammoSetField.GetValue(ammoUser);
            if (ammoSet != null && ammoTypesField.GetValue(ammoSet) is IList links)
            {
                foreach (object link in links)
                {
                    var ammo = link == null ? null : linkAmmoField.GetValue(link) as ThingDef;
                    var bullet = link == null ? null : linkProjectileField.GetValue(link) as ThingDef;
                    if (ammo == null || bullet == null)
                    {
                        continue;
                    }
                    var kind = ammoClassField.GetValue(ammo) as Def;
                    string shortLabel = kind == null ? null : labelShortField.GetValue(kind) as string;
                    options.Add(new AmmoOption(ammo, bullet, shortLabel.NullOrEmpty() ? kind?.label : shortLabel));
                }
            }
            ammoFor[weapon] = options;
            return options;
        }

        /// <summary>A CE bullet's sharp penetration (mm RHA), or null outside CE or for a vanilla bullet.</summary>
        public static float? SharpPenetration(ProjectileProperties bullet)
        {
            return BulletField(bullet, sharpField);
        }

        /// <summary>A CE bullet's blunt penetration (MPa), or null outside CE or for a vanilla bullet.</summary>
        public static float? BluntPenetration(ProjectileProperties bullet)
        {
            return BulletField(bullet, bluntField);
        }

        /// <summary>How many pellets a CE bullet splits into, 1 for anything else.</summary>
        public static int Pellets(ProjectileProperties bullet)
        {
            if (!Active || !Look() || bullet == null || !pelletsField.DeclaringType.IsInstanceOfType(bullet))
            {
                return 1;
            }
            return Math.Max(1, (int)pelletsField.GetValue(bullet));
        }

        /// <summary>A CE melee tool's sharp penetration, or null outside CE or for a vanilla tool.</summary>
        public static float? ToolSharpPenetration(Tool tool)
        {
            if (!Active || !Look() || tool == null || !toolSharpField.DeclaringType.IsInstanceOfType(tool))
            {
                return null;
            }
            return Convert.ToSingle(toolSharpField.GetValue(tool));
        }

        /// <summary>The verb a weapon fires with, as the info card reads it.</summary>
        public static VerbProperties PrimaryVerb(ThingDef weapon)
        {
            return weapon?.Verbs?.Find(verb => verb.isPrimary);
        }

        private static float? BulletField(ProjectileProperties bullet, FieldInfo field)
        {
            if (!Active || !Look() || bullet == null || !field.DeclaringType.IsInstanceOfType(bullet))
            {
                return null;
            }
            return Convert.ToSingle(field.GetValue(bullet));
        }

        /// <summary>Finds CE's types and fields once. False when any is missing.</summary>
        private static bool Look()
        {
            if (!looked)
            {
                looked = true;
                ammoUserType = AccessTools.TypeByName("CombatExtended.CompProperties_AmmoUser");
                ammoSetField = ammoUserType == null ? null : AccessTools.Field(ammoUserType, "ammoSet");
                Type ammoSetType = AccessTools.TypeByName("CombatExtended.AmmoSetDef");
                ammoTypesField = ammoSetType == null ? null : AccessTools.Field(ammoSetType, "ammoTypes");
                Type linkType = AccessTools.TypeByName("CombatExtended.AmmoLink");
                linkProjectileField = linkType == null ? null : AccessTools.Field(linkType, "projectile");
                linkAmmoField = linkType == null ? null : AccessTools.Field(linkType, "ammo");
                Type ammoType = AccessTools.TypeByName("CombatExtended.AmmoDef");
                ammoClassField = ammoType == null ? null : AccessTools.Field(ammoType, "ammoClass");
                Type categoryType = AccessTools.TypeByName("CombatExtended.AmmoCategoryDef");
                labelShortField = categoryType == null ? null : AccessTools.Field(categoryType, "labelShort");
                Type bulletType = AccessTools.TypeByName("CombatExtended.ProjectilePropertiesCE");
                sharpField = bulletType == null ? null : AccessTools.Field(bulletType, "armorPenetrationSharp");
                bluntField = bulletType == null ? null : AccessTools.Field(bulletType, "armorPenetrationBlunt");
                pelletsField = bulletType == null ? null : AccessTools.Field(bulletType, "pelletCount");
                Type toolType = AccessTools.TypeByName("CombatExtended.ToolCE");
                toolSharpField = toolType == null ? null : AccessTools.Field(toolType, "armorPenetrationSharp");
                if (!Found)
                {
                    Log.Warning("[Optimal Loadouts] Couldn't find Combat Extended's ammo, bullet or tool fields, so its weapon numbers read as unknown.");
                }
            }
            return Found;
        }

        private static bool Found => ammoUserType != null && ammoSetField != null && ammoTypesField != null && linkProjectileField != null
            && sharpField != null && bluntField != null && pelletsField != null && toolSharpField != null
            && linkAmmoField != null && ammoClassField != null && labelShortField != null;
    }
}
