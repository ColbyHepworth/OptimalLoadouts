using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// One open slot of the previewed outfit: its pieces, the piece tried on, and its rows in the order they keep while
    /// boxes are ticked, so the rank numbers and the highlight on the pawn's pick move instead.
    /// </summary>
    public class SlotView
    {
        private readonly PreviewSession session;

        private List<SlotChoice> ranked = new List<SlotChoice>();
        private (Pawn pawn, ApparelPolicy policy, IReadOnlyList<Apparel> outfit, List<Apparel> pieces, StatDef sortStat, bool lowestFirst,
            int version) rankedFor;

        /// <summary>The rows' order, set when the slot opens or the sort changes, and kept while boxes are ticked.</summary>
        private List<object> rowOrder = new List<object>();

        private (StatDef sortStat, bool lowestFirst) rowOrderFor;

        /// <summary>Where the slot's list is scrolled to.</summary>
        public Vector2 Scroll;

        internal SlotView(PreviewSession session, OutfitSlot slot)
        {
            this.session = session;
            Slot = slot;
        }

        public OutfitSlot Slot { get; }

        /// <summary>The search box over the slot's list.</summary>
        public QuickSearchWidget Search { get; } = new QuickSearchWidget();

        /// <summary>The open tab's pieces for the slot, allowed or not, or null until they're gathered.</summary>
        public List<Apparel> Pieces { get; private set; }

        /// <summary>The source the pieces were gathered from.</summary>
        public PieceSource PiecesFrom { get; private set; }

        /// <summary>The piece drawn on the pawn in place of the slot's, or null to draw the outfit as predicted.</summary>
        public Apparel TriedOn { get; private set; }

        /// <summary>Tries the piece on, or takes it off again when it's the one tried on.</summary>
        public void TryOn(Apparel piece)
        {
            TriedOn = piece == TriedOn ? null : piece;
        }

        internal void SetPieces(List<Apparel> pieces, PieceSource from)
        {
            Pieces = pieces;
            PiecesFrom = from;
        }

        /// <summary>
        /// The pieces ranked the way the pawn would pick, or by the session's sorted column, in the kept row order: pieces
        /// already listed keep their place, new ones join at the end.
        /// </summary>
        public List<SlotChoice> Ranked()
        {
            int version = ApparelOrdersComponent.Current?.Version ?? 0;
            var key = (session.Pawn, session.Policy, session.Outfit.Worn, Pieces, session.SortStat, session.LowestFirst, version);
            if (key == rankedFor)
            {
                return ranked;
            }
            rankedFor = key;
            List<SlotChoice> fresh;
            try
            {
                fresh = SlotRanking.Rank(session.Pawn, session.Policy, session.Outfit.Worn, Slot, Pieces, session.SortStat, session.LowestFirst);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't rank a slot's pieces for the preview: " + e, "OptimalLoadouts.RankSlot".GetHashCode());
                fresh = new List<SlotChoice>();
            }
            if (rowOrderFor != (session.SortStat, session.LowestFirst))
            {
                rowOrderFor = (session.SortStat, session.LowestFirst);
                rowOrder = new List<object>();
            }
            var place = new Dictionary<object, int>();
            for (int i = 0; i < rowOrder.Count; i++)
            {
                place[rowOrder[i]] = i;
            }
            ranked = fresh
                .Select((choice, rank) => (choice, at: place.TryGetValue(RowKey(choice.Piece), out int kept) ? kept : rowOrder.Count + rank))
                .OrderBy(entry => entry.at)
                .Select(entry => entry.choice)
                .ToList();
            rowOrder = ranked.Select(choice => RowKey(choice.Piece)).ToList();
            return ranked;
        }

        /// <summary>
        /// What makes a row the same row from one gathering to the next. Imagined pieces stand for their item type (the
        /// material can change when it's allowed or forbidden). Real pieces stand for their item, material, quality and
        /// taint, as identical stored copies share a row.
        /// </summary>
        public static object RowKey(Apparel piece)
        {
            if (piece.MapHeld == null)
            {
                return piece.def;
            }
            int quality = piece.TryGetQuality(out QualityCategory category) ? (int)category : -1;
            return (piece.def, piece.Stuff, quality, piece.WornByCorpse);
        }
    }
}
