namespace Arreo.Net
{
    /// <summary>
    /// What one copy of a player's horse does on this machine (D74): the owner reads input and
    /// moves it; everyone else sees an interpolated copy. Herd and rope only run on the host,
    /// and this week only for the host's own horse.
    /// </summary>
    public readonly struct PlayerRole
    {
        private PlayerRole(bool isOwner, bool isServer)
        {
            ReadsInput = isOwner;
            SimulatesMovement = isOwner;
            IsInterpolatedCopy = !isOwner;
            FollowedByCamera = isOwner;
            CameraIgnores = isOwner;
            RunsHostSystems = isOwner && isServer;
        }

        public static PlayerRole For(bool isOwner, bool isServer) => new PlayerRole(isOwner, isServer);

        /// <summary>Reads this machine's keyboard, mouse or gamepad.</summary>
        public bool ReadsInput { get; }

        /// <summary>Applies the HorseIntent and drives the Rigidbody (the owner has authority over its movement).</summary>
        public bool SimulatesMovement { get; }

        /// <summary>Only shows positions received from the owner, smoothed.</summary>
        public bool IsInterpolatedCopy { get; }

        public bool FollowedByCamera { get; }

        /// <summary>Tagged so the local camera's collision checks skip it (only the local rider).</summary>
        public bool CameraIgnores { get; }

        /// <summary>Herd, rope and the rest of the host-only world are wired to this horse.</summary>
        public bool RunsHostSystems { get; }
    }
}
