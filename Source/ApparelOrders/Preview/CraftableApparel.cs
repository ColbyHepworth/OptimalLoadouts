using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>What kind of thing stands between the colony and making a piece.</summary>
    public enum MissingKind
    {
        NoRecipe,
        Research,

        /// <summary>A recipe the colony can't use for a reason other than research, such as a meme it lacks.</summary>
        Unavailable,
        Bench,
        Skill,
        Materials,
    }

    /// <summary>One thing the colony lacks to make a piece, with its words ready to show.</summary>
    public sealed class MissingToMake
    {
        public MissingKind Kind { get; }
        public string Label { get; }

        /// <summary>The bench to build or the material short, for its icon. Null for the other kinds.</summary>
        public ThingDef Thing { get; }

        /// <summary>For a skill, which one and the level needed, so an outfit names each skill once at its highest.</summary>
        public SkillDef Skill { get; }

        public int LevelNeeded { get; }

        /// <summary>For a skill, the colony's best level in it and who has it (null when nobody can do the work).</summary>
        public int BestLevel { get; }

        public Pawn BestPawn { get; }

        public MissingToMake(MissingKind kind, string label, ThingDef thing = null, SkillDef skill = null, int levelNeeded = 0,
            int bestLevel = 0, Pawn bestPawn = null)
        {
            Kind = kind;
            Label = label;
            Thing = thing;
            Skill = skill;
            LevelNeeded = levelNeeded;
            BestLevel = bestLevel;
            BestPawn = bestPawn;
        }
    }

    /// <summary>What making a whole outfit takes: how many of its pieces the colony can make now, and what it lacks for the rest.</summary>
    public sealed class OutfitNeeds
    {
        public int Ready { get; }
        public int Total { get; }

        /// <summary>Each thing missing once, research first, then benches, skills and the rest, the materials added up across the outfit.</summary>
        public IReadOnlyList<OutfitNeed> Needs { get; }

        public OutfitNeeds(int ready, int total, IReadOnlyList<OutfitNeed> needs)
        {
            Ready = ready;
            Total = total;
            Needs = needs;
        }
    }

    /// <summary>One thing an outfit lacks, and the pieces it's for.</summary>
    public sealed class OutfitNeed
    {
        public MissingToMake Missing { get; internal set; }
        public IReadOnlyList<Apparel> For => pieces;

        private readonly List<Apparel> pieces = new List<Apparel>();

        public OutfitNeed(MissingToMake missing)
        {
            Missing = missing;
        }

        internal void AddPiece(Apparel piece)
        {
            if (!pieces.Contains(piece))
            {
                pieces.Add(piece);
            }
        }
    }

    /// <summary>
    /// Whether the colony has researched a piece (an unlocked recipe at a researched bench), whether it could make it
    /// now (a bench for it built, a colonist with the skill, and the cost in stock), and what it lacks when it can't.
    /// </summary>
    public static class CraftableApparel
    {
        /// <summary>The preview asks for every row each frame, so answers are kept this long.</summary>
        private const float MissingKeptSeconds = 1f;

        private static Dictionary<ThingDef, List<RecipeDef>> recipesFor;
        private static readonly Dictionary<RecipeDef, List<ThingDef>> benchesFor = new Dictionary<RecipeDef, List<ThingDef>>();
        private static readonly Dictionary<(ThingDef def, ThingDef stuff, Map map), List<MissingToMake>> missing =
            new Dictionary<(ThingDef, ThingDef, Map), List<MissingToMake>>();
        private static float missingKeptSince = float.MinValue;

        /// <summary>
        /// A recipe for the piece is unlocked now, as the bills menu judges it (its research finished, and any meme,
        /// faction or ideoligion it needs), at a bench whose research is finished too, or one the colony has built (a
        /// traded bench works without its research). The bench needn't be built.
        /// </summary>
        public static bool IsResearched(ThingDef def, Map map)
        {
            return RecipesFor().TryGetValue(def, out List<RecipeDef> recipes)
                && recipes.Any(recipe => recipe.AvailableNow
                    && BenchesFor(recipe).Any(bench => bench.IsResearchFinished || map.listerBuildings.ColonistsHaveBuilding(bench)));
        }

        /// <summary>
        /// A recipe for the piece is researched, the colony has built a bench that does it, and a colonist on the map
        /// has the skill it needs.
        /// </summary>
        public static bool HasRecipeNow(ThingDef def, Map map)
        {
            if (!RecipesFor().TryGetValue(def, out List<RecipeDef> recipes))
            {
                return false;
            }
            foreach (RecipeDef recipe in recipes)
            {
                if (recipe.AvailableNow && BenchesFor(recipe).Any(bench => map.listerBuildings.ColonistsHaveBuilding(bench))
                    && SomeoneHasSkill(recipe, map))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the colony lacks to make one piece in this material, from the recipe that lacks the least: research, a
        /// bench built, a colonist with the skill, materials in stock. Empty when it can make it now.
        /// </summary>
        public static List<MissingToMake> MissingFor(ThingDef def, ThingDef stuff, Map map)
        {
            if (Time.realtimeSinceStartup - missingKeptSince > MissingKeptSeconds)
            {
                missing.Clear();
                missingKeptSince = Time.realtimeSinceStartup;
            }
            var key = (def, stuff, map);
            if (!missing.TryGetValue(key, out List<MissingToMake> found))
            {
                found = WorkOutMissing(def, stuff, map);
                missing[key] = found;
            }
            return found;
        }

        private static List<MissingToMake> WorkOutMissing(ThingDef def, ThingDef stuff, Map map)
        {
            // A recipe no bench does can't be used, as the bills menu and Craftable judge it.
            List<RecipeDef> recipes = RecipesFor().TryGetValue(def, out List<RecipeDef> all)
                ? all.Where(recipe => BenchesFor(recipe).Count > 0).ToList()
                : new List<RecipeDef>();
            if (recipes.Count == 0)
            {
                return new List<MissingToMake> { new MissingToMake(MissingKind.NoRecipe, "OL_Missing_NoRecipe".Translate()) };
            }
            List<MissingToMake> fewest = null;
            foreach (RecipeDef recipe in recipes)
            {
                List<MissingToMake> lacking = MissingForRecipe(recipe);
                if (fewest == null || lacking.Count < fewest.Count)
                {
                    fewest = lacking;
                }
            }
            fewest.AddRange(MissingMaterials(def, stuff, map));
            return fewest;

            List<MissingToMake> MissingForRecipe(RecipeDef recipe)
            {
                var lacking = new List<MissingToMake>();
                var research = new List<ResearchProjectDef>(RecipeResearch(recipe).Where(project => !project.IsFinished));
                List<ThingDef> benches = BenchesFor(recipe);
                ThingDef benchToBuild = null;
                if (!benches.Any(bench => map.listerBuildings.ColonistsHaveBuilding(bench)))
                {
                    // The bench that needs the least research, so a tribe is pointed at the hand tailoring bench.
                    benchToBuild = benches.OrderBy(bench => UnfinishedResearch(bench).Count()).First();
                    research.AddRange(UnfinishedResearch(benchToBuild));
                }
                foreach (ResearchProjectDef project in research.Distinct())
                {
                    lacking.Add(new MissingToMake(MissingKind.Research, "OL_Missing_Research".Translate(project.LabelCap)));
                }
                if (!RecipeResearch(recipe).Any(project => !project.IsFinished) && !recipe.AvailableNow)
                {
                    lacking.Add(new MissingToMake(MissingKind.Unavailable, "OL_Missing_Unavailable".Translate()));
                }
                if (benchToBuild != null)
                {
                    lacking.Add(new MissingToMake(MissingKind.Bench, "OL_Missing_Bench".Translate(benchToBuild.LabelCap), benchToBuild));
                }
                if (!SomeoneHasSkill(recipe, map))
                {
                    int before = lacking.Count;
                    foreach (SkillRequirement requirement in recipe.skillRequirements)
                    {
                        (Pawn pawn, int best) = BestAt(requirement.skill, map);
                        if (best < requirement.minLevel)
                        {
                            lacking.Add(new MissingToMake(MissingKind.Skill,
                                "OL_Missing_Skill".Translate(requirement.skill.LabelCap, requirement.minLevel, best), skill: requirement.skill,
                                levelNeeded: requirement.minLevel, bestLevel: best, bestPawn: pawn));
                        }
                    }
                    if (lacking.Count == before)
                    {
                        // Each skill is there, but no one has them all.
                        lacking.Add(new MissingToMake(MissingKind.Skill, "OL_Missing_SkillsTogether".Translate()));
                    }
                }
                return lacking;
            }
        }

        /// <summary>
        /// What making the whole outfit takes. Research, benches and skills are named once. Materials are the outfit's
        /// whole cost against the stock, since its pieces draw on the same stockpiles.
        /// </summary>
        public static OutfitNeeds NeedsFor(IReadOnlyList<Apparel> outfit, Map map)
        {
            int ready = 0;
            var byKind = new Dictionary<MissingKind, List<OutfitNeed>>();
            var cost = new Dictionary<ThingDef, int>();
            var usedBy = new Dictionary<ThingDef, List<Apparel>>();
            foreach (Apparel piece in outfit)
            {
                List<MissingToMake> lacking = MissingFor(piece.def, piece.Stuff, map);
                if (lacking.Count == 0)
                {
                    ready++;
                }
                foreach (MissingToMake each in lacking.Where(each => each.Kind != MissingKind.Materials))
                {
                    if (!byKind.TryGetValue(each.Kind, out List<OutfitNeed> needs))
                    {
                        needs = new List<OutfitNeed>();
                        byKind[each.Kind] = needs;
                    }
                    MissingToMake need = each.Kind == MissingKind.NoRecipe || each.Kind == MissingKind.Unavailable
                        ? new MissingToMake(each.Kind, "OL_Missing_Piece".Translate(piece.LabelNoParenthesisCap, each.Label).Resolve(), piece.def)
                        : each;
                    // A skill is named once, at the highest level any piece needs.
                    OutfitNeed known = need.Skill != null
                        ? needs.FirstOrDefault(other => other.Missing.Skill == need.Skill)
                        : needs.FirstOrDefault(other => other.Missing.Label == need.Label);
                    if (known == null)
                    {
                        known = new OutfitNeed(need);
                        needs.Add(known);
                    }
                    else if (need.Skill != null && need.LevelNeeded > known.Missing.LevelNeeded)
                    {
                        known.Missing = need;
                    }
                    known.AddPiece(piece);
                }
                foreach (ThingDefCountClass each in piece.def.CostListAdjusted(piece.Stuff, errorOnNullStuff: false))
                {
                    if (each.thingDef != null)
                    {
                        cost.TryGetValue(each.thingDef, out int sum);
                        cost[each.thingDef] = sum + each.count;
                        if (!usedBy.TryGetValue(each.thingDef, out List<Apparel> users))
                        {
                            users = new List<Apparel>();
                            usedBy[each.thingDef] = users;
                        }
                        users.Add(piece);
                    }
                }
            }
            var lines = new List<OutfitNeed>();
            foreach (MissingKind kind in new[] { MissingKind.Research, MissingKind.Bench, MissingKind.Skill, MissingKind.Unavailable, MissingKind.NoRecipe })
            {
                if (byKind.TryGetValue(kind, out List<OutfitNeed> needs))
                {
                    lines.AddRange(needs);
                }
            }
            Dictionary<ThingDef, int> stock = map.resourceCounter.AllCountedAmounts;
            foreach (KeyValuePair<ThingDef, int> each in cost)
            {
                stock.TryGetValue(each.Key, out int count);
                if (count < each.Value)
                {
                    var need = new OutfitNeed(new MissingToMake(MissingKind.Materials, "OL_Missing_Materials".Translate(each.Value - count, each.Key.label), each.Key));
                    foreach (Apparel piece in usedBy[each.Key])
                    {
                        need.AddPiece(piece);
                    }
                    lines.Add(need);
                }
            }
            return new OutfitNeeds(ready, outfit.Count, lines);
        }

        /// <summary>
        /// "Can't make yet:" then what's missing, one to a line, for a piece made new; null for a real piece in storage
        /// (<paramref name="map"/> null) or one the colony can make now.
        /// </summary>
        public static string CantMakeText(Apparel piece, Map map)
        {
            if (map == null)
            {
                return null;
            }
            List<MissingToMake> lacking = MissingFor(piece.def, piece.Stuff, map);
            if (lacking.Count == 0)
            {
                return null;
            }
            return "OL_Preview_CantMakeYet".Translate() + "\n" + string.Join("\n", lacking.Select(each => "  " + each.Label));
        }

        private static IEnumerable<ResearchProjectDef> UnfinishedResearch(ThingDef bench)
        {
            return (bench.researchPrerequisites ?? Enumerable.Empty<ResearchProjectDef>()).Where(project => !project.IsFinished);
        }

        /// <summary>Drops the kept answers, so a loaded game doesn't keep the previous one's map.</summary>
        internal static void Forget()
        {
            missing.Clear();
            missingKeptSince = float.MinValue;
        }

        private static IEnumerable<ResearchProjectDef> RecipeResearch(RecipeDef recipe)
        {
            if (recipe.researchPrerequisite != null)
            {
                yield return recipe.researchPrerequisite;
            }
            if (recipe.researchPrerequisites != null)
            {
                foreach (ResearchProjectDef project in recipe.researchPrerequisites)
                {
                    yield return project;
                }
            }
        }

        private static IEnumerable<MissingToMake> MissingMaterials(ThingDef def, ThingDef stuff, Map map)
        {
            Dictionary<ThingDef, int> stock = map.resourceCounter.AllCountedAmounts;
            foreach (ThingDefCountClass cost in def.CostListAdjusted(stuff, errorOnNullStuff: false))
            {
                if (cost.thingDef == null)
                {
                    continue;
                }
                stock.TryGetValue(cost.thingDef, out int count);
                if (count < cost.count)
                {
                    yield return new MissingToMake(MissingKind.Materials, "OL_Missing_Materials".Translate(cost.count - count, cost.thingDef.label), cost.thingDef);
                }
            }
        }

        /// <summary>
        /// Whether a colonist or a colony mech on the map has every skill the recipe needs, as the game checks for a bill.
        /// </summary>
        private static bool SomeoneHasSkill(RecipeDef recipe, Map map)
        {
            if (recipe.skillRequirements == null || recipe.skillRequirements.Count == 0)
            {
                return true;
            }
            return Workers(map).Any(recipe.PawnSatisfiesSkillRequirements);
        }

        /// <summary>The best level any colonist has in the skill, or a colony mech whose work uses it, and who has it.</summary>
        private static (Pawn pawn, int level) BestAt(SkillDef skill, Map map)
        {
            int best = 0;
            Pawn bestPawn = null;
            foreach (Pawn pawn in Workers(map))
            {
                int level = 0;
                if (pawn.IsColonyMechPlayerControlled)
                {
                    if (pawn.RaceProps.mechEnabledWorkTypes.Any(work => work.relevantSkills.NotNullAndContains(skill)))
                    {
                        level = pawn.RaceProps.mechFixedSkillLevel;
                    }
                }
                else
                {
                    SkillRecord record = pawn.skills?.GetSkill(skill);
                    level = record == null || record.TotallyDisabled ? 0 : record.Level;
                }
                if (level > best || bestPawn == null && level == best)
                {
                    best = level;
                    bestPawn = pawn;
                }
            }
            return (bestPawn, best);
        }

        /// <summary>The colony's own pawns on the map who could work a bench: colonists, and mechs it controls.</summary>
        private static IEnumerable<Pawn> Workers(Map map)
        {
            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(pawn => pawn.RaceProps.Humanlike ? pawn.HostFaction == null : pawn.IsColonyMechPlayerControlled);
        }

        /// <summary>The colony's stock covers one piece in this material.</summary>
        public static bool CanAfford(ThingDef def, ThingDef stuff, Map map)
        {
            Dictionary<ThingDef, int> stock = map.resourceCounter.AllCountedAmounts;
            foreach (ThingDefCountClass cost in def.CostListAdjusted(stuff, errorOnNullStuff: false))
            {
                // A piece made of a material, asked about with none, lists its material as an empty entry.
                if (cost.thingDef == null)
                {
                    continue;
                }
                if (!stock.TryGetValue(cost.thingDef, out int count) || count < cost.count)
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<ThingDef, List<RecipeDef>> RecipesFor()
        {
            if (recipesFor == null)
            {
                recipesFor = new Dictionary<ThingDef, List<RecipeDef>>();
                foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    ThingDef product = recipe.ProducedThingDef;
                    // Weapons and their ammo too, for the weapon finder and its bills.
                    if (product == null)
                    {
                        continue;
                    }
                    if (!recipesFor.TryGetValue(product, out List<RecipeDef> recipes))
                    {
                        recipes = new List<RecipeDef>();
                        recipesFor[product] = recipes;
                    }
                    recipes.Add(recipe);
                }
            }
            return recipesFor;
        }

        private static List<ThingDef> BenchesFor(RecipeDef recipe)
        {
            // AllRecipeUsers searches every thing def each time it's read.
            if (!benchesFor.TryGetValue(recipe, out List<ThingDef> benches))
            {
                benches = recipe.AllRecipeUsers.Distinct().ToList();
                benchesFor[recipe] = benches;
            }
            return benches;
        }
    }
}
