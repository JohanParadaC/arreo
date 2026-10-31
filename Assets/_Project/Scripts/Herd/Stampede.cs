using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Barajustada: the whole herd bolts away from a scare (thunder) at full strength for a while,
    /// then calms down gradually so the herd can be gathered again.
    /// </summary>
    public sealed class Stampede
    {
        private readonly float _holdSeconds;
        private readonly float _fadeSeconds;
        private float _elapsed;
        private bool _triggered;

        public Stampede(float holdSeconds, float fadeSeconds)
        {
            _holdSeconds = holdSeconds;
            _fadeSeconds = fadeSeconds;
        }

        public Vector3 Origin { get; private set; }

        public float Intensity => IntensityAt(_elapsed);

        public bool IsActive => Intensity > 0f;

        /// <summary>
        /// How strong the barajustada is <paramref name="elapsed"/> seconds into it. A cow that hears the
        /// cabrestero's canto keeps its own clock that runs faster (<see cref="CantoCalm.StampedeTimeScale"/>).
        /// </summary>
        public float IntensityAt(float elapsed)
        {
            if (!_triggered)
                return 0f;
            if (elapsed <= _holdSeconds)
                return 1f;
            return Mathf.Clamp01(1f - (elapsed - _holdSeconds) / _fadeSeconds);
        }

        public void Trigger(Vector3 origin)
        {
            Origin = origin;
            _elapsed = 0f;
            _triggered = true;
        }

        public void Tick(float deltaTime)
        {
            if (_triggered)
                _elapsed += deltaTime;
        }
    }
}
