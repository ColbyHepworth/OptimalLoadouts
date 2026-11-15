using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>How a stat adds up over what a colonist wears.</summary>
    public enum StatTotal
    {
        /// <summary>It adds up across the pieces worn: what a piece gives its wearer, insulation, mass, equip delay, value.</summary>
        Summed,

        /// <summary>It protects the body parts a piece covers: every armor rating a damage type is judged by.</summary>
        PerBodyPart,

        /// <summary>How long a piece lasts (hit points, wear, flammability), weighed by how much of the body it covers.</summary>
        ByCoverage,
    }

    /// <summary>
    /// What a colonist ends up with on a priority line, wearing a set of pieces: a plain total for what adds up across
    /// pieces, armor body part by body part as the game's Gear tab totals it, and how long pieces last weighed by the body
    /// each covers. Adding armor up piece by piece would let more pieces win whatever they cover.
    /// </summary>
    public static class OutfitTotals
    {
        private static HashSet<StatDef> armorStats;
        private static HashSet<StatDef> byCoverageStats;
        private static bool? combatExtended;

        /// <summary>For each item and body, which of the body's parts it covers, as indexes and as a flag per part.</summary>
        private static readonly Dictionary<(ThingDef def, BodyDef body), (int[] parts, bool[] covers)> coverage =
            new Dictionary<(ThingDef, BodyDef), (int[], bool[])>();

        /// <summary>For each item and body, the share of the body it covers, the sum of its parts' coverage.</summary>
        private static readonly Dictionary<(ThingDef def, BodyDef body), float> bodyShare = new Dictionary<(ThingDef, BodyDef), float>();

        /// <summary>The worn pieces covering each body part, for the outfit last worked out, rebuilt when the outfit changes.</summary>
        private static List<Apparel>[] wornByPart;
        private static int wornKey;
        private static BodyDef wornBody;

        // Working buffers for the game's armor rule, reused so a comparison makes no garbage.
        private static bool[] affected;
        private static readonly List<int> affectedParts = new List<int>();
        private static readonly List<Apparel> excluded = new List<Apparel>();
        private static readonly List<float> excludedFactors = new List<float>();
        private static readonly List<bool[]> excludedCovers = new List<bool[]>();
        private static readonly List<bool[]> afterCovers = new List<bool[]>();
        private static readonly List<bool[]> beforeCovers = new List<bool[]>();
        private static int wornThroughVersion;
        private static readonly List<float> afterFactors = new List<float>();

        /// <summary>For the worn outfit last indexed, each line's share of a shot getting past it on each body part.</summary>
        private static readonly Dictionary<OrderLine, float[]> wornThroughByLine = new Dictionary<OrderLine, float[]>();
        private static readonly List<float> beforeFactors = new List<float>();

        public static StatTotal KindOf(StatDef stat)
        {
            BuildKinds();
            if (armorStats.Contains(stat))
            {
                return StatTotal.PerBodyPart;
            }
            return byCoverageStats.Contains(stat) ? StatTotal.ByCoverage : StatTotal.Summed;
        }

        /// <summary>
        /// One piece's share of the colonist's total on the line, for ranking single pieces: its value, weighed by the share
        /// of the body it covers for armor and how long it lasts. A piece that covers no body part (a belt) counts its own
        /// value.
        /// </summary>
        public static float ShareOf(BodyDef body, Apparel piece, OrderLine line)
        {
            float value = ApparelComparison.ValueOnLine(piece, line);
            if (value == 0f || !line.IsStat || KindOf(line.stat) == StatTotal.Summed)
            {
                return value;
            }
            float share = BodyShare(piece.def, body);
            return share > 0f ? value * share : value;
        }

        /// <summary>
        /// How much the colonist's total on the line changes when they keep <paramref name="rest"/> of what they
        /// <paramref name="worn"/> and wear <paramref name="after"/> in place of <paramref name="before"/>.
        /// </summary>
        internal static float Change(BodyDef body, List<Apparel> worn, List<Apparel> rest, List<Apparel> after, List<Apparel> before, OrderLine line)
        {
            if (KindOf(line.stat) == StatTotal.PerBodyPart && !CombatExtended)
            {
                return GameArmorChange(body, worn, rest, after, before, line);
            }
            // Every other total is a sum over pieces, so what the colonist keeps counts the same on both sides.
            float change = 0f;
            for (int i = 0; i < after.Count; i++)
            {
                change += ShareOf(body, after[i], line);
            }
            for (int i = 0; i < before.Count; i++)
            {
                change -= ShareOf(body, before[i], line);
            }
            return change;
        }

        /// <summary>
        /// The change in the game's overall armor, as its Gear tab works it out: on each body part each layer stops a share of
        /// what got past the one above, parts weigh by their size, and the total is doubled to the armor stat's scale. Only
        /// the parts the two sides cover change, and the pieces kept on each part are found from the worn outfit's index.
        /// </summary>
        private static float GameArmorChange(BodyDef body, List<Apparel> worn, List<Apparel> rest, List<Apparel> after, List<Apparel> before,
            OrderLine line)
        {
            IndexWorn(body, worn);
            float[] wornThrough = WornThrough(line);
            excluded.Clear();
            for (int i = 0; i < worn.Count; i++)
            {
                if (!rest.Contains(worn[i]))
                {
                    excluded.Add(worn[i]);
                }
            }
            Factors(excluded, line, excludedFactors, excludedCovers, body);
            Factors(after, line, afterFactors, afterCovers, body);
            Factors(before, line, beforeFactors, beforeCovers, body);
            List<BodyPartRecord> allParts = body.AllParts;
            if (affected == null || affected.Length < allParts.Count)
            {
                affected = new bool[allParts.Count];
            }
            affectedParts.Clear();
            MarkParts(after, body);
            MarkParts(before, body);

            float change = 0f;
            foreach (int part in affectedParts)
            {
                affected[part] = false;
                float kept = Kept(wornThrough, part, body, line);
                float afterLeft = kept * Through(afterFactors, afterCovers, part);
                float beforeLeft = kept * Through(beforeFactors, beforeCovers, part);
                change += allParts[part].coverageAbs * (beforeLeft - afterLeft);
            }
            return change * 2f;
        }

        /// <summary>
        /// The share of a shot on the part that gets past the worn pieces the colonist keeps: what gets past everything worn,
        /// with the pieces taken off divided back out. A piece that stops every shot can't be divided out, so then the
        /// part is worked out from what's kept.
        /// </summary>
        private static float Kept(float[] wornThrough, int part, BodyDef body, OrderLine line)
        {
            float kept = wornThrough[part];
            for (int i = 0; i < excluded.Count; i++)
            {
                if (!excludedCovers[i][part])
                {
                    continue;
                }
                if (excludedFactors[i] <= 0f)
                {
                    kept = 1f;
                    foreach (Apparel piece in wornByPart[part])
                    {
                        if (!excluded.Contains(piece))
                        {
                            kept *= Factor(piece, line);
                        }
                    }
                    return kept;
                }
                kept /= excludedFactors[i];
            }
            return kept;
        }

        /// <summary>
        /// For each body part, the share of a shot that gets past everything worn, worked out once per worn outfit and line.
        /// </summary>
        private static float[] WornThrough(OrderLine line)
        {
            // A line's value or layers can change while the outfit stays the same.
            int version = ApparelOrdersComponent.Current?.Version ?? 0;
            if (version != wornThroughVersion)
            {
                wornThroughByLine.Clear();
                wornThroughVersion = version;
            }
            if (wornThroughByLine.TryGetValue(line, out float[] through))
            {
                return through;
            }
            through = new float[wornByPart.Length];
            for (int part = 0; part < wornByPart.Length; part++)
            {
                float left = 1f;
                foreach (Apparel piece in wornByPart[part])
                {
                    left *= Factor(piece, line);
                }
                through[part] = left;
            }
            wornThroughByLine[line] = through;
            return through;
        }

        /// <summary>The share of a shot on the part that gets past the side's pieces covering it.</summary>
        private static float Through(List<float> factors, List<bool[]> covers, int part)
        {
            float left = 1f;
            for (int i = 0; i < factors.Count; i++)
            {
                if (covers[i][part])
                {
                    left *= factors[i];
                }
            }
            return left;
        }

        /// <summary>Each piece's share of a shot that gets past it, and the body parts it covers, read once per comparison.</summary>
        private static void Factors(List<Apparel> side, OrderLine line, List<float> factors, List<bool[]> covers, BodyDef body)
        {
            factors.Clear();
            covers.Clear();
            for (int i = 0; i < side.Count; i++)
            {
                factors.Add(Factor(side[i], line));
                covers.Add(Coverage(side[i].def, body).covers);
            }
        }

        /// <summary>The share of a shot that gets past the piece, as the Gear tab reads its armor.</summary>
        private static float Factor(Apparel piece, OrderLine line)
        {
            return 1f - Mathf.Clamp01(ApparelComparison.ValueOnLine(piece, line) / 2f);
        }

        private static void MarkParts(List<Apparel> side, BodyDef body)
        {
            for (int i = 0; i < side.Count; i++)
            {
                foreach (int part in Coverage(side[i].def, body).parts)
                {
                    if (!affected[part])
                    {
                        affected[part] = true;
                        affectedParts.Add(part);
                    }
                }
            }
        }

        /// <summary>Lists the worn pieces covering each body part, again only when the worn outfit or the body changed.</summary>
        private static void IndexWorn(BodyDef body, List<Apparel> worn)
        {
            int key = worn.Count;
            for (int i = 0; i < worn.Count; i++)
            {
                key = key * 31 + RuntimeHelpers.GetHashCode(worn[i]);
            }
            if (wornByPart != null && key == wornKey && body == wornBody)
            {
                return;
            }
            int partCount = body.AllParts.Count;
            if (wornByPart == null || wornByPart.Length < partCount)
            {
                wornByPart = new List<Apparel>[partCount];
                for (int i = 0; i < partCount; i++)
                {
                    wornByPart[i] = new List<Apparel>();
                }
            }
            for (int i = 0; i < partCount; i++)
            {
                wornByPart[i].Clear();
            }
            for (int i = 0; i < worn.Count; i++)
            {
                foreach (int part in Coverage(worn[i].def, body).parts)
                {
                    wornByPart[part].Add(worn[i]);
                }
            }
            wornKey = key;
            wornBody = body;
            wornThroughByLine.Clear();
        }

        private static (int[] parts, bool[] covers) Coverage(ThingDef def, BodyDef body)
        {
            if (!coverage.TryGetValue((def, body), out (int[] parts, bool[] covers) result))
            {
                List<BodyPartRecord> allParts = body.AllParts;
                var covers = new bool[allParts.Count];
                var parts = new List<int>();
                for (int i = 0; i < allParts.Count; i++)
                {
                    if (def.apparel.CoversBodyPart(allParts[i]))
                    {
                        covers[i] = true;
                        parts.Add(i);
                    }
                }
                result = (parts.ToArray(), covers);
                coverage[(def, body)] = result;
            }
            return result;
        }

        /// <summary>The share of the body the item covers: its parts' coverage added up, as the info card's coverage is.</summary>
        private static float BodyShare(ThingDef def, BodyDef body)
        {
            if (!bodyShare.TryGetValue((def, body), out float share))
            {
                List<BodyPartRecord> allParts = body.AllParts;
                share = 0f;
                foreach (int part in Coverage(def, body).parts)
                {
                    share += allParts[part].coverageAbs;
                }
                bodyShare[(def, body)] = share;
            }
            return share;
        }

        /// <summary>Combat Extended stacks the layers on a part by taking each one's armor off the shot in turn, so they add up.</summary>
        private static bool CombatExtended
        {
            get
            {
                if (combatExtended == null)
                {
                    combatExtended = ModsConfig.IsActive("CETeam.CombatExtended");
                }
                return combatExtended.Value;
            }
        }

        private static void BuildKinds()
        {
            if (armorStats != null)
            {
                return;
            }
            armorStats = new HashSet<StatDef>(DefDatabase<DamageArmorCategoryDef>.AllDefsListForReading
                .Select(category => category.armorRatingStat)
                .Where(stat => stat != null));
            byCoverageStats = new HashSet<StatDef> { StatDefOf.MaxHitPoints, StatDefOf.DeteriorationRate, StatDefOf.Flammability };
        }
    }
}
