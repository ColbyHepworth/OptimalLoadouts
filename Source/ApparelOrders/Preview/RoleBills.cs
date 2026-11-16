using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>What adding bills for a policy does with one item.</summary>
    public enum PlannedBillOutcome
    {
        /// <summary>A new bill at a bench.</summary>
        Add,

        /// <summary>A bill for it already counts toward a target, which goes up.</summary>
        Raise,

        /// <summary>A bill for it already makes enough.</summary>
        AlreadyEnough,

        /// <summary>The colony lacks the research, a bench or the skill.</summary>
        CantMake,

        /// <summary>Every bench that could make it has as many bills as the game allows.</summary>
        NoRoom,
    }

    /// <summary>What "Add bills" would do with one item of the policy's outfits.</summary>
    public sealed class PlannedBill
    {
        public ThingDef Item { get; }

        /// <summary>The material the policy names, or null when it picks the best or usual one, so the bill takes the bench's usual ingredients.</summary>
        public ThingDef Stuff { get; }

        /// <summary>How many to have, worn ones included: every colonist whose outfit holds it.</summary>
        public int Target { get; }

        public PlannedBillOutcome Outcome { get; }

        /// <summary>The bench a new bill goes to, or the one with the bill to raise.</summary>
        public Building_WorkTable Bench { get; }

        /// <summary>The bill that already makes it, for Raise and AlreadyEnough.</summary>
        public Bill_Production Existing { get; }

        internal RecipeDef Recipe { get; }

        /// <summary>For CantMake, the first thing the colony lacks.</summary>
        public string Why { get; }

        /// <summary>The colonists whose outfits hold the item, which the target counts.</summary>
        public IReadOnlyList<Pawn> For { get; internal set; } = new List<Pawn>();

        /// <summary>Whether to make or raise it. Ticked to start; the player can untick it before confirming.</summary>
        public bool Included { get; set; } = true;

        /// <summary>Whether confirming would change a bill for it: one to add or raise.</summary>
        public bool Acts => Outcome == PlannedBillOutcome.Add || Outcome == PlannedBillOutcome.Raise;

        internal PlannedBill(ThingDef item, ThingDef stuff, int target, PlannedBillOutcome outcome, Building_WorkTable bench = null,
            Bill_Production existing = null, RecipeDef recipe = null, string why = null)
        {
            Item = item;
            Stuff = stuff;
            Target = target;
            Outcome = outcome;
            Bench = bench;
            Existing = existing;
            Recipe = recipe;
            Why = why;
        }
    }

    /// <summary>
    /// Bills for what a policy's colonists are missing: one "do until you have" bill per item of their outfits, counting
    /// what colonists wear, at the bench the colony would use. Worked out first, then applied once the player confirms.
    /// </summary>
    public static class RoleBills
    {
        /// <summary>
        /// What adding bills for the policy would do, changing nothing. Each colonist on the map is predicted in the
        /// outfit their own policy gives them, made new from what the colony has researched. The items in this policy's
        /// colonists' outfits are planned, each with a target of every colonist on any policy whose outfit holds it,
        /// since a bill that counts worn pieces counts everyone's.
        /// </summary>
        public static List<PlannedBill> Plan(ApparelPolicy policy, Map map)
        {
            var plan = new List<PlannedBill>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.Where(pawn => pawn.outfits?.CurrentApparelPolicy != null && pawn.apparel != null).ToList();
            var outfits = colonists.ToDictionary(pawn => pawn, OutfitOf);
            // Only a material the policy names limits the bill. Its best one could be one the colony never has.
            ThingDef stuff = ApparelOrdersComponent.Current?.LimitMaterialOf(policy).Stuff;
            var wanted = new List<ThingDef>();
            foreach (Pawn pawn in colonists.Where(pawn => pawn.outfits.CurrentApparelPolicy == policy))
            {
                foreach (Apparel piece in outfits[pawn])
                {
                    if (!wanted.Contains(piece.def))
                    {
                        wanted.Add(piece.def);
                    }
                }
            }
            // Bills this plan puts on each bench, so they count toward its limit and its place in line.
            var planned = new Dictionary<Building_WorkTable, int>();
            foreach (ThingDef def in wanted.OrderBy(def => def.label))
            {
                List<Pawn> wearers = outfits.Where(outfit => outfit.Value.Any(piece => piece.def == def)).Select(outfit => outfit.Key).ToList();
                PlannedBill bill = PlanFor(def, def.MadeFromStuff ? stuff : null, wearers.Count, map, planned);
                bill.For = wearers;
                if (bill.Outcome == PlannedBillOutcome.Add)
                {
                    planned.TryGetValue(bill.Bench, out int count);
                    planned[bill.Bench] = count + 1;
                }
                plan.Add(bill);
            }
            return plan;
        }

        /// <summary>Makes the new bills and raises the targets the plan names, the ticked ones. Returns how many bills it added and raised.</summary>
        public static (int added, int raised) Apply(IEnumerable<PlannedBill> plan, ApparelPolicy policy)
        {
            int added = 0;
            int raised = 0;
            foreach (PlannedBill planned in plan.Where(each => each.Included))
            {
                if (planned.Outcome == PlannedBillOutcome.Raise)
                {
                    planned.Existing.targetCount = planned.Target;
                    planned.Existing.includeEquipped = true;
                    raised++;
                }
                else if (planned.Outcome == PlannedBillOutcome.Add && planned.Bench.BillStack.Count < BillStack.MaxCount)
                {
                    planned.Bench.BillStack.AddBill(MakeBill(planned, policy));
                    added++;
                }
            }
            return (added, raised);
        }

        /// <summary>The outfit the colonist's own policy gives them, made new from what's researched. Empty if it can't be worked out.</summary>
        private static List<Apparel> OutfitOf(Pawn pawn)
        {
            ApparelPolicy policy = pawn.outfits.CurrentApparelPolicy;
            try
            {
                LimitMaterial material = ApparelOrdersComponent.Current?.LimitMaterialOf(policy) ?? LimitMaterial.Best;
                List<Apparel> pieces = PieceSources.For(pawn, policy, PieceSource.Researched, MaterialChoice.For(material));
                return OutfitPredictor.Predict(pawn, policy, PieceSource.Researched, pieces).Worn.ToList();
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce($"[Optimal Loadouts] Couldn't work out {pawn.LabelShort}'s outfit for bills, so it's left out: {e}",
                    ("OptimalLoadouts.RoleBills" + pawn.thingIDNumber).GetHashCode());
                return new List<Apparel>();
            }
        }

        private static PlannedBill PlanFor(ThingDef def, ThingDef stuff, int target, Map map, Dictionary<Building_WorkTable, int> planned)
        {
            // Materials short aren't a reason not to add a bill: it waits for them, as any bill does.
            MissingToMake blocker = CraftableApparel.MissingFor(def, stuff, map).FirstOrDefault(each => each.Kind != MissingKind.Materials);
            if (blocker != null)
            {
                return new PlannedBill(def, stuff, target, PlannedBillOutcome.CantMake, why: blocker.Label);
            }
            List<Building_WorkTable> benches = map.listerBuildings.AllColonistBuildingsOfType<Building_WorkTable>().ToList();
            foreach (Building_WorkTable bench in benches)
            {
                // A suspended bill makes nothing, and a "do X times" bill stops (the game leaves a finished one at 0).
                foreach (Bill_Production bill in bench.BillStack.Bills.OfType<Bill_Production>()
                    .Where(bill => bill.recipe.ProducedThingDef == def && !bill.suspended && bill.repeatMode != BillRepeatModeDefOf.RepeatCount))
                {
                    if (bill.repeatMode == BillRepeatModeDefOf.TargetCount && bill.targetCount < target)
                    {
                        return new PlannedBill(def, stuff, target, PlannedBillOutcome.Raise, bench, bill);
                    }
                    return new PlannedBill(def, stuff, target, PlannedBillOutcome.AlreadyEnough, bench, bill);
                }
            }
            Building_WorkTable best = null;
            RecipeDef bestRecipe = null;
            bool anyBench = false;
            int Bills(Building_WorkTable bench) => bench.BillStack.Count + (planned.TryGetValue(bench, out int count) ? count : 0);
            foreach (Building_WorkTable bench in benches.OrderByDescending(Powered).ThenBy(Bills))
            {
                RecipeDef recipe = bench.def.AllRecipes.FirstOrDefault(each => each.ProducedThingDef == def && each.AvailableNow && each.AvailableOnNow(bench));
                if (recipe == null)
                {
                    continue;
                }
                anyBench = true;
                if (Bills(bench) < BillStack.MaxCount)
                {
                    best = bench;
                    bestRecipe = recipe;
                    break;
                }
            }
            if (best == null)
            {
                return new PlannedBill(def, stuff, target, anyBench ? PlannedBillOutcome.NoRoom : PlannedBillOutcome.CantMake,
                    why: anyBench ? null : "OL_Bills_NoBench".Translate().Resolve());
            }
            return new PlannedBill(def, stuff, target, PlannedBillOutcome.Add, best, recipe: bestRecipe);
        }

        /// <summary>A bench that needs power counts as usable only while it has it, so a working one is chosen first.</summary>
        private static bool Powered(Building_WorkTable bench)
        {
            CompPowerTrader power = bench.TryGetComp<CompPowerTrader>();
            return power == null || power.PowerOn;
        }

        /// <summary>
        /// "Do until you have" the target, counting what colonists wear, at the policy's hit point and quality ranges so
        /// a piece the policy would swap out doesn't count, made of the planned material.
        /// </summary>
        private static Bill_Production MakeBill(PlannedBill planned, ApparelPolicy policy)
        {
            var bill = (Bill_Production)planned.Recipe.MakeNewBill();
            bill.repeatMode = BillRepeatModeDefOf.TargetCount;
            bill.targetCount = planned.Target;
            bill.includeEquipped = true;
            bill.hpRange = policy.filter.AllowedHitPointsPercents;
            bill.qualityRange = policy.filter.AllowedQualityLevels;
            if (planned.Stuff != null)
            {
                foreach (ThingDef stuff in GenStuff.AllowedStuffsFor(planned.Item))
                {
                    bill.ingredientFilter.SetAllow(stuff, stuff == planned.Stuff);
                }
            }
            return bill;
        }
    }
}
