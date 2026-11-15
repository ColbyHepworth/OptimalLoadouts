using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>The stats an order can use, worked out from the loaded defs the first time they're asked for.</summary>
    public static class StatChoices
    {
        /// <summary>Values closer than this count as the same, so a stat every piece shares isn't offered.</summary>
        private const float Epsilon = 0.0001f;

        private const int HighestShown = 3;

        private static HashSet<StatDef> armorAndInsulation;
        private static HashSet<StatCategoryDef> pieceCategories;
        private static HashSet<StatDef> wearerBonuses;
        private static List<StatSection> sections;
        private static HashSet<StatDef> offered;
        private static List<CoveredPart> coveredParts;

        /// <summary>Armor and insulation, the piece's own stats that are the same for every copy of an item.</summary>
        internal static bool IsArmorOrInsulation(StatDef stat)
        {
            BuildKinds();
            return armorAndInsulation.Contains(stat);
        }

        /// <summary>
        /// Whether a stat belongs to the piece itself (the info card's Apparel and Basics sections, armor and insulation
        /// always), read from the piece, rather than a bonus it gives its wearer. A stat any apparel gives its wearer
        /// counts as a bonus for every piece.
        /// </summary>
        public static bool IsWornPieceStat(StatDef stat)
        {
            BuildKinds();
            if (armorAndInsulation.Contains(stat))
            {
                return true;
            }
            return stat.category != null && pieceCategories.Contains(stat.category) && !wearerBonuses.Contains(stat);
        }

        /// <summary>Every stat an order can use, by info card section: the piece's own first, then its wearer's.</summary>
        public static IReadOnlyList<StatSection> Sections
        {
            get
            {
                if (sections == null)
                {
                    sections = BuildSections();
                }
                return sections;
            }
        }

        /// <summary>
        /// Every body part some apparel names in its own coverage, from whichever mods are loaded, with the items that
        /// cover it as their info cards say, by name. A part no apparel names (the head a pawn headbutts with) isn't one.
        /// </summary>
        public static IReadOnlyList<CoveredPart> CoveredParts
        {
            get
            {
                if (coveredParts == null)
                {
                    List<ThingDef> apparel = Apparel().ToList();
                    coveredParts = apparel
                        .SelectMany(def => def.apparel.bodyPartGroups ?? new List<BodyPartGroupDef>())
                        .Where(part => part != null)
                        .Distinct()
                        .Select(part => new CoveredPart(part, apparel.Where(def => PieceCoverage.Covers(def, part)).OrderBy(def => def.label).ToList()))
                        .Where(entry => entry.Items.Count > 0)
                        .OrderBy(entry => entry.Part.label)
                        .ToList();
                }
                return coveredParts;
            }
        }

        /// <summary>Whether an order can use the stat: one of the stats the stat picker offers.</summary>
        public static bool IsOffered(StatDef stat)
        {
            if (offered == null)
            {
                offered = new HashSet<StatDef>(Sections.SelectMany(section => section.Stats).Select(choice => choice.Stat));
            }
            return stat != null && offered.Contains(stat);
        }

        private static void BuildKinds()
        {
            if (armorAndInsulation != null)
            {
                return;
            }
            armorAndInsulation = new HashSet<StatDef>
            {
                StatDefOf.ArmorRating_Sharp,
                StatDefOf.ArmorRating_Blunt,
                StatDefOf.ArmorRating_Heat,
                StatDefOf.Insulation_Cold,
                StatDefOf.Insulation_Heat,
            };
            pieceCategories = new HashSet<StatCategoryDef>
            {
                StatCategoryDefOf.Apparel,
                StatCategoryDefOf.Basics,
                StatCategoryDefOf.BasicsImportant,
                StatCategoryDefOf.BasicsNonPawn,
                StatCategoryDefOf.BasicsNonPawnImportant,
            };
            wearerBonuses = new HashSet<StatDef>();
            foreach (ThingDef def in Apparel())
            {
                if (def.equippedStatOffsets == null)
                {
                    continue;
                }
                foreach (StatModifier offset in def.equippedStatOffsets)
                {
                    if (offset.stat != null && !armorAndInsulation.Contains(offset.stat))
                    {
                        wearerBonuses.Add(offset.stat);
                    }
                }
            }
        }

        private static List<StatSection> BuildSections()
        {
            BuildKinds();
            List<ThingDef> apparel = Apparel().ToList();
            var offered = new List<(OfferedStat stat, bool ofPiece)>();
            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat.alwaysHide || stat.category == null)
                {
                    continue;
                }
                bool ofPiece = IsWornPieceStat(stat);
                if (!ofPiece && !wearerBonuses.Contains(stat))
                {
                    continue;
                }
                OfferedStat choice = ofPiece ? PieceStat(stat, apparel) : WearerBonus(stat, apparel);
                if (choice != null)
                {
                    offered.Add((choice, ofPiece));
                }
            }
            // The info card shows several Basics sections under one name, so sections with the same name are one.
            return offered
                .GroupBy(entry => (label: entry.stat.Stat.category.LabelCap.Resolve(), entry.ofPiece))
                .Select(group => new StatSection(
                    group.Select(entry => entry.stat.Stat.category).OrderBy(category => category.displayOrder).First(),
                    group.Key.ofPiece,
                    group.Select(entry => entry.stat).OrderBy(choice => choice.Stat.label).ToList()))
                .OrderBy(section => section.OfPiece ? 0 : 1)
                .ThenBy(section => section.Category.displayOrder)
                .ToList();
        }

        /// <summary>A stat of the piece, offered only when it differs between apparel, read in each item's usual material.</summary>
        private static OfferedStat PieceStat(StatDef stat, List<ThingDef> apparel)
        {
            var values = new List<(ThingDef item, float value)>();
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (ThingDef def in apparel)
            {
                float value;
                try
                {
                    if (!stat.Worker.ShouldShowFor(StatRequest.For(def, GenStuff.DefaultStuffFor(def))))
                    {
                        continue;
                    }
                    value = def.GetStatValueAbstract(stat, GenStuff.DefaultStuffFor(def));
                }
                catch (Exception)
                {
                    // A mod's stat worker that can't read a def it never expected is left out, not shown as an error.
                    continue;
                }
                values.Add((def, value));
                lowest = Math.Min(lowest, value);
                highest = Math.Max(highest, value);
            }
            if (values.Count == 0 || highest - lowest < Epsilon)
            {
                return null;
            }
            int itemCount = values.Count(entry => Math.Abs(entry.value) > Epsilon);
            return new OfferedStat(stat, itemCount, Highest(values));
        }

        private static OfferedStat WearerBonus(StatDef stat, List<ThingDef> apparel)
        {
            var values = new List<(ThingDef item, float value)>();
            foreach (ThingDef def in apparel)
            {
                float value = def.equippedStatOffsets.GetStatOffsetFromList(stat);
                if (Math.Abs(value) > Epsilon)
                {
                    values.Add((def, value));
                }
            }
            return values.Count == 0 ? null : new OfferedStat(stat, values.Count, Highest(values));
        }

        private static List<(ThingDef item, float value)> Highest(List<(ThingDef item, float value)> values)
        {
            return values.OrderByDescending(entry => entry.value).ThenBy(entry => entry.item.label).Take(HighestShown).ToList();
        }

        private static IEnumerable<ThingDef> Apparel()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.IsApparel);
        }
    }

    /// <summary>A body part the stat picker offers, with the apparel items that cover it.</summary>
    public class CoveredPart
    {
        public CoveredPart(BodyPartGroupDef part, List<ThingDef> items)
        {
            Part = part;
            Items = items;
        }

        public BodyPartGroupDef Part { get; }

        /// <summary>The items that cover it, by name.</summary>
        public IReadOnlyList<ThingDef> Items { get; }
    }

    /// <summary>One info card section of the stats an order can use.</summary>
    public sealed class StatSection
    {
        public StatSection(StatCategoryDef category, bool ofPiece, IReadOnlyList<OfferedStat> stats)
        {
            Category = category;
            OfPiece = ofPiece;
            Stats = stats;
        }

        /// <summary>The section's info card category, the first in display order when several share its name.</summary>
        public StatCategoryDef Category { get; }

        /// <summary>True for the piece's own stats, false for what it gives its wearer.</summary>
        public bool OfPiece { get; }

        public IReadOnlyList<OfferedStat> Stats { get; }
    }

    /// <summary>A stat an order can use, with how many apparel items change it and which have the highest values.</summary>
    public sealed class OfferedStat
    {
        public OfferedStat(StatDef stat, int itemCount, IReadOnlyList<(ThingDef item, float value)> highest)
        {
            Stat = stat;
            ItemCount = itemCount;
            Highest = highest;
        }

        public StatDef Stat { get; }

        public int ItemCount { get; }

        /// <summary>The items with the highest values, at most three.</summary>
        public IReadOnlyList<(ThingDef item, float value)> Highest { get; }
    }
}
