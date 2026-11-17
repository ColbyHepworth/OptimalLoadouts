using System;
using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>
    /// A number a weapon can be ranked on, such as its range or armor penetration, read from its def and material by its
    /// worker. A def, so a weapon priority line can save it and its label can be translated.
    /// </summary>
    public class WeaponNumberDef : Def
    {
        /// <summary>Which weapons the number is about, and its group in the picker.</summary>
        public WeaponNumberGroup group = WeaponNumberGroup.General;

        /// <summary>The direction a new line on this number starts with.</summary>
        public bool lowerIsBetter;

        /// <summary>
        /// False for a number Combat Extended takes away, such as vanilla's accuracy. A number only Combat Extended has is
        /// a def with MayRequire on it, so it isn't loaded without it.
        /// </summary>
        public bool withCombatExtended = true;

        public Type workerClass = typeof(StatWeaponNumber);

        /// <summary>The stat a <see cref="StatWeaponNumber"/> reads, and vanilla's stat for <see cref="MeleeArmorPenetration"/>.</summary>
        public StatDef stat;

        /// <summary>How many decimals the number shows with.</summary>
        public int decimals = 1;

        /// <summary>Shown as a percent without Combat Extended, as the info card shows vanilla's penetration and accuracy.</summary>
        public bool percentWithoutCombatExtended;

        private WeaponNumberWorker worker;

        public WeaponNumberWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    worker = (WeaponNumberWorker)Activator.CreateInstance(workerClass);
                    worker.def = this;
                }
                return worker;
            }
        }

        /// <summary>Whether the number means something with the mods loaded now, so the picker offers it.</summary>
        public bool Offered => withCombatExtended || !CombatExtendedWeapons.Active;

        /// <summary>The number shown the way the browser and tooltips show it.</summary>
        public string Format(float value)
        {
            return percentWithoutCombatExtended && !CombatExtendedWeapons.Active ? value.ToStringPercent() : value.ToString("F" + decimals);
        }

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (workerClass == null || !typeof(WeaponNumberWorker).IsAssignableFrom(workerClass))
            {
                yield return "workerClass must be a WeaponNumberWorker";
            }
            if (workerClass == typeof(StatWeaponNumber) && stat == null)
            {
                yield return "a StatWeaponNumber needs a stat";
            }
        }
    }
}
