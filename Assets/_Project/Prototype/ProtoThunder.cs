using Arreo.Herd;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 4-6 prototype): thunder near the herd, a light flash and a generated
    /// rumble. Any player asks for it with B; the host starts the barajustada and every machine
    /// plays the flash (<see cref="NetworkWorld"/>).
    /// </summary>
    public sealed class ProtoThunder : MonoBehaviour
    {
        [SerializeField] private ActiveHerd _herd;
        [SerializeField] private Light _sun;
        [Tooltip("How far from the herd's center the thunder strikes (m).")]
        [SerializeField] private float _strikeOffset = 12f;

        private AudioSource _audio;
        private float _baseIntensity;
        private float _flashTime = float.MaxValue;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = CreateRumble();
            _audio.spatialBlend = 0f;
            _baseIntensity = _sun.intensity;
        }

        private void Update()
        {
            _flashTime += Time.deltaTime;
            // Two quick flashes, then back to normal.
            bool lit = _flashTime < 0.08f || (_flashTime > 0.16f && _flashTime < 0.26f);
            _sun.intensity = lit ? _baseIntensity * 6f : _baseIntensity;
        }

        /// <summary>Host: where the thunder strikes, a little off the herd's center so the herd bolts one way.</summary>
        public Vector3 PickStrikePoint()
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 position in _herd.Positions)
                center += position;
            center /= Mathf.Max(1, _herd.Count);

            Vector2 offset = Random.insideUnitCircle.normalized * _strikeOffset;
            return center + new Vector3(offset.x, 0f, offset.y);
        }

        /// <summary>Flash and rumble on this machine (the host tells every machine to play it).</summary>
        public void PlayStrike()
        {
            _flashTime = 0f;
            _audio.Play();
        }

        private static AudioClip CreateRumble()
        {
            const int sampleRate = 44100;
            const float seconds = 3f;
            var samples = new float[(int)(sampleRate * seconds)];
            var random = new System.Random(3);
            float brown = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                // Brown noise (low rumble) with a sharp crack at the start and a slow fade.
                brown = Mathf.Clamp(brown + ((float)random.NextDouble() * 2f - 1f) * 0.05f, -1f, 1f);
                float t = i / (float)sampleRate;
                float crack = t < 0.15f ? ((float)random.NextDouble() * 2f - 1f) * (1f - t / 0.15f) : 0f;
                samples[i] = (brown * 3f + crack) * Mathf.Exp(-t * 1.2f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create("Thunder", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
