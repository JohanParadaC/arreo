using System.Collections.Generic;
using Arreo.Herd;
using Arreo.Rope;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// The herd on the network. The host simulates it (as offline) and sends every cow's position
    /// and heading, compressed, about 10 times a second; clients show kinematic copies interpolated
    /// a little in the past. The host keeps a short history so a client's lasso throw can be
    /// checked against where the herd was when that client saw it.
    /// </summary>
    public sealed class NetworkHerd : NetworkBehaviour, ILassoTargetDirectory
    {
        [SerializeField] private ActiveHerd _herd;
        [Tooltip("Snapshots per second sent to clients.")]
        [SerializeField, Min(1f)] private float _sendRate = 10f;
        [Tooltip("How far in the past clients draw the herd (s). About one snapshot interval, so there's always a newer one to blend to.")]
        [SerializeField, Min(0f)] private float _interpolationDelay = 0.1f;
        [Tooltip("Seconds of snapshots kept: for clients to interpolate and for the host to rewind.")]
        [SerializeField, Min(0.2f)] private float _historySeconds = 1f;

        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<float> _yaws = new List<float>();
        private readonly List<LassoTarget> _targets = new List<LassoTarget>();
        private readonly List<Vector3> _headOffsets = new List<Vector3>();
        private readonly List<RiderSample> _riders = new List<RiderSample>();
        private SnapshotBuffer _buffer;
        private ServerTimeEstimator _clock;
        private float _nextSend;

        /// <summary>The herd on this machine (at most one per scene).</summary>
        public static NetworkHerd Instance { get; private set; }

        public ActiveHerd Herd => _herd;

        /// <summary>
        /// The host time this machine is showing the herd at: now on the host, ~100 ms ago plus
        /// travel time on a client. Sent with lasso throws so the host can rewind to it.
        /// </summary>
        public float ViewServerTime { get; private set; }

        /// <summary>Size (bytes) of the last snapshot sent or received, and how many a client got.</summary>
        public int LastSnapshotBytes { get; private set; }
        public int SnapshotsReceived { get; private set; }

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
            _herd.SpawnHerd();
            _herd.SetRiderSource(GatherRiders);
            _buffer = new SnapshotBuffer(_historySeconds);
            CacheTargets();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized)
                return;
            _buffer = new SnapshotBuffer(_historySeconds);
            _clock = new ServerTimeEstimator(windowSize: 20);
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                ViewServerTime = Time.time;
                if (Time.time < _nextSend || _herd.Count == 0)
                    return;
                _nextSend = Time.time + 1f / _sendRate;
                byte[] data = HerdSnapshotCodec.Encode(_herd.Positions, _herd.Yaws);
                LastSnapshotBytes = data.Length;
                _buffer.Add(Time.time, _herd.Positions, _herd.Yaws);
                ObserversSnapshot(data, Time.time);
            }
            else if (_clock != null && _clock.IsReady)
            {
                ViewServerTime = _clock.ServerNow(Time.time) - _interpolationDelay;
                if (_buffer.Sample(ViewServerTime, _positions, _yaws))
                    _herd.ApplyPoses(_positions, _yaws);
            }
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ObserversSnapshot(byte[] data, float serverTime, Channel channel = Channel.Unreliable)
        {
            LastSnapshotBytes = data.Length;
            SnapshotsReceived++;
            HerdSnapshotCodec.Decode(data, _positions, _yaws);
            if (_herd.Count == 0)
            {
                _herd.SpawnPuppets(_positions.Count);
                CacheTargets();
            }
            _clock.Observe(serverTime, Time.time);
            _buffer.Add(serverTime, _positions, _yaws);
        }

        /// <summary>This machine's copy of a lassoable cow, by network id (for drawing a rope to its head).</summary>
        public bool TryGetLocalTarget(int targetId, out LassoTarget target)
        {
            target = targetId >= 0 && targetId < _targets.Count ? _targets[targetId] : null;
            return target != null;
        }

        public void GetCurrentHeads(List<Vector3> heads)
        {
            heads.Clear();
            foreach (LassoTarget target in _targets)
                heads.Add(target.HeadPosition);
        }

        public bool TryGetHeadAt(int targetId, float secondsAgo, out Vector3 head)
        {
            head = Vector3.zero;
            if (targetId < 0 || targetId >= _targets.Count || !_buffer.Sample(Time.time - secondsAgo, _positions, _yaws)
                || targetId >= _positions.Count)
                return false;
            head = _positions[targetId] + Quaternion.Euler(0f, _yaws[targetId], 0f) * _headOffsets[targetId];
            return true;
        }

        public LassoTarget GetTarget(int targetId) => TryGetLocalTarget(targetId, out LassoTarget target) ? target : null;

        private void CacheTargets()
        {
            _targets.Clear();
            _headOffsets.Clear();
            for (int i = 0; i < _herd.Cows.Count; i++)
            {
                Rigidbody cow = _herd.Cows[i];
                var target = cow.GetComponent<LassoTarget>();
                target.TargetId = i;
                _targets.Add(target);
                _headOffsets.Add(Quaternion.Inverse(cow.rotation) * (target.HeadPosition - cow.position));
            }
        }

        // Host: every player's horse scares the herd (D73).
        private IReadOnlyList<RiderSample> GatherRiders()
        {
            _riders.Clear();
            foreach (NetworkRider rider in NetworkRider.All)
                _riders.Add(new RiderSample(rider.transform.position, rider.PlanarVelocity.magnitude));
            return _riders;
        }
    }
}
