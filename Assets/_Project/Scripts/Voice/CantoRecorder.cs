using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Voice
{
    /// <summary>
    /// Records a canto from a microphone (rule 4) and plays a take back so the player can decide
    /// whether to keep it. Recording stops by itself at the longest canto; what it records goes
    /// through <see cref="CantoRules"/> (mono, 16 kHz, turned up to a common loudness).
    /// </summary>
    public sealed class CantoRecorder : MonoBehaviour
    {
        [SerializeField] private VoiceSettings _settings;
        [Tooltip("Plays a take back (2D, not from any rider).")]
        [SerializeField] private AudioSource _preview;

        // Recorded a bit longer than the longest canto, so stopping right at the end loses nothing.
        private const int ExtraSeconds = 1;
        private const int LevelWindow = 1600;

        private readonly float[] _levelBuffer = new float[LevelWindow];
        private string _device;
        private AudioClip _recording;
        private int _recordingRate;
        private float _startedAt;
        private CantoRules _rules;
        private AudioClip _previewClip;

        public VoiceSettings Settings => _settings;

        /// <summary>The microphones on this computer.</summary>
        public IReadOnlyList<string> Devices => Microphone.devices;

        public bool IsRecording { get; private set; }

        public float RecordedSeconds => IsRecording ? Time.unscaledTime - _startedAt : 0f;

        /// <summary>The player may stop once the take is long enough to be a canto.</summary>
        public bool CanStop => RecordedSeconds >= _settings.MinSeconds;

        /// <summary>The take is as long as a canto can be: time to <see cref="Finish"/>.</summary>
        public bool ReachedLongest => RecordedSeconds >= _settings.MaxSeconds;

        /// <summary>How loud the microphone is right now (0-1), for a level meter.</summary>
        public float Level { get; private set; }

        public bool IsPreviewing => _preview.isPlaying;

        private CantoRules Rules => _rules ??= _settings.CreateRules();

        /// <summary>Starts recording from <paramref name="device"/> (empty or null = the default microphone).</summary>
        public bool Begin(string device)
        {
            StopPreview();
            if (IsRecording || Microphone.devices.Length == 0)
                return false;

            _device = string.IsNullOrEmpty(device) ? null : device;
            _recordingRate = PickRate(_device);
            _recording = Microphone.Start(_device, false, Mathf.CeilToInt(_settings.MaxSeconds) + ExtraSeconds, _recordingRate);
            if (_recording == null)
                return false;
            _startedAt = Time.unscaledTime;
            IsRecording = true;
            return true;
        }

        /// <summary>Stops recording and turns the take into a canto, or says why it can't be one.</summary>
        public CantoTake Finish()
        {
            if (!IsRecording)
                return new CantoTake(CantoTakeStatus.TooShort, null, 0f, 1f);

            // Past its length the microphone stops on its own and its position goes back to 0.
            int frames = Microphone.IsRecording(_device) ? Microphone.GetPosition(_device) : _recording.samples;
            Microphone.End(_device);
            IsRecording = false;
            Level = 0f;

            var data = new float[_recording.samples * _recording.channels];
            _recording.GetData(data, 0);
            CantoTake take = Rules.FromRecording(data, _recording.channels, _recording.frequency, frames);
            Destroy(_recording);
            _recording = null;
            return take;
        }

        public void Cancel()
        {
            if (!IsRecording)
                return;
            Microphone.End(_device);
            IsRecording = false;
            Level = 0f;
            Destroy(_recording);
            _recording = null;
        }

        /// <summary>Plays a canto (a WAV) once, to listen to it before keeping it.</summary>
        public void Preview(byte[] wav)
        {
            StopPreview();
            if (!Rules.Check(wav, out float[] samples, out _))
                return;
            _previewClip = _settings.CreateClip("Canto (preview)", samples);
            _preview.clip = _previewClip;
            _preview.Play();
        }

        public void StopPreview()
        {
            if (_preview.isPlaying)
                _preview.Stop();
            _preview.clip = null;
            if (_previewClip != null)
                Destroy(_previewClip);
            _previewClip = null;
        }

        private void Update()
        {
            if (!IsRecording)
                return;
            if (RecordedSeconds >= _settings.MaxSeconds)
            {
                // Long enough: the player can listen to it now; Finish keeps the first 15 s.
                Level = 0f;
                return;
            }
            int position = Microphone.GetPosition(_device);
            if (position < LevelWindow)
                return;
            _recording.GetData(_levelBuffer, position - LevelWindow);
            float peak = 0f;
            foreach (float sample in _levelBuffer)
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            Level = peak;
        }

        private void OnDisable() => Cancel();

        // Most microphones record at any rate (caps 0, 0); otherwise the closest to 16 kHz, resampled later.
        private int PickRate(string device)
        {
            Microphone.GetDeviceCaps(device, out int min, out int max);
            if (min == 0 && max == 0)
                return _settings.SampleRate;
            return Mathf.Clamp(_settings.SampleRate, min, max);
        }
    }
}
