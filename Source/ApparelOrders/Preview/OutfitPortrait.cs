using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Draws a pawn in an imagined outfit into one texture that's reused for the whole session. It's drawn again only
    /// when the pawn, its rotation, the outfit or the size changes, and the game's portrait cache is never touched.
    /// </summary>
    public static class OutfitPortrait
    {
        /// <summary>The styling station's framing, the closest vanilla view to "how does this pawn look dressed".</summary>
        private static readonly Vector3 CameraOffset = new Vector3(0f, 0f, 0.15f);

        private const float CameraZoom = 1.1f;

        /// <summary>The portrait cache draws this much larger than shown, so edges stay smooth.</summary>
        private const float Supersample = 1.25f;

        private static RenderTexture texture;
        private static Pawn drawnPawn;
        private static Rot4 drawnRotation;
        private static readonly List<Apparel> drawnOutfit = new List<Apparel>();

        public static Texture Draw(Pawn pawn, IReadOnlyList<Apparel> outfit, Rot4 rotation, Vector2 size)
        {
            Vector2 pixels = size * Supersample * Prefs.UIScale;
            int width = Mathf.RoundToInt(pixels.x);
            int height = Mathf.RoundToInt(pixels.y);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
                texture = new RenderTexture(width, height, 24)
                {
                    name = "OptimalLoadoutsPreview",
                    useMipMap = false,
                    filterMode = FilterMode.Bilinear,
                };
                drawnPawn = null;
            }
            if (!texture.IsCreated())
            {
                texture.Create();
                drawnPawn = null;
            }
            if (drawnPawn != pawn || drawnRotation != rotation || !SameOutfit(outfit))
            {
                // Remembered first, so a drawing that fails isn't tried again every frame.
                drawnPawn = pawn;
                drawnRotation = rotation;
                drawnOutfit.Clear();
                drawnOutfit.AddRange(outfit);
                Render(pawn, outfit, rotation);
            }
            return texture;
        }

        /// <summary>Forgets the last pawn drawn, so a loaded game doesn't keep the previous one's pawn alive.</summary>
        public static void Forget()
        {
            drawnPawn = null;
            drawnOutfit.Clear();
        }

        private static void Render(Pawn pawn, IReadOnlyList<Apparel> outfit, Rot4 rotation)
        {
            PawnRenderer renderer = pawn.Drawer.renderer;
            PawnRenderTree tree = renderer.renderTree;
            try
            {
                using (new ImaginedOutfit(pawn, outfit))
                {
                    tree.SetDirty();
                    renderer.WoundOverlays.ClearCache();
                    Find.PawnCacheRenderer.RenderPawn(pawn, texture, CameraOffset, CameraZoom, 0f, rotation,
                        renderHead: pawn.health.hediffSet.HasHead, renderHeadgear: true, renderClothes: true, portrait: true,
                        stylingStation: true);
                }
            }
            finally
            {
                // Both were built from the imagined outfit, so they're rebuilt from the real one the next time the pawn is drawn.
                tree.SetDirty();
                renderer.WoundOverlays.ClearCache();
            }
        }

        private static bool SameOutfit(IReadOnlyList<Apparel> outfit)
        {
            if (outfit.Count != drawnOutfit.Count)
            {
                return false;
            }
            for (int i = 0; i < outfit.Count; i++)
            {
                if (outfit[i] != drawnOutfit[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
