using System;
using System.Collections.Generic;
using System.IO;
using Arreo.Horse;
using Arreo.Net;
using FishNet.Managing;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 5 prototype): network smoothness test for player builds. Off unless
    /// started with -arreoMeasure &lt;seconds&gt;. A scripted rider rides circles, alternating walk
    /// and gallop, while this machine records the other player's horse every frame and writes
    /// a <see cref="MotionSmoothness"/> report to -arreoMeasureOut &lt;file&gt;, then quits.
    /// </summary>
    public sealed class ProtoNetBot : MonoBehaviour
    {
        private const float WarmupSeconds = 3f;
        private const float FrameRate = 60f;
        // Faster than a galloping horse (12 m/s) could ever really move.
        private const float MaxPlausibleSpeed = 18f;

        [SerializeField] private NetworkManager _networkManager;

        private readonly List<Vector3> _samples = new List<Vector3>();
        private readonly List<float> _times = new List<float>();
        private float _duration;
        private string _outputPath;
        private float _elapsed;
        private float _measured;
        private long _pingSum;
        private int _pingSamples;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoMeasure");
            if (index < 0 || index + 1 >= args.Length)
            {
                enabled = false;
                return;
            }

            _duration = float.Parse(args[index + 1], System.Globalization.CultureInfo.InvariantCulture);
            int outIndex = Array.IndexOf(args, "-arreoMeasureOut");
            _outputPath = outIndex >= 0 && outIndex + 1 < args.Length ? args[outIndex + 1] : "netmeasure.txt";
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)FrameRate;
        }

        private void Update()
        {
            NetworkRider local = NetworkRider.Local;
            if (local == null)
                return;

            local.Input.enabled = false;
            _elapsed += Time.deltaTime;
            bool gallop = (int)(_elapsed / 5f) % 2 == 1;
            local.Horse.SetIntent(new HorseIntent(throttle: 1f, steer: gallop ? 0.25f : 0.5f, gallop));
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

            _samples.Add(remote.transform.position);
            _times.Add(Time.unscaledTime);
            _measured += Time.deltaTime;
            _pingSum += _networkManager.TimeManager.RoundTripTime;
            _pingSamples++;
            if (_measured >= _duration)
                Finish();
        }

        private static NetworkRider FindRemoteRider()
        {
            foreach (NetworkRider rider in FindObjectsByType<NetworkRider>(FindObjectsSortMode.None))
                if (!rider.IsOwner)
                    return rider;
            return null;
        }

        private void Finish()
        {
            enabled = false;
            MotionSmoothness.Report report = MotionSmoothness.Analyze(_samples, _times, MaxPlausibleSpeed);
            string role = _networkManager.IsServerStarted ? "host" : "client";
            string text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "role={0} frames={1} seconds={2:0.0} avgPingMs={3} jumps={4} stalls={5} maxSpeed={6:0.0} maxSpeedChangePerFrame={7:0.00}",
                role, _samples.Count, _measured, _pingSamples > 0 ? _pingSum / _pingSamples : 0, report.Jumps, report.Stalls,
                report.MaxSpeed, report.MaxSpeedChange);
            File.WriteAllText(_outputPath, text);
            Debug.Log("[ProtoNetBot] " + text);
            Application.Quit();
        }
    }
}
