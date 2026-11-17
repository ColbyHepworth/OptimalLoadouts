namespace OptimalLoadouts.WeaponOrders
{
    /// <summary>What a role carries: any weapon, only ranged or only melee ones, or none.</summary>
    public enum WeaponKind
    {
        Any,
        Ranged,
        Melee,
        None,
    }

    /// <summary>Which weapons a number is about, and its group in the picker.</summary>
    public enum WeaponNumberGroup
    {
        Ranged,
        Melee,
        General,
    }
}
