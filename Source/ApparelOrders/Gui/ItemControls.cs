using System.Collections.Generic;
using OptimalLoadouts.ApparelOrders.Preview;
using OptimalLoadouts.Gui;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace OptimalLoadouts.ApparelOrders.Gui
{
    /// <summary>The lock and the allow checkbox at the end of an item's row, the same in the slot browser and the outfit list.</summary>
    public static class ItemControls
    {
        /// <summary>
        /// The lock that keeps the item worn and allowed in the policy. Faint when unlocked. A base's lock is greyed and
        /// does nothing here, since it's unlocked in the base. Locking a piece the game never puts on this pawn says why,
        /// since the lock can't change that.
        /// </summary>
        public static void DrawLock(Rect rect, ThingDef item, Pawn pawn, ApparelPolicy policy, string cantWearReason)
        {
            ApparelOrdersComponent orders = ApparelOrdersComponent.Current;
            if (orders == null)
            {
                return;
            }
            bool locked = orders.IsLocked(policy, item);
            if (locked && !orders.LocksOwn(policy, item))
            {
                TooltipHandler.TipRegion(rect, "OL_Slot_LockedInBase".Translate(LockingBase(orders, policy, item)?.label ?? ""));
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    GUI.DrawTexture(rect, UiStyle.LockIcon);
                }
                return;
            }
            TooltipHandler.TipRegion(rect, locked ? "OL_Slot_Unlock".Translate() : "OL_Slot_Lock".Translate());
            Color color = locked ? Color.white : new Color(1f, 1f, 1f, UiStyle.UnlockedAlpha);
            if (Widgets.ButtonImage(rect, UiStyle.LockIcon, color))
            {
                if (locked)
                {
                    orders.Unlock(policy, item);
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                }
                else
                {
                    orders.Lock(policy, item);
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    if (cantWearReason != null)
                    {
                        Messages.Message("OL_Slot_LockWontWear".Translate(cantWearReason, pawn.LabelShort), MessageTypeDefOf.RejectInput,
                            historical: false);
                    }
                }
            }
        }

        /// <summary>
        /// The checkbox that allows or forbids the item in the policy, like its filter. A locked item's is greyed, since
        /// it stays allowed until it's unlocked.
        /// </summary>
        public static void DrawAllowed(Rect rect, ThingDef item, ApparelPolicy policy, PreviewSession session)
        {
            bool locked = ApparelOrdersComponent.Current?.IsLocked(policy, item) ?? false;
            bool wasAllowed = policy.filter.Allows(item);
            bool allowed = wasAllowed;
            Widgets.Checkbox(rect.x, rect.y, ref allowed, rect.width, disabled: locked);
            TooltipHandler.TipRegion(rect, locked ? "OL_Slot_LockedCheckbox".Translate() : "OL_Slot_AllowedCheckbox".Translate());
            if (allowed != wasAllowed)
            {
                policy.filter.SetAllow(item, allowed);
                session.FilterChanged();
            }
        }

        /// <summary>
        /// The × that removes a worn piece: it forbids the item in the policy, and the preview fills the slot again. A
        /// locked item's is greyed, since it stays allowed until it's unlocked.
        /// </summary>
        public static void DrawRemove(Rect rect, ThingDef item, ApparelPolicy policy, PreviewSession session)
        {
            if (ApparelOrdersComponent.Current?.IsLocked(policy, item) ?? false)
            {
                TooltipHandler.TipRegion(rect, "OL_Preview_RemoveLocked".Translate());
                using (new TextBlock(ColoredText.SubtleGrayColor))
                {
                    GUI.DrawTexture(rect, TexButton.Delete);
                }
                return;
            }
            if (Widgets.ButtonImage(rect, TexButton.Delete, true, "OL_Preview_Remove".Translate()))
            {
                SoundDefOf.Designate_Cancel.PlayOneShotOnCamera();
                policy.filter.SetAllow(item, false);
                session.FilterChanged();
            }
        }

        /// <summary>The nearest base whose own lines lock the item, or null.</summary>
        private static ApparelPolicy LockingBase(ApparelOrdersComponent orders, ApparelPolicy policy, ThingDef item)
        {
            var visited = new HashSet<ApparelPolicy> { policy };
            for (ApparelPolicy basePolicy = orders.BaseOf(policy); basePolicy != null && visited.Add(basePolicy); basePolicy = orders.BaseOf(basePolicy))
            {
                if (orders.LocksOwn(basePolicy, item))
                {
                    return basePolicy;
                }
            }
            return null;
        }
    }
}
