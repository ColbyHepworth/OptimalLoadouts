using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders;
using OptimalLoadouts.ApparelOrders.Preview;
using RimWorld;
using Verse;

namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>A weapon in a ranking, with its value on each of the role's weapon lines.</summary>
    public sealed class WeaponChoice
    {
        public ThingWithComps Weapon { get; }

        /// <summary>One per line, in the lines' order; null where the weapon has no such number.</summary>
        public IReadOnlyList<float?> Values { get; }

        /// <summary>Its place in the ranking, 0 for the best.</summary>
        public int Rank { get; internal set; }

        /// <summary>False for a weapon the policy forbids, which ranks after every allowed one and is never picked.</summary>
        public bool Allowed { get; }

        /// <summary>Under Combat Extended, the ammo the gun was judged on, which every number comes from. Null otherwise.</summary>
        public AmmoOption Ammo { get; }

        /// <summary>The bullet the numbers come from: the ammo's, or null for the gun's first.</summary>
        public ThingDef Bullet => Ammo?.Bullet;

        internal WeaponChoice(ThingWithComps weapon, IReadOnlyList<float?> values, bool allowed, AmmoOption ammo)
        {
            Weapon = weapon;
            Values = values;
            Allowed = allowed;
            Ammo = ammo;
        }
    }

    /// <summary>
    /// Which ammo a gun is judged on under Combat Extended: the one pinned on it, else the one that ranks best for the lines
    /// among the ammo a source offers.
    /// </summary>
    public sealed class AmmoPick
    {
        /// <summary>The ammo item pinned on a gun, or null, or no pins at all when null.</summary>
        public Func<ThingDef, ThingDef> Pinned { get; }

        /// <summary>Whether an ammo item is on offer from the source, or null for every ammo.</summary>
        public Func<ThingDef, bool> Available { get; }

        public AmmoPick(Func<ThingDef, ThingDef> pinned, Func<ThingDef, bool> available)
        {
            Pinned = pinned;
            Available = available;
        }
    }

    /// <summary>
    /// Ranks weapons for a role the way its apparel priorities rank pieces: the first line where two weapons differ
    /// decides, and a weapon with no such number loses to one that has it. Then quality, hit points and name.
    /// </summary>
    public static class WeaponRanking
    {
        /// <summary>Numbers that differ by less than this are equal.</summary>
        private const float Epsilon = 0.0001f;

        private static readonly Dictionary<(ThingDef weapon, ThingDef stuff, WeaponNumberDef number, ThingDef bullet), float?> values =
            new Dictionary<(ThingDef, ThingDef, WeaponNumberDef, ThingDef), float?>();

        /// <summary>
        /// The weapons ranked by the lines in order, best first. Under Combat Extended each gun is judged on the ammo
        /// <paramref name="ammo"/> picks (the best for the lines when null). Weapons of the wrong kind, and weapons a line
        /// rules out ("at least" or "at most" a value), are left out. Weapons in <paramref name="forbidden"/> follow every
        /// allowed one, marked, so they can be allowed again.
        /// </summary>
        public static List<WeaponChoice> Rank(IEnumerable<ThingWithComps> weapons, IReadOnlyList<WeaponLine> lines, WeaponKind kind,
            IReadOnlyCollection<ThingDef> forbidden = null, AmmoPick ammo = null)
        {
            List<WeaponChoice> ranked = weapons
                .Where(weapon => weapon != null && IsKind(weapon.def, kind))
                .Select(weapon =>
                {
                    AmmoOption chosen = ChooseAmmo(weapon.def, weapon.Stuff, lines, ammo);
                    List<float?> numbers = lines.Select(line => ValueOf(weapon.def, weapon.Stuff, line.number, chosen?.Bullet)).ToList();
                    return new WeaponChoice(weapon, numbers, forbidden == null || !forbidden.Contains(weapon.def), chosen);
                })
                .Where(choice => !RuledOut(choice, lines))
                .ToList();
            ranked.Sort((a, b) => a.Allowed != b.Allowed ? (a.Allowed ? -1 : 1) : Compare(a, b, lines));
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i;
            }
            return ranked;
        }

        /// <summary>
        /// The materials a weapon could be made of, in the order to try them: best first for the lines (Best), the game's
        /// default first (Typical), or the one named first. Empty for a weapon not made of one.
        /// </summary>
        public static List<ThingDef> MaterialsInOrder(ThingDef weapon, MaterialChoice choice, IReadOnlyList<WeaponLine> lines)
        {
            if (!weapon.MadeFromStuff)
            {
                return new List<ThingDef>();
            }
            ThingDef typical = GenStuff.DefaultStuffFor(weapon);
            List<ThingDef> ranked = GenStuff.AllowedStuffsFor(weapon)
                .OrderBy(stuff => stuff, Comparer<ThingDef>.Create((a, b) => CompareMaterials(weapon, a, b, lines)))
                .ThenBy(stuff => stuff == typical ? 0 : 1)
                .ToList();
            LimitMaterial limit = choice.Limit;
            ThingDef first = limit.IsBest ? null : limit.Stuff ?? typical;
            if (first != null && ranked.Remove(first))
            {
                ranked.Insert(0, first);
            }
            return ranked;
        }

        /// <summary>Whether a weapon is the kind a role carries.</summary>
        public static bool IsKind(ThingDef weapon, WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Ranged:
                    return weapon.IsRangedWeapon;
                case WeaponKind.Melee:
                    return weapon.IsMeleeWeapon;
                case WeaponKind.None:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// The ammo a gun is judged on: the one pinned on it, else the one on offer that ranks best for the lines, the gun's
        /// first breaking a tie, among those no line rules out when there are some. With none on offer, the gun's first.
        /// Null outside CE or for a gun without ammo.
        /// </summary>
        private static AmmoOption ChooseAmmo(ThingDef weapon, ThingDef stuff, IReadOnlyList<WeaponLine> lines, AmmoPick pick)
        {
            IReadOnlyList<AmmoOption> options = CombatExtendedWeapons.AmmoFor(weapon);
            if (options.Count == 0)
            {
                return null;
            }
            ThingDef pinned = pick?.Pinned?.Invoke(weapon);
            AmmoOption pinnedOption = pinned == null ? null : options.FirstOrDefault(option => option.Ammo == pinned);
            if (pinnedOption != null)
            {
                return pinnedOption;
            }
            List<AmmoOption> usable = options.Where(option => pick?.Available == null || pick.Available(option.Ammo)).ToList();
            if (usable.Count == 0)
            {
                // None on offer (no ammo researched yet): judged on what CE loads first, not on ammo nobody can have.
                return options[0];
            }
            // AP that passes "penetration at least 8" keeps the gun in, though HP does more damage.
            List<AmmoOption> passing = usable.Where(option => !lines.Any(line => line.RulesOut(ValueOf(weapon, stuff, line.number, option.Bullet)))).ToList();
            if (passing.Count > 0)
            {
                usable = passing;
            }
            // A stable sort, so the gun's own order breaks a tie.
            return usable.OrderBy(option => option, Comparer<AmmoOption>.Create((a, b) => CompareBullets(weapon, stuff, a.Bullet, b.Bullet, lines)))
                .First();
        }

        /// <summary>A weapon's number in a material, firing <paramref name="bullet"/> (null for its first), worked out once.</summary>
        public static float? ValueOf(ThingDef weapon, ThingDef stuff, WeaponNumberDef number, ThingDef bullet = null)
        {
            if (weapon == null || number == null)
            {
                return null;
            }
            var key = (weapon, stuff, number, bullet);
            if (!values.TryGetValue(key, out float? value))
            {
                try
                {
                    value = number.Worker.ValueFor(weapon, stuff, bullet);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce($"[Optimal Loadouts] Couldn't read {number.defName} for {weapon.defName}, so it reads as unknown: {e}",
                        ("OptimalLoadouts.WeaponNumber" + weapon.defName + number.defName).GetHashCode());
                    value = null;
                }
                values[key] = value;
            }
            return value;
        }

        public static void ClearCache()
        {
            values.Clear();
        }

        private static int CompareBullets(ThingDef weapon, ThingDef stuff, ThingDef a, ThingDef b, IReadOnlyList<WeaponLine> lines)
        {
            foreach (WeaponLine line in lines)
            {
                int byLine = CompareValues(line.Counted(ValueOf(weapon, stuff, line.number, a)), line.Counted(ValueOf(weapon, stuff, line.number, b)),
                    line.lowerIsBetter);
                if (byLine != 0)
                {
                    return byLine;
                }
            }
            return 0;
        }

        private static bool RuledOut(WeaponChoice choice, IReadOnlyList<WeaponLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].RulesOut(choice.Values[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static int Compare(WeaponChoice a, WeaponChoice b, IReadOnlyList<WeaponLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                int byLine = CompareValues(lines[i].Counted(a.Values[i]), lines[i].Counted(b.Values[i]), lines[i].lowerIsBetter);
                if (byLine != 0)
                {
                    return byLine;
                }
            }
            int byQuality = QualityOf(b.Weapon).CompareTo(QualityOf(a.Weapon));
            if (byQuality != 0)
            {
                return byQuality;
            }
            int byHitPoints = b.Weapon.HitPoints.CompareTo(a.Weapon.HitPoints);
            return byHitPoints != 0 ? byHitPoints : string.Compare(a.Weapon.LabelNoCount, b.Weapon.LabelNoCount, StringComparison.CurrentCulture);
        }

        private static int CompareMaterials(ThingDef weapon, ThingDef a, ThingDef b, IReadOnlyList<WeaponLine> lines)
        {
            foreach (WeaponLine line in lines)
            {
                int byLine = CompareValues(line.Counted(ValueOf(weapon, a, line.number)), line.Counted(ValueOf(weapon, b, line.number)), line.lowerIsBetter);
                if (byLine != 0)
                {
                    return byLine;
                }
            }
            return 0;
        }

        /// <summary>Negative when <paramref name="a"/> ranks first. A known number beats an unknown one.</summary>
        private static int CompareValues(float? a, float? b, bool lowerIsBetter)
        {
            if (a == null || b == null)
            {
                return a == null && b == null ? 0 : a == null ? 1 : -1;
            }
            float delta = a.Value - b.Value;
            if (Math.Abs(delta) <= Epsilon)
            {
                return 0;
            }
            bool aBetter = lowerIsBetter ? delta < 0f : delta > 0f;
            return aBetter ? -1 : 1;
        }

        private static int QualityOf(Thing weapon)
        {
            return weapon.TryGetQuality(out QualityCategory quality) ? (int)quality : -1;
        }
    }
}
