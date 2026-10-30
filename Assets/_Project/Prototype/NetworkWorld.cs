using Arreo.Herd;
using Arreo.Net;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 6-7 prototype): the shared bits of Proto_Sabana. Any player can ask for a
    /// barajustada (B), to work the corral gate (Interact, when near it) or, to repeat the drive
    /// and sale, to restock the herd (R, debug); the host applies it and every machine sees the
    /// same gate, the thunder and the corral count and clock.
    /// </summary>
    public sealed class NetworkWorld : NetworkBehaviour
    {
        [SerializeField] private ActiveHerd _herd;
        [SerializeField] private NetworkHerd _networkHerd;
        [SerializeField] private Corral _corral;
        [SerializeField] private ProtoGate _gate;
        [SerializeField] private ProtoThunder _thunder;
        [SerializeField] private InputActionReference _interact;
        [Tooltip("How often (s) the host updates the drive clock and stampede strength for everyone.")]
        [SerializeField, Min(0.05f)] private float _syncInterval = 0.25f;
        [Tooltip("A rider faster than this (m/s) starts the drive clock.")]
        [SerializeField, Min(0f)] private float _startSpeed = 0.5f;

        private readonly SyncVar<bool> _gateOpen = new SyncVar<bool>(true);
        private readonly SyncVar<int> _inCorral = new SyncVar<int>();
        private readonly SyncVar<int> _total = new SyncVar<int>();
        private readonly SyncVar<float> _elapsed = new SyncVar<float>();
        private readonly SyncVar<bool> _started = new SyncVar<bool>();
        private readonly SyncVar<bool> _complete = new SyncVar<bool>();
        private readonly SyncVar<float> _stampede = new SyncVar<float>();

        private HerdDrive _drive;
        private float _nextSync;

        public static NetworkWorld Instance { get; private set; }

        public bool HasDrive => _total.Value > 0;
        public int InCorral => _inCorral.Value;
        public int Total => _total.Value;
        public float Elapsed => _elapsed.Value;
        public bool Started => _started.Value;
        public bool Complete => _complete.Value;
        public float StampedeIntensity => _stampede.Value;
        public bool GateOpen => _gateOpen.Value;
        public bool LocalRiderNearGate => NetworkRider.Local != null && _gate.InReach(NetworkRider.Local.transform.position);

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

        public override void OnStartClient()
        {
            base.OnStartClient();
            _interact.action.Enable();
        }

        private void Update()
        {
            _gate.SetOpen(_gateOpen.Value);

            if (IsClientInitialized && NetworkRider.Local != null && NetworkRider.Local.Mount.HasControl)
            {
                if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                    RequestStampede();
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    RequestRestock();
                if (LocalRiderNearGate && _interact.action.WasPressedThisFrame())
                    RequestGate();
            }

            if (IsServerInitialized)
                ServerTick();
        }

        private void ServerTick()
        {
            // Selling or restocking changes the herd: the drive into the corral starts over with what's left.
            if (_drive != null && _drive.Total != _herd.PresentCount)
                _drive = null;
            if (_herd.PresentCount == 0)
            {
                if (_total.Value != 0)
                    _total.Value = 0;
                return;
            }
            _drive ??= new HerdDrive(_herd.PresentCount);
            if (!_drive.HasStarted && AnyRiderMoving())
                _drive.Begin();
            _drive.Tick(Time.deltaTime, _corral.CountInside(_herd.Positions, _herd.Present));

            if (_total.Value != _drive.Total)
                _total.Value = _drive.Total;
            if (_inCorral.Value != _drive.InCorral)
                _inCorral.Value = _drive.InCorral;
            if (_started.Value != _drive.HasStarted)
                _started.Value = _drive.HasStarted;
            if (_complete.Value != _drive.IsComplete)
            {
                _complete.Value = _drive.IsComplete;
                _elapsed.Value = _drive.ElapsedSeconds;
            }
            if (Time.time >= _nextSync)
            {
                _nextSync = Time.time + _syncInterval;
                _elapsed.Value = _drive.ElapsedSeconds;
                _stampede.Value = _herd.StampedeIntensity;
            }
        }

        private bool AnyRiderMoving()
        {
            foreach (NetworkRider rider in NetworkRider.All)
                if (rider.PlanarVelocity.magnitude > _startSpeed)
                    return true;
            return false;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestStampede()
        {
            if (_herd.Count == 0)
                return;
            _herd.TriggerStampede(_thunder.PickStrikePoint());
            _stampede.Value = 1f;
            ObserversThunder();
        }

        [ObserversRpc]
        private void ObserversThunder() => _thunder.PlayStrike();

        [ServerRpc(RequireOwnership = false)]
        private void RequestRestock()
        {
            if (_herd.Count == 0)
                return;
            _networkHerd.ServerRestock();
            _drive = null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestGate(NetworkConnection sender = null)
        {
            // Only a rider who is actually at the gate can work it.
            foreach (NetworkRider rider in NetworkRider.All)
                if (rider.Owner == sender && _gate.InReach(rider.transform.position))
                {
                    _gateOpen.Value = !_gateOpen.Value;
                    return;
                }
        }
    }
}
