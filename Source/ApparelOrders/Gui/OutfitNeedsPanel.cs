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
    /// What the previewed outfit needs before the colony can make it: research, benches, skills and materials, each with
    /// its icon and the pieces it's for. It reads the session every frame, so the list shrinks as needs are met.
    /// </summary>
    public static class OutfitNeedsPanel
    {
        /// <summary>Tall enough for vanilla's bar border, slim enough to sit in a row.</summary>
        private const float SkillBarHeight = 16f;

        /// <summary>Space above each group after the first, so the groups read apart.</summary>
        private const float GroupGap = 14f;

        /// <summary>A piece's name is shown for this many pieces, then "and 3 more".</summary>
        private const int NamedPieces = 2;

        private static readonly MissingKind[] Groups =
        {
            MissingKind.Research, MissingKind.Bench, MissingKind.Skill, MissingKind.Materials, MissingKind.NoRecipe,
        };

        private static Vector2 scroll;

        public static void Draw(Rect rect, PreviewSession session)
        {
            Map map = session == null ? null : OutfitPreviewPanel.MadeNewOn(session);
            Rect title = new Rect(rect.x, rect.y, rect.width, UiStyle.RowHeight);
            if (session?.Outfit == null || session.Policy == null || map == null)
            {
                UiStyle.Note(new Rect(rect.x, title.yMax, rect.width, UiStyle.RowHeight), "OL_Needs_NotMadeNew".Translate());
                return;
            }
            OutfitNeeds needs = CraftableApparel.NeedsFor(session.Outfit.Worn, map);
            using (new TextBlock(GameFont.Medium, TextAnchor.MiddleLeft))
            {
                Widgets.Label(title, session.Policy.label.Truncate(title.width));
            }
            float y = title.yMax;
            UiStyle.Note(new Rect(rect.x, y, rect.width, UiStyle.RowHeight), OutfitPreviewPanel.CanMakeText(needs));
            y += UiStyle.RowHeight + UiStyle.Gap / 2f;
            Rect list = new Rect(rect.x, y, rect.width, rect.yMax - y);
            if (needs.Needs.Count == 0)
            {
                UiStyle.Note(list, "OL_Needs_NothingMissing".Translate());
                return;
            }
            Rect view = new Rect(0f, 0f, list.width - UiStyle.ScrollbarWidth, Height(needs));
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                DrawGroups(view, needs);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float Height(OutfitNeeds needs)
        {
            float height = 0f;
            foreach (MissingKind kind in Groups)
            {
                int rows = needs.Needs.Count(need => GroupOf(need.Missing.Kind) == kind);
                if (rows > 0)
                {
                    height += (height > 0f ? GroupGap : 0f) + UiStyle.BandHeight + rows * 2f * UiStyle.RowHeight;
                }
            }
            return height;
        }

        private static void DrawGroups(Rect view, OutfitNeeds needs)
        {
            float y = view.y;
            foreach (MissingKind kind in Groups)
            {
                List<OutfitNeed> group = needs.Needs.Where(need => GroupOf(need.Missing.Kind) == kind).ToList();
                if (group.Count == 0)
                {
                    continue;
                }
                if (y > view.y)
                {
                    y += GroupGap;
                }
                Rect band = new Rect(view.x, y, view.width, UiStyle.BandHeight);
                Widgets.DrawBoxSolid(band, UiStyle.BandColor);
                using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
                {
                    Widgets.Label(band.ContractedBy(UiStyle.Gap / 2f, 0f), ("OL_Needs_Group_" + kind).Translate());
                }
                y += UiStyle.BandHeight;
                foreach (OutfitNeed need in group)
                {
                    Rect row = new Rect(view.x + UiStyle.Indent, y, view.width - UiStyle.Indent, UiStyle.RowHeight);
                    if (need.Missing.Kind == MissingKind.Skill && need.Missing.Skill != null)
                    {
                        DrawSkill(row, need.Missing);
                    }
                    else
                    {
                        DrawNeed(row, need.Missing);
                    }
                    y += UiStyle.RowHeight;
                    Rect forRect = new Rect(row.x + UiStyle.IconSize + UiStyle.Gap, y, row.width - UiStyle.IconSize - UiStyle.Gap, UiStyle.RowHeight);
                    List<string> names = need.For.Select(piece => piece.LabelNoParenthesis).Distinct().ToList();
                    string forText = names.Count <= NamedPieces
                        ? "OL_Needs_For".Translate(names.ToCommaList(useAnd: true)).Resolve()
                        : "OL_Needs_ForMore".Translate(names.Take(NamedPieces).ToCommaList(), names.Count - NamedPieces).Resolve();
                    using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft, ColoredText.SubtleGrayColor))
                    {
                        Widgets.Label(forRect, forText.Truncate(forRect.width));
                    }
                    TooltipHandler.TipRegion(forRect, "OL_Needs_For".Translate(names.ToCommaList(useAnd: true)).Resolve());
                    y += UiStyle.RowHeight;
                }
            }
        }

        /// <summary>A recipe the colony can't use is filed with items no recipe makes, as both can't be made.</summary>
        private static MissingKind GroupOf(MissingKind kind)
        {
            return kind == MissingKind.Unavailable ? MissingKind.NoRecipe : kind;
        }

        private static void DrawNeed(Rect row, MissingToMake need)
        {
            Rect icon = new Rect(row.x, row.y + (row.height - UiStyle.IconSize) / 2f, UiStyle.IconSize, UiStyle.IconSize);
            if (need.Thing != null)
            {
                Widgets.ThingIcon(icon, need.Thing);
            }
            else if (need.Kind == MissingKind.Research)
            {
                GUI.DrawTexture(icon, UiStyle.ResearchIcon);
            }
            Rect label = new Rect(icon.xMax + UiStyle.Gap, row.y, row.xMax - icon.xMax - UiStyle.Gap, row.height);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(label, need.Label.Truncate(label.width));
            }
            TooltipHandler.TipRegion(label, need.Label);
        }

        /// <summary>
        /// A skill the outfit needs: its name, vanilla's standard bar filled toward the level needed, and "best / needed".
        /// The bar's end is the level needed, so how close the colony is shows at a glance.
        /// </summary>
        private static void DrawSkill(Rect row, MissingToMake need)
        {
            string name = need.Skill.LabelCap.Resolve();
            string numbers = "OL_Missing_SkillNumbers".Translate(need.BestLevel, need.LevelNeeded);
            float nameWidth;
            float numbersWidth;
            using (new TextBlock(GameFont.Small))
            {
                nameWidth = Text.CalcSize(name).x + UiStyle.Gap;
                numbersWidth = Text.CalcSize(numbers).x + UiStyle.Gap;
            }
            // A skill has no icon, so its name starts in the icon column.
            Rect nameRect = new Rect(row.x, row.y, nameWidth, row.height);
            Rect numbersRect = new Rect(row.xMax - numbersWidth, row.y, numbersWidth, row.height);
            Rect bar = new Rect(nameRect.xMax, row.y + (row.height - SkillBarHeight) / 2f, numbersRect.x - nameRect.xMax, SkillBarHeight);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(nameRect, name);
            }
            if (bar.width > 0f)
            {
                Widgets.FillableBar(bar, need.LevelNeeded <= 0 ? 1f : Mathf.Clamp01(need.BestLevel / (float)need.LevelNeeded));
            }
            using (new TextBlock(TextAnchor.MiddleRight))
            {
                Widgets.Label(numbersRect, numbers);
            }
            TooltipHandler.TipRegion(row, need.BestPawn == null
                ? need.Label
                : "OL_Missing_SkillBest".Translate(need.BestPawn.LabelShort, need.Skill.LabelCap, need.BestLevel, need.LevelNeeded).Resolve());
        }
    }
}
