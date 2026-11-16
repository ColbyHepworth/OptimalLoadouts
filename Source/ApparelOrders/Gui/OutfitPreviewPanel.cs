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
    /// every piece it allows, what the colony has researched or can make now, or what's in storage, and a list by layer saying why each
    /// piece won and what your changes did. What it shows is the game's <see cref="PreviewSession"/>.
    /// </summary>
    public static class OutfitPreviewPanel
    {
        private const float PortraitSize = 180f;

        private const float ClearButtonWidth = 60f;

        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        /// <summary>
        /// Draws the preview into <paramref name="rect"/>. With a <paramref name="slotRect"/>, an open slot's pieces get
        /// that panel of their own; without one (Rect.zero, on narrow screens), they take the outfit list's place. With a
        /// <paramref name="statsRect"/>, a piece's stats get a panel there; without one, they go under the slot's list.
        /// </summary>
        public static void Draw(Rect rect, Rect slotRect, Rect statsRect, ApparelPolicy policy)
        {
            PreviewSession session = PreviewSession.Current;
            if (session == null)
            {
                return;
            }
            Rect section = rect;
            // Level with the policy column's box, which sits under its Allowed items and Priorities tabs.
            section.yMin += TabDrawer.TabHeight;
            Rect slotSection = slotRect;
            if (slotSection.width > 0f)
            {
                slotSection.yMin = section.yMin;
            }
            Rect statsSection = statsRect;
            if (statsSection.width > 0f)
            {
                statsSection.yMin = section.yMin;
            }
            Widgets.DrawMenuSection(section);
            try
            {
                DrawContents(section, slotSection, statsSection, policy, session);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Optimal Loadouts] Couldn't draw the outfit preview: " + e, "OptimalLoadouts.PreviewPanel".GetHashCode());
            }
        }

        private static void DrawContents(Rect section, Rect slotSection, Rect statsSection, ApparelPolicy policy, PreviewSession session)
        {
            List<Pawn> pawns = Colonists(policy);
            if (pawns.Count == 0 || policy == null)
            {
                UiStyle.Note(section.ContractedBy(UiStyle.Gap), "OL_Preview_NoPawn".Translate());
                return;
            }
            session.Show(policy, pawns);

            Rect inner = section.ContractedBy(UiStyle.Gap);
            float y = DrawControls(inner, session, pawns);
            Rect list = new Rect(inner.x, y, inner.width, inner.yMax - y);
            bool slotHasOwnPanel = slotSection.width > 0f;
            bool statsHaveOwnPanel = statsSection.width > 0f;
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
                    SlotBrowserPanel.Draw(slotArea, session, showBack: false, statsBelow: !statsHaveOwnPanel);
                }
            }
            else if (session.Slot != null && session.Slot.Pieces != null)
            {
                SlotBrowserPanel.Draw(list, session, showBack: true, statsBelow: !statsHaveOwnPanel);
            }
            else
            {
                DrawOutfitList(list, session);
            }
            // After the slot panel, which opens a slot when it has none.
            if (statsHaveOwnPanel)
            {
                Widgets.DrawMenuSection(statsSection);
                Rect statsArea = statsSection.ContractedBy(UiStyle.Gap);
                if (session.Slot == null)
                {
                    using (new TextBlock(TextAnchor.MiddleCenter))
                    {
                        UiStyle.Note(statsArea, "OL_Stats_NoPiece".Translate());
                    }
                }
                else
                {
                    PieceStatsPanel.Draw(statsArea, session);
                }
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
            if (session.Pawn.outfits.CurrentApparelPolicy != session.Policy)
            {
                y += DrawWrappedNote(new Rect(area.x, y, area.width, 0f), "OL_Preview_AsIfOnPolicy".Translate());
            }
            DrawFilters(new Rect(area.x, y, area.width, UiStyle.RowHeight), session);
            y += UiStyle.RowHeight;
            Rect button = new Rect(area.x, y, area.width, UiStyle.RowHeight);
            if (Widgets.ButtonText(button, "OL_Preview_Material".Translate(session.Material.Label).ToString().Truncate(button.width)))
            {
                var general = new List<(string, Action)>
                {
                    (MaterialChoice.BestForOrder.Label, () => session.Material = MaterialChoice.BestForOrder),
                    (MaterialChoice.Typical.Label, () => session.Material = MaterialChoice.Typical),
                };
                Find.WindowStack.Add(new Dialog_PickMaterial("OL_MaterialPicker_Title".Translate(), general,
                    stuff => session.Material = MaterialChoice.Specific(stuff)));
            }
            TooltipHandler.TipRegion(button, "OL_Preview_MaterialTip".Translate());
            y += UiStyle.RowHeight;
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

        /// <summary>
        /// Three toggles in one row, each with how many pieces it leaves: Researched, Craftable and In storage. With none
        /// ticked, pieces are everything allowed, made new. In storage greys out the other two, which keep their ticks.
        /// </summary>
        private static void DrawFilters(Rect row, PreviewSession session)
        {
            float third = (row.width - 2f * UiStyle.Gap) / 3f;
            Rect researchedRect = new Rect(row.x, row.y, third, row.height);
            Rect craftableRect = new Rect(researchedRect.xMax + UiStyle.Gap, row.y, third, row.height);
            Rect storageRect = new Rect(craftableRect.xMax + UiStyle.Gap, row.y, third, row.height);
            bool inStorage = session.InStorage;

            bool researched = session.ResearchedOnly;
            Widgets.CheckboxLabeled(researchedRect, Fit("OL_Preview_ResearchedOnly".Translate(), researchedRect), ref researched,
                disabled: inStorage, placeCheckboxNearText: true);
            session.ResearchedOnly = researched;
            TooltipHandler.TipRegion(researchedRect, FilterTip("OL_Preview_FromResearched", session.PieceCount(PieceSource.Researched), inStorage));

            bool craftable = session.CanMakeNow;
            Widgets.CheckboxLabeled(craftableRect, Fit("OL_Preview_CanMakeNow".Translate(), craftableRect), ref craftable,
                disabled: inStorage, placeCheckboxNearText: true);
            session.CanMakeNow = craftable;
            TooltipHandler.TipRegion(craftableRect, FilterTip("OL_Preview_FromCraftable", session.PieceCount(PieceSource.CraftableNow), inStorage));

            bool storage = inStorage;
            Widgets.CheckboxLabeled(storageRect, Fit("OL_Preview_InStorage".Translate(), storageRect), ref storage, placeCheckboxNearText: true);
            session.InStorage = storage;
            TooltipHandler.TipRegion(storageRect, FilterTip("OL_Preview_FromStorage", session.PieceCount(PieceSource.InStorage), false));
        }

        /// <summary>The label cut to leave room for the checkbox, so a long translation stays on one line.</summary>
        private static string Fit(string label, Rect rect)
        {
            return label.Truncate(rect.width - Widgets.CheckboxSize - UiStyle.Gap);
        }

        /// <summary>What the toggle picks, how many pieces it leaves, and while In storage is on, that it doesn't apply.</summary>
        private static string FilterTip(string key, int pieces, bool offInStorage)
        {
            string tip = key.Translate() + "\n\n" + "OL_Preview_FilterCount".Translate(pieces);
            return offInStorage ? tip + "\n\n" + "OL_Preview_FilterOffInStorage".Translate() : tip;
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
                    case PieceSource.Researched:
                        return "OL_Preview_NothingResearched".Translate();
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
        /// bands of a piece on several layers. While a piece is tried on, it's in its layer and what it takes off follows.
        /// From storage, what comes off follows. Then what your changes took off.
        /// </summary>
        private static void DrawOutfitList(Rect rect, PreviewSession session)
        {
            PredictedOutfit outfit = session.Outfit;
            Map madeOn = MadeNewOn(session);
            if (madeOn != null && outfit.Worn.Count > 0)
            {
                rect.yMin += DrawNeeds(rect, CraftableApparel.NeedsFor(outfit.Worn, madeOn));
            }
            DrawOutfitBand(new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight), session);
            rect.yMin += UiStyle.RowHeight;
            List<OutfitLayer> layers = OutfitLayers.For(session.Pawn, session.Drawn);
            OutfitChanges marks = session.Changes;
            IReadOnlyList<Apparel> triedOff = session.TriedOnTakesOff;
            // A worn piece taken off is listed under "Takes off" already, and a piece tried back on sits in its layer.
            List<Apparel> gone = marks == null
                ? new List<Apparel>()
                : marks.Removed.Except(outfit.TakenOff).Where(piece => piece != session.Slot?.TriedOn).ToList();
            List<Bracket> brackets = Brackets(layers);
            float gutter = brackets.Count == 0 ? 0f : (brackets.Max(bracket => bracket.Lane) + 1) * UiStyle.BracketWidth;
            float height = layers.Sum(layer => UiStyle.BandHeight + layer.Pieces.Count * UiStyle.RowHeight);
            if (triedOff.Count > 0)
            {
                height += (triedOff.Count + 1) * UiStyle.RowHeight;
            }
            if (outfit.TakenOff.Count > 0)
            {
                height += (outfit.TakenOff.Count + 1) * UiStyle.RowHeight;
            }
            if (marks != null)
            {
                height += (gone.Count + 1) * UiStyle.RowHeight;
            }
            Rect view = new Rect(0f, 0f, rect.width - GenUI.ScrollBarWidth, height);
            Widgets.BeginScrollView(rect, ref session.OutfitScroll, view);
            try
            {
                DrawBrackets(brackets, gutter);
                Rect rows = new Rect(gutter, 0f, view.width - gutter, view.height);
                DrawOutfitRows(rows, layers, session, marks, triedOff, gone);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// How many of the outfit's pieces the colony can make now. When something's missing, it's a button that opens
        /// the window listing what. Returns the height used.
        /// </summary>
        private static float DrawNeeds(Rect area, OutfitNeeds needs)
        {
            Rect line = new Rect(area.x, area.y, area.width, UiStyle.RowHeight);
            string text = CanMakeText(needs);
            if (needs.Needs.Count == 0)
            {
                UiStyle.Note(line, text);
                return UiStyle.RowHeight;
            }
            Rect button = new Rect(line.x, line.y + 2f, line.width, line.height - 4f);
            using (new TextBlock(GameFont.Tiny))
            {
                if (Widgets.ButtonText(button, text))
                {
                    Window_OutfitNeeds open = Find.WindowStack.WindowOfType<Window_OutfitNeeds>();
                    if (open == null)
                    {
                        Find.WindowStack.Add(new Window_OutfitNeeds(UI.GUIToScreenRect(button)));
                    }
                    else
                    {
                        Find.WindowStack.Notify_ClickedInsideWindow(open);
                    }
                }
            }
            TooltipHandler.TipRegion(button, "OL_Preview_NeedsShow".Translate());
            return UiStyle.RowHeight;
        }

        /// <summary>"Can make 5 of 8 pieces", or all of them, or all of them one at a time but short of materials for all.</summary>
        internal static string CanMakeText(OutfitNeeds needs)
        {
            if (needs.Ready < needs.Total)
            {
                return "OL_Preview_CanMakeSome".Translate(needs.Ready, needs.Total);
            }
            // Each piece can be made, but their materials together may be more than the stock.
            return needs.Needs.Count == 0 ? "OL_Preview_CanMakeAll".Translate(needs.Total) : "OL_Preview_CanMakeEach".Translate(needs.Total);
        }

        /// <summary>
        /// The list's title, on a band like the layers' that sets it apart from the drawing above. While the outfit has
        /// pieces, a button makes the policy allow only them. Reset takes the policy back to what its priorities pick.
        /// </summary>
        private static void DrawOutfitBand(Rect band, PreviewSession session)
        {
            Widgets.DrawBoxSolid(band, UiStyle.BandColor);
            Rect inner = band.ContractedBy(UiStyle.Gap / 2f, 0f);
            if (session.Drawn.Count > 0)
            {
                inner.xMax = DrawAllowOnly(inner, band, session) - UiStyle.Gap;
            }
            inner.xMax = DrawReset(inner, band, session) - UiStyle.Gap;
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(inner, "OL_Preview_OutfitHeader".Translate());
            }
        }

        /// <summary>
        /// The button that makes the policy allow only the outfit as drawn, after the game's confirmation. Returns where it
        /// starts, so the title stops short of it.
        /// </summary>
        private static float DrawAllowOnly(Rect inner, Rect band, PreviewSession session)
        {
            string label = "OL_Preview_AllowOnly".Translate();
            float width;
            using (new TextBlock(GameFont.Tiny))
            {
                width = Text.CalcSize(label).x + 2f * UiStyle.Gap;
            }
            Rect button = new Rect(inner.xMax - width, band.y + 2f, width, band.height - 4f);
            using (new TextBlock(GameFont.Tiny))
            {
                if (Widgets.ButtonText(button, label))
                {
                    ConfirmAllowOnly(session);
                }
            }
            TooltipHandler.TipRegion(button, "OL_Preview_AllowOnlyTip".Translate());
            return button.x;
        }

        /// <summary>
        /// The button that takes the policy back to what its priorities pick, after the game's confirmation. Returns where
        /// it starts, so the title stops short of it.
        /// </summary>
        private static float DrawReset(Rect inner, Rect band, PreviewSession session)
        {
            string label = "OL_Preview_Reset".Translate();
            float width;
            using (new TextBlock(GameFont.Tiny))
            {
                width = Text.CalcSize(label).x + 2f * UiStyle.Gap;
            }
            Rect button = new Rect(inner.xMax - width, band.y + 2f, width, band.height - 4f);
            using (new TextBlock(GameFont.Tiny))
            {
                if (Widgets.ButtonText(button, label))
                {
                    ConfirmReset(session);
                }
            }
            TooltipHandler.TipRegion(button, "OL_Preview_ResetTip".Translate());
            return button.x;
        }

        private static void ConfirmReset(PreviewSession session)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelPolicy policy = session.Policy;
            if (orders == null || policy == null)
            {
                return;
            }
            int locks = orders.OrderFor(policy)?.lines.Count(line => line.IsItem) ?? 0;
            int ticking = orders.TickedByReset(policy).Count;
            if (locks == 0 && ticking == 0)
            {
                Messages.Message("OL_Preview_ResetNothing".Translate(policy.label), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            string text = "OL_Preview_ResetConfirm".Translate(policy.label, locks, ticking);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
            {
                orders.ResetToPriorities(policy);
                session.StartChangesAfresh();
            }, destructive: true));
        }

        private static void ConfirmAllowOnly(PreviewSession session)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            ApparelPolicy policy = session.Policy;
            if (orders == null || policy == null)
            {
                return;
            }
            List<ThingDef> items = session.Drawn.Select(piece => piece.def).Distinct().ToList();
            int unticking = orders.UntickedByAllowOnly(policy, items).Count;
            string text = "OL_Preview_AllowOnlyConfirm".Translate(items.Count, policy.label, unticking, session.Pawn.LabelShort);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
            {
                orders.AllowOnly(policy, items);
                session.FilterChanged();
            }, destructive: true));
        }

        private static void DrawOutfitRows(Rect rows, List<OutfitLayer> layers, PreviewSession session, OutfitChanges marks,
            IReadOnlyList<Apparel> triedOff, List<Apparel> gone)
        {
            PredictedOutfit outfit = session.Outfit;
            Apparel tried = session.Slot?.TriedOn;
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
                    Rect row = new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight);
                    if (piece == tried)
                    {
                        // Opening its slot would end the try-on, so the row doesn't.
                        DrawPieceRow(row, piece, false, "OL_Preview_TriedOnTip".Translate(), opensSlot: false, session, UiStyle.AddedColor);
                    }
                    else
                    {
                        DrawWornRow(row, piece, session, marks);
                    }
                    y += UiStyle.RowHeight;
                }
            }
            if (triedOff.Count > 0)
            {
                UiStyle.Note(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), "OL_Preview_TriedOnTakesOff".Translate());
                y += UiStyle.RowHeight;
                foreach (Apparel piece in triedOff)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true,
                        "OL_Preview_ReplacedBy".Translate(tried.LabelCap).Resolve(), opensSlot: false, session, UiStyle.RemovedColor);
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
            if (marks != null)
            {
                DrawChangesLine(new Rect(rows.x, y, rows.width, UiStyle.RowHeight), session,
                    gone.Count > 0 ? "OL_Preview_ChangesTookOff" : "OL_Preview_ChangesPutOn");
                y += UiStyle.RowHeight;
                foreach (Apparel piece in gone)
                {
                    DrawPieceRow(new Rect(pieces.x, y, pieces.width, UiStyle.RowHeight), piece, true, WhyGone(piece, marks, session.Policy), opensSlot: false, session,
                        UiStyle.RemovedColor);
                    y += UiStyle.RowHeight;
                }
            }
        }

        /// <summary>What the change marks show, with the button that clears them at its end.</summary>
        private static void DrawChangesLine(Rect line, PreviewSession session, string key)
        {
            Rect clear = new Rect(line.xMax - ClearButtonWidth, line.y + 2f, ClearButtonWidth, line.height - 4f);
            using (new TextBlock(GameFont.Tiny))
            {
                if (Widgets.ButtonText(clear, "OL_Preview_ClearMarks".Translate()))
                {
                    session.ClearChanges();
                }
            }
            TooltipHandler.TipRegion(clear, "OL_Preview_ClearMarksTip".Translate());
            line.xMax = clear.x - UiStyle.Gap;
            UiStyle.Note(line, key.Translate());
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

        /// <summary>A piece of the outfit, with why it won and what your changes did.</summary>
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
            string cantMake = CraftableApparel.CantMakeText(piece, MadeNewOn(session));
            if (cantMake != null)
            {
                parts.Add(cantMake);
            }
            IReadOnlyList<OrderLine> lines = ApparelOrdersComponent.Current?.EffectiveLines(session.Policy) ?? NoLines;
            IReadOnlyList<(StatDef stat, float value)> bonuses = PieceBonuses.Of(piece, lines);
            if (bonuses.Count > 0)
            {
                parts.Add("OL_Preview_Gives".Translate() + "\n" + PieceBonusText.Lines(bonuses));
            }
            parts.Add("OL_Slot_ClickToOpen".Translate());
            Color? tint = null;
            if (TakenOffByHoveredRow(piece, session))
            {
                tint = UiStyle.RemovedColor;
            }
            else if (isNewInChange)
            {
                tint = UiStyle.AddedColor;
            }
            DrawPieceRow(row, piece, !putOn, string.Join("\n\n", parts), opensSlot: true, session, tint, withControls: true);
        }

        /// <summary>The map whose colony would make the pieces, or null when they're the real ones in storage.</summary>
        internal static Map MadeNewOn(PreviewSession session)
        {
            return session.InStorage ? null : session.Pawn?.Map;
        }

        /// <summary>Whether the slot browser row under the mouse would take this piece off too, besides the slot's own.</summary>
        private static bool TakenOffByHoveredRow(Apparel piece, PreviewSession session)
        {
            SlotChoice hovered = session.Slot?.Hovered;
            return hovered != null && hovered.AlsoReplaces.Contains(piece);
        }

        /// <summary>
        /// One piece of the outfit, on a faint <paramref name="tint"/> when given. A worn piece's row opens its slot. The ⓘ
        /// button at its far right opens its info card. With <paramref name="withControls"/>, the slot browser's lock and a
        /// remove button sit before it.
        /// </summary>
        private static void DrawPieceRow(Rect row, Apparel piece, bool dimmed, string tooltip, bool opensSlot, PreviewSession session,
            Color? tint = null, bool withControls = false)
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
            if (withControls)
            {
                float iconY = row.y + (row.height - UiStyle.IconSize) / 2f;
                Rect remove = new Rect(row.xMax - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                Rect lockRect = new Rect(remove.x - UiStyle.IconGap - UiStyle.IconSize, iconY, UiStyle.IconSize, UiStyle.IconSize);
                ItemControls.DrawRemove(remove, piece.def, session.Policy, session);
                ItemControls.DrawLock(lockRect, piece.def, session.Pawn, session.Policy, null);
                row.xMax = lockRect.x - UiStyle.Gap;
            }
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
            string why = AddedFor(swap) ?? ReasonWon(piece, swap.Verdict, lines, hasOrder);
            bool wonOnOrder = !swap.Verdict.GameDecides && !swap.Verdict.IsTie && swap.Verdict.Delta > 0f;
            if (swap.Partners.Count > 0 && wonOnOrder)
            {
                // It won as a set, so the reason names the pieces it counted on for the rest. A tie goes to the game's
                // own score, which weighs the piece alone.
                string partners = swap.Partners.Select(partner => partner.LabelNoParenthesis).ToCommaList(useAnd: true);
                why += " " + "OL_Preview_WithPartners".Translate(partners);
            }
            return why;
        }

        /// <summary>Why "only add pieces that help" let the piece through though it helps on no priority, or null.</summary>
        private static string AddedFor(PredictedSwap swap)
        {
            switch (swap.AddedFor)
            {
                case ExtraNeed.Warmth:
                    return "OL_Preview_WhyAddedWarmth".Translate();
                case ExtraNeed.Cover:
                    return "OL_Preview_WhyAddedCover".Translate();
                default:
                    return null;
            }
        }

        private static string ReasonWon(Apparel piece, OrderVerdict verdict, IReadOnlyList<OrderLine> lines, bool hasOrder)
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
                // A set can win on a locked partner, so the item is named when it isn't this piece.
                return piece.def == line.item ? "OL_Preview_WhyLocked".Translate() : "OL_Preview_WhyPartnerLocked".Translate(line.item.LabelCap);
            }
            if (line.IsCoverage)
            {
                return "OL_Preview_WhyCoverage".Translate(OrderLineText.StatOnLayerCap(line));
            }
            string key = line.lowerIsBetter ? "OL_Preview_WhyLess" : "OL_Preview_WhyMore";
            return key.Translate(OrderLineText.StatOnLayer(line), Amount(line, verdict.Delta));
        }

        /// <summary>Why a piece your changes took off is gone: what replaced it, or that it's no longer picked.</summary>
        private static string WhyGone(Apparel piece, OutfitChanges marks, ApparelPolicy policy)
        {
            Apparel replacement = marks.Added.FirstOrDefault(added => marks.Replaced(added).Contains(piece));
            string why = replacement != null
                ? "OL_Preview_ReplacedBy".Translate(replacement.LabelCap).Resolve()
                : "OL_Preview_NoLongerPicked".Translate().Resolve();
            return policy.filter.Allows(piece.def) ? why : "OL_Preview_GoneForbidden".Translate(policy.label).Resolve() + "\n\n" + why;
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

        /// <summary>
        /// What an amount on the line is of: the stat with its layers, "over the body" when it's totalled body part by body
        /// part or by the body each piece covers, so the amount reads as an average over the body, not a piece's own value.
        /// </summary>
        /// <summary>How much the outfit's total on the line changed, signed: up on a higher-first line, down on a lower-first one.</summary>
        private static string Amount(OrderLine line, float delta)
        {
            float change = line.lowerIsBetter ? -Mathf.Abs(delta) : Mathf.Abs(delta);
            // A difference, so temperatures convert as a span (10 °C reads 18 °F, not 50 °F).
            return line.stat.ValueToString(change, ToStringNumberSense.Offset);
        }
    }
}
