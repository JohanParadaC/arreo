using System;

namespace Arreo.Economy
{
    /// <summary>What a sale at the fair pays. For now every head is worth the same (EconomySettings).</summary>
    public static class CattleSale
    {
        public static long Total(int head, long pricePerHead)
        {
            if (head < 0)
                throw new ArgumentOutOfRangeException(nameof(head), head, "Must be zero or more.");
            if (pricePerHead < 0)
                throw new ArgumentOutOfRangeException(nameof(pricePerHead), pricePerHead, "Must be zero or more.");
            return checked(head * pricePerHead);
        }
    }
}
