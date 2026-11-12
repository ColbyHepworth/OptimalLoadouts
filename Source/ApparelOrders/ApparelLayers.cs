using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>The apparel layers the mod's lines and limits can count on, and which of them are on the head.</summary>
    public static class ApparelLayers
    {
        private static List<ApparelLayerDef> worn;
        private static HashSet<ApparelLayerDef> onHead;

        /// <summary>Every layer some apparel is worn on, Combat Extended's and other mods' included, in the order the game draws them.</summary>
        public static IReadOnlyList<ApparelLayerDef> Worn
        {
            get
            {
                Load();
                return worn;
            }
        }

        /// <summary>Whether the layer is on the head: most apparel on it is headgear by the game's own test.</summary>
        public static bool OnHead(ApparelLayerDef layer)
        {
            Load();
            return onHead.Contains(layer);
        }

        /// <summary>The game's test of headgear (full head, upper head or eyes), true when it holds for most of the apparel on one layer.</summary>
        public static bool MostlyHeadgear(List<ThingDef> apparelOnLayer)
        {
            return apparelOnLayer.Count(PawnApparelGenerator.IsHeadgear) * 2 > apparelOnLayer.Count;
        }

        private static void Load()
        {
            if (worn != null)
            {
                return;
            }
            var byLayer = new Dictionary<ApparelLayerDef, List<ThingDef>>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsApparel || def.apparel.layers == null)
                {
                    continue;
                }
                foreach (ApparelLayerDef layer in def.apparel.layers)
                {
                    if (!byLayer.TryGetValue(layer, out List<ThingDef> defs))
                    {
                        defs = new List<ThingDef>();
                        byLayer[layer] = defs;
                    }
                    defs.Add(def);
                }
            }
            worn = byLayer.Keys.OrderBy(layer => layer.drawOrder).ThenBy(layer => layer.label).ToList();
            onHead = new HashSet<ApparelLayerDef>(byLayer.Where(layer => MostlyHeadgear(layer.Value)).Select(layer => layer.Key));
        }
    }
}
