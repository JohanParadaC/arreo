using System;
using System.Collections.Generic;
using Arreo.Horse;
using Arreo.Player;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// One player's horse and rider on the network. The owner reads input and moves the horse
    /// (D74); every other machine gets an interpolated copy from the NetworkTransform, which
    /// also makes its Rigidbody kinematic. The same <see cref="HorseIntent"/> drives the horse;
    /// only who applies it changed. What the host decides about this rider (being dragged by a
    /// rope, being thrown off) it orders to the owner, and the fall is played on every machine.
    /// </summary>
    public sealed class NetworkRider : NetworkBehaviour
    {
        private const string LocalRiderTag = "Player";
        private const string OtherRiderTag = "Untagged";
        // How often (s) the host re-sends a changing drag to a remote owner.
        private const float DragSendInterval = 0.1f;
        // Time constant (s) of the planar velocity estimate.
        private const float VelocitySmoothing = 0.1f;

        private static readonly List<NetworkRider> AllRiders = new List<NetworkRider>();

        [SerializeField] private HorseController _horse;
        [SerializeField] private RiderInput _input;
        [SerializeField] private LassoInput _lasso;
        [SerializeField] private RiderMount _mount;
        [SerializeField] private NetworkRope _rope;
        [Tooltip("Point the local camera follows.")]
        [SerializeField] private Transform _cameraTarget;

        private RiderFall _serverFall;
        private Vector3 _lastPosition;
        private Vector3 _sentDrag;
        private float _dragSentAt;

        /// <summary>This machine's own rider, once spawned.</summary>
        public static NetworkRider Local { get; private set; }

        /// <summary>Every rider spawned on this machine (all players).</summary>
        public static IReadOnlyList<NetworkRider> All => AllRiders;

        /// <summary>Raised when this machine's own rider spawns.</summary>
        public static event Action<NetworkRider> LocalSpawned;

        public PlayerRole Role { get; private set; }
        public HorseController Horse => _horse;
        public RiderInput Input => _input;
        public LassoInput Lasso => _lasso;
        public RiderMount Mount => _mount;
        public NetworkRope Rope => _rope;
        public Transform CameraTarget => _cameraTarget;
        public Rigidbody Body => _horse.GetComponent<Rigidbody>();

        /// <summary>
        /// Ground velocity measured from position. Remote horses are kinematic copies whose
        /// Rigidbody reports nothing, but the herd needs to know how fast every rider moves.
        /// </summary>
        public Vector3 PlanarVelocity { get; private set; }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            AllRiders.Add(this);
            _lastPosition = transform.position;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            AllRiders.Remove(this);
        }

        // A scene unload can destroy a rider without the network stopping it first.
        private void OnDestroy()
        {
            AllRiders.Remove(this);
            if (Local == this)
                Local = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverFall = _mount.Settings.CreateFall();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Role = PlayerRole.For(IsOwner, IsServerInitialized);

            _input.enabled = Role.ReadsInput;
            _horse.enabled = Role.SimulatesMovement;
            _lasso.enabled = Role.ReadsInput;
            // Every machine plays this rider's falls; only the owner's horse is reined in.
            _mount.enabled = true;
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

        private void FixedUpdate()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            Vector3 measured = new Vector3(delta.x, 0f, delta.z) / Time.fixedDeltaTime;
            PlanarVelocity = Vector3.Lerp(PlanarVelocity, measured, 1f - Mathf.Exp(-Time.fixedDeltaTime / VelocitySmoothing));
        }

        private void Update()
        {
            if (_serverFall != null)
                _serverFall.Tick(Time.deltaTime);
        }

        /// <summary>Host: a rope is dragging this horse. The owner applies it (D74).</summary>
        [Server]
        public void ServerSetDrag(Vector3 velocity)
        {
            if (Owner.IsLocalClient)
            {
                _horse.SetExternalVelocity(velocity);
                return;
            }

            bool stopped = velocity == Vector3.zero;
            bool changed = (velocity - _sentDrag).sqrMagnitude > 0.01f;
            bool due = Time.time - _dragSentAt >= DragSendInterval;
            if ((stopped && _sentDrag != Vector3.zero) || (changed && due))
            {
                _sentDrag = velocity;
                _dragSentAt = Time.time;
                TargetSetDrag(Owner, velocity);
            }
        }

        [TargetRpc]
        private void TargetSetDrag(NetworkConnection target, Vector3 velocity) => _horse.SetExternalVelocity(velocity);

        /// <summary>Host: this rider's rope snapped. If it was hard enough, everyone sees them thrown off.</summary>
        [Server]
        public void ServerRopeSnapped(float snapTension, Vector3 pullDirection)
        {
            if (_serverFall.TryFall(snapTension, pullDirection, out Vector3 launch))
                ObserversPlayFall(launch + PlanarVelocity);
        }

        [ObserversRpc]
        private void ObserversPlayFall(Vector3 velocity) => _mount.PlayFall(velocity);
    }
}
