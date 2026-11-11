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
    /// The outfit preview beside a policy's filter: any colonist drawn in the outfit the policy would give them, from
    /// every piece it allows, what the colony can make now, or what's in storage, and a list saying why each piece won.
    /// </summary>
    public static class OutfitPreviewPanel
    {
        private const float PortraitSize = 180f;

        /// <summary>How often the pieces on offer are gathered again, so edits to the filter, stock and storage show up.</summary>
        private const float RefreshSeconds = 1f;

        private static readonly PieceSource[] Sources = { PieceSource.AnythingAllowed, PieceSource.CraftableNow, PieceSource.InStorage };

        private static Game shownGame;
        private static Pawn chosenPawn;
        private static PieceSource source = PieceSource.AnythingAllowed;
        private static MaterialChoice material = MaterialChoice.BestForOrder;
        private static Rot4 rotation = Rot4.South;
        private static Vector2 scroll;

        private static readonly Dictionary<PieceSource, List<Apparel>> pieces = new Dictionary<PieceSource, List<Apparel>>();
        private static float gatheredAt;
        private static (Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material, int version) predictedFor;
        private static List<Apparel> predictedFrom;
        private static PredictedOutfit prediction;
        private static ApparelPolicy shownPolicy;

        /// <summary>Forgets the last game's pawns, policy, pieces and prediction, so a loaded game doesn't keep them alive.</summary>
        private static void Forget()
        {
            OutfitPortrait.Forget();
            ImaginedPieces.Forget();
            PieceSources.Forget();
            chosenPawn = null;
            shownPolicy = null;
            pieces.Clear();
            gatheredAt = float.MinValue;
            predictedFor = default;
            predictedFrom = null;
            prediction = null;
            scroll = Vector2.zero;
        }

        public static void Draw(Rect rect, ApparelPolicy policy)
        {
            if (Current.Game != shownGame)
            {
                shownGame = Current.Game;
                Forget();
            }
            Rect section = rect;
            section.yMin += TabDrawer.TabHeight;
            Widgets.DrawMenuSection(section);
            try
            {
                DrawContents(section, policy);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't draw the outfit preview: " + e, "OptimalLoadouts.PreviewPanel".GetHashCode());
            }
        }

        private static void DrawContents(Rect section, ApparelPolicy policy)
        {
            List<Pawn> pawns = Colonists(policy);
            if (pawns.Count == 0 || policy == null)
            {
                UiStyle.Note(section.ContractedBy(UiStyle.Gap), "OL_Preview_NoPawn".Translate());
                return;
            }
            if (!pawns.Contains(chosenPawn))
            {
                chosenPawn = pawns[0];
            }
            if (policy != shownPolicy)
            {
                shownPolicy = policy;
                scroll = Vector2.zero;
            }
            Pawn pawn = chosenPawn;
            GatherPieces(pawn, policy);
            DrawSourceTabs(section);
            PredictedOutfit outfit = PredictionFor(pawn, policy);

            Rect inner = section.ContractedBy(UiStyle.Gap);
            float y = inner.y;
            DrawPawnRow(new Rect(inner.x, y, inner.width, UiStyle.RowHeight), pawn, pawns, policy);
            y += UiStyle.RowHeight;
            string sourceText = SourceText(pawn, policy);
            // "As if on this policy" can run to a second line.
            float sourceHeight = Mathf.Max(UiStyle.RowHeight, Text.CalcHeight(sourceText, inner.width));
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                UiStyle.Note(new Rect(inner.x, y, inner.width, sourceHeight), sourceText);
            }
            y += sourceHeight;
            if (source != PieceSource.InStorage)
            {
                Rect button = new Rect(inner.x, y, inner.width, UiStyle.RowHeight);
                if (Widgets.ButtonText(button, "OL_Preview_Material".Translate(material.Label)))
                {
                    Find.WindowStack.Add(new Dialog_PickMaterial(choice => material = choice));
                }
                TooltipHandler.TipRegion(button, "OL_Preview_MaterialTip".Translate());
                y += UiStyle.RowHeight;
            }
            y += UiStyle.Gap;
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

        /// <summary>Every colonist who can wear apparel, the policy's own first.</summary>
        private static List<Pawn> Colonists(ApparelPolicy policy)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return new List<Pawn>();
            }
            return map.mapPawns.FreeColonistsSpawned
                .Where(pawn => pawn.apparel != null && pawn.outfits != null)
                .OrderBy(pawn => pawn.outfits.CurrentApparelPolicy == policy ? 0 : 1)
                .ToList();
        }

        /// <summary>Gathers each source's pieces at most once a second, for the tab counts and the prediction.</summary>
        private static void GatherPieces(Pawn pawn, ApparelPolicy policy)
        {
            if (pawn == predictedFor.pawn && policy == predictedFor.policy && material == predictedFor.material
                && Time.realtimeSinceStartup - gatheredAt < RefreshSeconds)
            {
                return;
            }
            gatheredAt = Time.realtimeSinceStartup;
            foreach (PieceSource each in Sources)
            {
                try
                {
                    pieces[each] = PieceSources.For(pawn, policy, each, material);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Optimal Loadouts] Couldn't gather pieces for the outfit preview: " + e, "OptimalLoadouts.Gather".GetHashCode());
                    pieces[each] = new List<Apparel>();
                }
            }
        }

        /// <summary>
        /// The prediction, worked out again when the pawn, policy, source, material or any order changes, when the
        /// pieces on offer change, and every refresh for storage, where reach and what's worn change too.
        /// </summary>
        private static PredictedOutfit PredictionFor(Pawn pawn, ApparelPolicy policy)
        {
            int version = ApparelOrdersComponent.Current?.Version ?? 0;
            var key = (pawn, policy, source, material, version);
            List<Apparel> offered = pieces[source];
            bool storageRefreshed = source == PieceSource.InStorage && offered != predictedFrom;
            if (prediction != null && key == predictedFor && !storageRefreshed && SamePieces(offered, predictedFrom))
            {
                return prediction;
            }
            predictedFor = key;
            predictedFrom = offered;
            try
            {
                prediction = OutfitPredictor.Predict(pawn, policy, source, offered);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't predict an outfit for the preview: " + e, "OptimalLoadouts.Predict".GetHashCode());
                prediction = new PredictedOutfit(new List<Apparel>(), new List<Apparel>(), new List<Apparel>(),
                    new List<Apparel>(), new List<PredictedSwap>(), true, false);
            }
            return prediction;
        }

        private static bool SamePieces(List<Apparel> a, List<Apparel> b)
        {
            if (a == b)
            {
                return true;
            }
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void DrawSourceTabs(Rect section)
        {
            var tabs = new List<TabRecord>
            {
                SourceTab(PieceSource.AnythingAllowed, "OL_Preview_SourceAllowed"),
                SourceTab(PieceSource.CraftableNow, "OL_Preview_SourceCraftable"),
                SourceTab(PieceSource.InStorage, "OL_Preview_SourceStorage"),
            };
            using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
            {
                TabDrawer.DrawTabs(section, tabs);
            }
        }

        private static TabRecord SourceTab(PieceSource tabSource, string key)
        {
            int count = pieces.TryGetValue(tabSource, out List<Apparel> offered) ? offered.Count : 0;
            return new TabRecord(key.Translate(count), () =>
            {
                source = tabSource;
                scroll = Vector2.zero;
            }, source == tabSource);
        }

        private static string SourceText(Pawn pawn, ApparelPolicy policy)
        {
            string from;
            switch (source)
            {
                case PieceSource.AnythingAllowed:
                    from = "OL_Preview_FromAllowed".Translate();
                    break;
                case PieceSource.CraftableNow:
                    from = "OL_Preview_FromCraftable".Translate();
                    break;
                default:
                    from = "OL_Preview_FromStorage".Translate();
                    break;
            }
            return pawn.outfits.CurrentApparelPolicy == policy ? from : "OL_Preview_AsIfOnPolicy".Translate(from).ToString();
        }

        private static void DrawPawnRow(Rect row, Pawn pawn, List<Pawn> pawns, ApparelPolicy policy)
        {
            Rect previous = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect next = new Rect(row.xMax - UiStyle.IconSize, previous.y, UiStyle.IconSize, UiStyle.IconSize);
            int index = pawns.IndexOf(pawn);
            if (pawns.Count > 1)
            {
                if (Widgets.ButtonImage(previous, TexUI.ArrowTexLeft, true, "OL_Preview_PreviousPawn".Translate()))
                {
                    chosenPawn = pawns[(index + pawns.Count - 1) % pawns.Count];
                }
                if (Widgets.ButtonImage(next, TexUI.ArrowTexRight, true, "OL_Preview_NextPawn".Translate()))
                {
                    chosenPawn = pawns[(index + 1) % pawns.Count];
                }
            }
            Rect button = new Rect(previous.xMax + UiStyle.Gap, row.y, next.x - previous.xMax - 2f * UiStyle.Gap, row.height);
            string label = "OL_Preview_Pawn".Translate(pawn.LabelShort, index + 1, pawns.Count);
            if (Widgets.ButtonText(button, label.Truncate(button.width)))
            {
                List<FloatMenuOption> options = pawns
                    .Select(each => new FloatMenuOption(
                        "OL_Preview_PawnOption".Translate(each.LabelShort, each.outfits.CurrentApparelPolicy?.label ?? ""),
                        () => chosenPawn = each))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
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
                switch (source)
                {
                    case PieceSource.AnythingAllowed:
                        return "OL_Preview_NothingAllowed".Translate();
                    case PieceSource.CraftableNow:
                        return "OL_Preview_NothingCraftable".Translate();
                    default:
                        return "OL_Preview_NothingToPick".Translate();
                }
            }
            if (outfit.PutOn.Count == 0 && outfit.TakenOff.Count == 0)
            {
                return source == PieceSource.InStorage ? "OL_Preview_NoChanges".Translate() : "OL_Preview_NothingWorn".Translate();
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
            // Imagined outfits are all new, so only storage marks what changes.
            bool marksChanges = source == PieceSource.InStorage;
            float y = 0f;
            foreach (Apparel piece in worn)
            {
                bool isNew = outfit.PutOn.Contains(piece);
                string tag = marksChanges && isNew ? "OL_Preview_New".Translate().ToString() : null;
                string tooltip = isNew ? WhyPutOn(piece, outfit) : "OL_Preview_Kept".Translate().ToString();
                DrawPieceRow(new Rect(0f, y, view.width, UiStyle.RowHeight), piece, marksChanges && !isNew, tag, tooltip);
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
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(predictedFor.policy);
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
