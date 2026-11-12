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
    /// piece won and what the last edit changed.
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

        /// <summary>The open tab's pieces for the open slot, allowed or not, for the slot browser.</summary>
        private static List<Apparel> slotPieces;

        private static (PieceSource source, OutfitSlot slot) slotPiecesFor;

        /// <summary>What the open slot was opened for. It closes when any of these changes, since its pieces no longer apply.</summary>
        private static (Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material) slotOpenedFor;

        /// <summary>The slot opened last, so a new outfit opens the same slot when it has one.</summary>
        private static OutfitSlot lastSlot;

        /// <summary>Whether the open slot is a layer opened while empty, so it turns into the slot of the piece that fills it.</summary>
        private static bool openedEmptyLayer;

        private static (Pawn pawn, ApparelPolicy policy, PieceSource source, MaterialChoice material, int version) predictedFor;
        private static List<Apparel> predictedFrom;
        private static PredictedOutfit prediction;
        private static ApparelPolicy shownPolicy;

        /// <summary>What the last change to the outfit put on and took off, or null since the colonist, policy, tab or material changed.</summary>
        private static OutfitChanges changes;

        /// <summary>Forgets the last game's pawns, policy, pieces and prediction, so a loaded game doesn't keep them alive.</summary>
        private static void Forget()
        {
            OutfitPortrait.Forget();
            ImaginedPieces.Forget();
            PieceSources.Forget();
            SlotBrowserPanel.Close();
            lastSlot = null;
            changes = null;
            chosenPawn = null;
            shownPolicy = null;
            pieces.Clear();
            slotPieces = null;
            gatheredAt = float.MinValue;
            predictedFor = default;
            predictedFrom = null;
            prediction = null;
            scroll = Vector2.zero;
        }

        /// <summary>
        /// Draws the preview into <paramref name="rect"/>. With a <paramref name="slotRect"/>, an open slot's pieces get
        /// that panel of their own; without one (Rect.zero, on narrow screens), they take the outfit list's place.
        /// </summary>
        public static void Draw(Rect rect, Rect slotRect, ApparelPolicy policy)
        {
            if (Current.Game != shownGame)
            {
                shownGame = Current.Game;
                Forget();
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
                DrawContents(section, slotSection, policy);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't draw the outfit preview: " + e, "OptimalLoadouts.PreviewPanel".GetHashCode());
            }
        }

        private static void DrawContents(Rect section, Rect slotSection, ApparelPolicy policy)
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
            if (SlotBrowserPanel.IsOpen && slotOpenedFor != (pawn, policy, source, material))
            {
                SlotBrowserPanel.Close();
            }
            GatherPieces(pawn, policy);
            DrawSourceTabs(section);
            PredictedOutfit outfit = PredictionFor(pawn, policy);

            FollowFilledLayer(pawn, outfit);
            Rect inner = section.ContractedBy(UiStyle.Gap);
            float y = DrawControls(inner, pawn, pawns, policy, outfit);
            Rect list = new Rect(inner.x, y, inner.width, inner.yMax - y);
            bool slotHasOwnPanel = slotSection.width > 0f;
            if (slotHasOwnPanel)
            {
                // The slot panel always shows a slot: the one picked last if the outfit still has it, else the outermost.
                KeepSlotOpen(pawn, outfit);
                DrawOutfitList(list, outfit);
                Widgets.DrawMenuSection(slotSection);
                Rect slotArea = slotSection.ContractedBy(UiStyle.Gap);
                if (!SlotBrowserPanel.IsOpen)
                {
                    using (new TextBlock(TextAnchor.MiddleCenter))
                    {
                        UiStyle.Note(slotArea, "OL_Slot_Hint".Translate());
                    }
                }
                else if (slotPieces != null)
                {
                    DrawSlot(slotArea, pawn, policy, outfit, showBack: false);
                }
            }
            else if (SlotBrowserPanel.IsOpen && slotPieces != null)
            {
                DrawSlot(list, pawn, policy, outfit, showBack: true);
            }
            else
            {
                DrawOutfitList(list, outfit);
            }
        }

        private static void DrawSlot(Rect area, Pawn pawn, ApparelPolicy policy, PredictedOutfit outfit, bool showBack)
        {
            if (SlotBrowserPanel.Draw(area, pawn, policy, outfit.Worn, slotPieces, showBack))
            {
                // The filter changed, so gather and predict again now rather than at the next refresh.
                gatheredAt = float.MinValue;
            }
        }

        /// <summary>
        /// Opens the slot picked last: a layer again, or a piece's place when a piece of the new outfit fills it. Otherwise
        /// the outermost worn piece's, or with nothing worn, the outermost layer's.
        /// </summary>
        private static void KeepSlotOpen(Pawn pawn, PredictedOutfit outfit)
        {
            if (SlotBrowserPanel.IsOpen)
            {
                return;
            }
            BodyDef body = pawn.RaceProps.body;
            if (lastSlot != null && lastSlot.Layer != null)
            {
                OpenSlot(lastSlot, followWhenFilled: lastSlot.FilledBy(outfit.Worn, body) == null);
                return;
            }
            List<OutfitLayer> layers = OutfitLayers.For(pawn, outfit.Worn);
            List<Apparel> worn = layers.SelectMany(layer => layer.Pieces).ToList();
            Apparel sameSlot = lastSlot == null ? null : lastSlot.FilledBy(worn, body);
            if (sameSlot != null || worn.Count > 0)
            {
                OpenSlot(OutfitSlot.Of(sameSlot ?? worn[0]));
            }
            else if (layers.Count > 0)
            {
                OpenSlot(OutfitSlot.OnLayer(layers[0].Layer), followWhenFilled: true);
            }
        }

        /// <summary>
        /// A layer opened while empty turns into the slot of the piece that fills it, since pieces on one layer that cover
        /// different body parts (a shirt and trousers) don't compete.
        /// </summary>
        private static void FollowFilledLayer(Pawn pawn, PredictedOutfit outfit)
        {
            OutfitSlot open = SlotBrowserPanel.Slot;
            if (open == null || open.Layer == null || !openedEmptyLayer)
            {
                return;
            }
            Apparel filling = open.FilledBy(outfit.Worn, pawn.RaceProps.body);
            if (filling != null)
            {
                OpenSlot(OutfitSlot.Of(filling));
            }
        }

        private static void OpenSlot(OutfitSlot slot, bool followWhenFilled = false)
        {
            SlotBrowserPanel.Open(slot);
            slotOpenedFor = (chosenPawn, shownPolicy, source, material);
            slotPieces = null;
            lastSlot = slot;
            openedEmptyLayer = followWhenFilled;
        }

        /// <summary>
        /// Whether the open slot is this piece's own, so its outfit row shows which slot the list is for. A layer's slot
        /// lights its band instead.
        /// </summary>
        private static bool InOpenSlot(Apparel piece)
        {
            OutfitSlot slot = SlotBrowserPanel.Slot;
            return slot != null && slot.Piece != null && slot.Holds(piece.def, chosenPawn.RaceProps.body);
        }

        /// <summary>The colonist, where pieces come from, the material, the drawing and a status line. Returns the y below them.</summary>
        private static float DrawControls(Rect area, Pawn pawn, List<Pawn> pawns, ApparelPolicy policy, PredictedOutfit outfit)
        {
            float y = area.y;
            DrawPawnRow(new Rect(area.x, y, area.width, UiStyle.RowHeight), pawn, pawns);
            y += UiStyle.RowHeight;
            y += DrawWrappedNote(new Rect(area.x, y, area.width, 0f), SourceText(pawn, policy));
            if (source != PieceSource.InStorage)
            {
                Rect button = new Rect(area.x, y, area.width, UiStyle.RowHeight);
                if (Widgets.ButtonText(button, "OL_Preview_Material".Translate(material.Label).ToString().Truncate(button.width)))
                {
                    Find.WindowStack.Add(new Dialog_PickMaterial(choice => material = choice));
                }
                TooltipHandler.TipRegion(button, "OL_Preview_MaterialTip".Translate());
                y += UiStyle.RowHeight;
            }
            y += UiStyle.Gap;
            DrawPortrait(new Rect(area.x, y, area.width, PortraitSize), pawn, Drawn(outfit));
            y += PortraitSize + UiStyle.Gap;
            string status = StatusText(outfit);
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

        /// <summary>The outfit to draw: as predicted, or with the piece being tried on in place of its slot's.</summary>
        private static IReadOnlyList<Apparel> Drawn(PredictedOutfit outfit)
        {
            Apparel tried = SlotBrowserPanel.TriedOn;
            if (tried == null)
            {
                return outfit.Worn;
            }
            BodyDef body = chosenPawn.RaceProps.body;
            List<Apparel> drawn = outfit.Worn.Where(worn => ApparelUtility.CanWearTogether(worn.def, tried.def, body)).ToList();
            drawn.Add(tried);
            return drawn;
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
            bool slotPiecesMissing = SlotBrowserPanel.IsOpen && (slotPieces == null || slotPiecesFor != (source, SlotBrowserPanel.Slot));
            if (pawn == predictedFor.pawn && policy == predictedFor.policy && material == predictedFor.material
                && Time.realtimeSinceStartup - gatheredAt < RefreshSeconds && !slotPiecesMissing)
            {
                return;
            }
            gatheredAt = Time.realtimeSinceStartup;
            if (SlotBrowserPanel.IsOpen)
            {
                try
                {
                    slotPieces = PieceSources.For(pawn, policy, source, material, SlotBrowserPanel.Slot);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[Optimal Loadouts] Couldn't gather a slot's pieces for the preview: " + e, "OptimalLoadouts.GatherSlot".GetHashCode());
                    slotPieces = new List<Apparel>();
                }
                slotPiecesFor = (source, SlotBrowserPanel.Slot);
            }
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
        /// pieces on offer change, and every refresh for storage, where reach and what's worn change too. A new outfit
        /// for the same pawn, policy, source and material is compared with the last, for the change marks.
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
            PredictedOutfit before = prediction;
            bool sameOutfit = before != null && (pawn, policy, source, material) == (predictedFor.pawn, predictedFor.policy, predictedFor.source, predictedFor.material);
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
            if (!sameOutfit)
            {
                changes = null;
            }
            else
            {
                // An outfit that came out the same keeps the marks of the last change that made a difference.
                OutfitChanges fresh = OutfitChanges.Between(before, prediction, pawn.RaceProps.body);
                if (fresh.Any)
                {
                    changes = fresh;
                }
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

        private static void DrawPawnRow(Rect row, Pawn pawn, List<Pawn> pawns)
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

        private static void DrawPortrait(Rect area, Pawn pawn, IReadOnlyList<Apparel> outfit)
        {
            Rect portrait = new Rect(area.center.x - PortraitSize / 2f, area.y, PortraitSize, PortraitSize);
            GUI.DrawTexture(portrait, OutfitPortrait.Draw(pawn, outfit, rotation, portrait.size));
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
            if (SlotBrowserPanel.TriedOn != null)
            {
                return "OL_Slot_TryingOn".Translate(SlotBrowserPanel.TriedOn.LabelCap);
            }
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

        /// <summary>
        /// The outfit under a band for each layer, head layers first, each outermost first. A bracket on the left joins the
        /// bands of a piece on several layers. From storage, what comes off follows. Then what the last change took off.
        /// </summary>
        private static void DrawOutfitList(Rect rect, PredictedOutfit outfit)
        {
            DrawOutfitBand(new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight));
            rect.yMin += UiStyle.RowHeight;
            List<OutfitLayer> layers = OutfitLayers.For(chosenPawn, outfit.Worn);
            OutfitChanges marks = changes;
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
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                DrawBrackets(brackets, gutter);
                Rect rows = new Rect(gutter, 0f, view.width - gutter, view.height);
                DrawOutfitRows(rows, layers, outfit, marks, gone);
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

        private static void DrawOutfitRows(Rect rows, List<OutfitLayer> layers, PredictedOutfit outfit, OutfitChanges marks, List<Apparel> gone)
        {
            float y = rows.y;
            // Pieces sit a little in from their layer's band.
            Rect pieces = new Rect(rows.x + UiStyle.Indent, rows.y, rows.width - UiStyle.Indent, rows.height);
            foreach (OutfitLayer layer in layers)
            {
                // A layer covered from the band above sits in from it, like a piece.
                float indent = layer.Nested ? UiStyle.Indent : 0f;
                DrawLayerBand(new Rect(rows.x + indent, y, rows.width - indent, UiStyle.BandHeight), layer);
                y += UiStyle.BandHeight;
                foreach (Apparel piece in layer.Pieces)
                {
                    DrawWornRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, outfit, marks);
                    y += UiStyle.RowHeight;
                }
            }
            if (outfit.TakenOff.Count > 0)
            {
                UiStyle.Note(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), "OL_Preview_TakesOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in outfit.TakenOff)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true, WhyTakenOff(piece, outfit), opensSlot: false);
                    y += UiStyle.RowHeight;
                }
            }
            if (gone.Count > 0)
            {
                UiStyle.Note(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), "OL_Preview_LastChangeTookOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in gone)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true, WhyGone(piece, marks), opensSlot: false,
                        UiStyle.RemovedColor);
                    y += UiStyle.RowHeight;
                }
            }
        }

        /// <summary>
        /// A layer's name on a dark band that opens the layer's slot. On the right, "nothing" when nothing is on it, or the
        /// pieces listed under an outer layer that cover it.
        /// </summary>
        private static void DrawLayerBand(Rect band, OutfitLayer layer)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            OutfitSlot open = SlotBrowserPanel.Slot;
            if (open != null && open.Layer == layer.Layer)
            {
                Widgets.DrawHighlightSelected(band);
            }
            bool empty = layer.Pieces.Count == 0 && layer.CoveredBy.Count == 0;
            if (UiStyle.ClickableRow(band))
            {
                OpenSlot(OutfitSlot.OnLayer(layer.Layer), followWhenFilled: empty);
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
        private static void DrawWornRow(Rect row, Apparel piece, PredictedOutfit outfit, OutfitChanges marks)
        {
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
            string why = putOn ? WhyPutOn(piece, outfit) : "OL_Preview_Kept".Translate().ToString();
            if (why != null)
            {
                parts.Add(why);
            }
            parts.Add("OL_Slot_ClickToOpen".Translate());
            DrawPieceRow(row, piece, !putOn, string.Join("\n\n", parts), opensSlot: true, isNewInChange ? UiStyle.AddedColor : (Color?)null);
        }

        /// <summary>
        /// One piece of the outfit, on a faint <paramref name="tint"/> when given. A worn piece's row opens its slot. The ⓘ
        /// button opens its info card.
        /// </summary>
        private static void DrawPieceRow(Rect row, Apparel piece, bool dimmed, string tooltip, bool opensSlot, Color? tint = null)
        {
            if (tint != null)
            {
                Widgets.DrawBoxSolid(row, tint.Value);
            }
            if (opensSlot && InOpenSlot(piece))
            {
                Widgets.DrawHighlightSelected(row);
            }
            Rect info = new Rect(row.xMax - Widgets.InfoCardButtonSize, row.y + (row.height - Widgets.InfoCardButtonSize) / 2f,
                Widgets.InfoCardButtonSize, Widgets.InfoCardButtonSize);
            Widgets.InfoCardButton(info.x, info.y, piece);
            row.xMax = info.x - UiStyle.Gap;
            if (opensSlot && UiStyle.ClickableRow(row))
            {
                OpenSlot(OutfitSlot.Of(piece));
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
