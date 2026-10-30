using System;

namespace Arreo.Economy
{
    /// <summary>Whose money a wallet holds: the hato (shared by everyone) or one player.</summary>
    public readonly struct WalletOwner : IEquatable<WalletOwner>
    {
        private const int HatoId = -1;

        private readonly int _playerId;
        private readonly bool _isPlayer;

        private WalletOwner(int playerId)
        {
            _playerId = playerId;
            _isPlayer = true;
        }

        public static WalletOwner Hato => default;

        /// <param name="playerId">The player's id for this game (on the network, their connection id).</param>
        public static WalletOwner Player(int playerId) => new WalletOwner(playerId);

        public bool IsHato => !_isPlayer;

        /// <summary>The player's id, or -1 for the hato.</summary>
        public int PlayerId => _isPlayer ? _playerId : HatoId;

        public bool Equals(WalletOwner other) => _isPlayer == other._isPlayer && PlayerId == other.PlayerId;
        public override bool Equals(object obj) => obj is WalletOwner other && Equals(other);
        public override int GetHashCode() => _isPlayer ? _playerId : int.MinValue;
        public override string ToString() => _isPlayer ? $"player {_playerId}" : "hato";
    }
}
