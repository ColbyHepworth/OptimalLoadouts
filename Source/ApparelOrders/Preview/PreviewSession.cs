using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// What the outfit preview and slot browser show in the current game: the colonist, where pieces come from, the
    /// material, the predicted outfit with what the last change did, and the open slot. Kept while the game runs, not
    /// saved, so the choices survive closing the window and a loaded game starts fresh.
    /// </summary>
    public class PreviewSession
    {
        /// <summary>How often the pieces on offer are gathered again, so edits to the filter, stock and storage show up.</summary>
        private const float RefreshSeconds = 1f;

        private static readonly PieceSource[] Sources = { PieceSource.AnythingAllowed, PieceSource.CraftableNow, PieceSource.InStorage };

        private static PreviewSession current;

        private readonly Game game;
        private PieceSource source = PieceSource.AnythingAllowed;

        private readonly Dictionary<PieceSource, List<Apparel>> pieces = new Dictionary<PieceSource, List<Apparel>>();
        private float gatheredAt = float.MinValue;

        /// <summary>What the open slot was opened for. It closes when any of these changes, since its pieces no longer apply.</summary>
        private (Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material) slotOpenedFor;

        /// <summary>The slot opened last, so a new outfit opens the same slot when it has one.</summary>
        private OutfitSlot lastSlot;

        /// <summary>Whether the open slot is a layer opened while empty, so it turns into the slot of the piece that fills it.</summary>
        private bool openedEmptyLayer;

        private (Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material, int version) predictedFor;
        private List<Apparel> predictedFrom;

        /// <summary>Where the outfit list is scrolled to.</summary>
        public Vector2 OutfitScroll;

        private PreviewSession(Game game)
        {
            this.game = game;
            // These caches hold the last game's pawns and policies.
            OutfitPortrait.Forget();
            PieceSources.Forget();
        }

        /// <summary>The current game's session, or null outside a game.</summary>
        public static PreviewSession Current
        {
            get
            {
                Game now = Verse.Current.Game;
                if (now == null)
                {
                    return null;
                }
                if (current == null || current.game != now)
                {
                    current = new PreviewSession(now);
                }
                return current;
            }
        }

        /// <summary>The colonist shown, or null before a policy is shown.</summary>
        public Pawn Pawn { get; private set; }

        /// <summary>The policy shown, or null before one is.</summary>
        public ApparelPolicy Policy { get; private set; }

        public PieceSource Source
        {
            get => source;
            set
            {
                source = value;
                OutfitScroll = Vector2.zero;
            }
        }

        public MaterialChoice Material { get; set; } = MaterialChoice.BestForOrder;

        public Rot4 Rotation { get; set; } = Rot4.South;

        /// <summary>The outfit predicted for the colonist under the policy, or null before a policy is shown.</summary>
        public PredictedOutfit Outfit { get; private set; }

        /// <summary>What the last change to the outfit put on and took off, or null since the colonist, policy, tab or material changed.</summary>
        public OutfitChanges Changes { get; private set; }

        /// <summary>The open slot, or null.</summary>
        public SlotView Slot { get; private set; }

        /// <summary>The column the slot browser is sorted by, or null to sort the way the colonist would pick. Kept across slots.</summary>
        public StatDef SortStat { get; set; }

        public bool LowestFirst { get; set; }

        /// <summary>A stat shown as an extra column in the slot browser, kept across slots until it's removed.</summary>
        public StatDef AddedStat { get; set; }

        /// <summary>
        /// Shows the policy: keeps the chosen colonist if they're still among <paramref name="colonists"/>, else the
        /// first. Closes the slot when the colonist, policy, tab or material changed since it opened. Gathers pieces (at
        /// most once a second) and predicts again when anything the outfit depends on changed.
        /// </summary>
        public void Show(ApparelPolicy policy, List<Pawn> colonists)
        {
            if (!colonists.Contains(Pawn))
            {
                Pawn = colonists[0];
            }
            if (policy != Policy)
            {
                Policy = policy;
                OutfitScroll = Vector2.zero;
            }
            if (Slot != null && slotOpenedFor != (Pawn, Policy, source, Material))
            {
                CloseSlot();
            }
            GatherPieces();
            Predict();
            FollowFilledLayer();
        }

        public void ChoosePawn(Pawn pawn)
        {
            Pawn = pawn;
        }

        /// <summary>How many pieces a source offers, for its tab.</summary>
        public int PieceCount(PieceSource from)
        {
            return pieces.TryGetValue(from, out List<Apparel> offered) ? offered.Count : 0;
        }

        /// <summary>The outfit to draw: as predicted, or with the piece being tried on in place of its slot's.</summary>
        public IReadOnlyList<Apparel> Drawn
        {
            get
            {
                Apparel tried = Slot?.TriedOn;
                if (tried == null)
                {
                    return Outfit.Worn;
                }
                BodyDef body = Pawn.RaceProps.body;
                List<Apparel> drawn = Outfit.Worn.Where(worn => ApparelUtility.CanWearTogether(worn.def, tried.def, body)).ToList();
                drawn.Add(tried);
                return drawn;
            }
        }

        /// <summary>Opens a slot. With <paramref name="followWhenFilled"/>, a layer opened while empty turns into the slot of the piece that fills it.</summary>
        public void OpenSlot(OutfitSlot slot, bool followWhenFilled = false)
        {
            Slot = new SlotView(this, slot);
            slotOpenedFor = (Pawn, Policy, source, Material);
            lastSlot = slot;
            openedEmptyLayer = followWhenFilled;
        }

        public void CloseSlot()
        {
            Slot = null;
        }

        /// <summary>
        /// Where the slot has a panel of its own, it always shows one. Opens the slot picked last: a layer again, or a
        /// piece's place when a piece of the new outfit fills it. Otherwise the outermost worn piece's, or with nothing
        /// worn, the outermost layer's.
        /// </summary>
        public void KeepSlotOpen()
        {
            if (Slot != null)
            {
                return;
            }
            BodyDef body = Pawn.RaceProps.body;
            if (lastSlot != null && lastSlot.Layer != null)
            {
                OpenSlot(lastSlot, followWhenFilled: lastSlot.FilledBy(Outfit.Worn, body) == null);
                return;
            }
            List<OutfitLayer> layers = OutfitLayers.For(Pawn, Outfit.Worn);
            List<Apparel> worn = layers.SelectMany(layer => layer.Pieces).ToList();
            Apparel sameSlot = lastSlot == null ? null : lastSlot.FilledBy(worn, body);
            if (sameSlot != null || worn.Count > 0)
            {
                OpenSlot(OutfitSlot.Of(sameSlot ?? worn[0]));
            }
            else if (layers.Count > 0)
            {
                OpenSlot(OutfitSlot.OnLayer(layers[0].Layer), followWhenFilled: true);
            }
        }

        /// <summary>The policy's filter changed, so the pieces are gathered and the outfit predicted again now rather than at the next refresh.</summary>
        public void FilterChanged()
        {
            gatheredAt = float.MinValue;
        }

        /// <summary>
        /// A layer opened while empty turns into the slot of the piece that fills it, since pieces on one layer that cover
        /// different body parts (a shirt and trousers) don't compete.
        /// </summary>
        private void FollowFilledLayer()
        {
            OutfitSlot open = Slot?.Slot;
            if (open == null || open.Layer == null || !openedEmptyLayer)
            {
                return;
            }
            Apparel filling = open.FilledBy(Outfit.Worn, Pawn.RaceProps.body);
            if (filling != null)
            {
                OpenSlot(OutfitSlot.Of(filling));
            }
        }

        /// <summary>Gathers each source's pieces, and the open slot's, at most once a second, for the tab counts and the prediction.</summary>
        private void GatherPieces()
        {
            bool slotPiecesMissing = Slot != null && (Slot.Pieces == null || Slot.PiecesFrom != source);
            if (Pawn == predictedFor.pawn && Policy == predictedFor.policy && Material == predictedFor.material
                && Time.realtimeSinceStartup - gatheredAt < RefreshSeconds && !slotPiecesMissing)
            {
                return;
            }
            gatheredAt = Time.realtimeSinceStartup;
            if (Slot != null)
            {
                List<Apparel> slotPieces;
                try
                {
                    slotPieces = PieceSources.For(Pawn, Policy, source, Material, Slot.Slot);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Optimal Loadouts] Couldn't gather a slot's pieces for the preview: " + e, "OptimalLoadouts.GatherSlot".GetHashCode());
                    slotPieces = new List<Apparel>();
                }
                Slot.SetPieces(slotPieces, source);
            }
            foreach (PieceSource each in Sources)
            {
                try
                {
                    pieces[each] = PieceSources.For(Pawn, Policy, each, Material);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Optimal Loadouts] Couldn't gather pieces for the outfit preview: " + e, "OptimalLoadouts.Gather".GetHashCode());
                    pieces[each] = new List<Apparel>();
                }
            }
        }

        /// <summary>
        /// Predicts the outfit again when the pawn, policy, source, material or any order changes, when the pieces on
        /// offer change, and every refresh for storage, where reach and what's worn change too. A new outfit for the same
        /// pawn, policy, source and material is compared with the last, for the change marks.
        /// </summary>
        private void Predict()
        {
            int version = ApparelOrdersComponent.Current?.Version ?? 0;
            var key = (Pawn, Policy, source, Material, version);
            List<Apparel> offered = pieces[source];
            bool storageRefreshed = source == PieceSource.InStorage && offered != predictedFrom;
            if (Outfit != null && key == predictedFor && !storageRefreshed && SamePieces(offered, predictedFrom))
            {
                return;
            }
            PredictedOutfit before = Outfit;
            bool sameOutfit = before != null
                && (Pawn, Policy, source, Material) == (predictedFor.pawn, predictedFor.policy, predictedFor.source, predictedFor.material);
            predictedFor = key;
            predictedFrom = offered;
            try
            {
                Outfit = OutfitPredictor.Predict(Pawn, Policy, source, offered);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't predict an outfit for the preview: " + e, "OptimalLoadouts.Predict".GetHashCode());
                Outfit = new PredictedOutfit(new List<Apparel>(), new List<Apparel>(), new List<Apparel>(),
                    new List<Apparel>(), new List<PredictedSwap>(), true, false);
            }
            if (!sameOutfit)
            {
                Changes = null;
            }
            else
            {
                // An outfit that came out the same keeps the marks of the last change that made a difference.
                OutfitChanges fresh = OutfitChanges.Between(before, Outfit, Pawn.RaceProps.body);
                if (fresh.Any)
                {
                    Changes = fresh;
                }
            }
        }

        private static bool SamePieces(List<Apparel> a, List<Apparel> b)
        {
            if (a == b)
            {
                return true;
            }
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
