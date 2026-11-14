using OptimalLoadouts.Gui;
using UnityEngine;

namespace OptimalLoadouts.ApparelOrders.Patches
{
    /// <summary>
    /// Where the panels of Manage apparel policies go, worked out from the window's width alone: the policy's column, the
    /// outfit preview, the slot's pieces and a piece's stats, each in a column of its own where the screen has room.
    /// </summary>
    public readonly struct PolicyWindowLayout
    {
        public const float PreviewWidth = 400f;

        /// <summary>Room added to the policy's column for each stat line's layer button.</summary>
        public const float PolicyColumnExtra = 100f;

        /// <summary>Wide enough for a slot's pieces with their names and two stat columns.</summary>
        public const float PreferredSlotWidth = 620f;

        /// <summary>The narrowest slot panel worth its own column. Narrower screens show the slot inside the preview.</summary>
        public const float MinSlotWidth = 420f;

        /// <summary>A piece's stats, one to a row as on the info card.</summary>
        public const float StatsWidth = 320f;

        /// <summary>The narrowest the preview gets before the filter gives way instead, on small screens.</summary>
        public const float MinPreviewWidth = 380f;

        private PolicyWindowLayout(Rect policyColumn, Rect preview, Rect slot, Rect stats)
        {
            PolicyColumn = policyColumn;
            Preview = preview;
            Slot = slot;
            Stats = stats;
        }

        /// <summary>The policy's column: vanilla's filter, a little wider, with the tab row still in it.</summary>
        public Rect PolicyColumn { get; }

        public Rect Preview { get; }

        /// <summary>The slot's own column, or Rect.zero when the slot shows inside the preview, on narrow screens.</summary>
        public Rect Slot { get; }

        /// <summary>The stats' own column, or Rect.zero when they show under the slot's list.</summary>
        public Rect Stats { get; }

        /// <summary>The width the window asks for: vanilla's, the priorities' extra room, the preview, the slot and the stats.</summary>
        public static float WantedWidth(float vanillaWidth)
        {
            return vanillaWidth + PolicyColumnExtra + PreviewWidth + PreferredSlotWidth + StatsWidth + 3f * UiStyle.Gap;
        }

        /// <summary>
        /// The panels for the window's contents. With room for the preview, the slot at its narrowest and the stats, each
        /// gets a column and the slot takes what's left. With room for the preview and the slot only, the stats go under
        /// the slot's list. With less, the slot goes inside the preview.
        /// </summary>
        public static PolicyWindowLayout For(Rect contents, float windowWidth, float vanillaWidth)
        {
            Rect policy = contents;
            float room = windowWidth - vanillaWidth - PolicyColumnExtra - UiStyle.Gap;
            Rect slot = Rect.zero;
            Rect stats = Rect.zero;
            Rect preview;
            if (room >= PreviewWidth + MinSlotWidth + StatsWidth + 2f * UiStyle.Gap)
            {
                stats = new Rect(contents.xMax - StatsWidth, contents.y, StatsWidth, contents.height);
                float slotWidth = room - PreviewWidth - StatsWidth - 2f * UiStyle.Gap;
                slot = new Rect(stats.x - UiStyle.Gap - slotWidth, contents.y, slotWidth, contents.height);
                preview = new Rect(slot.x - UiStyle.Gap - PreviewWidth, contents.y, PreviewWidth, contents.height);
                policy.width -= room + UiStyle.Gap;
            }
            else if (room >= PreviewWidth + UiStyle.Gap + MinSlotWidth)
            {
                float slotWidth = room - PreviewWidth - UiStyle.Gap;
                slot = new Rect(contents.xMax - slotWidth, contents.y, slotWidth, contents.height);
                preview = new Rect(slot.x - UiStyle.Gap - PreviewWidth, contents.y, PreviewWidth, contents.height);
                policy.width -= room + UiStyle.Gap;
            }
            else
            {
                float previewWidth = Mathf.Min(Mathf.Max(room, MinPreviewWidth), contents.width / 2f);
                preview = new Rect(contents.xMax - previewWidth, contents.y, previewWidth, contents.height);
                policy.width -= previewWidth + UiStyle.Gap;
            }
            return new PolicyWindowLayout(policy, preview, slot, stats);
        }
    }
}
