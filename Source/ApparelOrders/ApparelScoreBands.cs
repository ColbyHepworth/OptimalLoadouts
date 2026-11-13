using System;
using OptimalLoadouts.Orders;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Turns a verdict into the one number the game's apparel check ranks pieces by. Each line of the order gets its
    /// own band of scores, so a gain on an earlier line always outranks one on a later line.
    /// </summary>
    public static class ApparelScoreBands
    {
        /// <summary>
        /// At or below this, a score gain is a "never wear this". Halfway, since subtracting the replaced pieces' scores
        /// can lift such a mark a little. Under CE, a worn required piece can push a real gain lower, but required apparel
        /// is left to the game anyway.
        /// </summary>
        internal const float NeverWear = -500f;

        /// <summary>Each line's score band. The game and other mods mark "never wear this" with -1000 or lower.</summary>
        private const float BandSize = 1000f;

        private const float MaxInBand = 999f;

        /// <summary>Below the apparel check's 0.05 threshold, so the piece is skipped, but not a "never wear this".</summary>
        internal const float Worse = -1f;

        /// <summary>
        /// The apparel check's score for this verdict. A bigger gain on the same line ranks higher. A tie keeps the
        /// game's own score, capped below every band, and a choice left to the game keeps its score as it is.
        /// </summary>
        public static float ScoreGain(OrderVerdict verdict, int lineCount, float gameGain)
        {
            if (verdict.GameDecides)
            {
                return gameGain;
            }
            if (verdict.IsTie)
            {
                return Math.Min(gameGain, MaxInBand);
            }
            if (verdict.Delta < 0f)
            {
                return Worse;
            }
            // Squashed into the band so big stats (carrying capacity, CE armor) still rank by size.
            float withinBand = MaxInBand * verdict.Delta / (verdict.Delta + 1f);
            return BandSize * (lineCount - verdict.DecidingLine) + withinBand;
        }
    }
}
