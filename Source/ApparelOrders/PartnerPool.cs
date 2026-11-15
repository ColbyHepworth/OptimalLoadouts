using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// The pieces a comparison may complete a set from, while it's in use: the outfit preview's pieces, or the stored
    /// pieces the game's apparel check is weighing. Without one in use, pieces are judged alone, as the game does.
    /// The outfit doesn't change while a pool is in use, so each candidate item's partners are worked out once.
    /// </summary>
    public sealed class PartnerPool : IDisposable
    {
        /// <summary>A side gets at most this many partners, enough for a shirt and trousers in place of a one-piece suit.</summary>
        private const int MaxPartners = 2;

        private static readonly List<Apparel> NoPartners = new List<Apparel>();

        private static PartnerPool current;

        private readonly PartnerPool previous;
        private readonly Func<IEnumerable<Apparel>> pieces;
        private readonly Dictionary<ThingDef, (List<Apparel> forCandidate, List<Apparel> forReplaced)> byCandidate =
            new Dictionary<ThingDef, (List<Apparel> forCandidate, List<Apparel> forReplaced)>();
        /// <summary>Partners that won but that the colonist can't reach or another mod marks "never wear this".</summary>
        private readonly HashSet<Apparel> rejected = new HashSet<Apparel>();

        /// <summary>The worn pieces' scores, as the apparel check reads them, for testing a partner's own score.</summary>
        private List<float> wornScores;

        /// <summary>True while a partner's own score is worked out, which judges it alone.</summary>
        private bool suspended;

        /// <summary>Each piece's values down the priorities, read once, and only for pieces that could fill a gap.</summary>
        private readonly Dictionary<Apparel, float[]> values = new Dictionary<Apparel, float[]>();

        private readonly Dictionary<Apparel, bool> usable = new Dictionary<Apparel, bool>();

        /// <summary>The pool's pieces of each item, under every place on the body the item covers. Built when first needed.</summary>
        private Dictionary<(ApparelLayerDef layer, BodyPartGroupDef group), List<List<Apparel>>> byCell;

        private PartnerPool(Pawn pawn, Func<IEnumerable<Apparel>> pieces, OutfitSlot area)
        {
            Pawn = pawn;
            this.pieces = pieces;
            Area = area;
            previous = current;
        }

        public Pawn Pawn { get; }

        /// <summary>A slot to complete a piece to even when it takes nothing off, for the slot browser's ranking. Usually null.</summary>
        public OutfitSlot Area { get; }

        /// <summary>
        /// Comparisons for the pawn inside the using complete sets from these pieces. With an <paramref name="area"/>, a
        /// piece that takes nothing off is completed to cover the slot too.
        /// </summary>
        public static PartnerPool Use(Pawn pawn, IReadOnlyList<Apparel> pieces, OutfitSlot area = null)
        {
            return Use(pawn, () => pieces, area);
        }

        /// <summary>The same, reading the pieces only when a set is first completed, for a list the caller fills afterwards.</summary>
        public static PartnerPool Use(Pawn pawn, Func<IEnumerable<Apparel>> pieces, OutfitSlot area = null)
        {
            current = new PartnerPool(pawn, pieces, area);
            return current;
        }

        public void Dispose()
        {
            if (current == this)
            {
                current = previous;
            }
        }

        /// <summary>The pool in use for the pawn, or null.</summary>
        internal static PartnerPool For(Pawn pawn)
        {
            return current != null && current.Pawn == pawn && !current.suspended ? current : null;
        }

        /// <summary>
        /// The partners for each side when <paramref name="candidate"/> takes off <paramref name="replaced"/>: the best
        /// pool pieces for the area the other side covers and this one doesn't. The area is what both sides cover, and
        /// the pool's slot. The pieces it takes off get none when there are none, since then it's compared with nothing.
        /// </summary>
        internal (List<Apparel> forCandidate, List<Apparel> forReplaced) Partners(Apparel candidate, List<Apparel> replaced,
            List<Apparel> rest, IReadOnlyList<OrderLine> lines)
        {
            // What a piece takes off depends only on its item, and the outfit is fixed while the pool is in use.
            if (byCandidate.TryGetValue(candidate.def, out (List<Apparel> forCandidate, List<Apparel> forReplaced) known))
            {
                return known;
            }
            var area = new HashSet<(ApparelLayerDef layer, BodyPartGroupDef group)>(PieceCoverage.Of(candidate.def));
            PieceCoverage.AddTo(area, replaced);
            if (Area?.Piece != null)
            {
                area.UnionWith(PieceCoverage.Of(Area.Piece.def));
            }
            List<Apparel> forCandidate = Complete(new List<Apparel> { candidate }, area, rest, lines);
            List<Apparel> forReplaced = replaced.Count == 0 ? NoPartners : Complete(replaced, area, rest, lines);
            byCandidate[candidate.def] = (forCandidate, forReplaced);
            return (forCandidate, forReplaced);
        }

        /// <summary>The best pieces to add to <paramref name="side"/> so it covers what it leaves of <paramref name="area"/>.</summary>
        private List<Apparel> Complete(List<Apparel> side, HashSet<(ApparelLayerDef layer, BodyPartGroupDef group)> area,
            List<Apparel> rest, IReadOnlyList<OrderLine> lines)
        {
            var left = new HashSet<(ApparelLayerDef layer, BodyPartGroupDef group)>(area);
            foreach (Apparel piece in side)
            {
                left.ExceptWith(PieceCoverage.Of(piece.def));
            }
            if (left.Count == 0)
            {
                return NoPartners;
            }
            var partners = new List<Apparel>();
            var completed = new List<Apparel>(side);
            for (int i = 0; i < MaxPartners && left.Count > 0; i++)
            {
                Apparel best = Best(completed, left, rest, lines);
                if (best == null)
                {
                    break;
                }
                partners.Add(best);
                completed.Add(best);
                left.ExceptWith(PieceCoverage.Of(best.def));
            }
            return partners;
        }

        /// <summary>
        /// The best piece covering some of <paramref name="left"/> that can be worn with the side and the rest of the
        /// outfit: down the priorities, then by the game's own score. Only the winner is tested for reach and for being
        /// put on at all, which cost a path search and a score, and one that fails gives way to the next.
        /// </summary>
        private Apparel Best(List<Apparel> side, HashSet<(ApparelLayerDef layer, BodyPartGroupDef group)> left, List<Apparel> rest,
            IReadOnlyList<OrderLine> lines)
        {
            Index();
            ApparelPolicy policy = PreviewPolicy.PolicyOf(Pawn);
            BodyDef body = Pawn.RaceProps.body;
            while (true)
            {
                Apparel best = null;
                float bestRaw = float.NaN;
                var seen = new HashSet<ThingDef>();
                foreach ((ApparelLayerDef layer, BodyPartGroupDef group) cell in left)
                {
                    if (!byCell.TryGetValue(cell, out List<List<Apparel>> items))
                    {
                        continue;
                    }
                    foreach (List<Apparel> copies in items)
                    {
                        if (!seen.Add(copies[0].def) || !WearableWith(copies[0].def, side, body) || !WearableWith(copies[0].def, rest, body))
                        {
                            continue;
                        }
                        Apparel piece = BestCopy(copies, policy, lines);
                        if (piece == null)
                        {
                            continue;
                        }
                        int byOrder = best == null ? 1 : ByOrder(piece, best, lines);
                        if (byOrder < 0)
                        {
                            continue;
                        }
                        if (byOrder == 0)
                        {
                            // The game's own score settles a tie, and is only worked out when one needs settling.
                            if (float.IsNaN(bestRaw))
                            {
                                bestRaw = JobGiver_OptimizeApparel.ApparelScoreRaw(Pawn, best);
                            }
                            float raw = JobGiver_OptimizeApparel.ApparelScoreRaw(Pawn, piece);
                            if (raw <= bestRaw)
                            {
                                continue;
                            }
                            bestRaw = raw;
                        }
                        else
                        {
                            bestRaw = float.NaN;
                        }
                        best = piece;
                    }
                }
                if (best == null || WouldPutOn(best))
                {
                    return best;
                }
                rejected.Add(best);
            }
        }

        /// <summary>
        /// Whether the colonist would put the partner on when its turn came: in reach, and not marked "never wear this" by
        /// another mod's score (an ideoligion that abhors leather), judged alone so the test can't count partners itself.
        /// </summary>
        private bool WouldPutOn(Apparel partner)
        {
            if (partner.MapHeld != null && !OutfitPredictor.CanPutOn(Pawn, partner, fromStorage: true))
            {
                return false;
            }
            if (wornScores == null)
            {
                wornScores = Pawn.apparel.WornApparel.Select(worn => JobGiver_OptimizeApparel.ApparelScoreRaw(Pawn, worn)).ToList();
            }
            suspended = true;
            try
            {
                return JobGiver_OptimizeApparel.ApparelScoreGain(Pawn, partner, wornScores) > ApparelScoreBands.NeverWear;
            }
            finally
            {
                suspended = false;
            }
        }

        /// <summary>The best usable copy of an item that hasn't been turned down, or null.</summary>
        private Apparel BestCopy(List<Apparel> copies, ApparelPolicy policy, IReadOnlyList<OrderLine> lines)
        {
            Apparel best = null;
            foreach (Apparel copy in copies)
            {
                if (rejected.Contains(copy) || !IsUsable(copy, policy))
                {
                    continue;
                }
                if (best == null || ByOrder(copy, best, lines) > 0)
                {
                    best = copy;
                }
            }
            return best;
        }

        /// <summary>The pool's pieces of each item, under every place on the body it covers. Which are usable is worked out as they come up.</summary>
        private void Index()
        {
            if (byCell != null)
            {
                return;
            }
            byCell = new Dictionary<(ApparelLayerDef layer, BodyPartGroupDef group), List<List<Apparel>>>();
            foreach (IGrouping<ThingDef, Apparel> item in pieces().Where(piece => piece?.def.apparel != null).GroupBy(piece => piece.def))
            {
                List<Apparel> copies = item.ToList();
                foreach ((ApparelLayerDef layer, BodyPartGroupDef group) cell in PieceCoverage.Of(item.Key))
                {
                    if (!byCell.TryGetValue(cell, out List<List<Apparel>> items))
                    {
                        items = new List<List<Apparel>>();
                        byCell[cell] = items;
                    }
                    items.Add(copies);
                }
            }
        }

        private bool IsUsable(Apparel piece, ApparelPolicy policy)
        {
            if (!usable.TryGetValue(piece, out bool result))
            {
                result = Usable(piece, policy);
                usable[piece] = result;
            }
            return result;
        }

        /// <summary>The game's own tests for a piece the apparel check would consider, and the policy allowing it.</summary>
        private bool Usable(Apparel piece, ApparelPolicy policy)
        {
            if (policy == null || !policy.filter.Allows(piece) || GameApparelRules.CantWear(Pawn, piece)
                || !OutfitPredictor.CanPutOn(Pawn, piece, fromStorage: false))
            {
                return false;
            }
            if (piece.MapHeld == null)
            {
                return true;
            }
            Gender gender = piece.def.apparel.gender;
            return piece.IsInAnyStorage() && !piece.IsForbidden(Pawn) && !piece.IsBurning() && (gender == Gender.None || gender == Pawn.gender);
        }

        private static bool WearableWith(ThingDef def, List<Apparel> others, BodyDef body)
        {
            for (int i = 0; i < others.Count; i++)
            {
                if (others[i].def == def || !PieceCoverage.CanWearTogether(def, others[i].def, body))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Positive when <paramref name="a"/> is better down the priorities, negative when worse, 0 on a tie.</summary>
        private int ByOrder(Apparel a, Apparel b, IReadOnlyList<OrderLine> lines)
        {
            float[] valuesA = ValuesOf(a, lines);
            float[] valuesB = ValuesOf(b, lines);
            for (int i = 0; i < lines.Count; i++)
            {
                OrderLine line = lines[i];
                float delta = valuesA[i] - valuesB[i];
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
                if (Math.Abs(delta) > 0.0001f)
                {
                    return delta > 0f ? 1 : -1;
                }
            }
            return 0;
        }

        private float[] ValuesOf(Apparel piece, IReadOnlyList<OrderLine> lines)
        {
            if (!values.TryGetValue(piece, out float[] onLines))
            {
                onLines = new float[lines.Count];
                for (int i = 0; i < lines.Count; i++)
                {
                    onLines[i] = ApparelComparison.ValueOnLine(piece, lines[i]);
                }
                values[piece] = onLines;
            }
            return onLines;
        }
    }
}
