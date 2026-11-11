using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>Edits each apparel policy's stat order and base.</summary>
    public class Dialog_ApparelOrders : Window
    {
        private const float PolicyListWidth = 220f;
        private const float RowHeight = 30f;
        private const float IconSize = 24f;
        private const float DirectionWidth = 110f;
        private const float Gap = 12f;

        private ApparelPolicy selected;
        private Vector2 policyScroll;
        private Vector2 linesScroll;

        public Dialog_ApparelOrders(ApparelPolicy selected)
        {
            this.selected = selected;
            doCloseX = true;
            absorbInputAroundWindow = true;
            draggable = true;
            optionalTitle = "OL_Editor_Title".Translate();
        }

        public override Vector2 InitialSize => new Vector2(780f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null)
            {
                Close();
                return;
            }
            List<ApparelPolicy> policies = Current.Game.outfitDatabase.AllOutfits;
            if (selected == null || !policies.Contains(selected))
            {
                selected = policies.FirstOrDefault();
            }

            string help = "OL_Editor_Help".Translate();
            float helpHeight = Text.CalcHeight(help, inRect.width);
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, helpHeight), help);
            GUI.color = Color.white;

            float top = inRect.y + helpHeight + Gap;
            Rect policyRect = new Rect(inRect.x, top, PolicyListWidth, inRect.yMax - top);
            Rect orderRect = new Rect(policyRect.xMax + Gap, top, inRect.width - PolicyListWidth - Gap, inRect.yMax - top);
            DrawPolicyList(policyRect, policies);
            if (selected != null)
            {
                DrawOrder(orderRect, orders, policies);
            }
        }

        private void DrawPolicyList(Rect rect, List<ApparelPolicy> policies)
        {
            Widgets.DrawMenuSection(rect);
            Rect outRect = rect.ContractedBy(4f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, policies.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref policyScroll, viewRect);
            for (int i = 0; i < policies.Count; i++)
            {
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (policies[i] == selected)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                Widgets.DrawHighlightIfMouseover(row);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(row.ContractedBy(6f, 0f), policies[i].label.Truncate(row.width - 12f));
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(row))
                {
                    selected = policies[i];
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawOrder(Rect rect, ApparelOrdersComponent orders, List<ApparelPolicy> policies)
        {
            float y = rect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 36f), selected.label);
            Text.Font = GameFont.Small;
            y += 40f;

            DrawBaseRow(new Rect(rect.x, y, rect.width, RowHeight), orders, policies);
            y += RowHeight + Gap;

            if (Widgets.ButtonText(new Rect(rect.x, y, 160f, RowHeight), "OL_Editor_AddStat".Translate()))
            {
                ApparelPolicy policy = selected;
                // Only the policy's own stats are hidden, so a base's stat can be added here to move it up or flip it.
                IEnumerable<StatDef> listed = (orders.OrderFor(policy)?.lines ?? new List<OrderLine>()).Select(line => line.stat);
                Find.WindowStack.Add(new Dialog_PickStat(stat => orders.AddLine(policy, stat), listed));
            }
            y += RowHeight + Gap;

            Rect listRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            List<OrderLine> ownLines = orders.OrderFor(selected)?.lines ?? new List<OrderLine>();
            List<(ApparelPolicy from, OrderLine line)> fromBase = BaseLines(orders, ownLines);
            int headerRows = fromBase.Select(entry => entry.from).Distinct().Count();
            float viewHeight = (ownLines.Count + fromBase.Count + headerRows + 1) * RowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(listRect, ref linesScroll, viewRect);
            float rowY = 0f;
            if (ownLines.Count == 0 && fromBase.Count == 0)
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(0f, rowY, viewRect.width, RowHeight), "OL_Editor_Empty".Translate());
                GUI.color = Color.white;
            }
            for (int i = 0; i < ownLines.Count; i++)
            {
                DrawOwnLine(new Rect(0f, rowY, viewRect.width, RowHeight), orders, ownLines, i);
                rowY += RowHeight;
            }
            ApparelPolicy currentSource = null;
            foreach ((ApparelPolicy from, OrderLine line) in fromBase)
            {
                if (from != currentSource)
                {
                    currentSource = from;
                    rowY += 4f;
                    GUI.color = ColoredText.SubtleGrayColor;
                    Widgets.Label(new Rect(0f, rowY, viewRect.width, RowHeight), "OL_Editor_FromBase".Translate(from.label));
                    rowY += RowHeight - 4f;
                }
                DrawBaseLine(new Rect(0f, rowY, viewRect.width, RowHeight), line);
                rowY += RowHeight;
            }
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private void DrawBaseRow(Rect row, ApparelOrdersComponent orders, List<ApparelPolicy> policies)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x, row.y, 70f, row.height), "OL_Editor_Base".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            ApparelPolicy basePolicy = orders.BaseOf(selected);
            Rect button = new Rect(row.x + 70f, row.y, 220f, row.height);
            string label = basePolicy?.label ?? "OL_Editor_NoBase".Translate();
            TooltipHandler.TipRegion(button, "OL_Editor_BaseTip".Translate());
            if (Widgets.ButtonText(button, label.Truncate(button.width - 12f)))
            {
                ApparelPolicy policy = selected;
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("OL_Editor_NoBase".Translate(), () => orders.SetBase(policy, null)),
                };
                foreach (ApparelPolicy candidate in policies)
                {
                    if (orders.CanBeBase(policy, candidate))
                    {
                        options.Add(new FloatMenuOption(candidate.label, () => orders.SetBase(policy, candidate)));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void DrawOwnLine(Rect row, ApparelOrdersComponent orders, List<OrderLine> lines, int index)
        {
            OrderLine line = lines[index];
            Widgets.DrawHighlightIfMouseover(row);
            float x = row.x;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, row.y, 28f, row.height), "OL_Editor_LineNumber".Translate(index + 1));
            x += 28f;

            float labelWidth = row.width - 28f - DirectionWidth - 3 * (IconSize + 4f) - Gap;
            Widgets.Label(new Rect(x, row.y, labelWidth, row.height), line.stat.LabelCap.Resolve().Truncate(labelWidth));
            Text.Anchor = TextAnchor.UpperLeft;
            if (!line.stat.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(new Rect(x, row.y, labelWidth, row.height), line.stat.description);
            }
            x += labelWidth + Gap;

            Rect direction = new Rect(x, row.y + 2f, DirectionWidth, row.height - 4f);
            string directionLabel = line.lowerIsBetter ? "OL_Order_LowerFirst".Translate() : "OL_Order_HigherFirst".Translate();
            TooltipHandler.TipRegion(direction, "OL_Editor_DirectionTip".Translate());
            if (Widgets.ButtonText(direction, directionLabel))
            {
                orders.ToggleDirection(selected, index);
            }
            x += DirectionWidth + 4f;

            float iconY = row.y + (row.height - IconSize) / 2f;
            if (Widgets.ButtonImage(new Rect(x, iconY, IconSize, IconSize), TexButton.ReorderUp, true, "OL_Editor_MoveUp".Translate()))
            {
                orders.MoveLine(selected, index, -1);
            }
            x += IconSize + 4f;
            if (Widgets.ButtonImage(new Rect(x, iconY, IconSize, IconSize), TexButton.ReorderDown, true, "OL_Editor_MoveDown".Translate()))
            {
                orders.MoveLine(selected, index, 1);
            }
            x += IconSize + 4f;
            if (Widgets.ButtonImage(new Rect(x, iconY, IconSize, IconSize), TexButton.Delete, true, "OL_Editor_Remove".Translate()))
            {
                orders.RemoveLine(selected, index);
            }
        }

        private static void DrawBaseLine(Rect row, OrderLine line)
        {
            GUI.color = ColoredText.SubtleGrayColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            string direction = line.lowerIsBetter ? "OL_Order_LowerFirst".Translate() : "OL_Order_HigherFirst".Translate();
            Widgets.Label(new Rect(row.x + 28f, row.y, row.width - 28f, row.height), "OL_Editor_BaseLine".Translate(line.stat.LabelCap, direction));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>The lines the selected policy gets from its bases, each with the policy it comes from.</summary>
        private List<(ApparelPolicy from, OrderLine line)> BaseLines(ApparelOrdersComponent orders, List<OrderLine> ownLines)
        {
            var result = new List<(ApparelPolicy, OrderLine)>();
            var listed = new HashSet<StatDef>(ownLines.Select(line => line.stat));
            var visited = new HashSet<ApparelPolicy> { selected };
            for (ApparelPolicy basePolicy = orders.BaseOf(selected); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                List<OrderLine> baseLines = orders.OrderFor(basePolicy)?.lines;
                if (baseLines == null)
                {
                    break;
                }
                foreach (OrderLine line in baseLines)
                {
                    if (line.stat != null && listed.Add(line.stat))
                    {
                        result.Add((basePolicy, line));
                    }
                }
            }
            return result;
        }
    }
}
