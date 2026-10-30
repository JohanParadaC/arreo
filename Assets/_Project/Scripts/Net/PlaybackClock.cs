using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// The time a client plays the herd at. It follows a target (the estimated host time minus
    /// the interpolation delay) but never jumps when that estimate is corrected: it runs a little
    /// faster or slower until it catches up, and only snaps when it is far off.
    /// </summary>
    public sealed class PlaybackClock
    {
        // Time constant (s) of closing the gap once it's small enough not to need the full slew.
        private const float CatchUpSeconds = 0.25f;

        private readonly float _maxSlew;
        private readonly float _snapDistance;
        private bool _started;

        /// <param name="maxSlew">Most the clock may run fast or slow, as a fraction (0.1 = ±10 %).</param>
        /// <param name="snapDistance">Gap (s) past which it jumps straight to the target.</param>
        public PlaybackClock(float maxSlew, float snapDistance)
        {
            _maxSlew = maxSlew;
            _snapDistance = snapDistance;
        }

        public float Time { get; private set; }

        public float Advance(float deltaTime, float target)
        {
            float gap = target - (Time + deltaTime);
            if (!_started || Mathf.Abs(gap) > _snapDistance)
            {
                _started = true;
                Time = target;
                return Time;
            }

            float slew = Mathf.Clamp(gap / CatchUpSeconds, -_maxSlew, _maxSlew);
            Time += deltaTime * (1f + slew);
            return Time;
        }
    }
}
