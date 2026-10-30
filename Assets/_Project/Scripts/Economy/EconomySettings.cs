using UnityEngine;

namespace Arreo.Economy
{
    /// <summary>Prices and money for a game. Placeholder numbers until the economy is designed.</summary>
    [CreateAssetMenu(menuName = "Arreo/Economy Settings", fileName = "EconomySettings")]
    public sealed class EconomySettings : ScriptableObject
    {
        [Tooltip("What the fair pays for each head of cattle (pesos).")]
        [SerializeField, Min(0)] private long _pricePerHead = 1_000_000;
        [Tooltip("Money the hato (or each player, if money is split) starts a new game with (pesos).")]
        [SerializeField, Min(0)] private long _startingMoney;

        public long PricePerHead => _pricePerHead;
        public long StartingMoney => _startingMoney;

        public long SaleTotal(int head) => CattleSale.Total(head, _pricePerHead);
    }
}
