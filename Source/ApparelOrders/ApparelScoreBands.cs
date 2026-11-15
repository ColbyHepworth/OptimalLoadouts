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

        /// <summary>
        /// How far the game's own score can move a win that nothing under its line told apart. Under the tie-break a
        /// line below adds for a difference of 0.01, so any real difference there still outranks the game.
        /// </summary>
        private const float GameShare = 0.004f;

        /// <summary>
        /// The log of the game's score where it takes half of <see cref="GameShare"/>, about a score of 20. The game's scores
        /// run from under 1 to the hundreds under Combat Extended, so they're squashed on a log scale to keep both ends apart.
        /// </summary>
        private const float GameScoreScale = 3f;

        /// <summary>Below the apparel check's 0.05 threshold, so the piece is skipped, but not a "never wear this".</summary>
        internal const float Worse = -1f;

        /// <summary>
        /// The apparel check's score for this verdict. A bigger gain on the same line ranks higher. A tie keeps the
        /// game's own score, capped below every band, and a choice left to the game keeps its score as it is. Pieces that
        /// won an item or body part line alike, with nothing under it differing, are ranked by the game's score.
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
            float delta = verdict.Delta;
            if (verdict.RestTies)
            {
                float logGain = (float)Math.Log(1f + Math.Abs(gameGain));
                float sign = gameGain > 0f ? 1f : gameGain < 0f ? -1f : 0f;
                delta += sign * GameShare * logGain / (logGain + GameScoreScale);
            }
            // Squashed into the band so big stats (carrying capacity, CE armor) still rank by size.
            float withinBand = MaxInBand * delta / (delta + 1f);
            return BandSize * (lineCount - verdict.DecidingLine) + withinBand;
        }
    }
}
