using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>What a piece gives its wearer: its info card's "when worn" stats, with its quality applied.</summary>
    public static class PieceBonuses
    {
        /// <summary>Gear bonuses come in steps of 0.01 or more, so anything smaller counts as none.</summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// The piece's non-zero wearer bonuses, penalties included. Stats on the lines come first, in line order, then the
        /// rest by name. Armor and insulation are the piece's own stats, so they're never listed.
        /// </summary>
        public static IReadOnlyList<(StatDef stat, float value)> Of(Apparel piece, IReadOnlyList<OrderLine> lines)
        {
            var bonuses = new List<(StatDef stat, float value)>();
            List<StatModifier> offsets = piece.def.equippedStatOffsets;
            if (offsets == null)
            {
                return bonuses;
            }
            foreach (StatModifier offset in offsets)
            {
                StatDef stat = offset.stat;
                if (stat == null || StatChoices.IsArmorOrInsulation(stat) || bonuses.Any(bonus => bonus.stat == stat))
                {
                    continue;
                }
                float value = PieceStats.Value(piece, stat);
                if (Math.Abs(value) > Epsilon)
                {
                    bonuses.Add((stat, value));
                }
            }
            return bonuses
                .OrderBy(bonus => LinePlace(bonus.stat, lines))
                .ThenBy(bonus => bonus.stat.label)
                .ToList();
        }

        /// <summary>Where the stat's first line is in the order, or past the end when no line ranks by it.</summary>
        private static int LinePlace(StatDef stat, IReadOnlyList<OrderLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].IsItem && lines[i].stat == stat)
                {
                    return i;
                }
            }
            return int.MaxValue;
        }
    }
}
