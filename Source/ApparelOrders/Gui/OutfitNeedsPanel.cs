using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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

        /// <summary>The repeat mode's button, "Until you have" or "Times".</summary>
        private const float RepeatModeWidth = 120f;

        private const float CountFieldWidth = 44f;

        /// <summary>Room for "1 here + 2 others" or "2 lack it, 1 stored".</summary>
        private const float CountHintWidth = 120f;

        private const float BillColumnWidth = RepeatModeWidth + UiStyle.IconGap + CountFieldWidth + UiStyle.IconGap + CountHintWidth;

        private const float MaterialColumnWidth = 130f;

        /// <summary>The bench's icon and a name like "Electric tailoring bench".</summary>
        private const float BenchColumnWidth = 190f;

        /// <summary>The policy's material button above the table.</summary>
        private const float PolicyMaterialWidth = 260f;

        /// <summary>A count past any colony's size, so a typo can't ask for thousands.</summary>
        private const int MaxCount = 999;

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

        private static Map planMap;

        /// <summary>The policy window the plan was made in. Once it closes, however it closes, the plan is dropped.</summary>
        private static Window planWindow;

        /// <summary>What's typed in each row's count box, kept between frames as the game's number boxes need.</summary>
        private static readonly Dictionary<PlannedBill, string> countBuffers = new Dictionary<PlannedBill, string>();

        /// <summary>What's typed in the box that sets every bill's count. Empty until something is typed.</summary>
        private static string everyCountBuffer = string.Empty;

        /// <summary>The number in that box last given to every bill.</summary>
        private static string everyCountApplied = string.Empty;

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
            if (plan != null && Find.WindowStack.WindowOfType<Dialog_ManageApparelPolicies>() != planWindow)
            {
                // The colony moved on while the policy window was closed, so the plan may name benches or bills that are gone.
                ClearPlan();
            }
            if (plan != null && planFor == session.Policy)
            {
                DrawPlan(body, session);
            }
            else
            {
                ClearPlan();
                DrawNeeds(body, session, map);
            }
        }

        /// <summary>Whether the panel shows a bills plan for this session, which needs a wider window than the needs.</summary>
        public static bool ShowsPlan(PreviewSession session)
        {
            return plan != null && session != null && planFor == session.Policy;
        }

        private static void ClearPlan()
        {
            plan = null;
            planFor = null;
            planMap = null;
            planWindow = null;
            countBuffers.Clear();
            everyCountBuffer = string.Empty;
            everyCountApplied = string.Empty;
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
                StartPlan(session, map);
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

        /// <summary>Plans the bills for the policy's colonists, or for the outfit the preview shows when nobody is on it yet.</summary>
        private static void StartPlan(PreviewSession session, Map map)
        {
            ApparelPolicy policy = session.Policy;
            List<PlannedBill> worked = RoleBills.Plan(policy, map, session.Outfit?.Worn);
            if (worked.Count == 0)
            {
                Messages.Message("OL_Bills_NothingResearched".Translate(policy.label), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            ClearPlan();
            plan = worked;
            planFor = policy;
            planMap = map;
            planWindow = Find.WindowStack.WindowOfType<Dialog_ManageApparelPolicies>();
            scroll = Vector2.zero;
        }

        /// <summary>
        /// The bills "Add bills" would add or raise, grouped by what happens to each, as a table under the policy's
        /// material: a box to leave one out, the item, then its repeat mode and count, material and bench, each editable
        /// on a new bill (the count on a raised one). Then Back, Reset and Add. The rest of the plan (already made, no
        /// room, can't make yet) is listed with why.
        /// </summary>
        private static void DrawPlan(Rect body, PreviewSession session)
        {
            ApparelPolicy policy = session.Policy;
            int ticked = plan.Count(each => each.WillChange);
            Rect buttons = new Rect(body.x, body.yMax - ButtonHeight, body.width, ButtonHeight);
            float buttonWidth = (buttons.width - 2f * UiStyle.Gap) / 3f;
            Rect back = new Rect(buttons.x, buttons.y, buttonWidth, buttons.height);
            Rect reset = new Rect(back.xMax + UiStyle.Gap, buttons.y, buttonWidth, buttons.height);
            Rect add = new Rect(reset.xMax + UiStyle.Gap, buttons.y, buttonWidth, buttons.height);
            if (Widgets.ButtonText(back, "OL_Bills_Back".Translate()))
            {
                ClearPlan();
                return;
            }
            bool edited = plan.Any(each => each.Edited);
            if (ButtonText(reset, "OL_Bills_Reset".Translate(), edited))
            {
                foreach (PlannedBill planned in plan)
                {
                    planned.Reset();
                }
                countBuffers.Clear();
                everyCountBuffer = string.Empty;
                everyCountApplied = string.Empty;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(reset, (edited ? "OL_Bills_ResetTip" : "OL_Bills_NothingToReset").Translate());
            if (ButtonText(add, "OL_Bills_Confirm".Translate(ticked), ticked > 0))
            {
                (int added, int raised) = RoleBills.Apply(plan, policy);
                Messages.Message("OL_Bills_Done".Translate(added, raised), MessageTypeDefOf.TaskCompletion, historical: false);
                ClearPlan();
                Find.WindowStack.TryRemove(typeof(Window_OutfitNeeds), doCloseSound: false);
                return;
            }
            Rect header = new Rect(body.x, body.y + UiStyle.Gap / 2f, body.width - UiStyle.ScrollbarWidth, UiStyle.RowHeight);
            DrawColumnTitles(Row(header, header.y), session);
            Rect list = new Rect(body.x, header.yMax, body.width, buttons.y - UiStyle.Gap / 2f - header.yMax);
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
                        if (plan == null)
                        {
                            // A bench was shown on the map, which drops the plan.
                            return;
                        }
                        y += UiStyle.RowHeight;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>A button whose text greys while it can't be clicked, since the game draws an inactive button as a working one.</summary>
        private static bool ButtonText(Rect rect, string label, bool active)
        {
            using (new TextBlock(active ? Color.white : ColoredText.SubtleGrayColor))
            {
                return Widgets.ButtonText(rect, label, active: active);
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

        /// <summary>Where a row's bill (repeat mode and count), material and bench go, lined up under the column titles.</summary>
        private struct BillColumns
        {
            public Rect Bill;
            public Rect Material;
            public Rect Bench;

            public BillColumns(Rect row)
            {
                Bench = new Rect(row.xMax - BenchColumnWidth, row.y, BenchColumnWidth, row.height);
                Material = new Rect(Bench.x - UiStyle.Gap - MaterialColumnWidth, row.y, MaterialColumnWidth, row.height);
                Bill = new Rect(Material.x - UiStyle.Gap - BillColumnWidth, row.y, BillColumnWidth, row.height);
            }
        }

        /// <summary>
        /// The column titles, each over the left edge of its controls, and over the items' names the policy's material:
        /// the outfit planner's own setting, with its picker. Changing it plans the bills again in the new material.
        /// </summary>
        private static void DrawColumnTitles(Rect row, PreviewSession session)
        {
            var columns = new BillColumns(row);
            float buttonWidth = Mathf.Min(PolicyMaterialWidth, columns.Bill.x - row.x - UiStyle.Gap);
            Rect material = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, buttonWidth, UiStyle.IconSize);
            TooltipHandler.TipRegion(material, "OL_Bills_PolicyMaterialTip".Translate() + "\n" + "OL_Bills_PolicyMaterialReplanTip".Translate());
            DrawEveryBillMode(columns.Bill);
            if (Widgets.ButtonText(material, "OL_Preview_Material".Translate(session.Material.Label).ToString().Truncate(material.width - UiStyle.Gap)))
            {
                Map map = planMap;
                void Pick(MaterialChoice choice)
                {
                    session.Material = choice;
                    // Cleared first, so a plan that comes back empty doesn't leave the old one under the new material.
                    ClearPlan();
                    StartPlan(session, map);
                }
                var general = new List<(string, Action)>
                {
                    (MaterialChoice.BestForOrder.Label, () => Pick(MaterialChoice.BestForOrder)),
                    (MaterialChoice.Typical.Label, () => Pick(MaterialChoice.Typical)),
                };
                // Above the needs window, which steps aside for pickers on the policy window's layer.
                Find.WindowStack.Add(new Dialog_PickMaterial("OL_MaterialPicker_Title".Translate(), general,
                    stuff => Pick(MaterialChoice.Specific(stuff))) { layer = WindowLayer.SubSuper });
            }
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(columns.Material, "OL_Bills_Column_Material".Translate());
                Widgets.Label(columns.Bench, "OL_Bills_Column_Bench".Translate());
            }
        }

        /// <summary>
        /// Over the bill column, a button that sets every new bill's repeat mode at once, labelled with the mode they
        /// share or "Mixed", and a box that sets every new bill's count (those with one), empty until something is typed.
        /// </summary>
        private static void DrawEveryBillMode(Rect column)
        {
            List<PlannedBill> rows = plan.Where(each => each.Outcome == PlannedBillOutcome.Add).ToList();
            if (rows.Count == 0)
            {
                using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(column, "OL_Bills_Column_Bill".Translate());
                }
                return;
            }
            Rect button = new Rect(column.x, column.y + (column.height - UiStyle.IconSize) / 2f, RepeatModeWidth, UiStyle.IconSize);
            bool shared = rows.All(each => each.RepeatMode == rows[0].RepeatMode);
            string label = shared ? RepeatModeLabel(rows[0].RepeatMode) : "OL_Bills_ModeMixed".Translate().Resolve();
            if (Widgets.ButtonText(button, label.Truncate(button.width - UiStyle.Gap)))
            {
                var options = new List<FloatMenuOption>();
                foreach (BillRepeatModeDef mode in RepeatModes)
                {
                    options.Add(new FloatMenuOption(RepeatModeLabel(mode), () =>
                    {
                        foreach (PlannedBill each in rows)
                        {
                            each.SetRepeatMode(mode);
                            countBuffers.Remove(each);
                        }
                        everyCountBuffer = string.Empty;
                        everyCountApplied = string.Empty;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(button, "OL_Bills_EveryBillModeTip".Translate());
            List<PlannedBill> counted = rows.Where(each => !each.MakesForever).ToList();
            if (counted.Count == 0)
            {
                return;
            }
            Rect field = new Rect(button.xMax + UiStyle.IconGap, button.y, CountFieldWidth, UiStyle.IconSize);
            int typed = 0;
            Widgets.TextFieldNumeric(field, ref typed, ref everyCountBuffer, 0f, MaxCount);
            // Applied once per new number, so a row changed afterwards keeps its own count.
            if (everyCountBuffer != everyCountApplied && int.TryParse(everyCountBuffer, out int count))
            {
                everyCountApplied = everyCountBuffer;
                foreach (PlannedBill each in counted)
                {
                    each.SetTarget(count);
                    countBuffers.Remove(each);
                }
            }
            TooltipHandler.TipRegion(field, "OL_Bills_EveryBillCountTip".Translate());
        }

        /// <summary>
        /// One bill: a box to leave it out (for one to add or raise), the item's icon and name (its tooltip naming who
        /// they're for), then its count, material and bench. A new bill's three can be changed, a raised bill's count.
        /// The rest say why in grey. The bench's icon shows it on the map.
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
            var columns = new BillColumns(row);
            Rect icon = new Rect(box.xMax + UiStyle.Gap / 2f, iconY, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.ThingIcon(icon, planned.Item, planned.Stuff);
            Rect name = new Rect(icon.xMax + UiStyle.Gap / 2f, row.y, columns.Bill.x - icon.xMax - UiStyle.Gap, row.height);
            string label = planned.Stuff == null ? planned.Item.LabelCap.Resolve() : GenLabel.ThingLabel(planned.Item, planned.Stuff).CapitalizeFirst();
            Color color = planned.WillChange ? Color.white : ColoredText.SubtleGrayColor;
            using (new TextBlock(TextAnchor.MiddleLeft, color))
            {
                Widgets.Label(name, label.Truncate(name.width));
            }
            TooltipHandler.TipRegion(new Rect(icon.x, row.y, name.xMax - icon.x, row.height), ForList(planned));
            string why = WhyNot(planned);
            if (why != null)
            {
                Rect reason = new Rect(columns.Bill.x, row.y, columns.Material.xMax - columns.Bill.x, row.height);
                using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(reason, why.Truncate(reason.width));
                }
            }
            else
            {
                DrawBill(columns.Bill, planned);
                DrawMaterial(columns.Material, planned);
            }
            DrawBench(columns.Bench, planned, color);
        }

        /// <summary>The policy's colonists whose outfits hold the item, by name.</summary>
        private static string ForList(PlannedBill planned)
        {
            if (planned.For.Count == 0)
            {
                return "OL_Bills_NoOneOnPolicyTip".Translate();
            }
            string heading = (planned.Item.apparel == null ? "OL_Bills_CarriersTip" : "OL_Bills_WearersTip").Translate();
            return heading + "\n" + planned.For.Select(pawn => pawn.LabelShort).ToLineList("  ");
        }

        /// <summary>
        /// The bill: its repeat mode ("Until you have" or "Times", a menu on a new bill), a box for the count, and a grey
        /// note on where the count comes from ("1 here + 2 others", "2 lack it, 1 stored", or "was 1" on a raised bill).
        /// </summary>
        private static void DrawBill(Rect cell, PlannedBill planned)
        {
            float controlY = cell.y + (cell.height - UiStyle.IconSize) / 2f;
            Rect mode = new Rect(cell.x, controlY, RepeatModeWidth, UiStyle.IconSize);
            Rect field = new Rect(mode.xMax + UiStyle.IconGap, controlY, CountFieldWidth, UiStyle.IconSize);
            Rect hint = new Rect(field.xMax + UiStyle.IconGap, cell.y, cell.xMax - field.xMax - UiStyle.IconGap, cell.height);
            string modeLabel = RepeatModeLabel(planned.RepeatMode);
            if (planned.Outcome == PlannedBillOutcome.Add)
            {
                if (planned.RepeatModeEdited)
                {
                    MarkEdited(mode, RepeatModeLabel(BillRepeatModeDefOf.RepeatCount));
                }
                if (Widgets.ButtonText(mode, modeLabel.Truncate(mode.width - UiStyle.Gap)))
                {
                    var options = new List<FloatMenuOption>();
                    foreach (BillRepeatModeDef each in RepeatModes)
                    {
                        options.Add(new FloatMenuOption(RepeatModeLabel(each), () =>
                        {
                            planned.SetRepeatMode(each);
                            countBuffers.Remove(planned);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                TooltipHandler.TipRegion(mode, "OL_Bills_RepeatModeTip".Translate());
            }
            else
            {
                using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(mode, modeLabel.Truncate(mode.width));
                }
            }
            if (planned.MakesForever)
            {
                // "Do forever" has no count, so the box gives way to the note.
                using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(new Rect(field.x, cell.y, cell.xMax - field.x, cell.height), "OL_Bills_HintForever".Translate());
                }
                return;
            }
            if (planned.TargetEdited)
            {
                MarkEdited(field, "OL_Bills_Count".Translate(planned.MakesOnce ? planned.Missing : planned.PlannedTarget));
            }
            int target = planned.Target;
            countBuffers.TryGetValue(planned, out string buffer);
            Widgets.TextFieldNumeric(field, ref target, ref buffer, 0f, MaxCount);
            countBuffers[planned] = buffer;
            planned.SetTarget(target);
            TooltipHandler.TipRegion(field, planned.MakesOnce
                ? "OL_Bills_TimesTip".Translate() + "\n\n" + ForList(planned)
                : "OL_Bills_CountTip".Translate() + "\n" + "OL_Bills_CountOthersTip".Translate() + "\n\n" + ForList(planned));
            using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
            {
                Widgets.Label(hint, CountHint(planned).Truncate(hint.width));
            }
        }

        /// <summary>The repeat modes a new bill can take, in menu order.</summary>
        private static readonly BillRepeatModeDef[] RepeatModes =
        {
            BillRepeatModeDefOf.RepeatCount, BillRepeatModeDefOf.TargetCount, BillRepeatModeDefOf.Forever,
        };

        private static string RepeatModeLabel(BillRepeatModeDef mode)
        {
            if (mode == BillRepeatModeDefOf.Forever)
            {
                return "OL_Bills_ModeForever".Translate();
            }
            return (mode == BillRepeatModeDefOf.RepeatCount ? "OL_Bills_ModeTimes" : "OL_Bills_ModeUntil").Translate();
        }

        /// <summary>Where the count comes from: who lacks it and what's on hand, or how many wear it, or a raised bill's old count.</summary>
        private static string CountHint(PlannedBill planned)
        {
            if (planned.Outcome == PlannedBillOutcome.Raise)
            {
                return "OL_Bills_HintWas".Translate(planned.Existing.targetCount);
            }
            if (planned.For.Count == 0)
            {
                return "OL_Bills_HintPreview".Translate();
            }
            if (planned.MakesOnce)
            {
                int onHand = planned.Stored + planned.InBills;
                return onHand > 0
                    ? "OL_Bills_HintLackingOnHand".Translate(planned.Lacking, onHand)
                    : "OL_Bills_HintLacking".Translate(planned.Lacking);
            }
            return (planned.Item.apparel == null ? "OL_Bills_HintCarry" : "OL_Bills_HintWear").Translate(planned.PlannedTarget);
        }

        /// <summary>
        /// A new bill's material, as a button listing the ones its recipe takes with how many are in stock. A raised bill
        /// keeps its own, so its cell says when the count is no higher than it already is.
        /// </summary>
        private static void DrawMaterial(Rect cell, PlannedBill planned)
        {
            if (planned.Outcome == PlannedBillOutcome.Raise && planned.Target <= planned.Existing.targetCount)
            {
                using (new TextBlock(TextAnchor.MiddleLeft, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(cell, "OL_Bills_AlreadyAt".Translate(planned.Existing.targetCount));
                }
                return;
            }
            if (!planned.CanChooseStuff)
            {
                return;
            }
            Rect button = new Rect(cell.x, cell.y + (cell.height - UiStyle.IconSize) / 2f, cell.width, UiStyle.IconSize);
            if (planned.StuffEdited)
            {
                MarkEdited(button, MaterialLabel(planned.PlannedStuff));
            }
            string label = MaterialLabel(planned.Stuff);
            // A material the colony has none of: the bill would wait for it, so the row says so.
            bool noneInStock = planned.Stuff != null && InStock(planned.Stuff) == 0;
            if (noneInStock)
            {
                TooltipHandler.TipRegion(button, "OL_Bills_NoneInStockTip".Translate(planned.Stuff.label));
            }
            bool clicked;
            using (new TextBlock(noneInStock ? ColorLibrary.RedReadable : Color.white))
            {
                clicked = Widgets.ButtonText(button, label.Truncate(button.width - UiStyle.Gap));
            }
            if (clicked)
            {
                var options = new List<FloatMenuOption>();
                foreach (MaterialChoice choice in new[] { MaterialChoice.BestForOrder, MaterialChoice.Typical })
                {
                    ThingDef resolved = RoleBills.MaterialFor(planned, choice, planFor);
                    if (resolved != null)
                    {
                        options.Add(new FloatMenuOption("OL_Bills_ChoiceOption".Translate(choice.Label, resolved.LabelCap), () => planned.SetStuff(resolved), resolved));
                    }
                }
                options.Add(new FloatMenuOption(MaterialOption(planned, null), () => planned.SetStuff(null)));
                foreach (ThingDef stuff in RoleBills.MaterialsFor(planned).OrderByDescending(InStock).ThenBy(stuff => stuff.label))
                {
                    options.Add(new FloatMenuOption(MaterialOption(planned, stuff), () => planned.SetStuff(stuff), stuff));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static string MaterialLabel(ThingDef stuff)
        {
            return stuff == null ? "OL_Bills_AnyMaterial".Translate().Resolve() : stuff.LabelCap.Resolve();
        }

        /// <summary>A material in the menu, with how many are in stock, and marked when it's the policy's own.</summary>
        private static string MaterialOption(PlannedBill planned, ThingDef stuff)
        {
            string option = stuff == null
                ? MaterialLabel(null)
                : "OL_Bills_MaterialOption".Translate(stuff.LabelCap, InStock(stuff)).Resolve();
            return stuff == planned.PlannedStuff ? "OL_Bills_PolicysChoice".Translate(option).Resolve() : option;
        }

        private static int InStock(ThingDef stuff)
        {
            return planMap?.resourceCounter.GetCount(stuff) ?? 0;
        }

        /// <summary>
        /// The bench: its icon shows it on the map, and its name is a button listing every bench that can make the item
        /// for a new bill, or plain text for any other.
        /// </summary>
        private static void DrawBench(Rect cell, PlannedBill planned, Color color)
        {
            if (planned.Bench == null)
            {
                return;
            }
            Rect icon = new Rect(cell.x, cell.y + (cell.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.DrawHighlightIfMouseover(icon);
            // The def's own icon, since a bench drawn from its map graphic shrinks to a strip.
            Widgets.DefIcon(icon, planned.Bench.def, planned.Bench.Stuff);
            TooltipHandler.TipRegion(icon, "OL_Bills_ShowOnMap".Translate());
            if (Widgets.ButtonInvisible(icon))
            {
                ShowOnMap(planned.Bench);
                return;
            }
            Rect name = new Rect(icon.xMax + UiStyle.IconGap, icon.y, cell.xMax - icon.xMax - UiStyle.IconGap, UiStyle.IconSize);
            string label = BenchLabel(planned.Bench);
            if (planned.Outcome != PlannedBillOutcome.Add)
            {
                using (new TextBlock(TextAnchor.MiddleLeft, color))
                {
                    Widgets.Label(name, label.Truncate(name.width));
                }
                return;
            }
            if (planned.BenchEdited)
            {
                MarkEdited(name, BenchLabel(planned.PlannedBench));
            }
            if (Widgets.ButtonText(name, label.Truncate(name.width - UiStyle.Gap)))
            {
                var options = new List<FloatMenuOption>();
                foreach (BenchChoice choice in RoleBills.BenchesFor(planned, plan, planMap))
                {
                    Building_WorkTable bench = choice.Bench;
                    Action pick = choice.Full ? null : (Action)(() => planned.SetBench(bench));
                    options.Add(new FloatMenuOption(BenchOption(choice), pick, bench.def));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        /// <summary>The bench's kind, "Hand tailoring bench", without the material it's built of.</summary>
        private static string BenchLabel(Building_WorkTable bench)
        {
            return bench.def.LabelCap;
        }

        private static string BenchOption(BenchChoice choice)
        {
            string bench = BenchLabel(choice.Bench);
            if (choice.Full)
            {
                return "OL_Bills_BenchFull".Translate(bench);
            }
            return (choice.Powered ? "OL_Bills_BenchOption" : "OL_Bills_BenchNoPower").Translate(bench, choice.Bills, BillStack.MaxCount);
        }

        /// <summary>A faint amber behind a value the player changed, with what the plan had picked.</summary>
        private static void MarkEdited(Rect rect, string planned)
        {
            Widgets.DrawBoxSolid(rect.ExpandedBy(2f), UiStyle.EditedColor);
            TooltipHandler.TipRegion(rect, "OL_Bills_ChangedFrom".Translate(planned));
        }

        /// <summary>
        /// Closes the policy window, which sits over the middle of the map, and the Assign tab it opens from, then moves
        /// the camera to the bench and selects it, so its bills show in the inspect pane. The plan is dropped, since the
        /// colony moves on once the window closes.
        /// </summary>
        private static void ShowOnMap(Building_WorkTable bench)
        {
            ClearPlan();
            Find.WindowStack.TryRemove(typeof(Dialog_ManageApparelPolicies), doCloseSound: false);
            // The inspect pane opens for a selection only while no main tab is open.
            Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            CameraJumper.TryJumpAndSelect(bench);
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
