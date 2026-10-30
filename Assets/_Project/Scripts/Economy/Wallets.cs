using System.Collections.Generic;

namespace Arreo.Economy
{
    /// <summary>
    /// Every wallet in a game (D75). Shared: one hato wallet that every player earns into and
    /// spends from. Per player: each player has their own, opened with the starting money the
    /// first time it's needed, and there's no hato wallet.
    /// </summary>
    public sealed class Wallets
    {
        private readonly Dictionary<int, Wallet> _players = new Dictionary<int, Wallet>();
        private readonly long _startingMoney;

        public Wallets(MoneyMode mode, long startingMoney = 0)
        {
            Mode = mode;
            _startingMoney = startingMoney;
            if (mode == MoneyMode.Shared)
                Hato = new Wallet(WalletOwner.Hato, startingMoney);
        }

        public MoneyMode Mode { get; }

        /// <summary>The shared wallet; null when each player keeps their own money.</summary>
        public Wallet Hato { get; }

        /// <summary>The wallet a player earns into and spends from.</summary>
        public Wallet For(int playerId)
        {
            if (Mode == MoneyMode.Shared)
                return Hato;
            if (!_players.TryGetValue(playerId, out Wallet wallet))
            {
                wallet = new Wallet(WalletOwner.Player(playerId), _startingMoney);
                _players.Add(playerId, wallet);
            }
            return wallet;
        }
    }
}
