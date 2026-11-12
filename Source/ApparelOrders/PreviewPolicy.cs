using System;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Makes the stat order judge one pawn by another policy until disposed. Nothing is written to the pawn: setting
    /// its real policy would make it check its apparel at once.
    /// </summary>
    public readonly struct PreviewPolicy : IDisposable
    {
        private static Pawn previewedPawn;
        private static ApparelPolicy previewedPolicy;

        private readonly Pawn previousPawn;
        private readonly ApparelPolicy previousPolicy;

        public PreviewPolicy(Pawn pawn, ApparelPolicy policy)
        {
            previousPawn = previewedPawn;
            previousPolicy = previewedPolicy;
            previewedPawn = pawn;
            previewedPolicy = policy;
        }

        /// <summary>The policy the pawn is judged by: the previewed one inside a preview, otherwise its own.</summary>
        public static ApparelPolicy PolicyOf(Pawn pawn)
        {
            return pawn == previewedPawn ? previewedPolicy : pawn.outfits.CurrentApparelPolicy;
        }

        public void Dispose()
        {
            previewedPawn = previousPawn;
            previewedPolicy = previousPolicy;
        }
    }
}
