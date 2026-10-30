using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Arreo.Herd;
using Arreo.Horse;
using Arreo.Net;
using Arreo.Player;
using Arreo.Rope;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 5-6 prototype): two-player network test for player builds. Off unless
    /// started with -arreoMeasure &lt;seconds&gt;; -arreoHerd &lt;n&gt; sets the herd size on the host.
    /// Both riders ride circles, alternating walk and gallop. The client then hunts the nearest
    /// cow, lassoes it and gallops away until the rope snaps. Each machine records the other
    /// player's horse and every cow each frame, the bytes it receives, what it sees of the
    /// client's rope and fall, and any warnings, writes a report to -arreoMeasureOut &lt;file&gt;
    /// and quits.
    /// </summary>
    public sealed class ProtoNetBot : MonoBehaviour
    {
        private const float WarmupSeconds = 3f;
        private const float FrameRate = 60f;
        // Faster than a galloping horse (12 m/s) or a stampeding cow could ever really move.
        private const float MaxPlausibleSpeed = 18f;
        // Client script: circles, then hunt a cow, then drag it until the rope gives.
        private const float HuntFrom = 10f;
        private const float HuntGiveUp = 20f;
        private const float DragGiveUp = 15f;
        private const float ThrowDistance = 9f;
        private const float ThrowCooldown = 1.5f;

        private enum Phase { Circles, Hunt, Drag, Done }

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private ActiveHerd _herd;

        private readonly List<Vector3> _samples = new List<Vector3>();
        private readonly List<float> _times = new List<float>();
        private readonly List<List<Vector3>> _cowSamples = new List<List<Vector3>>();
        private readonly List<string> _problems = new List<string>();
        private float _duration;
        private string _outputPath;
        private float _elapsed;
        private float _measured;
        private long _pingSum;
        private int _pingSamples;
        private long _bytesReceived;
        private int _firstSnapshotCount = -1;
        private int _warnings;
        private int _errors;

        private Phase _phase;
        private float _phaseStart;
        private int _throws;
        private float _lastThrow = float.MinValue;
        private int _ownCatchId = -1;
        private int _seenRemoteCatchId = -1;
        private bool _ownFall;
        private bool _seenRemoteFall;
        private string _ropeEnd = "none";
        private float _peakTension;
        private float _peakBreak;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoMeasure");
            if (index < 0 || index + 1 >= args.Length)
            {
                enabled = false;
                return;
            }

            _duration = float.Parse(args[index + 1], CultureInfo.InvariantCulture);
            int outIndex = Array.IndexOf(args, "-arreoMeasureOut");
            _outputPath = outIndex >= 0 && outIndex + 1 < args.Length ? args[outIndex + 1] : "netmeasure.txt";
            int herdIndex = Array.IndexOf(args, "-arreoHerd");
            if (herdIndex >= 0 && herdIndex + 1 < args.Length)
                _herd.SetCount(int.Parse(args[herdIndex + 1], CultureInfo.InvariantCulture));
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)FrameRate;
            Application.logMessageReceived += CountProblems;
        }

        private void OnDestroy() => Application.logMessageReceived -= CountProblems;

        private void Start()
        {
            if (enabled)
                _networkManager.TransportManager.Transport.OnClientReceivedData += CountBytes;
        }

        private void CountBytes(ClientReceivedDataArgs args)
        {
            if (_elapsed >= WarmupSeconds)
                _bytesReceived += args.Data.Count;
        }

        private void CountProblems(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
                return;
            if (type == LogType.Warning)
                _warnings++;
            else
                _errors++;
            if (_problems.Count < 3 && !_problems.Contains(message))
                _problems.Add(message);
        }

        private void Update()
        {
            // FishNet sets its own frame rate once it starts; measure at a player-like 60 fps.
            if (Application.targetFrameRate != (int)FrameRate)
                Application.targetFrameRate = (int)FrameRate;

            NetworkRider local = NetworkRider.Local;
            if (local == null)
                return;

            local.Input.enabled = false;
            _elapsed += Time.deltaTime;
            bool isClient = !_networkManager.IsServerStarted;
            if (isClient)
                RunClientScript(local);
            if (_phase == Phase.Circles || _phase == Phase.Done)
                RideCircles(local);
        }

        private void RideCircles(NetworkRider local)
        {
            bool gallop = (int)(_elapsed / 5f) % 2 == 1;
            local.Horse.SetIntent(new HorseIntent(throttle: 1f, steer: gallop ? 0.25f : 0.5f, gallop));
        }

        private void RunClientScript(NetworkRider local)
        {
            RopeSnapshot rope = local.Rope.IsConfigured ? local.Rope.Snapshot : default;
            if (local.Mount.State != RiderState.Mounted)
                _ownFall = true;

            switch (_phase)
            {
                case Phase.Circles:
                    if (_elapsed >= HuntFrom && NetworkHerd.Instance != null && NetworkHerd.Instance.Herd.Count > 0)
                        Enter(Phase.Hunt);
                    break;

                case Phase.Hunt:
                    if (rope.State == RopeState.Lassoed || rope.State == RopeState.Taut)
                    {
                        _ownCatchId = rope.CaughtId;
                        Enter(Phase.Drag);
                        break;
                    }
                    if (_elapsed - _phaseStart > HuntGiveUp)
                    {
                        Enter(Phase.Done);
                        break;
                    }
                    LassoTarget target = NearestCow(local.transform.position);
                    if (target == null)
                        break;
                    Steer(local, target.HeadPosition - local.transform.position, gallop: true);
                    // The host's answer takes a round trip: one throw, then wait for it.
                    if (rope.State == RopeState.Coiled && _elapsed - _lastThrow > ThrowCooldown
                        && Vector3.Distance(local.transform.position, target.HeadPosition) < ThrowDistance)
                    {
                        _lastThrow = _elapsed;
                        Vector3 aim = target.HeadPosition + target.Velocity * local.Rope.EstimateFlightTime(target.HeadPosition);
                        local.Rope.Throw(aim, target.TargetId);
                        _throws++;
                    }
                    break;

                case Phase.Drag:
                    _ropeEnd = rope.State.ToString();
                    _peakTension = Mathf.Max(_peakTension, rope.Tension01);
                    _peakBreak = Mathf.Max(_peakBreak, rope.BreakProgress);
                    if (rope.State == RopeState.Broken || rope.State == RopeState.Coiled || _elapsed - _phaseStart > DragGiveUp)
                    {
                        Enter(Phase.Done);
                        break;
                    }
                    if (rope.CaughtHead != null)
                        Steer(local, local.transform.position - rope.CaughtHead.position, gallop: true);
                    break;
            }
        }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _phaseStart = _elapsed;
        }

        private static void Steer(NetworkRider local, Vector3 toward, bool gallop)
        {
            float angle = Vector3.SignedAngle(local.transform.forward, new Vector3(toward.x, 0f, toward.z), Vector3.up);
            local.Horse.SetIntent(new HorseIntent(throttle: 1f, steer: Mathf.Clamp(angle / 25f, -1f, 1f), gallop));
        }

        private static LassoTarget NearestCow(Vector3 from)
        {
            LassoTarget nearest = null;
            float best = float.MaxValue;
            for (int i = 0; NetworkHerd.Instance.TryGetLocalTarget(i, out LassoTarget target); i++)
            {
                float distance = (target.HeadPosition - from).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = target;
                }
            }
            return nearest;
        }

        private void LateUpdate()
        {
            NetworkRider remote = FindRemoteRider();
            // The other player left (their test ended first): report what was measured so far.
            if (remote == null && _samples.Count > 0)
            {
                Finish();
                return;
            }
            if (remote == null || _elapsed < WarmupSeconds)
                return;

            // On the host, the client's rope is the real simulation; its fall is what this window shows.
            if (_networkManager.IsServerStarted)
            {
                RopeSnapshot remoteRope = remote.Rope.Snapshot;
                if (remoteRope.CaughtId >= 0)
                    _seenRemoteCatchId = remoteRope.CaughtId;
                _peakTension = Mathf.Max(_peakTension, remoteRope.Tension01);
                _peakBreak = Mathf.Max(_peakBreak, remoteRope.BreakProgress);
                if (remote.Mount.State != RiderState.Mounted)
                    _seenRemoteFall = true;
            }

            _samples.Add(remote.transform.position);
            _times.Add(Time.unscaledTime);
            SampleHerd();
            _measured += Time.deltaTime;
            _pingSum += _networkManager.TimeManager.RoundTripTime;
            _pingSamples++;
            if (_measured >= _duration)
                Finish();
        }

        private void SampleHerd()
        {
            NetworkHerd herd = NetworkHerd.Instance;
            if (herd == null || herd.Herd.Count == 0)
                return;
            if (_firstSnapshotCount < 0)
                _firstSnapshotCount = herd.SnapshotsReceived;
            IReadOnlyList<Rigidbody> cows = herd.Herd.Cows;
            while (_cowSamples.Count < cows.Count)
                _cowSamples.Add(new List<Vector3>());
            for (int i = 0; i < cows.Count; i++)
                _cowSamples[i].Add(cows[i].transform.position);
        }

        private static NetworkRider FindRemoteRider()
        {
            foreach (NetworkRider rider in NetworkRider.All)
                if (!rider.IsOwner)
                    return rider;
            return null;
        }

        private void Finish()
        {
            enabled = false;
            MotionSmoothness.Report horse = MotionSmoothness.Analyze(_samples, _times, MaxPlausibleSpeed);

            // Every cow over the same frames (a cow that joined late is skipped).
            int cowJumps = 0, cowStalls = 0;
            float cowMaxSpeed = 0f;
            foreach (List<Vector3> cow in _cowSamples)
            {
                if (cow.Count != _times.Count)
                    continue;
                MotionSmoothness.Report report = MotionSmoothness.Analyze(cow, _times, MaxPlausibleSpeed);
                cowJumps += report.Jumps;
                cowStalls += report.Stalls;
                cowMaxSpeed = Mathf.Max(cowMaxSpeed, report.MaxSpeed);
            }

            NetworkHerd herd = NetworkHerd.Instance;
            bool isHost = _networkManager.IsServerStarted;
            float seconds = Mathf.Max(0.001f, _measured);
            int snapshots = herd != null && _firstSnapshotCount >= 0 ? herd.SnapshotsReceived - _firstSnapshotCount : 0;
            string text = string.Format(CultureInfo.InvariantCulture,
                "role={0} cows={1} frames={2} seconds={3:0.0} avgPingMs={4}\n" +
                "horse: jumps={5} stalls={6} maxSpeed={7:0.0} maxSpeedChangePerFrame={8:0.00}\n" +
                "herd: jumps={9} stalls={10} maxSpeed={11:0.0} snapshotBytes={12} herdPayloadBytesPerSecond={13:0}\n" +
                "bandwidth: receivedBytesPerSecond={14:0}\n" +
                "rope: throws={15} ownCatchId={16} ownFall={17} ropeEnd={18} hostSawCatchId={19} hostSawFall={20} peakTension={24:0.00} peakBreak={25:0.00}\n" +
                "console: warnings={21} errors={22} first={23}",
                isHost ? "host" : "client", herd != null ? herd.Herd.Count : 0, _samples.Count, _measured,
                _pingSamples > 0 ? _pingSum / _pingSamples : 0,
                horse.Jumps, horse.Stalls, horse.MaxSpeed, horse.MaxSpeedChange,
                cowJumps, cowStalls, cowMaxSpeed, herd != null ? herd.LastSnapshotBytes : 0,
                isHost ? 0f : snapshots * (herd != null ? herd.LastSnapshotBytes : 0) / seconds,
                _bytesReceived / seconds,
                _throws, _ownCatchId, _ownFall, _ropeEnd, _seenRemoteCatchId, _seenRemoteFall,
                _warnings, _errors, string.Join(" | ", _problems), _peakTension, _peakBreak);
            File.WriteAllText(_outputPath, text);
            Debug.Log("[ProtoNetBot] " + text);
            Application.Quit();
        }
    }
}
