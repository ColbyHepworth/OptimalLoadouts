namespace OptimalLoadouts.Orders
{
    /// <summary>
    /// The outcome of comparing a choice with what it replaces, down a stat order: the first line where they differ
    /// and by how much, or that the choice isn't the order's to rank.
    /// </summary>
    public readonly struct OrderVerdict
    {
        public static readonly OrderVerdict Tie = new OrderVerdict(-1, 0f, false);

        /// <summary>The game's own rules decide this choice, so its score stands untouched.</summary>
        public static readonly OrderVerdict LeftToGame = new OrderVerdict(-1, 0f, true);

        /// <summary>Index of the first line that differs, or -1 when every line ties.</summary>
        public readonly int DecidingLine;

        /// <summary>How much better (positive) or worse (negative) on that line, with its direction already applied.</summary>
        public readonly float Delta;

        public readonly bool GameDecides;

        public OrderVerdict(int decidingLine, float delta) : this(decidingLine, delta, false)
        {
        }

        private OrderVerdict(int decidingLine, float delta, bool gameDecides)
        {
            DecidingLine = decidingLine;
            Delta = delta;
            GameDecides = gameDecides;
        }

        public bool IsTie => DecidingLine < 0 && !GameDecides;
    }
}
