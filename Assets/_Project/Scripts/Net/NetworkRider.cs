using System;
using Arreo.Horse;
using Arreo.Player;
using FishNet.Object;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// One player's horse and rider on the network. The owner reads input and moves the horse
    /// (D74); every other machine gets an interpolated copy from the NetworkTransform, which
    /// also makes its Rigidbody kinematic. The same <see cref="HorseIntent"/> drives the horse;
    /// only who applies it changed.
    /// </summary>
    public sealed class NetworkRider : NetworkBehaviour
    {
        private const string LocalRiderTag = "Player";
        private const string OtherRiderTag = "Untagged";

        [SerializeField] private HorseController _horse;
        [SerializeField] private RiderInput _input;
        [SerializeField] private LassoInput _lasso;
        [SerializeField] private RiderMount _mount;
        [Tooltip("Point the local camera follows.")]
        [SerializeField] private Transform _cameraTarget;

        /// <summary>This machine's own rider, once spawned.</summary>
        public static NetworkRider Local { get; private set; }

        /// <summary>Raised when this machine's own rider spawns.</summary>
        public static event Action<NetworkRider> LocalSpawned;

        public PlayerRole Role { get; private set; }
        public HorseController Horse => _horse;
        public RiderInput Input => _input;
        public LassoInput Lasso => _lasso;
        public RiderMount Mount => _mount;
        public Transform CameraTarget => _cameraTarget;
        public Rigidbody Body => _horse.GetComponent<Rigidbody>();

        public override void OnStartClient()
        {
            base.OnStartClient();
            Role = PlayerRole.For(IsOwner, IsServerInitialized);

            _input.enabled = Role.ReadsInput;
            _horse.enabled = Role.SimulatesMovement;
            // Rope and the fall only exist on the host this week, so only the host's own rider lassoes.
            _lasso.enabled = Role.ReadsInput && Role.RunsHostSystems;
            _mount.enabled = Role.RunsHostSystems;
            // The camera's line-of-sight check sees through the local rider only.
            gameObject.tag = Role.CameraIgnores ? LocalRiderTag : OtherRiderTag;

            if (IsOwner)
            {
                Local = this;
                LocalSpawned?.Invoke(this);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Local == this)
                Local = null;
        }
    }
}
