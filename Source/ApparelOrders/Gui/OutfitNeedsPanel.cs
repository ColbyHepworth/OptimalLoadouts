using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// What the previewed outfit needs before the colony can make it: research, benches, skills and materials, each with
    /// its icon and the pieces it's for. It reads the session every frame, so the list shrinks as needs are met.
    /// </summary>
    public static class OutfitNeedsPanel
    {
        /// <summary>Tall enough for vanilla's bar border, slim enough to sit in a row.</summary>
        private const float SkillBarHeight = 16f;

        /// <summary>Skill names line up across rows, as on the Character tab.</summary>
        private const float SkillNameWidth = 90f;

        /// <summary>A bar this long reads the gap at a glance without filling the row.</summary>
        private const float SkillBarMaxWidth = 180f;

        private const float GroupTitleHeight = 26f;

        /// <summary>Space above each group after the first, so the groups read apart.</summary>
        private const float GroupGap = 14f;

        /// <summary>Vanilla's standard button height.</summary>
        private const float ButtonHeight = 32f;

        private static readonly MissingKind[] Groups =
        {
            MissingKind.Research, MissingKind.Bench, MissingKind.Skill, MissingKind.Materials, MissingKind.NoRecipe,
        };

        private static readonly PlannedBillOutcome[] BillGroups =
        {
            PlannedBillOutcome.Add, PlannedBillOutcome.Raise, PlannedBillOutcome.AlreadyEnough, PlannedBillOutcome.NoRoom,
            PlannedBillOutcome.CantMake,
        };

        private static Vector2 scroll;

        /// <summary>The bills "Add bills" worked out, shown in place of the needs until confirmed or cancelled.</summary>
        private static List<PlannedBill> plan;

        private static ApparelPolicy planFor;

        public static void Draw(Rect rect, PreviewSession session)
        {
            Map map = session == null ? null : OutfitPreviewPanel.MadeNewOn(session);
            // The window's close button sits at the title's end.
            Rect title = new Rect(rect.x, rect.y, rect.width - Widgets.CloseButtonSize, UiStyle.RowHeight);
            if (session?.Outfit == null || session.Policy == null || map == null)
            {
                UiStyle.Note(new Rect(rect.x, title.yMax, rect.width, UiStyle.RowHeight), "OL_Needs_NotMadeNew".Translate());
                return;
            }
            using (new TextBlock(GameFont.Medium, TextAnchor.MiddleLeft))
            {
                Widgets.Label(title, session.Policy.label.Truncate(title.width));
            }
            Rect body = new Rect(rect.x, title.yMax, rect.width, rect.yMax - title.yMax);
            if (plan != null && planFor == session.Policy)
            {
                DrawPlan(body, session.Policy);
            }
            else
            {
                plan = null;
                DrawNeeds(body, session, map);
            }
        }

        private static void DrawNeeds(Rect body, PreviewSession session, Map map)
        {
            OutfitNeeds needs = CraftableApparel.NeedsFor(session.Outfit.Worn, map);
            float y = body.y;
            UiStyle.Note(new Rect(body.x, y, body.width, UiStyle.RowHeight), OutfitPreviewPanel.CanMakeText(needs));
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;
            Rect addBills = new Rect(body.x, body.yMax - ButtonHeight, body.width, ButtonHeight);
            if (Widgets.ButtonText(addBills, "OL_Bills_Button".Translate()))
            {
                StartPlan(session.Policy, map);
            }
            TooltipHandler.TipRegion(addBills, "OL_Bills_ButtonTip".Translate());
            Rect list = new Rect(body.x, y, body.width, addBills.y - UiStyle.Gap / 2f - y);
            if (needs.Needs.Count == 0)
            {
                UiStyle.Note(list, "OL_Needs_NothingMissing".Translate());
                return;
            }
            Rect view = new Rect(0f, 0f, list.width - UiStyle.ScrollbarWidth, Height(needs));
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                DrawGroups(view, needs);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void StartPlan(ApparelPolicy policy, Map map)
        {
            List<PlannedBill> worked = RoleBills.Plan(policy, map);
            if (worked.Count == 0)
            {
                bool anyone = map.mapPawns.FreeColonistsSpawned.Any(pawn => pawn.outfits?.CurrentApparelPolicy == policy);
                Messages.Message((anyone ? "OL_Bills_NothingResearched" : "OL_Bills_NoColonists").Translate(policy.label), MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }
            plan = worked;
            planFor = policy;
            scroll = Vector2.zero;
        }

        /// <summary>
        /// The bills "Add bills" would add or raise, grouped by what happens to each, with a box to leave one out, then
        /// Back and Add. The rest of the plan (already made, no room, can't make yet) is listed with why.
        /// </summary>
        private static void DrawPlan(Rect body, ApparelPolicy policy)
        {
            int ticked = plan.Count(each => each.Acts && each.Included);
            Rect buttons = new Rect(body.x, body.yMax - ButtonHeight, body.width, ButtonHeight);
            Rect back = new Rect(buttons.x, buttons.y, (buttons.width - UiStyle.Gap) / 2f, buttons.height);
            Rect add = new Rect(back.xMax + UiStyle.Gap, buttons.y, back.width, buttons.height);
            if (Widgets.ButtonText(back, "OL_Bills_Back".Translate()))
            {
                plan = null;
                return;
            }
            if (Widgets.ButtonText(add, "OL_Bills_Confirm".Translate(ticked), active: ticked > 0))
            {
                (int added, int raised) = RoleBills.Apply(plan, policy);
                Messages.Message("OL_Bills_Done".Translate(added, raised), MessageTypeDefOf.TaskCompletion, historical: false);
                plan = null;
                return;
            }
            Rect list = new Rect(body.x, body.y + UiStyle.Gap / 2f, body.width, buttons.y - UiStyle.Gap / 2f - body.y - UiStyle.Gap / 2f);
            Rect view = new Rect(0f, 0f, list.width - UiStyle.ScrollbarWidth, PlanHeight());
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                float y = view.y;
                foreach (PlannedBillOutcome outcome in BillGroups)
                {
                    List<PlannedBill> group = plan.Where(each => each.Outcome == outcome).ToList();
                    if (group.Count == 0)
                    {
                        continue;
                    }
                    if (y > view.y)
                    {
                        y += GroupGap;
                    }
                    DrawGroupTitle(ref y, view.width, ("OL_Bills_Group_" + outcome).Translate());
                    foreach (PlannedBill planned in group)
                    {
                        DrawBillRow(Row(view, y), planned);
                        y += UiStyle.RowHeight;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float PlanHeight()
        {
            float height = 0f;
            foreach (PlannedBillOutcome outcome in BillGroups)
            {
                int rows = plan.Count(each => each.Outcome == outcome);
                if (rows > 0)
                {
                    height += (height > 0f ? GroupGap : 0f) + GroupTitleHeight + rows * UiStyle.RowHeight;
                }
            }
            return height;
        }

        /// <summary>
        /// One bill: a box to leave it out (for one to add or raise), the item's icon and name, how many ("×3", or "×1 → ×3"
        /// when raised, its tooltip naming who they're for), and the bench's icon. The rest say why in grey.
        /// </summary>
        private static void DrawBillRow(Rect row, PlannedBill planned)
        {
            float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
            Rect box = new Rect(row.x, iconY, UiStyle.IconSize, UiStyle.IconSize);
            if (planned.Acts)
            {
                bool included = planned.Included;
                Widgets.Checkbox(box.x, box.y, ref included, UiStyle.IconSize);
                planned.Included = included;
            }
            Rect icon = new Rect(box.xMax + UiStyle.Gap / 2f, iconY, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.ThingIcon(icon, planned.Item, planned.Stuff);
            Rect bench = new Rect(row.xMax - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
            if (planned.Bench != null)
            {
                Widgets.ThingIcon(bench, planned.Bench);
                TooltipHandler.TipRegion(bench, planned.Bench.LabelCap);
            }
            string count = planned.Outcome == PlannedBillOutcome.Raise
                ? "OL_Bills_RaiseCount".Translate(planned.Existing.targetCount, planned.Target).Resolve()
                : "OL_Bills_Count".Translate(planned.Target).Resolve();
            string why = WhyNot(planned);
            float countWidth;
            using (new TextBlock(GameFont.Small))
            {
                countWidth = Text.CalcSize(why ?? count).x + UiStyle.Gap;
            }
            Rect countRect = new Rect(bench.x - UiStyle.Gap / 2f - countWidth, row.y, countWidth, row.height);
            Rect name = new Rect(icon.xMax + UiStyle.Gap / 2f, row.y, countRect.x - icon.xMax - UiStyle.Gap, row.height);
            string label = planned.Stuff == null ? planned.Item.LabelCap.Resolve() : GenLabel.ThingLabel(planned.Item, planned.Stuff).CapitalizeFirst();
            Color color = planned.Acts && planned.Included ? Color.white : ColoredText.SubtleGrayColor;
            using (new TextBlock(TextAnchor.MiddleLeft, color))
            {
                Widgets.Label(name, label.Truncate(name.width));
            }
            using (new TextBlock(TextAnchor.MiddleRight, why == null ? color : ColoredText.SubtleGrayColor))
            {
                Widgets.Label(countRect, why ?? count);
            }
            TooltipHandler.TipRegion(new Rect(icon.x, row.y, countRect.xMax - icon.x, row.height),
                "OL_Bills_ForTip".Translate(planned.Target, planned.For.Select(pawn =>
                    "OL_Bills_ForPawn".Translate(pawn.LabelShort, pawn.outfits.CurrentApparelPolicy.label).Resolve()).ToLineList("  ")));
        }

        /// <summary>A group's title on a solid band across the list, as the stats table's sections have.</summary>
        private static void DrawGroupTitle(ref float y, float width, string label)
        {
            Rect band = new Rect(0f, y, width, GroupTitleHeight);
            Widgets.DrawBoxSolid(band, UiStyle.SectionBandColor);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), label);
            }
            y += GroupTitleHeight;
        }

        /// <summary>Why a bill won't be added or raised, short, or null for one that will.</summary>
        private static string WhyNot(PlannedBill planned)
        {
            switch (planned.Outcome)
            {
                case PlannedBillOutcome.AlreadyEnough:
                    return "OL_Bills_Enough".Translate();
                case PlannedBillOutcome.NoRoom:
                    return "OL_Bills_NoRoom".Translate();
                case PlannedBillOutcome.CantMake:
                    return planned.Why;
                default:
                    return null;
            }
        }

        private static float Height(OutfitNeeds needs)
        {
            float height = 0f;
            foreach (MissingKind kind in Groups)
            {
                int rows = needs.Needs.Count(need => GroupOf(need.Missing.Kind) == kind);
                if (rows > 0)
                {
                    height += (height > 0f ? GroupGap : 0f) + GroupTitleHeight + rows * UiStyle.RowHeight;
                }
            }
            return height;
        }

        private static void DrawGroups(Rect view, OutfitNeeds needs)
        {
            float y = view.y;
            foreach (MissingKind kind in Groups)
            {
                List<OutfitNeed> group = needs.Needs.Where(need => GroupOf(need.Missing.Kind) == kind).ToList();
                if (group.Count == 0)
                {
                    continue;
                }
                if (y > view.y)
                {
                    y += GroupGap;
                }
                DrawGroupTitle(ref y, view.width, ("OL_Needs_Group_" + kind).Translate());
                foreach (OutfitNeed need in group)
                {
                    Rect row = Row(view, y);
                    if (need.Missing.Kind == MissingKind.Skill && need.Missing.Skill != null)
                    {
                        DrawSkill(row, need.Missing);
                    }
                    else
                    {
                        DrawNeed(row, need.Missing);
                    }
                    string pieces = "OL_Needs_For".Translate() + "\n" + need.For.Select(piece => piece.LabelNoParenthesis).Distinct().ToLineList("  ");
                    TooltipHandler.TipRegion(row, NeedTip(need.Missing) + "\n\n" + pieces);
                    y += UiStyle.RowHeight;
                }
            }
        }

        /// <summary>A recipe the colony can't use is filed with items no recipe makes, as both can't be made.</summary>
        private static MissingKind GroupOf(MissingKind kind)
        {
            return kind == MissingKind.Unavailable ? MissingKind.NoRecipe : kind;
        }

        private static void DrawNeed(Rect row, MissingToMake need)
        {
            Rect icon = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            if (need.Thing != null)
            {
                Widgets.ThingIcon(icon, need.Thing);
            }
            else if (need.Kind == MissingKind.Research)
            {
                GUI.DrawTexture(icon, UiStyle.ResearchIcon);
            }
            Rect label = new Rect(icon.xMax + UiStyle.Gap, row.y, row.xMax - icon.xMax - UiStyle.Gap, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(label, need.Label.Truncate(label.width));
            }
        }

        /// <summary>What the need is in full: the label, or for a skill who's closest.</summary>
        private static string NeedTip(MissingToMake need)
        {
            return need.Kind != MissingKind.Skill || need.BestPawn == null
                ? need.Label
                : "OL_Missing_SkillBest".Translate(need.BestPawn.LabelShort, need.Skill.LabelCap, need.BestLevel, need.LevelNeeded).Resolve();
        }

        /// <summary>A row of a list, in from its title and clear of the right edge.</summary>
        private static Rect Row(Rect view, float y)
        {
            return new Rect(view.x + UiStyle.Indent, y, view.width - UiStyle.Indent - UiStyle.Gap, UiStyle.RowHeight);
        }

        /// <summary>
        /// A skill the outfit needs: its name, vanilla's standard bar filled toward the level needed, and "best / needed".
        /// The bar's end is the level needed, so how close the colony is shows at a glance.
        /// </summary>
        private static void DrawSkill(Rect row, MissingToMake need)
        {
            string name = need.Skill.LabelCap.Resolve();
            string numbers = "OL_Missing_SkillNumbers".Translate(need.BestLevel, need.LevelNeeded);
            float nameWidth;
            float numbersWidth;
            using (new TextBlock(GameFont.Small))
            {
                nameWidth = Text.CalcSize(name).x + UiStyle.Gap;
                numbersWidth = Text.CalcSize(numbers).x + UiStyle.Gap;
            }
            // A skill has no icon, so its name starts in the icon column.
            Rect nameRect = new Rect(row.x, row.y, Mathf.Max(nameWidth, SkillNameWidth), row.height);
            // The numbers follow the bar, so they read with it.
            float barWidth = Mathf.Min(row.xMax - numbersWidth - UiStyle.Gap - nameRect.xMax - UiStyle.Gap, SkillBarMaxWidth);
            Rect bar = new Rect(nameRect.xMax + UiStyle.Gap, row.y + (row.height - SkillBarHeight) / 2f, barWidth, SkillBarHeight);
            Rect numbersRect = new Rect(bar.xMax + UiStyle.Gap, row.y, numbersWidth, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(nameRect, name);
            }
            if (bar.width > 0f)
            {
                Widgets.FillableBar(bar, need.LevelNeeded <= 0 ? 1f : Mathf.Clamp01(need.BestLevel / (float)need.LevelNeeded));
            }
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(numbersRect, numbers);
            }
        }
    }
}
