namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>Where the pieces a preview picks from come from.</summary>
    public enum PieceSource
    {
        /// <summary>Every apparel the policy allows, imagined new.</summary>
        AnythingAllowed,

        /// <summary>Apparel the colony could make now: researched, with a bench built and the material in stock.</summary>
        CraftableNow,

        /// <summary>The real items in storage, played out the way the game's apparel check will.</summary>
        InStorage,
    }
}
