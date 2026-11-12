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
    /// every piece it allows, what the colony can make now, or what's in storage, and a list by layer saying why each
    /// piece won and what the last edit changed. What it shows is the game's <see cref="PreviewSession"/>.
    /// </summary>
    public static class OutfitPreviewPanel
    {
        private const float PortraitSize = 180f;

        /// <summary>
        /// Draws the preview into <paramref name="rect"/>. With a <paramref name="slotRect"/>, an open slot's pieces get
        /// that panel of their own; without one (Rect.zero, on narrow screens), they take the outfit list's place.
        /// </summary>
        public static void Draw(Rect rect, Rect slotRect, ApparelPolicy policy)
        {
            PreviewSession session = PreviewSession.Current;
            if (session == null)
            {
                return;
            }
            Rect section = rect;
            section.yMin += TabDrawer.TabHeight;
            Rect slotSection = slotRect;
            if (slotSection.width > 0f)
            {
                slotSection.yMin = section.yMin;
            }
            Widgets.DrawMenuSection(section);
            try
            {
                DrawContents(section, slotSection, policy, session);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't draw the outfit preview: " + e, "OptimalLoadouts.PreviewPanel".GetHashCode());
            }
        }

        private static void DrawContents(Rect section, Rect slotSection, ApparelPolicy policy, PreviewSession session)
        {
            List<Pawn> pawns = Colonists(policy);
            if (pawns.Count == 0 || policy == null)
            {
                UiStyle.Note(section.ContractedBy(UiStyle.Gap), "OL_Preview_NoPawn".Translate());
                return;
            }
            session.Show(policy, pawns);
            DrawSourceTabs(section, session);

            Rect inner = section.ContractedBy(UiStyle.Gap);
            float y = DrawControls(inner, session, pawns);
            Rect list = new Rect(inner.x, y, inner.width, inner.yMax - y);
            bool slotHasOwnPanel = slotSection.width > 0f;
            if (slotHasOwnPanel)
            {
                // The slot panel always shows a slot: the one picked last if the outfit still has it, else the outermost.
                session.KeepSlotOpen();
                DrawOutfitList(list, session);
                Widgets.DrawMenuSection(slotSection);
                Rect slotArea = slotSection.ContractedBy(UiStyle.Gap);
                if (session.Slot == null)
                {
                    using (new TextBlock(TextAnchor.MiddleCenter))
                    {
                        UiStyle.Note(slotArea, "OL_Slot_Hint".Translate());
                    }
                }
                else if (session.Slot.Pieces != null)
                {
                    SlotBrowserPanel.Draw(slotArea, session, showBack: false);
                }
            }
            else if (session.Slot != null && session.Slot.Pieces != null)
            {
                SlotBrowserPanel.Draw(list, session, showBack: true);
            }
            else
            {
                DrawOutfitList(list, session);
            }
        }

        /// <summary>
        /// Whether the open slot is this piece's own, so its outfit row shows which slot the list is for. A layer's slot
        /// lights its band instead.
        /// </summary>
        private static bool InOpenSlot(Apparel piece, PreviewSession session)
        {
            OutfitSlot slot = session.Slot?.Slot;
            return slot != null && slot.Piece != null && slot.Holds(piece.def, session.Pawn.RaceProps.body);
        }

        /// <summary>The colonist, where pieces come from, the material, the drawing and a status line. Returns the y below them.</summary>
        private static float DrawControls(Rect area, PreviewSession session, List<Pawn> pawns)
        {
            float y = area.y;
            DrawPawnRow(new Rect(area.x, y, area.width, UiStyle.RowHeight), session, pawns);
            y += UiStyle.RowHeight;
            y += DrawWrappedNote(new Rect(area.x, y, area.width, 0f), SourceText(session));
            if (session.Source != PieceSource.InStorage)
            {
                Rect button = new Rect(area.x, y, area.width, UiStyle.RowHeight);
                if (Widgets.ButtonText(button, "OL_Preview_Material".Translate(session.Material.Label).ToString().Truncate(button.width)))
                {
                    Find.WindowStack.Add(new Dialog_PickMaterial(choice => session.Material = choice));
                }
                TooltipHandler.TipRegion(button, "OL_Preview_MaterialTip".Translate());
                y += UiStyle.RowHeight;
            }
            y += UiStyle.Gap;
            DrawPortrait(new Rect(area.x, y, area.width, PortraitSize), session);
            y += PortraitSize + UiStyle.Gap;
            string status = StatusText(session);
            if (status != null)
            {
                y += DrawWrappedNote(new Rect(area.x, y, area.width, 0f), status);
            }
            return y;
        }

        /// <summary>A centred grey note as tall as its text needs, at least a row. Returns its height.</summary>
        private static float DrawWrappedNote(Rect at, string text)
        {
            float height = Mathf.Max(UiStyle.RowHeight, Text.CalcHeight(text, at.width));
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                UiStyle.Note(new Rect(at.x, at.y, at.width, height), text);
            }
            return height;
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

        private static void DrawSourceTabs(Rect section, PreviewSession session)
        {
            var tabs = new List<TabRecord>
            {
                SourceTab(session, PieceSource.AnythingAllowed, "OL_Preview_SourceAllowed"),
                SourceTab(session, PieceSource.CraftableNow, "OL_Preview_SourceCraftable"),
                SourceTab(session, PieceSource.InStorage, "OL_Preview_SourceStorage"),
            };
            using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
            {
                TabDrawer.DrawTabs(section, tabs);
            }
        }

        private static TabRecord SourceTab(PreviewSession session, PieceSource tabSource, string key)
        {
            return new TabRecord(key.Translate(session.PieceCount(tabSource)), () => session.Source = tabSource, session.Source == tabSource);
        }

        private static string SourceText(PreviewSession session)
        {
            string from;
            switch (session.Source)
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
            return session.Pawn.outfits.CurrentApparelPolicy == session.Policy ? from : "OL_Preview_AsIfOnPolicy".Translate(from).ToString();
        }

        private static void DrawPawnRow(Rect row, PreviewSession session, List<Pawn> pawns)
        {
            Pawn pawn = session.Pawn;
            Rect previous = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect next = new Rect(row.xMax - UiStyle.IconSize, previous.y, UiStyle.IconSize, UiStyle.IconSize);
            int index = pawns.IndexOf(pawn);
            if (pawns.Count > 1)
            {
                if (Widgets.ButtonImage(previous, TexUI.ArrowTexLeft, true, "OL_Preview_PreviousPawn".Translate()))
                {
                    session.ChoosePawn(pawns[(index + pawns.Count - 1) % pawns.Count]);
                }
                if (Widgets.ButtonImage(next, TexUI.ArrowTexRight, true, "OL_Preview_NextPawn".Translate()))
                {
                    session.ChoosePawn(pawns[(index + 1) % pawns.Count]);
                }
            }
            Rect button = new Rect(previous.xMax + UiStyle.Gap, row.y, next.x - previous.xMax - 2f * UiStyle.Gap, row.height);
            string label = "OL_Preview_Pawn".Translate(pawn.LabelShort, index + 1, pawns.Count);
            if (Widgets.ButtonText(button, label.Truncate(button.width)))
            {
                List<FloatMenuOption> options = pawns
                    .Select(each => new FloatMenuOption(
                        "OL_Preview_PawnOption".Translate(each.LabelShort, each.outfits.CurrentApparelPolicy?.label ?? ""),
                        () => session.ChoosePawn(each)))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawPortrait(Rect area, PreviewSession session)
        {
            Rect portrait = new Rect(area.center.x - PortraitSize / 2f, area.y, PortraitSize, PortraitSize);
            GUI.DrawTexture(portrait, OutfitPortrait.Draw(session.Pawn, session.Drawn, session.Rotation, portrait.size));
            Rect turnLeft = new Rect(portrait.x - UiStyle.IconSize - UiStyle.Gap, portrait.center.y - UiStyle.IconSize / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Rect turnRight = new Rect(portrait.xMax + UiStyle.Gap, turnLeft.y, UiStyle.IconSize, UiStyle.IconSize);
            Rot4 rotation = session.Rotation;
            if (Widgets.ButtonImage(turnLeft, TexUI.RotLeftTex, true, "OL_Preview_Turn".Translate()))
            {
                rotation.Rotate(RotationDirection.Counterclockwise);
            }
            if (Widgets.ButtonImage(turnRight, TexUI.RotRightTex, true, "OL_Preview_Turn".Translate()))
            {
                rotation.Rotate(RotationDirection.Clockwise);
            }
            session.Rotation = rotation;
        }

        private static string StatusText(PreviewSession session)
        {
            PredictedOutfit outfit = session.Outfit;
            Apparel tried = session.Slot?.TriedOn;
            if (tried != null)
            {
                return "OL_Slot_TryingOn".Translate(tried.LabelCap);
            }
            if (!outfit.Settled)
            {
                return "OL_Preview_Unsettled".Translate(OutfitPredictor.MaxSwaps);
            }
            if (outfit.NothingToPick)
            {
                switch (session.Source)
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
                return session.Source == PieceSource.InStorage ? "OL_Preview_NoChanges".Translate() : "OL_Preview_NothingWorn".Translate();
            }
            return null;
        }

        /// <summary>
        /// The outfit under a band for each layer, head layers first, each outermost first. A bracket on the left joins the
        /// bands of a piece on several layers. From storage, what comes off follows. Then what the last change took off.
        /// </summary>
        private static void DrawOutfitList(Rect rect, PreviewSession session)
        {
            PredictedOutfit outfit = session.Outfit;
            DrawOutfitBand(new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight));
            rect.yMin += UiStyle.RowHeight;
            List<OutfitLayer> layers = OutfitLayers.For(session.Pawn, outfit.Worn);
            OutfitChanges marks = session.Changes;
            // A worn piece taken off is listed under "Takes off" already.
            List<Apparel> gone = marks == null ? new List<Apparel>() : marks.Removed.Except(outfit.TakenOff).ToList();
            List<Bracket> brackets = Brackets(layers);
            float gutter = brackets.Count == 0 ? 0f : (brackets.Max(bracket => bracket.Lane) + 1) * UiStyle.BracketWidth;
            float height = layers.Sum(layer => UiStyle.BandHeight + layer.Pieces.Count * UiStyle.RowHeight);
            if (outfit.TakenOff.Count > 0)
            {
                height += (outfit.TakenOff.Count + 1) * UiStyle.RowHeight;
            }
            if (gone.Count > 0)
            {
                height += (gone.Count + 1) * UiStyle.RowHeight;
            }
            Rect view = new Rect(0f, 0f, rect.width - GenUI.ScrollBarWidth, height);
            Widgets.BeginScrollView(rect, ref session.OutfitScroll, view);
            try
            {
                DrawBrackets(brackets, gutter);
                Rect rows = new Rect(gutter, 0f, view.width - gutter, view.height);
                DrawOutfitRows(rows, layers, session, marks, gone);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>The list's title, on a band like the layers' that sets it apart from the drawing above.</summary>
        private static void DrawOutfitBand(Rect band)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), "OL_Preview_OutfitHeader".Translate());
            }
        }

        private static void DrawOutfitRows(Rect rows, List<OutfitLayer> layers, PreviewSession session, OutfitChanges marks, List<Apparel> gone)
        {
            PredictedOutfit outfit = session.Outfit;
            float y = rows.y;
            // Pieces sit a little in from their layer's band.
            Rect pieces = new Rect(rows.x + UiStyle.Indent, rows.y, rows.width - UiStyle.Indent, rows.height);
            foreach (OutfitLayer layer in layers)
            {
                // A layer covered from the band above sits in from it, like a piece.
                float indent = layer.Nested ? UiStyle.Indent : 0f;
                DrawLayerBand(new Rect(rows.x + indent, y, rows.width - indent, UiStyle.BandHeight), layer, session);
                y += UiStyle.BandHeight;
                foreach (Apparel piece in layer.Pieces)
                {
                    DrawWornRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, session, marks);
                    y += UiStyle.RowHeight;
                }
            }
            if (outfit.TakenOff.Count > 0)
            {
                UiStyle.Note(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), "OL_Preview_TakesOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in outfit.TakenOff)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true, WhyTakenOff(piece, outfit), opensSlot: false, session);
                    y += UiStyle.RowHeight;
                }
            }
            if (gone.Count > 0)
            {
                UiStyle.Note(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), "OL_Preview_LastChangeTookOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in gone)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true, WhyGone(piece, marks), opensSlot: false, session,
                        UiStyle.RemovedColor);
                    y += UiStyle.RowHeight;
                }
            }
        }

        /// <summary>
        /// A layer's name on a dark band that opens the layer's slot. On the right, "nothing" when nothing is on it, or the
        /// pieces listed under an outer layer that cover it.
        /// </summary>
        private static void DrawLayerBand(Rect band, OutfitLayer layer, PreviewSession session)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            OutfitSlot open = session.Slot?.Slot;
            if (open != null && open.Layer == layer.Layer)
            {
                Widgets.DrawHighlightSelected(band);
            }
            bool empty = layer.Pieces.Count == 0 && layer.CoveredBy.Count == 0;
            if (UiStyle.ClickableRow(band))
            {
                session.OpenSlot(OutfitSlot.OnLayer(layer.Layer), followWhenFilled: empty);
            }
            string onRight = null;
            string tip;
            if (empty)
            {
                onRight = "OL_Preview_LayerNothing".Translate();
                tip = "OL_Preview_LayerEmptyTip".Translate();
            }
            else if (layer.Pieces.Count == 0)
            {
                onRight = layer.CoveredBy.Select(piece => PieceLabel.Of(piece)).ToCommaList();
                tip = "OL_Preview_LayerCoveredTip".Translate(onRight);
            }
            else
            {
                tip = "OL_Preview_LayerTip".Translate();
            }
            TooltipHandler.TipRegion(band, tip);
            Rect text = band.ContractedBy(UiStyle.Gap / 2f, 0f);
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleLeft))
            {
                Widgets.Label(text, layer.Layer.LabelCap.ToString().Truncate(text.width / 2f));
            }
            if (onRight != null)
            {
                using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleRight, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(text, onRight.Truncate(text.width / 2f));
                }
            }
        }

        /// <summary>A piece of the outfit, with why it won and what the last change did.</summary>
        private static void DrawWornRow(Rect row, Apparel piece, PreviewSession session, OutfitChanges marks)
        {
            PredictedOutfit outfit = session.Outfit;
            var parts = new List<string>();
            bool isNewInChange = marks != null && marks.Added.Contains(piece);
            if (isNewInChange)
            {
                List<string> replaced = marks.Replaced(piece).Select(gone => PieceLabel.Of(gone).UncapitalizeFirst()).ToList();
                parts.Add(replaced.Count > 0
                    ? "OL_Preview_NewReplacing".Translate(replaced.ToCommaList(useAnd: true)).Resolve()
                    : "OL_Preview_New".Translate().Resolve());
            }
            // Pieces kept from what the pawn wears now are dimmed, so what's put on stands out.
            bool putOn = outfit.PutOn.Contains(piece);
            string why = putOn ? WhyPutOn(piece, session) : "OL_Preview_Kept".Translate().ToString();
            if (why != null)
            {
                parts.Add(why);
            }
            parts.Add("OL_Slot_ClickToOpen".Translate());
            Color? tint = null;
            if (TakenOffByHoveredRow(piece, session) || TakenOffByTriedOn(piece, session))
            {
                tint = UiStyle.RemovedColor;
            }
            else if (isNewInChange)
            {
                tint = UiStyle.AddedColor;
            }
            DrawPieceRow(row, piece, !putOn, string.Join("\n\n", parts), opensSlot: true, session, tint);
        }

        /// <summary>Whether the slot browser row under the mouse would take this piece off too, besides the slot's own.</summary>
        private static bool TakenOffByHoveredRow(Apparel piece, PreviewSession session)
        {
            SlotChoice hovered = session.Slot?.Hovered;
            return hovered != null && hovered.AlsoReplaces.Contains(piece);
        }

        /// <summary>Whether the piece being tried on takes this one off, the slot's own piece included.</summary>
        private static bool TakenOffByTriedOn(Apparel piece, PreviewSession session)
        {
            Apparel tried = session.Slot?.TriedOn;
            return tried != null && tried != piece && !ApparelUtility.CanWearTogether(piece.def, tried.def, session.Pawn.RaceProps.body);
        }

        /// <summary>
        /// One piece of the outfit, on a faint <paramref name="tint"/> when given. A worn piece's row opens its slot. The ⓘ
        /// button opens its info card.
        /// </summary>
        private static void DrawPieceRow(Rect row, Apparel piece, bool dimmed, string tooltip, bool opensSlot, PreviewSession session,
            Color? tint = null)
        {
            if (tint != null)
            {
                Widgets.DrawBoxSolid(row, tint.Value);
            }
            if (opensSlot && InOpenSlot(piece, session))
            {
                Widgets.DrawHighlightSelected(row);
            }
            Rect info = new Rect(row.xMax - Widgets.InfoCardButtonSize, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f,
                Widgets.InfoCardButtonSize, Widgets.InfoCardButtonSize);
            Widgets.InfoCardButton(info.x, info.y, piece);
            row.xMax = info.x - UiStyle.Gap;
            if (opensSlot && UiStyle.ClickableRow(row))
            {
                session.OpenSlot(OutfitSlot.Of(piece));
            }
            TooltipHandler.TipRegion(row, tooltip);
            Rect icon = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            Widgets.ThingIcon(icon, piece, dimmed ? 0.5f : 1f);
            Rect label = new Rect(icon.xMax + UiStyle.Gap, row.y, row.width - icon.width - UiStyle.Gap, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft, dimmed ? ColoredText.SubtleGrayColor : Color.white))
            {
                Widgets.Label(label, PieceLabel.Of(piece).Truncate(label.width));
            }
        }

        /// <summary>A line beside the outfit list joining the bands of the layers one piece is on.</summary>
        private sealed class Bracket
        {
            /// <summary>The middle of each band the piece is on, top to bottom, with how far in the band starts.</summary>
            public readonly List<(float y, float indent)> Marks;

            public int Lane;

            public Bracket(List<(float y, float indent)> marks)
            {
                Marks = marks;
            }

            public float Top => Marks[0].y;

            public float Bottom => Marks[Marks.Count - 1].y;
        }

        /// <summary>
        /// A bracket for each listed piece on several layers, laid out as <see cref="DrawOutfitRows"/> lays out the rows.
        /// Brackets that overlap take separate lanes, the first nearest the list.
        /// </summary>
        private static List<Bracket> Brackets(List<OutfitLayer> layers)
        {
            var ownBand = new Dictionary<Apparel, (float y, float indent)>();
            var coveredBands = new Dictionary<Apparel, List<(float y, float indent)>>();
            float y = 0f;
            foreach (OutfitLayer layer in layers)
            {
                (float y, float indent) band = (y + UiStyle.BandHeight / 2f, layer.Nested ? UiStyle.Indent : 0f);
                foreach (Apparel piece in layer.CoveredBy)
                {
                    if (!coveredBands.TryGetValue(piece, out List<(float y, float indent)> bands))
                    {
                        bands = new List<(float y, float indent)>();
                        coveredBands[piece] = bands;
                    }
                    bands.Add(band);
                }
                foreach (Apparel piece in layer.Pieces)
                {
                    ownBand[piece] = band;
                }
                y += UiStyle.BandHeight + layer.Pieces.Count * UiStyle.RowHeight;
            }
            var brackets = new List<Bracket>();
            foreach (KeyValuePair<Apparel, List<(float y, float indent)>> covered in coveredBands)
            {
                if (ownBand.TryGetValue(covered.Key, out (float y, float indent) own))
                {
                    var marks = new List<(float y, float indent)>(covered.Value) { own };
                    marks.SortBy(mark => mark.y);
                    brackets.Add(new Bracket(marks));
                }
            }
            brackets.SortBy(bracket => bracket.Top);
            var laneBottoms = new List<float>();
            foreach (Bracket bracket in brackets)
            {
                int lane = laneBottoms.FindIndex(bottom => bottom < bracket.Top);
                if (lane < 0)
                {
                    lane = laneBottoms.Count;
                    laneBottoms.Add(0f);
                }
                laneBottoms[lane] = bracket.Bottom;
                bracket.Lane = lane;
            }
            return brackets;
        }

        /// <summary>Each bracket as a line in its lane, with a tick out to each band it joins.</summary>
        private static void DrawBrackets(List<Bracket> brackets, float gutter)
        {
            Color color = ColoredText.SubtleGrayColor;
            foreach (Bracket bracket in brackets)
            {
                float x = gutter - (bracket.Lane + 0.5f) * UiStyle.BracketWidth;
                Widgets.DrawBoxSolid(new Rect(x, bracket.Top, 1f, bracket.Bottom - bracket.Top + 1f), color);
                foreach ((float y, float indent) mark in bracket.Marks)
                {
                    Widgets.DrawLineHorizontal(x, mark.y, gutter + mark.indent - x, color);
                }
            }
        }

        private static string WhyPutOn(Apparel piece, PreviewSession session)
        {
            PredictedOutfit outfit = session.Outfit;
            PredictedSwap swap = outfit.Swaps.LastOrDefault(s => s.PutOn == piece);
            if (swap == null)
            {
                return null;
            }
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(session.Policy);
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
            OrderLine line = lines[verdict.DecidingLine];
            if (line.IsItem)
            {
                return "OL_Preview_WhyLocked".Translate(line.item.LabelCap);
            }
            return "OL_Preview_WhyBetter".Translate(OrderLineText.StatOnLayer(line), Amount(line.stat, verdict.Delta));
        }

        private static string ReasonRunnerUpLost(PredictedSwap swap, IReadOnlyList<OrderLine> lines, bool hasOrder)
        {
            string name = swap.RunnerUp.LabelCap;
            OrderVerdict runnerUp = swap.RunnerUpVerdict;
            if (!hasOrder || runnerUp.GameDecides || runnerUp.IsTie || runnerUp.DecidingLine >= lines.Count)
            {
                return "OL_Preview_RunnerUpScoredLower".Translate(name);
            }
            OrderLine line = lines[runnerUp.DecidingLine];
            if (line.IsItem)
            {
                if (runnerUp.Delta < 0f)
                {
                    return "OL_Preview_RunnerUpNotLocked".Translate(name, line.item.label);
                }
                return runnerUp.DecidingLine == swap.Verdict.DecidingLine
                    ? "OL_Preview_RunnerUpSameLocked".Translate(name)
                    : "OL_Preview_RunnerUpLaterItem".Translate(name);
            }
            string onLine = OrderLineText.StatOnLayer(line);
            if (runnerUp.Delta < 0f)
            {
                return "OL_Preview_RunnerUpWorse".Translate(name, onLine, Amount(line.stat, runnerUp.Delta));
            }
            if (runnerUp.DecidingLine == swap.Verdict.DecidingLine)
            {
                return "OL_Preview_RunnerUpSmallerGain".Translate(name, onLine);
            }
            return "OL_Preview_RunnerUpLaterStat".Translate(name, onLine);
        }

        /// <summary>Why a piece the last change took off is gone: what replaced it, or that it's no longer picked.</summary>
        private static string WhyGone(Apparel piece, OutfitChanges marks)
        {
            Apparel replacement = marks.Added.FirstOrDefault(added => marks.Replaced(added).Contains(piece));
            return replacement != null
                ? "OL_Preview_ReplacedBy".Translate(replacement.LabelCap).Resolve()
                : "OL_Preview_NoLongerPicked".Translate().Resolve();
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
