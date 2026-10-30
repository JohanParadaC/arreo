using Arreo.Rope;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// A player's lasso on the network. The owner asks to throw (where, and which cow they aimed
    /// at) or let go; the host validates and simulates the verlet rope. Clients only receive the
    /// state, tension, loop end and what's caught, and draw the curve themselves, tied to their
    /// own copy of the caught cow.
    /// </summary>
    public sealed class NetworkRope : NetworkBehaviour, IRopeSnapshotSource, ILassoActions
    {
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private NetworkRider _rider;
        [Tooltip("Rope updates per second while it's in use.")]
        [SerializeField, Min(1f)] private float _sendRate = 20f;
        [Tooltip("Rope updates per second while it's coiled and idle.")]
        [SerializeField, Min(0.2f)] private float _idleSendRate = 2f;
        [Tooltip("Seconds a client takes to glide the loop end to each new position it receives.")]
        [SerializeField, Min(0f)] private float _endSmoothing = 0.05f;

        private bool _hasState;
        private RopeState _state;
        private float _tension01;
        private float _breakProgress;
        private int _caughtId = -1;
        private Vector3 _end;
        private Vector3 _endTarget;
        private float _nextSend;
        private RopeState _sentState;

        public bool IsConfigured => IsServerInitialized ? _rope.IsConfigured : _hasState && _rope.IsConfigured;

        public RopeSnapshot Snapshot
        {
            get
            {
                if (IsServerInitialized)
                    return _rope.Snapshot;

                Vector3 end = _end;
                Transform head = null;
                bool holding = _state == RopeState.Lassoed || _state == RopeState.Taut;
                if (holding && NetworkHerd.Instance != null && NetworkHerd.Instance.TryGetLocalTarget(_caughtId, out LassoTarget target))
                {
                    end = target.HeadPosition;
                    head = target.Head;
                }
                return new RopeSnapshot(_rope.SaddlePoint, end, _tension01, _state, _breakProgress, holding ? _caughtId : -1, head);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _rope.SetTargetDirectory(NetworkHerd.Instance);
            _rope.Snapped += OnSnapped;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _rope.Snapped -= OnSnapped;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Only the host simulates ropes; clients keep the component for its saddle point.
            if (!IsServerInitialized)
                _rope.enabled = false;
            if (IsOwner)
                _rider.Lasso.BindRope(this);
        }

        public void Throw(Vector3 target, int targetId) =>
            ThrowRequest(target, targetId, NetworkHerd.Instance != null ? NetworkHerd.Instance.ViewServerTime : 0f);

        public void Release() => ReleaseRequest();

        public float EstimateFlightTime(Vector3 target) => _rope.EstimateFlightTime(target);

        [ServerRpc]
        private void ThrowRequest(Vector3 target, int targetId, float clientViewTime)
        {
            // How far behind the host this player's view was; the rope clamps it to its max rewind.
            float rewind = LassoValidator.RewindSeconds(Time.time, clientViewTime, maxRewind: 1f);
            _rope.Throw(target, targetId, rewind);
        }

        [ServerRpc]
        private void ReleaseRequest() => _rope.Release();

        private void OnSnapped(float snapTension, Vector3 pullDirection) => _rider.ServerRopeSnapped(snapTension, pullDirection);

        private void FixedUpdate()
        {
            if (IsServerInitialized)
                _rider.ServerSetDrag(_rope.DragVelocity);
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                bool busy = _rope.State != RopeState.Coiled;
                // A change of state (thrown, caught, snapped) goes out at once, not at the idle rate.
                bool changed = _rope.State != _sentState;
                if (Time.time < _nextSend && !changed)
                    return;
                _sentState = _rope.State;
                _nextSend = Time.time + 1f / (busy ? _sendRate : _idleSendRate);
                RopeSnapshot snapshot = _rope.Snapshot;
                ObserversRopeState((byte)snapshot.State, PackTension(snapshot.Tension01), PackUnit(snapshot.BreakProgress),
                    (short)snapshot.CaughtId, snapshot.End);
            }
            else if (_hasState)
            {
                float blend = _endSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / _endSmoothing);
                _end = Vector3.Lerp(_end, _endTarget, blend);
            }
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ObserversRopeState(byte state, byte tension01, byte breakProgress, short caughtId, Vector3 end,
            Channel channel = Channel.Unreliable)
        {
            if (!_hasState)
                _end = end;
            _hasState = true;
            _state = (RopeState)state;
            _tension01 = tension01 / 255f * 2f;
            _breakProgress = breakProgress / 255f;
            _caughtId = caughtId;
            _endTarget = end;
        }

        // Tension (fraction of the break limit) can go past 1, so 0-2 is packed into a byte.
        private static byte PackTension(float tension01) => (byte)Mathf.Clamp(Mathf.RoundToInt(tension01 / 2f * 255f), 0, 255);

        private static byte PackUnit(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }
}
