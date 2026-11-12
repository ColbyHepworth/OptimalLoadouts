using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// One slot of an outfit the slot browser can open: the place of a worn piece (everything that can't be worn
    /// together with it, by the game's own test), or a layer (every piece on it), so an empty layer can be opened too.
    /// </summary>
    public sealed class OutfitSlot
    {
        /// <summary>The piece whose place this is, or null for a layer slot.</summary>
        public Apparel Piece { get; }

        /// <summary>The layer, or null for a piece slot.</summary>
        public ApparelLayerDef Layer { get; }

        private OutfitSlot(Apparel piece, ApparelLayerDef layer)
        {
            Piece = piece;
            Layer = layer;
        }

        /// <summary>Everything that can't be worn together with the piece.</summary>
        public static OutfitSlot Of(Apparel piece)
        {
            return new OutfitSlot(piece, null);
        }

        /// <summary>Every piece on the layer.</summary>
        public static OutfitSlot OnLayer(ApparelLayerDef layer)
        {
            return new OutfitSlot(null, layer);
        }

        /// <summary>Whether an item competes for this slot.</summary>
        public bool Holds(ThingDef def, BodyDef body)
        {
            if (def.apparel == null)
            {
                return false;
            }
            if (Piece != null)
            {
                return !ApparelUtility.CanWearTogether(def, Piece.def, body);
            }
            return def.apparel.layers != null && def.apparel.layers.Contains(Layer);
        }

        /// <summary>The worn piece in the slot now, or null.</summary>
        public Apparel FilledBy(IReadOnlyList<Apparel> outfit, BodyDef body)
        {
            for (int i = 0; i < outfit.Count; i++)
            {
                if (Holds(outfit[i].def, body))
                {
                    return outfit[i];
                }
            }
            return null;
        }
    }
}
