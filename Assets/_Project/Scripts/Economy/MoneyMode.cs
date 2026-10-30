namespace Arreo.Economy
{
    /// <summary>
    /// Who keeps what a player earns, chosen when the game is created (D75, like Stardew Valley):
    /// the whole hato by default, or each player their own.
    /// </summary>
    public enum MoneyMode
    {
        Shared,
        PerPlayer,
    }
}
