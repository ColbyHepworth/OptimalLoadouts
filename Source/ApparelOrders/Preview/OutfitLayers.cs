using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>One layer of the outfit as the list shows it, with the worn pieces whose outermost layer it is.</summary>
    public sealed class OutfitLayer
    {
        public ApparelLayerDef Layer { get; }

        /// <summary>Empty when nothing is listed on the layer.</summary>
        public IReadOnlyList<Apparel> Pieces { get; }

        /// <summary>Worn pieces listed under an outer layer that sit on this one too, such as a duster on the middle layer.</summary>
        public IReadOnlyList<Apparel> CoveredBy { get; }

        /// <summary>Whether it's a head layer: most of the apparel on it is headgear.</summary>
        public bool OnHead { get; }

        /// <summary>
        /// Whether it's listed right under the layer of the piece covering it, rather than in its own place: a layer
        /// with no pieces of its own that a piece listed elsewhere covers.
        /// </summary>
        public bool Nested { get; }

        public OutfitLayer(ApparelLayerDef layer, IReadOnlyList<Apparel> pieces, IReadOnlyList<Apparel> coveredBy, bool onHead,
            bool nested)
        {
            Layer = layer;
            Pieces = pieces;
            CoveredBy = coveredBy;
            OnHead = onHead;
            Nested = nested;
        }
    }

    /// <summary>
    /// The outfit by layer: every layer the pawn could wear something on, head layers first, each outermost first. A layer
    /// only a piece listed elsewhere covers sits right under that piece's layer.
    /// </summary>
    public static class OutfitLayers
    {
        private static readonly List<Apparel> None = new List<Apparel>();

        /// <summary>For each gender and life stage, the apparel on each layer and the head layers, so a pawn's layers are found quickly.</summary>
        private static readonly Dictionary<(Gender gender, DevelopmentalStage stage), LayerApparel> apparelByLayer =
            new Dictionary<(Gender, DevelopmentalStage), LayerApparel>();

        private sealed class LayerApparel
        {
            public readonly Dictionary<ApparelLayerDef, List<ThingDef>> ByLayer = new Dictionary<ApparelLayerDef, List<ThingDef>>();
            public readonly HashSet<ApparelLayerDef> OnHead = new HashSet<ApparelLayerDef>();
        }

        /// <summary>
        /// Each layer with the worn pieces whose outermost layer it is. A piece on several layers is listed under its
        /// outermost, and each of its other layers names it in <see cref="OutfitLayer.CoveredBy"/>. Those with no pieces
        /// of their own are <see cref="OutfitLayer.Nested"/> right under it.
        /// </summary>
        public static List<OutfitLayer> For(Pawn pawn, IReadOnlyList<Apparel> worn)
        {
            var byLayer = new Dictionary<ApparelLayerDef, List<Apparel>>();
            var coveredBy = new Dictionary<ApparelLayerDef, List<Apparel>>();
            foreach (Apparel piece in worn)
            {
                ApparelLayerDef outermost = Outermost(piece.def);
                Add(byLayer, outermost, piece);
                foreach (ApparelLayerDef layer in piece.def.apparel.layers)
                {
                    if (layer != outermost)
                    {
                        Add(coveredBy, layer, piece);
                    }
                }
            }
            LayerApparel apparel = ApparelByLayer(pawn);
            List<OutfitLayer> ordered = WearableLayers(pawn, apparel)
                .Concat(byLayer.Keys)
                .Concat(coveredBy.Keys)
                .Distinct()
                .Select(layer =>
                {
                    List<Apparel> pieces = byLayer.TryGetValue(layer, out List<Apparel> listed)
                        ? listed.OrderBy(piece => piece.LabelCap.ToString()).ToList()
                        : None;
                    List<Apparel> covering = coveredBy.TryGetValue(layer, out List<Apparel> over) ? over : None;
                    // A layer no apparel of this pawn's kind is on goes by what's worn there.
                    bool onHead = apparel.ByLayer.ContainsKey(layer)
                        ? apparel.OnHead.Contains(layer)
                        : pieces.Concat(covering).Any(piece => PawnApparelGenerator.IsHeadgear(piece.def));
                    return new OutfitLayer(layer, pieces, covering, onHead, nested: pieces.Count == 0 && covering.Count > 0);
                })
                .OrderBy(row => row.OnHead ? 0 : 1)
                .ThenByDescending(row => row.Layer.drawOrder)
                .ThenBy(row => row.Layer.label)
                .ToList();
            return Nest(ordered);
        }

        /// <summary>Moves each nested layer to right under the layer its first covering piece is listed under, keeping their order.</summary>
        private static List<OutfitLayer> Nest(List<OutfitLayer> ordered)
        {
            var result = ordered.Where(row => !row.Nested).ToList();
            foreach (OutfitLayer nested in ordered.Where(row => row.Nested))
            {
                Apparel cover = nested.CoveredBy[0];
                int under = result.FindIndex(row => row.Pieces.Contains(cover));
                if (under < 0)
                {
                    result.Add(nested);
                    continue;
                }
                int at = under + 1;
                while (at < result.Count && result[at].Nested)
                {
                    at++;
                }
                result.Insert(at, nested);
            }
            return result;
        }

        private static void Add(Dictionary<ApparelLayerDef, List<Apparel>> lists, ApparelLayerDef layer, Apparel piece)
        {
            if (!lists.TryGetValue(layer, out List<Apparel> pieces))
            {
                pieces = new List<Apparel>();
                lists[layer] = pieces;
            }
            pieces.Add(piece);
        }

        /// <summary>The layer a piece is listed under: the outermost it's worn on.</summary>
        private static ApparelLayerDef Outermost(ThingDef def)
        {
            return def.apparel.layers.OrderByDescending(layer => layer.drawOrder).First();
        }

        /// <summary>Every layer some apparel the pawn could wear is on: by their gender, life stage and the body parts they have.</summary>
        private static IEnumerable<ApparelLayerDef> WearableLayers(Pawn pawn, LayerApparel apparel)
        {
            foreach (KeyValuePair<ApparelLayerDef, List<ThingDef>> layer in apparel.ByLayer)
            {
                if (layer.Value.Any(def => ApparelUtility.HasPartsToWear(pawn, def)))
                {
                    yield return layer.Key;
                }
            }
        }

        /// <summary>The apparel of the pawn's gender and life stage on each layer, and the head layers, worked out once for each.</summary>
        private static LayerApparel ApparelByLayer(Pawn pawn)
        {
            var key = (pawn.gender, pawn.DevelopmentalStage);
            if (apparelByLayer.TryGetValue(key, out LayerApparel apparel))
            {
                return apparel;
            }
            apparel = new LayerApparel();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsApparel || def.apparel.layers == null
                    || (def.apparel.gender != Gender.None && def.apparel.gender != pawn.gender)
                    || !def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage))
                {
                    continue;
                }
                foreach (ApparelLayerDef layer in def.apparel.layers)
                {
                    if (!apparel.ByLayer.TryGetValue(layer, out List<ThingDef> defs))
                    {
                        defs = new List<ThingDef>();
                        apparel.ByLayer[layer] = defs;
                    }
                    defs.Add(def);
                }
            }
            foreach (KeyValuePair<ApparelLayerDef, List<ThingDef>> layer in apparel.ByLayer)
            {
                if (ApparelLayers.MostlyHeadgear(layer.Value))
                {
                    apparel.OnHead.Add(layer.Key);
                }
            }
            apparelByLayer[key] = apparel;
            return apparel;
        }
    }
}
