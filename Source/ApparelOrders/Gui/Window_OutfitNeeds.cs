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
    /// Hosts <see cref="OutfitNeedsPanel"/>, opened just above its button. Not modal, so the preview stays usable while it
    /// follows it. It closes with the policy window, and steps aside while one of that window's pickers or boxes is open.
    /// </summary>
    public class Window_OutfitNeeds : Window
    {
        private const float WindowGap = 4f;

        /// <summary>The button that opened it, on the screen, so the window opens just above it.</summary>
        private readonly Rect opener;

        public Window_OutfitNeeds(Rect opener)
        {
            this.opener = opener;
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            preventCameraMotion = false;
            // Above the policy window, which takes the clicks of every window under it.
            layer = WindowLayer.SubSuper;
        }

        /// <summary>The needs list's width, beside the preview it follows.</summary>
        private const float NeedsWidth = 480f;

        /// <summary>Wide enough for the bills plan's bill, material and bench columns beside the item's name.</summary>
        private const float PlanWidth = 940f;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(NeedsWidth, UI.screenWidth), 560f);

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            float height = Mathf.Min(windowRect.height, Mathf.Max(opener.y - WindowGap, MinHeight));
            float x = Mathf.Clamp(opener.x, 0f, UI.screenWidth - windowRect.width);
            float y = Mathf.Clamp(opener.y - WindowGap - height, 0f, UI.screenHeight - height);
            windowRect = new Rect(x, y, windowRect.width, height);
        }

        /// <summary>When the button is near the top of the screen, the window still has room for a few rows.</summary>
        private const float MinHeight = 240f;

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // Wide only while it shows the bills plan, kept on the screen.
            float width = Mathf.Min(OutfitNeedsPanel.ShowsPlan(PreviewSession.Current) ? PlanWidth : NeedsWidth, UI.screenWidth);
            if (windowRect.width != width)
            {
                windowRect.width = width;
                windowRect.x = Mathf.Clamp(windowRect.x, 0f, UI.screenWidth - width);
            }
            // The policy window's pickers and boxes open under this one's layer, so it steps aside for them.
            Window policies = Find.WindowStack.WindowOfType<Dialog_ManageApparelPolicies>();
            if (policies == null || Find.WindowStack.Windows.Any(window => window != policies && window != this
                    && window.layer == WindowLayer.Dialog && window.absorbInputAroundWindow))
            {
                Close(doCloseSound: false);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            OutfitNeedsPanel.Draw(inRect, PreviewSession.Current);
        }
    }
}
