using UnityEngine;

namespace Arreo.Voice
{
    /// <summary>
    /// Tuning for the cabrestero's canto (rule 4): how it's recorded, sent and played. How the herd
    /// reacts to it lives in HerdSettings ("Canto del cabrestero").
    /// </summary>
    [CreateAssetMenu(menuName = "Arreo/Voice Settings", fileName = "VoiceSettings")]
    public sealed class VoiceSettings : ScriptableObject
    {
        [Header("Recording (rule 4: WAV mono 16 kHz, 5-15 s)")]
        [Tooltip("Samples per second of every canto. Rule 4 fixes it at 16000: plenty for a voice, a third of a CD's size.")]
        [SerializeField, Min(8000)] private int _sampleRate = 16000;
        [Tooltip("Shortest canto that can be kept (s).")]
        [SerializeField, Min(1f)] private float _minSeconds = 5f;
        [Tooltip("Longest canto (s). Recording stops by itself here.")]
        [SerializeField, Min(1f)] private float _maxSeconds = 15f;
        [Tooltip("A recording is turned up until its loudest moment reaches this (0-1), so every player's canto plays about as loud.")]
        [SerializeField, Range(0.1f, 1f)] private float _targetPeak = 0.9f;
        [Tooltip("Most a quiet recording is turned up, so a far-away microphone doesn't turn background noise into a roar.")]
        [SerializeField, Min(1f)] private float _maxGain = 8f;
        [Tooltip("A recording whose loudest moment stays below this (0-1) has no voice in it and isn't kept.")]
        [SerializeField, Range(0f, 0.5f)] private float _silencePeak = 0.02f;

        [Header("Canto fijo")]
        [Tooltip("Played for a player who never recorded their own canto.")]
        [SerializeField] private AudioClip _fixedCanto;

        [Header("Network (rule 4: sent in parts, once per session)")]
        [Tooltip("Bytes per part. FishNet won't take more than 20 KB from a client in one message, so keep it well under that.")]
        [SerializeField, Range(1024, 16384)] private int _partBytes = 8192;
        [Tooltip("Parts sent per second to each player. 6 x 8 KB = 48 KB/s: a 15 s canto (480 KB) arrives in about 10 s without crowding the game's own messages.")]
        [SerializeField, Min(1f)] private float _partsPerSecond = 6f;

        [Header("Playback (3D, from the rider)")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [Tooltip("Up to this distance (m) the canto plays at full volume.")]
        [SerializeField, Min(0.1f)] private float _minDistance = 10f;
        [Tooltip("Beyond this distance (m) the canto can't be heard.")]
        [SerializeField, Min(1f)] private float _maxDistance = 150f;
        [Tooltip("Seconds the canto takes to fade out after letting go of the button, so it doesn't cut with a click.")]
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.3f;

        public int SampleRate => _sampleRate;
        public float MaxSeconds => _maxSeconds;
        public float MinSeconds => _minSeconds;
        public AudioClip FixedCanto => _fixedCanto;
        public int PartBytes => _partBytes;
        public float PartsPerSecond => _partsPerSecond;
        public float Volume => _volume;
        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;
        public float FadeOutSeconds => _fadeOutSeconds;

        public CantoRules CreateRules() => new CantoRules(_sampleRate, _minSeconds, _maxSeconds, _targetPeak, _maxGain, _silencePeak);

        /// <summary>An AudioClip from a canto's samples (mono, at the canto sample rate).</summary>
        public AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, _sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
