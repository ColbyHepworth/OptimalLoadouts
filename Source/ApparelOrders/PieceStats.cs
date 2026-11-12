using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// A piece's value on a stat: one of its own stats, or what it adds to its wearer. Armor, insulation and wearer
    /// bonuses are cached per item type, material, quality and stat, since the apparel check asks for every stored piece.
    /// </summary>
    public static class PieceStats
    {
        private static readonly Dictionary<Key, float> cache = new Dictionary<Key, float>();

        public static float Value(Apparel piece, StatDef stat)
        {
            bool ofPiece = StatChoices.IsWornPieceStat(stat);
            if (ofPiece && !StatChoices.IsArmorOrInsulation(stat))
            {
                // Other stats of the piece (market value) change with its condition and taint, so copies can differ.
                return piece.GetStatValue(stat);
            }
            int quality = piece.TryGetQuality(out QualityCategory category) ? (int)category : -1;
            var key = new Key(piece.def, piece.Stuff, quality, stat);
            if (!cache.TryGetValue(key, out float value))
            {
                value = ofPiece
                    ? piece.GetStatValue(stat)
                    : StatWorker.StatOffsetFromGear(piece, stat);
                cache[key] = value;
            }
            return value;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly ThingDef def;
            private readonly ThingDef stuff;
            private readonly int quality;
            private readonly StatDef stat;

            public Key(ThingDef def, ThingDef stuff, int quality, StatDef stat)
            {
                this.def = def;
                this.stuff = stuff;
                this.quality = quality;
                this.stat = stat;
            }

            public bool Equals(Key other)
            {
                return def == other.def && stuff == other.stuff && quality == other.quality && stat == other.stat;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(def, stuff, quality, stat);
            }
        }
    }
}
