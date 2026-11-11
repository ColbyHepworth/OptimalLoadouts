using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>The pieces a preview picks from, for each source: imagined new ones or the real ones in storage.</summary>
    public static class PieceSources
    {
        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>Each piece's materials in the order to try them, kept until any order changes.</summary>
        private static readonly Dictionary<(ThingDef def, ApparelPolicy policy, MaterialChoice material), List<ThingDef>> ranked =
            new Dictionary<(ThingDef, ApparelPolicy, MaterialChoice), List<ThingDef>>();

        private static int rankedForVersion = -1;

        /// <summary>Drops the ranked materials, so a loaded game doesn't keep the previous one's policies.</summary>
        public static void Forget()
        {
            ranked.Clear();
            rankedForVersion = -1;
        }

        /// <summary>
        /// The pieces the pawn could pick under this policy. Imagined pieces get the first material in the choice's
        /// order that the policy allows (and, for craftable, that's in stock), at normal quality or the policy's
        /// nearest allowed quality.
        /// </summary>
        public static List<Apparel> For(Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material)
        {
            if (source == PieceSource.InStorage)
            {
                return InStorage(pawn, policy);
            }
            return Imagined(pawn, policy, source == PieceSource.CraftableNow, material);
        }

        /// <summary>
        /// The real pieces the game's apparel check scores, gathered as it gathers them (outfit stands included):
        /// allowed, stored, not forbidden, not burning and the right gender.
        /// </summary>
        private static List<Apparel> InStorage(Pawn pawn, ApparelPolicy policy)
        {
            var things = new List<Thing>();
            pawn.Map.listerThings.GetAllThings(in things, ThingRequestGroup.Apparel, null, true);
            foreach (IHaulSource source in pawn.Map.haulDestinationManager.AllHaulSourcesListForReading)
            {
                foreach (Thing held in source.GetDirectlyHeldThings())
                {
                    if (held is Apparel)
                    {
                        things.Add(held);
                    }
                }
            }
            var pieces = new List<Apparel>();
            foreach (Thing thing in things)
            {
                if (thing is Apparel piece
                    && !pieces.Contains(piece)
                    && policy.filter.Allows(piece)
                    && piece.IsInAnyStorage()
                    && !piece.IsForbidden(pawn)
                    && !piece.IsBurning()
                    && RightGender(pawn, piece.def))
                {
                    pieces.Add(piece);
                }
            }
            return pieces;
        }

        private static List<Apparel> Imagined(Pawn pawn, ApparelPolicy policy, bool craftableOnly, MaterialChoice material)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            IReadOnlyList<OrderLine> lines = orders?.EffectiveLines(policy) ?? NoLines;
            int version = orders?.Version ?? 0;
            if (version != rankedForVersion)
            {
                ranked.Clear();
                rankedForVersion = version;
            }
            QualityCategory quality = QualityFor(policy);
            Map map = pawn.Map;
            var pieces = new List<Apparel>();
            foreach (ThingDef def in policy.filter.AllowedThingDefs)
            {
                if (def.apparel == null || !RightGender(pawn, def) || (craftableOnly && !CraftableApparel.HasRecipeNow(def, map)))
                {
                    continue;
                }
                Apparel piece = FirstAllowed(def, policy, material, lines, quality, craftableOnly ? map : null);
                if (piece != null)
                {
                    pieces.Add(piece);
                }
            }
            return pieces;
        }

        /// <summary>The piece in the first material the policy allows, or null when none is allowed or affordable.</summary>
        private static Apparel FirstAllowed(ThingDef def, ApparelPolicy policy, MaterialChoice material, IReadOnlyList<OrderLine> lines,
            QualityCategory quality, Map stockFrom)
        {
            if (!def.MadeFromStuff)
            {
                if (stockFrom != null && !CraftableApparel.CanAfford(def, null, stockFrom))
                {
                    return null;
                }
                Apparel piece = ImaginedPieces.For(def, null, quality);
                return piece != null && policy.filter.Allows(piece) ? piece : null;
            }
            var key = (def, policy, material);
            if (!ranked.TryGetValue(key, out List<ThingDef> stuffs))
            {
                stuffs = material.InOrderOfPreference(def, lines);
                ranked[key] = stuffs;
            }
            foreach (ThingDef stuff in stuffs)
            {
                if (stockFrom != null && !CraftableApparel.CanAfford(def, stuff, stockFrom))
                {
                    continue;
                }
                Apparel piece = ImaginedPieces.For(def, stuff, quality);
                if (piece != null && policy.filter.Allows(piece))
                {
                    return piece;
                }
            }
            return null;
        }

        /// <summary>Normal, or the nearest quality the policy allows.</summary>
        private static QualityCategory QualityFor(ApparelPolicy policy)
        {
            QualityRange allowed = policy.filter.AllowedQualityLevels;
            if (QualityCategory.Normal < allowed.min)
            {
                return allowed.min;
            }
            if (QualityCategory.Normal > allowed.max)
            {
                return allowed.max;
            }
            return QualityCategory.Normal;
        }

        private static bool RightGender(Pawn pawn, ThingDef def)
        {
            return def.apparel.gender == Gender.None || def.apparel.gender == pawn.gender;
        }
    }
}
