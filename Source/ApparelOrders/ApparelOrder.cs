using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using OptimalLoadouts.WeaponOrders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// An apparel policy's own priorities, the base policy whose priorities go under them, the items its priorities
    /// that rule pieces out unticked in the policy's checklist, and the weapon its colonists carry and its priorities.
    /// </summary>
    public class ApparelOrder : IExposable
    {
        public ApparelPolicy policy;
        public ApparelPolicy basePolicy;
        public List<OrderLine> lines = new List<OrderLine>();

        /// <summary>The items this policy's ruling-out priorities unticked, so they tick back when none catches them.</summary>
        public List<ThingDef> unticked = new List<ThingDef>();

        /// <summary>The material the policy's cutoffs judge in, or null for the usual one (or the best, with <see cref="limitBest"/>).</summary>
        public ThingDef limitStuff;

        public bool limitBest = true;

        /// <summary>Whether pawns on the policy add a piece to an empty slot only when it helps the priorities.</summary>
        public bool onlyWhatHelps;

        /// <summary>The policy's own weapon priorities, apart from its apparel lines so apparel scoring never sees them.</summary>
        public List<WeaponLine> weaponLines = new List<WeaponLine>();

        /// <summary>What the policy's colonists carry. Any takes the base's choice.</summary>
        public WeaponKind weaponKind = WeaponKind.Any;

        /// <summary>Weapons the policy's colonists never get, unticked in the weapon browser. The policy's own, like its checklist.</summary>
        public List<ThingDef> forbiddenWeapons = new List<ThingDef>();

        /// <summary>
        /// Under Combat Extended, the ammo a gun is judged on for this policy, pinned on the gun in the weapon browser. A gun
        /// with no pin is judged on its ammo that ranks best for the weapon priorities.
        /// </summary>
        public Dictionary<ThingDef, ThingDef> pinnedAmmo = new Dictionary<ThingDef, ThingDef>();

        /// <summary>Which material the policy's cutoffs judge apparel in and its preview imagines pieces in.</summary>
        public LimitMaterial LimitMaterial
        {
            get => limitBest ? LimitMaterial.Best : LimitMaterial.Of(limitStuff);
            set
            {
                limitBest = value.IsBest;
                limitStuff = value.Stuff;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref policy, "policy");
            Scribe_References.Look(ref basePolicy, "base");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Collections.Look(ref unticked, "unticked", LookMode.Def);
            Scribe_Defs.Look(ref limitStuff, "limitStuff");
            Scribe_Values.Look(ref limitBest, "limitBest", true);
            Scribe_Values.Look(ref onlyWhatHelps, "onlyWhatHelps", false);
            Scribe_Collections.Look(ref weaponLines, "weaponLines", LookMode.Deep);
            Scribe_Values.Look(ref weaponKind, "weaponKind", WeaponKind.Any);
            Scribe_Collections.Look(ref forbiddenWeapons, "forbiddenWeapons", LookMode.Def);
            Scribe_Collections.Look(ref pinnedAmmo, "pinnedAmmo", LookMode.Def, LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (forbiddenWeapons == null)
                {
                    forbiddenWeapons = new List<ThingDef>();
                }
                forbiddenWeapons.RemoveAll(def => def == null);
                if (pinnedAmmo == null)
                {
                    pinnedAmmo = new Dictionary<ThingDef, ThingDef>();
                }
                // A gun or ammo from a mod that's gone.
                pinnedAmmo.RemoveAll(pin => pin.Key == null || pin.Value == null);
                if (weaponLines == null)
                {
                    weaponLines = new List<WeaponLine>();
                }
                // A number from a mod that's gone, such as one only Combat Extended has.
                weaponLines.RemoveAll(line => line?.number == null);
                if (lines == null)
                {
                    lines = new List<OrderLine>();
                }
                lines.RemoveAll(line => line == null || line.Subject == null);
                // Locked items rank above every stat, so they're kept first, as moving lines keeps them.
                lines = lines.Where(line => line.IsItem).Concat(lines.Where(line => !line.IsItem)).ToList();
                if (unticked == null)
                {
                    unticked = new List<ThingDef>();
                }
                unticked.RemoveAll(def => def == null);
            }
        }
    }
}
