using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Which material an imagined piece is made of. Each choice ranks every material the piece can be made of, best
    /// first, so a material the policy or the stock rules out falls through to the next.
    /// </summary>
    public abstract class MaterialChoice
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller is a tie.</summary>
        private const float Epsilon = 0.0001f;

        /// <summary>For each piece, the material that ranks best down the policy's order.</summary>
        public static readonly MaterialChoice BestForOrder = new Best();

        /// <summary>The game's default material for each piece.</summary>
        public static readonly MaterialChoice Typical = new Default();

        private static readonly Dictionary<(ThingDef def, ThingDef stuff, StatDef stat), float> values =
            new Dictionary<(ThingDef, ThingDef, StatDef), float>();

        /// <summary>One choice per material, so caches keyed by the choice don't grow with every pick.</summary>
        private static readonly Dictionary<ThingDef, MaterialChoice> specific = new Dictionary<ThingDef, MaterialChoice>();

        /// <summary>This material where a piece can be made of it, and the best for the order elsewhere.</summary>
        public static MaterialChoice Specific(ThingDef stuff)
        {
            if (!specific.TryGetValue(stuff, out MaterialChoice choice))
            {
                choice = new Chosen(stuff);
                specific[stuff] = choice;
            }
            return choice;
        }

        public abstract string Label { get; }

        /// <summary>Every material the piece can be made of, in the order to try them.</summary>
        public abstract List<ThingDef> InOrderOfPreference(ThingDef def, IReadOnlyList<OrderLine> lines);

        /// <summary>
        /// The materials ranked down the order, the way the order ranks pieces: the first line that differs decides.
        /// Only the piece's own stats (armor, insulation) change with the material. Ties keep the game's default first.
        /// </summary>
        protected static List<ThingDef> RankedForOrder(ThingDef def, IReadOnlyList<OrderLine> lines)
        {
            ThingDef typical = GenStuff.DefaultStuffFor(def);
            List<ThingDef> stuffs = GenStuff.AllowedStuffsFor(def).ToList();
            return stuffs
                .OrderBy(stuff => stuff, Comparer<ThingDef>.Create((a, b) => Compare(def, a, b, lines)))
                .ThenBy(stuff => stuff == typical ? 0 : 1)
                .ToList();
        }

        /// <summary>Negative when <paramref name="a"/> ranks before <paramref name="b"/>.</summary>
        private static int Compare(ThingDef def, ThingDef a, ThingDef b, IReadOnlyList<OrderLine> lines)
        {
            foreach (OrderLine line in lines)
            {
                if (line.stat == null || !StatChoices.IsWornPieceStat(line.stat))
                {
                    continue;
                }
                float delta = Value(def, a, line.stat) - Value(def, b, line.stat);
                if (line.lowerIsBetter)
                {
                    delta = -delta;
                }
                if (Math.Abs(delta) > Epsilon)
                {
                    return delta > 0f ? -1 : 1;
                }
            }
            return 0;
        }

        private static float Value(ThingDef def, ThingDef stuff, StatDef stat)
        {
            var key = (def, stuff, stat);
            if (!values.TryGetValue(key, out float value))
            {
                value = def.GetStatValueAbstract(stat, stuff);
                values[key] = value;
            }
            return value;
        }

        private sealed class Best : MaterialChoice
        {
            public override string Label => "OL_Preview_MaterialBest".Translate();

            public override List<ThingDef> InOrderOfPreference(ThingDef def, IReadOnlyList<OrderLine> lines)
            {
                return RankedForOrder(def, lines);
            }
        }

        private sealed class Default : MaterialChoice
        {
            public override string Label => "OL_Preview_MaterialTypical".Translate();

            public override List<ThingDef> InOrderOfPreference(ThingDef def, IReadOnlyList<OrderLine> lines)
            {
                return FirstIfPresent(GenStuff.DefaultStuffFor(def), RankedForOrder(def, lines));
            }
        }

        private sealed class Chosen : MaterialChoice
        {
            private readonly ThingDef stuff;

            public Chosen(ThingDef stuff)
            {
                this.stuff = stuff;
            }

            public override string Label => stuff.LabelCap;

            public override List<ThingDef> InOrderOfPreference(ThingDef def, IReadOnlyList<OrderLine> lines)
            {
                return FirstIfPresent(stuff, RankedForOrder(def, lines));
            }
        }

        private static List<ThingDef> FirstIfPresent(ThingDef first, List<ThingDef> ranked)
        {
            if (first != null && ranked.Remove(first))
            {
                ranked.Insert(0, first);
            }
            return ranked;
        }
    }
}
