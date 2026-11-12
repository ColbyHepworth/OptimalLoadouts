using System;
using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>
    /// Tick boxes for the layers a priority line or a limit counts on, opened by its layer button: "All layers", then the
    /// head layers and the body layers, each under a heading that ticks its whole group. Each tick applies at once, so the
    /// preview follows while the list is open.
    /// </summary>
    public class Dialog_PickLayers : Window
    {
        private const float Width = 260f;
        private const float MaxHeight = 560f;

        private readonly Action<List<ApparelLayerDef>> onChange;
        private readonly List<ApparelLayerDef> headLayers;
        private readonly List<ApparelLayerDef> bodyLayers;
        /// <summary>The layers shown ticked: every one when the line counts on every layer.</summary>
        private HashSet<ApparelLayerDef> ticked;
        private Vector2 scroll;

        public Dialog_PickLayers(List<ApparelLayerDef> current, Action<List<ApparelLayerDef>> onChange)
        {
            this.onChange = onChange;
            ticked = Shown(current);
            // Outermost first, as the outfit list shows them.
            List<ApparelLayerDef> worn = ApparelLayers.Worn.Reverse().ToList();
            headLayers = worn.Where(ApparelLayers.OnHead).ToList();
            bodyLayers = worn.Where(layer => !ApparelLayers.OnHead(layer)).ToList();
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doWindowBackground = true;
        }

        public override Vector2 InitialSize => new Vector2(Width, Mathf.Min(MaxHeight, Rows * UiStyle.RowHeight + 2f * Margin));

        /// <summary>The title, "All layers", and each group's heading and layers.</summary>
        private int Rows => 2 + (headLayers.Count > 0 ? headLayers.Count + 1 : 0) + (bodyLayers.Count > 0 ? bodyLayers.Count + 1 : 0);

        /// <summary>Opens by the mouse, as a menu does, kept on screen.</summary>
        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            Vector2 at = UI.MousePositionOnUIInverted;
            at.x = Mathf.Min(at.x, UI.screenWidth - size.x);
            at.y = Mathf.Min(at.y, UI.screenHeight - size.y);
            windowRect = new Rect(at.x, at.y, size.x, size.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            float rowsHeight = (Rows - 1) * UiStyle.RowHeight;
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, UiStyle.RowHeight), "OL_LayerPicker_Title".Translate());
            }
            Rect list = new Rect(inRect.x, inRect.y + UiStyle.RowHeight, inRect.width, inRect.height - UiStyle.RowHeight);
            Rect view = new Rect(0f, 0f, list.width - (rowsHeight > list.height ? UiStyle.ScrollbarWidth : 0f), rowsHeight);
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                float y = 0f;
                bool all = ApparelLayers.Worn.All(ticked.Contains);
                bool allBefore = all;
                Widgets.CheckboxLabeled(new Rect(0f, y, view.width, UiStyle.RowHeight), "OL_Layer_All".Translate(), ref all);
                if (all && !allBefore)
                {
                    Change(Enumerable.Empty<ApparelLayerDef>());
                }
                y += UiStyle.RowHeight;
                y = DrawGroup(y, view.width, "OL_Layer_Head".Translate(), headLayers);
                DrawGroup(y, view.width, "OL_Layer_Body".Translate(), bodyLayers);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>A group's heading, which ticks or clears the whole group, then each of its layers. Returns the y below them.</summary>
        private float DrawGroup(float y, float width, string heading, List<ApparelLayerDef> layers)
        {
            if (layers.Count == 0)
            {
                return y;
            }
            bool whole = layers.All(ticked.Contains);
            bool wholeBefore = whole;
            // Unticking it would leave no layer, so it stays ticked until another is.
            bool onlyGroup = whole && ticked.All(layers.Contains);
            Rect headingRow = new Rect(0f, y, width, UiStyle.RowHeight);
            Widgets.DrawLineHorizontal(headingRow.x, headingRow.yMax - 1f, headingRow.width, ColoredText.SubtleGrayColor);
            Widgets.CheckboxLabeled(headingRow, heading, ref whole, disabled: onlyGroup);
            if (onlyGroup)
            {
                TooltipHandler.TipRegion(headingRow, "OL_LayerPicker_LastTip".Translate());
            }
            if (whole != wholeBefore)
            {
                Change(whole ? ticked.Concat(layers) : ticked.Except(layers));
            }
            y += UiStyle.RowHeight;
            foreach (ApparelLayerDef layer in layers)
            {
                bool on = ticked.Contains(layer);
                bool onBefore = on;
                bool onlyLayer = on && ticked.Count == 1;
                Rect row = new Rect(UiStyle.Indent, y, width - UiStyle.Indent, UiStyle.RowHeight);
                Widgets.CheckboxLabeled(row, layer.LabelCap, ref on, disabled: onlyLayer);
                if (onlyLayer)
                {
                    TooltipHandler.TipRegion(row, "OL_LayerPicker_LastTip".Translate());
                }
                if (on != onBefore)
                {
                    Change(on ? ticked.Append(layer) : ticked.Where(each => each != layer));
                }
                y += UiStyle.RowHeight;
            }
            return y;
        }

        private void Change(IEnumerable<ApparelLayerDef> picked)
        {
            List<ApparelLayerDef> layers = LayerScope.Normalized(picked);
            ticked = Shown(layers);
            onChange(layers);
        }

        /// <summary>A stored set as ticks: every layer ticked when it's empty, since empty means every layer.</summary>
        private static HashSet<ApparelLayerDef> Shown(List<ApparelLayerDef> layers)
        {
            return new HashSet<ApparelLayerDef>(layers.Count == 0 ? (IEnumerable<ApparelLayerDef>)ApparelLayers.Worn : layers);
        }
    }
}
