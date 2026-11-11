using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>The stats an order can use, worked out from the loaded defs the first time they're asked for.</summary>
    public static class StatChoices
    {
        private static List<StatDef> wornPieceStats;
        private static List<StatDef> wearerBonuses;

        /// <summary>Stats of the piece itself: its armor and insulation.</summary>
        public static IReadOnlyList<StatDef> WornPieceStats
        {
            get
            {
                Build();
                return wornPieceStats;
            }
        }

        /// <summary>Stats a piece changes on its wearer: every stat in any apparel's equipped stat offsets.</summary>
        public static IReadOnlyList<StatDef> WearerBonuses
        {
            get
            {
                Build();
                return wearerBonuses;
            }
        }

        public static bool IsWornPieceStat(StatDef stat)
        {
            Build();
            return wornPieceStats.Contains(stat);
        }

        private static void Build()
        {
            if (wornPieceStats != null)
            {
                return;
            }
            wornPieceStats = new List<StatDef>
            {
                StatDefOf.ArmorRating_Sharp,
                StatDefOf.ArmorRating_Blunt,
                StatDefOf.ArmorRating_Heat,
                StatDefOf.Insulation_Cold,
                StatDefOf.Insulation_Heat,
            };
            var bonuses = new HashSet<StatDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsApparel || def.equippedStatOffsets == null)
                {
                    continue;
                }
                foreach (StatModifier offset in def.equippedStatOffsets)
                {
                    if (offset.stat != null && !wornPieceStats.Contains(offset.stat))
                    {
                        bonuses.Add(offset.stat);
                    }
                }
            }
            wearerBonuses = bonuses.OrderBy(stat => stat.label).ToList();
        }
    }
}
