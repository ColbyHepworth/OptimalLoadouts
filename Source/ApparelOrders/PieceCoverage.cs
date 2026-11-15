using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Where on the body an item sits: each of its layers on each of its body part groups, the data the game's own test
    /// of what can be worn together reads.
    /// </summary>
    public static class PieceCoverage
    {
        private static readonly Dictionary<ThingDef, List<(ApparelLayerDef layer, BodyPartGroupDef group)>> byDef =
            new Dictionary<ThingDef, List<(ApparelLayerDef layer, BodyPartGroupDef group)>>();

        private static readonly Dictionary<(ThingDef, ThingDef, BodyDef), bool> wearableTogether = new Dictionary<(ThingDef, ThingDef, BodyDef), bool>();

        private static readonly Dictionary<(ThingDef, BodyPartGroupDef), bool> covers = new Dictionary<(ThingDef, BodyPartGroupDef), bool>();

        public static IReadOnlyList<(ApparelLayerDef layer, BodyPartGroupDef group)> Of(ThingDef def)
        {
            if (!byDef.TryGetValue(def, out List<(ApparelLayerDef layer, BodyPartGroupDef group)> coverage))
            {
                coverage = new List<(ApparelLayerDef layer, BodyPartGroupDef group)>();
                ApparelProperties apparel = def.apparel;
                if (apparel?.layers != null && apparel.bodyPartGroups != null)
                {
                    foreach (ApparelLayerDef layer in apparel.layers)
                    {
                        foreach (BodyPartGroupDef group in apparel.bodyPartGroups)
                        {
                            coverage.Add((layer, group));
                        }
                    }
                }
                byDef[def] = coverage;
            }
            return coverage;
        }

        /// <summary>The game's own test of whether two items can be worn together, remembered, since it never changes for two items.</summary>
        public static bool CanWearTogether(ThingDef a, ThingDef b, BodyDef body)
        {
            if (!wearableTogether.TryGetValue((a, b, body), out bool together))
            {
                together = ApparelUtility.CanWearTogether(a, b, body);
                wearableTogether[(a, b, body)] = together;
            }
            return together;
        }

        /// <summary>
        /// Whether the item covers the body part as its info card says: it covers some outer part of the human body in that
        /// group. A helmet listed for the full head covers the eyes, since the eyes are in the full head too.
        /// </summary>
        public static bool Covers(ThingDef item, BodyPartGroupDef part)
        {
            if (!covers.TryGetValue((item, part), out bool result))
            {
                result = item.apparel?.bodyPartGroups != null && BodyDefOf.Human.AllParts.Any(record =>
                    record.depth == BodyPartDepth.Outside && record.groups.Contains(part) && item.apparel.CoversBodyPart(record));
                covers[(item, part)] = result;
            }
            return result;
        }

        /// <summary>Adds what the pieces cover to <paramref name="area"/>.</summary>
        public static void AddTo(HashSet<(ApparelLayerDef layer, BodyPartGroupDef group)> area, IEnumerable<Apparel> pieces)
        {
            foreach (Apparel piece in pieces)
            {
                area.UnionWith(Of(piece.def));
            }
        }
    }
}
