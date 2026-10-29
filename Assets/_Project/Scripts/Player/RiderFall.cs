using UnityEngine;

namespace Arreo.Player
{
    public enum RiderState
    {
        /// <summary>In the saddle, in control.</summary>
        Mounted,
        /// <summary>Thrown off and tumbling; no control.</summary>
        Fallen,
        /// <summary>Back on their feet and climbing into the saddle; still no control.</summary>
        Remounting,
    }

    /// <summary>
    /// Rider thrown off the horse when a rope snaps under a hard enough pull. Decides whether
    /// they fall, how they're launched (along the pull, angled up, faster for harder pulls) and
    /// times getting up and remounting.
    /// </summary>
    public sealed class RiderFall
    {
        private readonly float _fallTension;
        private readonly float _launchSpeed;
        private readonly float _launchSpeedPerNewton;
        private readonly float _maxLaunchSpeed;
        private readonly float _launchAngle;
        private readonly float _getUpSeconds;
        private readonly float _remountSeconds;
        private float _stateTime;

        /// <param name="fallTension">A rope snapping at or above this tension (N) throws the rider off.</param>
        /// <param name="launchSpeed">Launch speed (m/s) at exactly the fall tension.</param>
        /// <param name="launchSpeedPerNewton">Extra launch speed per newton above the fall tension.</param>
        /// <param name="launchAngle">Degrees above the ground the rider is launched at.</param>
        public RiderFall(float fallTension, float launchSpeed, float launchSpeedPerNewton, float maxLaunchSpeed,
            float launchAngle, float getUpSeconds, float remountSeconds)
        {
            _fallTension = fallTension;
            _launchSpeed = launchSpeed;
            _launchSpeedPerNewton = launchSpeedPerNewton;
            _maxLaunchSpeed = maxLaunchSpeed;
            _launchAngle = launchAngle;
            _getUpSeconds = getUpSeconds;
            _remountSeconds = remountSeconds;
        }

        public RiderState State { get; private set; } = RiderState.Mounted;
        public bool HasControl => State == RiderState.Mounted;

        /// <summary>0 to 1 while remounting: from where the rider got up back to the saddle.</summary>
        public float RemountProgress => State == RiderState.Remounting ? Mathf.Clamp01(_stateTime / _remountSeconds) : 0f;

        /// <param name="snapTension">Tension the rope snapped at (N).</param>
        /// <param name="pullDirection">Direction the rope was pulling the rider (toward the caught end).</param>
        /// <param name="launchVelocity">Velocity (m/s) to throw the rider with, if they fall.</param>
        public bool TryFall(float snapTension, Vector3 pullDirection, out Vector3 launchVelocity)
        {
            launchVelocity = Vector3.zero;
            if (State != RiderState.Mounted || snapTension < _fallTension)
                return false;

            Vector3 flat = new Vector3(pullDirection.x, 0f, pullDirection.z);
            float angle = _launchAngle * Mathf.Deg2Rad;
            Vector3 direction = flat.sqrMagnitude > 0.000001f
                ? flat.normalized * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)
                : Vector3.up;
            float speed = Mathf.Min(_launchSpeed + (snapTension - _fallTension) * _launchSpeedPerNewton, _maxLaunchSpeed);

            launchVelocity = direction * speed;
            State = RiderState.Fallen;
            _stateTime = 0f;
            return true;
        }

        public void Tick(float deltaTime)
        {
            _stateTime += deltaTime;
            if (State == RiderState.Fallen && _stateTime >= _getUpSeconds)
            {
                State = RiderState.Remounting;
                _stateTime -= _getUpSeconds;
            }
            if (State == RiderState.Remounting && _stateTime >= _remountSeconds)
            {
                State = RiderState.Mounted;
                _stateTime = 0f;
            }
        }
    }
}
