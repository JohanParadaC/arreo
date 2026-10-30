using Arreo.Economy;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// The game's money on the network. Only the host holds the real <see cref="Wallets"/> (D74:
    /// economy is the host's): a client asks for a sale or a purchase, the host applies it here
    /// and everyone sees the new balance. D75: money is the hato's, shared by default; the split
    /// per-player mode exists in <see cref="Wallets"/> but isn't networked yet.
    /// </summary>
    public sealed class NetworkEconomy : NetworkBehaviour
    {
        [SerializeField] private EconomySettings _settings;

        private readonly SyncVar<long> _hatoBalance = new SyncVar<long>();
        private Wallets _wallets;

        /// <summary>The economy on this machine (at most one per scene).</summary>
        public static NetworkEconomy Instance { get; private set; }

        public EconomySettings Settings => _settings;

        /// <summary>The hato's money, the same on every machine.</summary>
        public long HatoBalance => _hatoBalance.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _wallets = new Wallets(MoneyMode.Shared, _settings.StartingMoney);
            _wallets.Hato.Changed += wallet => _hatoBalance.Value = wallet.Balance;
            _hatoBalance.Value = _wallets.Hato.Balance;
        }

        /// <summary>Host: money a player earned, into their wallet (the hato's, while money is shared).</summary>
        [Server]
        public void ServerEarn(NetworkConnection player, long amount) => _wallets.For(player.ClientId).Earn(amount);

        /// <summary>Host: takes money from a player's wallet if there's enough; never leaves it negative.</summary>
        [Server]
        public bool ServerTrySpend(NetworkConnection player, long amount) => _wallets.For(player.ClientId).TrySpend(amount);
    }
}
