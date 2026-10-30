using System;

namespace Arreo.Economy
{
    /// <summary>
    /// Money in pesos, owned by the hato or by one player (D75). It never goes negative: spending
    /// more than it holds fails and leaves it untouched. On the network only the host's copy is real.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(WalletOwner owner, long balance = 0)
        {
            if (balance < 0)
                throw new ArgumentOutOfRangeException(nameof(balance), balance, "A wallet can't start in debt.");
            Owner = owner;
            Balance = balance;
        }

        public WalletOwner Owner { get; }
        public long Balance { get; private set; }

        /// <summary>Raised after the balance goes up or down.</summary>
        public event Action<Wallet> Changed;

        public void Earn(long amount)
        {
            RequireNotNegative(amount);
            if (amount == 0)
                return;
            Balance += amount;
            Changed?.Invoke(this);
        }

        public bool CanAfford(long amount)
        {
            RequireNotNegative(amount);
            return amount <= Balance;
        }

        public bool TrySpend(long amount)
        {
            if (!CanAfford(amount))
                return false;
            if (amount == 0)
                return true;
            Balance -= amount;
            Changed?.Invoke(this);
            return true;
        }

        private static void RequireNotNegative(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be zero or more.");
        }
    }
}
