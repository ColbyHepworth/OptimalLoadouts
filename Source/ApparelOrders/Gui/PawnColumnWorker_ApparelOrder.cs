using System.Collections.Generic;
using System.Linq;
using System.Text;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// The Assign tab column beside the apparel policy: each pawn's stat order in short, why they wear each piece,
    /// and a header button that opens the order editor.
    /// </summary>
    public class PawnColumnWorker_ApparelOrder : PawnColumnWorker
    {
        private const int TopAreaHeight = 65;
        private const float ButtonHeight = 32f;
        private const int LinesShown = 3;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            Rect button = new Rect(rect.x, rect.y + (rect.height - TopAreaHeight), Mathf.Min(rect.width, 360f), ButtonHeight);
            if (Widgets.ButtonText(button, "OL_Column_Button".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ApparelOrders(null));
            }
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelPolicy policy = pawn.outfits?.CurrentApparelPolicy;
            if (orders == null || policy == null)
            {
                return;
            }
            IReadOnlyList<OrderLine> lines = orders.EffectiveLines(policy);
            Widgets.DrawHighlightIfMouseover(rect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = rect.ContractedBy(4f, 0f);
            Widgets.Label(labelRect, Summary(lines).Truncate(labelRect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(rect, new TipSignal(() => Tooltip(pawn, policy, orders.EffectiveLines(policy)), pawn.thingIDNumber * 397));
            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new Dialog_ApparelOrders(policy));
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 160);
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            return Mathf.Clamp(240, GetMinWidth(table), GetMaxWidth(table));
        }

        public override int GetMinHeaderHeight(PawnTable table)
        {
            return Mathf.Max(base.GetMinHeaderHeight(table), TopAreaHeight);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return string.CompareOrdinal(SummaryFor(a), SummaryFor(b));
        }

        private static string SummaryFor(Pawn pawn)
        {
            ApparelPolicy policy = pawn.outfits?.CurrentApparelPolicy;
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            return policy == null || orders == null ? "" : Summary(orders.EffectiveLines(policy));
        }

        private static string Summary(IReadOnlyList<OrderLine> lines)
        {
            if (lines.Count == 0)
            {
                return "OL_Column_NoOrder".Translate();
            }
            string shown = lines.Take(LinesShown)
                .Select(line => line.stat.LabelCap.Resolve())
                .Aggregate((before, next) => "OL_Column_Then".Translate(before, next).Resolve());
            return lines.Count > LinesShown ? "OL_Column_More".Translate(shown) : shown;
        }

        private static string Tooltip(Pawn pawn, ApparelPolicy policy, IReadOnlyList<OrderLine> lines)
        {
            var text = new StringBuilder();
            if (lines.Count == 0)
            {
                text.AppendLine("OL_Column_NoOrderTip".Translate(policy.label));
            }
            else
            {
                text.AppendLine("OL_Column_TipHeader".Translate(policy.label));
                for (int i = 0; i < lines.Count; i++)
                {
                    string direction = lines[i].lowerIsBetter ? "OL_Order_LowerFirst".Translate() : "OL_Order_HigherFirst".Translate();
                    text.AppendLine("  " + "OL_Column_TipLine".Translate(i + 1, lines[i].stat.LabelCap, direction));
                }
                List<Apparel> worn = pawn.apparel?.WornApparel;
                if (!worn.NullOrEmpty())
                {
                    text.AppendLine();
                    text.AppendLine("OL_Column_WornHeader".Translate());
                    foreach (Apparel piece in worn)
                    {
                        text.AppendLine("  " + WhyWorn(piece, lines));
                    }
                }
            }
            text.AppendLine();
            text.Append("OL_Column_ClickToEdit".Translate());
            return text.ToString();
        }

        private static string WhyWorn(Apparel piece, IReadOnlyList<OrderLine> lines)
        {
            OrderLine served = ApparelComparison.FirstLineServed(piece, lines);
            if (served == null)
            {
                return "OL_Column_WornUsual".Translate(piece.LabelCap);
            }
            float value = PieceStats.Value(piece, served.stat);
            ToStringNumberSense sense = StatChoices.IsWornPieceStat(served.stat) ? ToStringNumberSense.Absolute : ToStringNumberSense.Offset;
            return "OL_Column_WornServed".Translate(piece.LabelCap, served.stat.LabelCap, served.stat.ValueToString(value, sense));
        }
    }
}
