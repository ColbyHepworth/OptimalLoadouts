using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Whether the colony has researched a piece (an unlocked recipe at a researched bench), and whether it could make it
    /// now (a bench for it built, and the cost in stock).
    /// </summary>
    public static class CraftableApparel
    {
        private static Dictionary<ThingDef, List<RecipeDef>> recipesFor;
        private static readonly Dictionary<RecipeDef, List<ThingDef>> benchesFor = new Dictionary<RecipeDef, List<ThingDef>>();

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

        /// <summary>A recipe for the piece is researched and the colony has built a bench that does it.</summary>
        public static bool HasRecipeNow(ThingDef def, Map map)
        {
            if (!RecipesFor().TryGetValue(def, out List<RecipeDef> recipes))
            {
                return false;
            }
            foreach (RecipeDef recipe in recipes)
            {
                if (recipe.AvailableNow && BenchesFor(recipe).Any(bench => map.listerBuildings.ColonistsHaveBuilding(bench)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The colony's stock covers one piece in this material.</summary>
        public static bool CanAfford(ThingDef def, ThingDef stuff, Map map)
        {
            Dictionary<ThingDef, int> stock = map.resourceCounter.AllCountedAmounts;
            foreach (ThingDefCountClass cost in def.CostListAdjusted(stuff, errorOnNullStuff: false))
            {
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
                    if (product?.apparel == null)
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
