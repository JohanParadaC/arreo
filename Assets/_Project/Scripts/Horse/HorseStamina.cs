using System;
using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>
    /// Drains while galloping and recovers otherwise. Once empty the horse is exhausted
    /// and can't gallop again until it recovers past the threshold, so it doesn't flicker
    /// in and out of a gallop at zero.
    /// </summary>
    public sealed class HorseStamina
    {
        private readonly float _max;
        private readonly float _drainPerSecond;
        private readonly float _recoveryPerSecond;
        private readonly float _recoveryThreshold;
        private float _current;
        private bool _isExhausted;

        public HorseStamina(float max, float drainPerSecond, float recoveryPerSecond, float recoveryThreshold)
        {
            if (max <= 0f)
                throw new ArgumentOutOfRangeException(nameof(max), max, "Must be greater than zero.");
            if (recoveryThreshold <= 0f || recoveryThreshold > 1f)
                throw new ArgumentOutOfRangeException(nameof(recoveryThreshold), recoveryThreshold, "Must be in (0, 1].");

            _max = max;
            _drainPerSecond = drainPerSecond;
            _recoveryPerSecond = recoveryPerSecond;
            _recoveryThreshold = recoveryThreshold;
            _current = max;
        }

        public float Current => _current;
        public float Normalized => _current / _max;
        public bool IsExhausted => _isExhausted;
        public bool CanGallop => !_isExhausted;

        public void Tick(bool galloping, float deltaTime)
        {
            float rate = galloping ? -_drainPerSecond : _recoveryPerSecond;
            _current = Mathf.Clamp(_current + rate * deltaTime, 0f, _max);

            if (_current <= 0f)
                _isExhausted = true;
            else if (_isExhausted && Normalized >= _recoveryThreshold)
                _isExhausted = false;
        }
    }
}
