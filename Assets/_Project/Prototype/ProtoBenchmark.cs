using System;
using System.Collections.Generic;
using System.IO;
using Arreo.Herd;
using Arreo.Horse;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 3 prototype): FPS benchmark for player builds. Does nothing unless the
    /// player is started with -arreoBenchmark &lt;seconds&gt;. Optional: -arreoHerd &lt;count&gt;,
    /// -arreoBenchmarkOut &lt;file&gt;. A scripted rider gallops in a wide circle through the herd.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ProtoBenchmark : MonoBehaviour
    {
        private const float WarmupSeconds = 3f;

        [SerializeField] private ActiveHerd _herd;
        [SerializeField] private HorseController _horse;
        [Tooltip("The player's input component, disabled while the benchmark drives the horse.")]
        [SerializeField] private Behaviour _riderInput;

        private readonly List<float> _frameTimes = new List<float>();
        private float _duration;
        private string _outputPath;
        private float _elapsed;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!TryGetArg(args, "-arreoBenchmark", out string seconds))
            {
                enabled = false;
                return;
            }

            _duration = float.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
            if (TryGetArg(args, "-arreoHerd", out string count))
                _herd.SetCount(int.Parse(count));
            _outputPath = TryGetArg(args, "-arreoBenchmarkOut", out string path) ? path : "benchmark.txt";

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _riderInput.enabled = false;
        }

        private void Update()
        {
            _horse.SetIntent(new HorseIntent(throttle: 1f, steer: 0.35f, gallop: true));

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < WarmupSeconds)
                return;

            _frameTimes.Add(Time.unscaledDeltaTime);
            if (_elapsed >= WarmupSeconds + _duration)
                Finish();
        }

        private void Finish()
        {
            enabled = false;
            _frameTimes.Sort();
            float total = 0f;
            foreach (float t in _frameTimes)
                total += t;

            float averageMs = total / _frameTimes.Count * 1000f;
            float p99Ms = _frameTimes[Mathf.Min(_frameTimes.Count - 1, (int)(_frameTimes.Count * 0.99f))] * 1000f;
            string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "cows={0} frames={1} avgFps={2:0.0} avgMs={3:0.00} p99Ms={4:0.00} onePercentLowFps={5:0.0} resolution={6}x{7} gpu={8}",
                _herd.Count, _frameTimes.Count, 1000f / averageMs, averageMs, p99Ms, 1000f / p99Ms,
                Screen.width, Screen.height, SystemInfo.graphicsDeviceName);

            File.WriteAllText(_outputPath, report);
            Debug.Log("[ProtoBenchmark] " + report);
            Application.Quit();
        }

        private static bool TryGetArg(string[] args, string name, out string value)
        {
            int index = Array.IndexOf(args, name);
            value = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            return value != null;
        }
    }
}
