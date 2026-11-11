using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using OptimalLoadouts.Orders;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// The outfit preview beside a policy's filter: one of the policy's pawns drawn in the outfit it will end up in from
    /// what's in storage, and a list of that outfit saying what changes and why.
    /// </summary>
    public static class OutfitPreviewPanel
    {
        private const float PortraitSize = 180f;

        /// <summary>How often the prediction is worked out again, so edits to the filter or the order show up.</summary>
        private const float RefreshSeconds = 1f;

        private static Game shownGame;
        private static ApparelPolicy shownPolicy;
        private static int pawnIndex;
        private static Rot4 rotation = Rot4.South;
        private static Pawn predictedPawn;
        private static ApparelPolicy predictedPolicy;
        private static PredictedOutfit prediction;
        private static float predictedAt;
        private static Vector2 scroll;

        /// <summary>Forgets the last game's pawns, policy and prediction, so a loaded game doesn't keep them alive.</summary>
        private static void Forget()
        {
            OutfitPortrait.Forget();
            shownPolicy = null;
            predictedPawn = null;
            predictedPolicy = null;
            prediction = null;
            pawnIndex = 0;
            scroll = Vector2.zero;
        }

        public static void Draw(Rect rect, ApparelPolicy policy)
        {
            Widgets.DrawMenuSection(rect);
            if (Current.Game != shownGame)
            {
                shownGame = Current.Game;
                Forget();
            }
            try
            {
                DrawContents(rect.ContractedBy(UiStyle.Gap), policy);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't draw the outfit preview: " + e, "OptimalLoadouts.PreviewPanel".GetHashCode());
            }
        }

        private static void DrawContents(Rect inner, ApparelPolicy policy)
        {
            if (policy != shownPolicy)
            {
                shownPolicy = policy;
                pawnIndex = 0;
                scroll = Vector2.zero;
            }
            List<Pawn> pawns = PawnsOn(policy);
            if (pawns.Count == 0)
            {
                UiStyle.Note(inner, "OL_Preview_NoPawn".Translate());
                return;
            }
            pawnIndex = Mathf.Clamp(pawnIndex, 0, pawns.Count - 1);
            Pawn pawn = pawns[pawnIndex];
            PredictedOutfit outfit = PredictionFor(pawn, policy);

            float y = inner.y;
            DrawPawnRow(new Rect(inner.x, y, inner.width, UiStyle.RowHeight), pawn, pawns.Count);
            y += UiStyle.RowHeight;
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                UiStyle.Note(new Rect(inner.x, y, inner.width, UiStyle.RowHeight), "OL_Preview_FromStorage".Translate());
            }
            y += UiStyle.RowHeight + UiStyle.Gap;
            DrawPortrait(new Rect(inner.x, y, inner.width, PortraitSize), pawn, outfit);
            y += PortraitSize + UiStyle.Gap;
            string status = StatusText(outfit);
            if (status != null)
            {
                UiStyle.Note(new Rect(inner.x, y, inner.width, UiStyle.RowHeight), status);
                y += UiStyle.RowHeight;
            }
            DrawOutfitList(new Rect(inner.x, y, inner.width, inner.yMax - y), outfit);
        }

        private static List<Pawn> PawnsOn(ApparelPolicy policy)
        {
            Map map = Find.CurrentMap;
            if (map == null || policy == null)
            {
                return new List<Pawn>();
            }
            return map.mapPawns.FreeColonistsSpawned.Where(pawn => pawn.outfits?.CurrentApparelPolicy == policy && pawn.apparel != null).ToList();
        }

        private static PredictedOutfit PredictionFor(Pawn pawn, ApparelPolicy policy)
        {
            if (pawn != predictedPawn || policy != predictedPolicy || prediction == null
                || Time.realtimeSinceStartup - predictedAt > RefreshSeconds)
            {
                predictedPawn = pawn;
                predictedPolicy = policy;
                predictedAt = Time.realtimeSinceStartup;
                try
                {
                    prediction = OutfitPredictor.Predict(pawn);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Optimal Loadouts] Couldn't predict an outfit for the preview: " + e, "OptimalLoadouts.Predict".GetHashCode());
                    prediction = new PredictedOutfit(pawn.apparel.WornApparel.ToList(), new List<Apparel>(), new List<Apparel>(),
                        new List<Apparel>(), new List<PredictedSwap>(), true, false);
                }
            }
            return prediction;
        }

        private static void DrawPawnRow(Rect row, Pawn pawn, int pawnCount)
        {
            Rect previous = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect next = new Rect(row.xMax - UiStyle.IconSize, previous.y, UiStyle.IconSize, UiStyle.IconSize);
            if (pawnCount > 1)
            {
                if (Widgets.ButtonImage(previous, TexUI.ArrowTexLeft, true, "OL_Preview_PreviousPawn".Translate()))
                {
                    pawnIndex = (pawnIndex + pawnCount - 1) % pawnCount;
                }
                if (Widgets.ButtonImage(next, TexUI.ArrowTexRight, true, "OL_Preview_NextPawn".Translate()))
                {
                    pawnIndex = (pawnIndex + 1) % pawnCount;
                }
            }
            Rect label = new Rect(previous.xMax, row.y, next.x - previous.xMax, row.height);
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(label, "OL_Preview_Pawn".Translate(pawn.LabelShort, pawnIndex + 1, pawnCount).Truncate(label.width));
            }
        }

        private static void DrawPortrait(Rect area, Pawn pawn, PredictedOutfit outfit)
        {
            Rect portrait = new Rect(area.center.x - PortraitSize / 2f, area.y, PortraitSize, PortraitSize);
            GUI.DrawTexture(portrait, OutfitPortrait.Draw(pawn, outfit.Worn, rotation, portrait.size));
            Rect turnLeft = new Rect(portrait.x - UiStyle.IconSize - UiStyle.Gap, portrait.center.y - UiStyle.IconSize / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect turnRight = new Rect(portrait.xMax + UiStyle.Gap, turnLeft.y, UiStyle.IconSize, UiStyle.IconSize);
            if (Widgets.ButtonImage(turnLeft, TexUI.RotLeftTex, true, "OL_Preview_Turn".Translate()))
            {
                rotation.Rotate(RotationDirection.Counterclockwise);
            }
            if (Widgets.ButtonImage(turnRight, TexUI.RotRightTex, true, "OL_Preview_Turn".Translate()))
            {
                rotation.Rotate(RotationDirection.Clockwise);
            }
        }

        private static string StatusText(PredictedOutfit outfit)
        {
            if (!outfit.Settled)
            {
                return "OL_Preview_Unsettled".Translate(OutfitPredictor.MaxSwaps);
            }
            if (outfit.NothingToPick)
            {
                return "OL_Preview_NothingToPick".Translate();
            }
            if (outfit.PutOn.Count == 0 && outfit.TakenOff.Count == 0)
            {
                return "OL_Preview_NoChanges".Translate();
            }
            return null;
        }

        private static void DrawOutfitList(Rect rect, PredictedOutfit outfit)
        {
            List<Apparel> worn = outfit.Worn.OrderByDescending(piece => piece.def.apparel.LastLayer.drawOrder).ToList();
            int rows = worn.Count + (outfit.TakenOff.Count > 0 ? outfit.TakenOff.Count + 1 : 0);
            Rect view = new Rect(0f, 0f, rect.width - GenUI.ScrollBarWidth, rows * UiStyle.RowHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                DrawOutfitRows(view, worn, outfit);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawOutfitRows(Rect view, List<Apparel> worn, PredictedOutfit outfit)
        {
            float y = 0f;
            foreach (Apparel piece in worn)
            {
                bool isNew = outfit.PutOn.Contains(piece);
                DrawPieceRow(new Rect(0f, y, view.width, UiStyle.RowHeight), piece, !isNew,
                    isNew ? "OL_Preview_New".Translate().ToString() : null, isNew ? WhyPutOn(piece, outfit) : "OL_Preview_Kept".Translate().ToString());
                y += UiStyle.RowHeight;
            }
            if (outfit.TakenOff.Count > 0)
            {
                UiStyle.Note(new Rect(0f, y, view.width, UiStyle.RowHeight), "OL_Preview_TakesOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in outfit.TakenOff)
                {
                    DrawPieceRow(new Rect(0f, y, view.width, UiStyle.RowHeight), piece, true, null, WhyTakenOff(piece, outfit));
                    y += UiStyle.RowHeight;
                }
            }
        }

        private static void DrawPieceRow(Rect row, Apparel piece, bool dimmed, string tag, string tooltip)
        {
            if (UiStyle.ClickableRow(row))
            {
                Find.WindowStack.Add(new Dialog_InfoCard(piece));
            }
            TooltipHandler.TipRegion(row, tooltip);
            Rect icon = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.ThingIcon(icon, piece, dimmed ? 0.5f : 1f);
            float tagWidth = tag == null ? 0f : Text.CalcSize(tag).x + UiStyle.Gap;
            Rect label = new Rect(icon.xMax + UiStyle.Gap, row.y, row.width - icon.width - UiStyle.Gap - tagWidth, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft, dimmed ? ColoredText.SubtleGrayColor : Color.white))
            {
                Widgets.Label(label, piece.LabelCap.Truncate(label.width));
            }
            if (tag != null)
            {
                using (new TextBlock(TextAnchor.MiddleRight))
                {
                    Widgets.Label(new Rect(row.xMax - tagWidth, row.y, tagWidth, row.height), tag);
                }
            }
        }

        private static string WhyPutOn(Apparel piece, PredictedOutfit outfit)
        {
            PredictedSwap swap = outfit.Swaps.LastOrDefault(s => s.PutOn == piece);
            if (swap == null)
            {
                return null;
            }
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(shownPolicy);
            bool hasOrder = lines != null && lines.Count > 0;
            string why = ReasonWon(swap.Verdict, lines, hasOrder);
            string runnerUp = swap.RunnerUp == null ? null : ReasonRunnerUpLost(swap, lines, hasOrder);
            return runnerUp == null ? why : why + "\n\n" + runnerUp;
        }

        private static string ReasonWon(OrderVerdict verdict, IReadOnlyList<OrderLine> lines, bool hasOrder)
        {
            if (!hasOrder)
            {
                return "OL_Preview_WhyNoOrder".Translate();
            }
            if (verdict.GameDecides)
            {
                return "OL_Preview_WhyGame".Translate();
            }
            if (verdict.IsTie || verdict.Delta <= 0f || verdict.DecidingLine >= lines.Count)
            {
                return "OL_Preview_WhyTie".Translate();
            }
            StatDef stat = lines[verdict.DecidingLine].stat;
            return "OL_Preview_WhyBetter".Translate(stat.label, Amount(stat, verdict.Delta));
        }

        private static string ReasonRunnerUpLost(PredictedSwap swap, IReadOnlyList<OrderLine> lines, bool hasOrder)
        {
            string name = swap.RunnerUp.LabelCap;
            OrderVerdict runnerUp = swap.RunnerUpVerdict;
            if (!hasOrder || runnerUp.GameDecides || runnerUp.IsTie || runnerUp.DecidingLine >= lines.Count)
            {
                return "OL_Preview_RunnerUpScoredLower".Translate(name);
            }
            StatDef stat = lines[runnerUp.DecidingLine].stat;
            if (runnerUp.Delta < 0f)
            {
                return "OL_Preview_RunnerUpWorse".Translate(name, stat.label, Amount(stat, runnerUp.Delta));
            }
            if (runnerUp.DecidingLine == swap.Verdict.DecidingLine)
            {
                return "OL_Preview_RunnerUpSmallerGain".Translate(name, stat.label);
            }
            return "OL_Preview_RunnerUpLaterStat".Translate(name, stat.label);
        }

        private static string WhyTakenOff(Apparel piece, PredictedOutfit outfit)
        {
            if (outfit.NotAllowed.Contains(piece))
            {
                return "OL_Preview_NotAllowed".Translate();
            }
            PredictedSwap swap = outfit.Swaps.FirstOrDefault(s => s.Replaced.Contains(piece));
            return swap == null ? null : "OL_Preview_ReplacedBy".Translate(swap.PutOn.LabelCap).ToString();
        }

        private static string Amount(StatDef stat, float delta)
        {
            // A difference, so temperatures convert as a span (10 °C reads 18 °F, not 50 °F).
            return stat.ValueToString(Mathf.Abs(delta), ToStringNumberSense.Offset);
        }
    }
}
