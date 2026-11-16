using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
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

    /// <summary>
    /// What "Add bills" would do with one item of the policy's outfits. The player can change a new bill's repeat mode,
    /// count, material and bench before confirming, and a raised bill's count. What the plan picked is kept, for Reset.
    /// </summary>
    public sealed class PlannedBill
    {
        public ThingDef Item { get; }

        /// <summary>The bill's material, or null for the bench's usual ingredients. Starts as the material the policy's material setting gives the item, as the preview shows it.</summary>
        public ThingDef Stuff { get; private set; }

        /// <summary>
        /// "Do X times" (the game's RepeatCount), which makes what's missing now and stops, the start for a new bill, or
        /// "do until you have" (TargetCount), which keeps the count up as pieces wear out.
        /// </summary>
        public BillRepeatModeDef RepeatMode { get; private set; } = BillRepeatModeDefOf.TargetCount;

        private BillRepeatModeDef plannedRepeatMode = BillRepeatModeDefOf.TargetCount;

        /// <summary>How many to make, or how many to have with worn ones included, by the repeat mode.</summary>
        public int Target { get; private set; }

        /// <summary>The colonists on this policy whose outfits hold the item but who don't wear one now.</summary>
        public int Lacking { get; internal set; }

        /// <summary>The pieces in storage this policy's colonists could take: allowed by it, not forbidden, not burning.</summary>
        public int Stored { get; internal set; }

        /// <summary>What "do X times" bills for the item still have to make.</summary>
        public int InBills { get; internal set; }

        /// <summary>The count for "do X times": the colonists lacking it, less what's stored and what bills will make.</summary>
        public int Missing => System.Math.Max(0, Lacking - Stored - InBills);

        /// <summary>Whether it makes a set number and stops, rather than keeping a count.</summary>
        public bool MakesOnce => RepeatMode == BillRepeatModeDefOf.RepeatCount;

        /// <summary>Whether it keeps making with no count ("do forever"), so its count doesn't matter.</summary>
        public bool MakesForever => RepeatMode == BillRepeatModeDefOf.Forever;

        public PlannedBillOutcome Outcome { get; }

        /// <summary>The bench a new bill goes to, or the one with the bill to raise.</summary>
        public Building_WorkTable Bench { get; private set; }

        /// <summary>The bill that already makes it, for Raise and AlreadyEnough.</summary>
        public Bill_Production Existing { get; }

        internal RecipeDef Recipe { get; private set; }

        /// <summary>The material the plan picked, before any edit.</summary>
        public ThingDef PlannedStuff { get; }

        /// <summary>The count "do until you have" starts at: every colonist on this policy whose outfit holds the item.</summary>
        public int PlannedTarget { get; }

        /// <summary>The bench the plan picked, before any edit.</summary>
        public Building_WorkTable PlannedBench { get; }

        private readonly RecipeDef plannedRecipe;

        /// <summary>For CantMake, the first thing the colony lacks.</summary>
        public string Why { get; }

        /// <summary>The colonists on this policy whose outfits hold the item, which the target counts.</summary>
        public IReadOnlyList<Pawn> For { get; internal set; } = new List<Pawn>();

        /// <summary>Whether to make or raise it. Ticked to start; the player can untick it before confirming.</summary>
        public bool Included { get; set; } = true;

        /// <summary>Whether it's a bill to add or raise, so the player can tick and edit it.</summary>
        public bool Acts => Outcome == PlannedBillOutcome.Add || Outcome == PlannedBillOutcome.Raise;

        /// <summary>
        /// Whether confirming changes a bill: ticked, a count above 0 (or "do forever"), and a new bill or a raise above
        /// the bill's current target.
        /// </summary>
        public bool WillChange => Included && (Target > 0 || MakesForever)
            && (Outcome == PlannedBillOutcome.Add || (Outcome == PlannedBillOutcome.Raise && Target > Existing.targetCount));

        /// <summary>Whether the count differs from what the plan picks for the current repeat mode. "Do forever" has none.</summary>
        public bool TargetEdited => !MakesForever && Target != (MakesOnce ? Missing : PlannedTarget);

        public bool RepeatModeEdited => RepeatMode != plannedRepeatMode;

        public bool StuffEdited => Stuff != PlannedStuff;

        public bool BenchEdited => Bench != PlannedBench;

        public bool Edited => TargetEdited || RepeatModeEdited || StuffEdited || BenchEdited;

        /// <summary>Whether its material can be chosen: a new bill for an item made from materials.</summary>
        public bool CanChooseStuff => Outcome == PlannedBillOutcome.Add && Item.MadeFromStuff && Recipe != null;

        internal PlannedBill(ThingDef item, ThingDef stuff, int target, PlannedBillOutcome outcome, Building_WorkTable bench = null,
            Bill_Production existing = null, RecipeDef recipe = null, string why = null)
        {
            Item = item;
            Stuff = PlannedStuff = stuff;
            Target = PlannedTarget = target;
            Outcome = outcome;
            Bench = PlannedBench = bench;
            Existing = existing;
            Recipe = plannedRecipe = recipe;
            Why = why;
        }

        /// <summary>Sets how many to have, for a bill to add or raise. A count of 0 leaves it out.</summary>
        public void SetTarget(int count)
        {
            if (Acts)
            {
                Target = System.Math.Max(0, count);
            }
        }

        /// <summary>A new bill starts as "do X times" what's missing, once <see cref="Lacking"/>, <see cref="Stored"/> and <see cref="InBills"/> are known.</summary>
        internal void StartMakingOnce()
        {
            plannedRepeatMode = BillRepeatModeDefOf.RepeatCount;
            RepeatMode = plannedRepeatMode;
            Target = Missing;
        }

        /// <summary>
        /// Sets a new bill to "do X times" (RepeatCount), "do until you have" (TargetCount) or "do forever" (Forever),
        /// with the count the plan picks for it: what this policy's colonists are missing, or every one whose outfit holds
        /// it. "Do forever" keeps the latter, unused. Other modes are ignored.
        /// </summary>
        public void SetRepeatMode(BillRepeatModeDef mode)
        {
            if (Outcome != PlannedBillOutcome.Add
                || (mode != BillRepeatModeDefOf.TargetCount && mode != BillRepeatModeDefOf.RepeatCount && mode != BillRepeatModeDefOf.Forever))
            {
                return;
            }
            RepeatMode = mode;
            Target = MakesOnce ? Missing : PlannedTarget;
        }

        /// <summary>Sets a new bill's material, or null for the bench's usual ingredients. A material its recipe doesn't take is ignored.</summary>
        public void SetStuff(ThingDef stuff)
        {
            if (CanChooseStuff && (stuff == null || Recipe.fixedIngredientFilter.Allows(stuff)))
            {
                Stuff = stuff;
            }
        }

        /// <summary>
        /// Moves a new bill to another bench, with that bench's recipe for the item. A bench that can't make it now is
        /// ignored. A material the new recipe doesn't take goes back to the bench's usual ingredients.
        /// </summary>
        public void SetBench(Building_WorkTable bench)
        {
            RecipeDef recipe = bench == null ? null : RoleBills.RecipeAt(bench, Item);
            if (Outcome != PlannedBillOutcome.Add || recipe == null)
            {
                return;
            }
            Bench = bench;
            Recipe = recipe;
            if (Stuff != null && !recipe.fixedIngredientFilter.Allows(Stuff))
            {
                Stuff = null;
            }
        }

        /// <summary>Puts the repeat mode, count, material and bench back to what the plan picked.</summary>
        public void Reset()
        {
            RepeatMode = plannedRepeatMode;
            Target = MakesOnce ? Missing : PlannedTarget;
            Stuff = PlannedStuff;
            Bench = PlannedBench;
            Recipe = plannedRecipe;
        }
    }

    /// <summary>A bench a planned bill could move to, as its bench menu lists it.</summary>
    public sealed class BenchChoice
    {
        public Building_WorkTable Bench { get; }

        /// <summary>Whether it works now: it needs no power, or has it.</summary>
        public bool Powered { get; }

        /// <summary>Its bills, plus the plan's other new bills on it.</summary>
        public int Bills { get; }

        /// <summary>Whether it has as many bills as the game allows, so the bill can't move there.</summary>
        public bool Full => Bills >= BillStack.MaxCount;

        internal BenchChoice(Building_WorkTable bench, bool powered, int bills)
        {
            Bench = bench;
            Powered = powered;
            Bills = bills;
        }
    }

    /// <summary>
    /// Bills for what a policy's colonists are missing: one bill per item of their outfits, "do until you have" one for
    /// each wearer to start, at the bench the colony would use, in the policy's material. Worked out first, then applied
    /// once the player confirms.
    /// </summary>
    public static class RoleBills
    {
        /// <summary>
        /// What adding bills for the policy would do, changing nothing. Each of the policy's colonists is predicted in the
        /// outfit it gives them, made new from what the colony has researched, and the items in their outfits are
        /// planned for them alone. A new bill makes what they're missing once ("do X times"): those who don't wear one,
        /// less what's stored and what other "do X times" bills will make. "Do until you have" counts one per wearer on
        /// this policy. A new bill's material is the one the policy's material setting gives the item, the setting the
        /// preview shows pieces in. With no colonist on the policy, <paramref name="previewed"/> (the outfit the preview
        /// shows) is planned instead, one of each.
        /// </summary>
        public static List<PlannedBill> Plan(ApparelPolicy policy, Map map, IReadOnlyList<Apparel> previewed = null)
        {
            var plan = new List<PlannedBill>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.Where(pawn => pawn.outfits?.CurrentApparelPolicy == policy && pawn.apparel != null).ToList();
            var outfits = colonists.ToDictionary(pawn => pawn, OutfitOf);
            LimitMaterial setting = ApparelOrdersComponent.Current?.LimitMaterialOf(policy) ?? LimitMaterial.Best;
            MaterialChoice choice = MaterialChoice.For(setting);
            // What the colony lacks to make an item is judged in a material the policy names, as the needs window judges it.
            ThingDef stuff = setting.Stuff;
            bool fromPreview = colonists.Count == 0 && previewed != null;
            var wanted = new List<ThingDef>();
            foreach (Apparel piece in fromPreview ? previewed : outfits.Values.SelectMany(outfit => outfit))
            {
                if (!wanted.Contains(piece.def))
                {
                    wanted.Add(piece.def);
                }
            }
            // Bills this plan puts on each bench, so they count toward its limit and its place in line.
            var planned = new Dictionary<Building_WorkTable, int>();
            foreach (ThingDef def in wanted.OrderBy(def => def.label))
            {
                List<Pawn> wearers = outfits.Where(outfit => outfit.Value.Any(piece => piece.def == def)).Select(outfit => outfit.Key).ToList();
                // With nobody on the policy, the preview's outfit is one set that nobody wears yet.
                int toKeep = fromPreview ? 1 : wearers.Count;
                int lacking = fromPreview ? 1 : wearers.Count(pawn => !pawn.apparel.WornApparel.Any(piece => piece.def == def));
                int stored = StoredFor(def, policy, map);
                int inBills = MakingOnce(def, map).Sum(bill => bill.repeatCount);
                PlannedBill bill = PlanFor(def, def.MadeFromStuff ? stuff : null, toKeep, lacking - stored - inBills, map, planned, choice, policy);
                bill.For = wearers;
                bill.Lacking = lacking;
                bill.Stored = stored;
                bill.InBills = inBills;
                if (bill.Outcome == PlannedBillOutcome.Add)
                {
                    bill.StartMakingOnce();
                    planned.TryGetValue(bill.Bench, out int count);
                    planned[bill.Bench] = count + 1;
                }
                plan.Add(bill);
            }
            return plan;
        }

        /// <summary>Makes the new bills and raises the targets the plan names, the ones that will change. Returns how many bills it added and raised.</summary>
        public static (int added, int raised) Apply(IEnumerable<PlannedBill> plan, ApparelPolicy policy)
        {
            int added = 0;
            int raised = 0;
            foreach (PlannedBill planned in plan.Where(each => each.WillChange))
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

        /// <summary>
        /// Every built bench that can make the item now, in the order the plan picks from: powered first, then the fewest
        /// bills. Each counts the plan's other new bills on it.
        /// </summary>
        public static List<BenchChoice> BenchesFor(PlannedBill planned, IEnumerable<PlannedBill> plan, Map map)
        {
            var others = new Dictionary<Building_WorkTable, int>();
            foreach (PlannedBill other in plan.Where(each => each != planned && each.Outcome == PlannedBillOutcome.Add && each.WillChange))
            {
                others.TryGetValue(other.Bench, out int count);
                others[other.Bench] = count + 1;
            }
            return BenchesMaking(planned.Item, map, others)
                .Select(each => new BenchChoice(each.bench, Powered(each.bench), Bills(each.bench, others)))
                .ToList();
        }

        /// <summary>The materials a new bill for the item can be limited to: those the item can be made of that its recipe takes.</summary>
        public static List<ThingDef> MaterialsFor(PlannedBill planned)
        {
            if (!planned.CanChooseStuff)
            {
                return new List<ThingDef>();
            }
            return GenStuff.AllowedStuffsFor(planned.Item).Where(stuff => planned.Recipe.fixedIngredientFilter.Allows(stuff)).ToList();
        }

        /// <summary>
        /// The material a choice gives a new bill for the item, as the preview picks one: the first the choice ranks for
        /// the policy's priorities that the bill's recipe takes and the policy allows the piece in, else the first the
        /// recipe takes. Null when the item isn't made of materials.
        /// </summary>
        public static ThingDef MaterialFor(PlannedBill planned, MaterialChoice choice, ApparelPolicy policy)
        {
            return planned.CanChooseStuff ? MaterialFor(planned.Item, planned.Recipe, choice, policy) : null;
        }

        private static ThingDef MaterialFor(ThingDef item, RecipeDef recipe, MaterialChoice choice, ApparelPolicy policy)
        {
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(policy) ?? new List<OrderLine>();
            List<ThingDef> takes = choice.InOrderOfPreference(item, lines).Where(stuff => recipe.fixedIngredientFilter.Allows(stuff)).ToList();
            return takes.FirstOrDefault(stuff => AllowedIn(item, stuff, policy)) ?? takes.FirstOrDefault();
        }

        /// <summary>Whether the policy lets its colonists wear the piece in that material, such as burnable apparel.</summary>
        private static bool AllowedIn(ThingDef item, ThingDef stuff, ApparelPolicy policy)
        {
            Apparel piece = ImaginedPieces.For(item, stuff, QualityCategory.Normal);
            return piece != null && policy.filter.Allows(piece);
        }

        /// <summary>The bench's recipe that makes the item now, or null.</summary>
        internal static RecipeDef RecipeAt(Building_WorkTable bench, ThingDef item)
        {
            return bench.def.AllRecipes.FirstOrDefault(each => each.ProducedThingDef == item && each.AvailableNow && each.AvailableOnNow(bench));
        }

        /// <summary>The colony's benches that can make the item now, with the recipe, powered first, then the fewest bills.</summary>
        private static IEnumerable<(Building_WorkTable bench, RecipeDef recipe)> BenchesMaking(ThingDef item, Map map, Dictionary<Building_WorkTable, int> planned)
        {
            return map.listerBuildings.AllColonistBuildingsOfType<Building_WorkTable>()
                .OrderByDescending(Powered)
                .ThenBy(bench => Bills(bench, planned))
                .Select(bench => (bench, recipe: RecipeAt(bench, item)))
                .Where(each => each.recipe != null);
        }

        /// <summary>A bench's bills, with the ones a plan puts on it.</summary>
        private static int Bills(Building_WorkTable bench, Dictionary<Building_WorkTable, int> planned)
        {
            return bench.BillStack.Count + (planned.TryGetValue(bench, out int count) ? count : 0);
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

        /// <summary>
        /// The pieces of the item in storage the policy's colonists could take, gathered as the game's apparel check
        /// gathers them (outfit stands included): allowed by the policy, not forbidden, not burning.
        /// </summary>
        private static int StoredFor(ThingDef def, ApparelPolicy policy, Map map)
        {
            var things = new List<Thing>();
            map.listerThings.GetAllThings(in things, ThingRequestGroup.Apparel, null, true);
            foreach (IHaulSource source in map.haulDestinationManager.AllHaulSourcesListForReading)
            {
                things.AddRange(source.GetDirectlyHeldThings().Where(held => held is Apparel));
            }
            return things.Distinct().Count(thing => thing.def == def && policy.filter.Allows(thing) && thing.IsInAnyStorage()
                && !thing.IsForbidden(Faction.OfPlayer) && !thing.IsBurning());
        }

        /// <summary>The colony's "do X times" bills for the item that still have some to make and aren't suspended.</summary>
        private static IEnumerable<Bill_Production> MakingOnce(ThingDef def, Map map)
        {
            return map.listerBuildings.AllColonistBuildingsOfType<Building_WorkTable>()
                .SelectMany(bench => bench.BillStack.Bills.OfType<Bill_Production>())
                .Where(bill => bill.recipe.ProducedThingDef == def && !bill.suspended && bill.repeatMode == BillRepeatModeDefOf.RepeatCount
                    && bill.repeatCount > 0);
        }

        /// <summary>
        /// One item's bill. A "do until you have" bill for it is raised to the target, or left when it's high enough.
        /// Otherwise "do X times" bills that already cover what's missing are enough. Anything else is a new bill.
        /// </summary>
        private static PlannedBill PlanFor(ThingDef def, ThingDef stuff, int target, int missing, Map map, Dictionary<Building_WorkTable, int> planned,
            MaterialChoice choice, ApparelPolicy policy)
        {
            // Materials short and skills nobody has yet aren't reasons not to add a bill: it waits for them, as any bill does.
            MissingToMake blocker = CraftableApparel.MissingFor(def, stuff, map)
                .FirstOrDefault(each => each.Kind != MissingKind.Materials && each.Kind != MissingKind.Skill);
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
            Bill_Production makingOnce = MakingOnce(def, map).FirstOrDefault();
            if (makingOnce != null && missing <= 0)
            {
                return new PlannedBill(def, stuff, target, PlannedBillOutcome.AlreadyEnough, makingOnce.billStack.billGiver as Building_WorkTable, makingOnce);
            }
            Building_WorkTable best = null;
            RecipeDef bestRecipe = null;
            bool anyBench = false;
            foreach ((Building_WorkTable bench, RecipeDef recipe) in BenchesMaking(def, map, planned))
            {
                anyBench = true;
                if (Bills(bench, planned) < BillStack.MaxCount)
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
            ThingDef material = def.MadeFromStuff ? MaterialFor(def, bestRecipe, choice, policy) : null;
            return new PlannedBill(def, material, target, PlannedBillOutcome.Add, best, recipe: bestRecipe);
        }

        /// <summary>A bench that needs power counts as usable only while it has it, so a working one is chosen first.</summary>
        private static bool Powered(Building_WorkTable bench)
        {
            CompPowerTrader power = bench.TryGetComp<CompPowerTrader>();
            return power == null || power.PowerOn;
        }

        /// <summary>
        /// "Do until you have" the target, counting what colonists wear, or "do X times", at the policy's hit point and
        /// quality ranges so a piece the policy would swap out doesn't count, made of the planned material.
        /// </summary>
        private static Bill_Production MakeBill(PlannedBill planned, ApparelPolicy policy)
        {
            var bill = (Bill_Production)planned.Recipe.MakeNewBill();
            bill.repeatMode = planned.RepeatMode;
            if (planned.MakesOnce)
            {
                bill.repeatCount = planned.Target;
            }
            else if (!planned.MakesForever)
            {
                bill.targetCount = planned.Target;
                bill.includeEquipped = true;
            }
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
