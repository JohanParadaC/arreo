using System;
using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>
    /// Horse movement state (speed and heading) advanced from a <see cref="HorseIntent"/>.
    /// Plain C#: the MonoBehaviour only turns the result into Rigidbody motion.
    /// </summary>
    public sealed class HorseLocomotion
    {
        // Below this fraction of the expected speed the horse counts as blocked (hit a wall),
        // so small losses on ramps don't eat its momentum.
        private const float BlockedSpeedRatio = 0.5f;

        private readonly HorseGait _gait;
        private float _speed;
        private float _heading;

        public HorseLocomotion(HorseGait gait, HorseStamina stamina, float heading)
        {
            _gait = gait ?? throw new ArgumentNullException(nameof(gait));
            Stamina = stamina ?? throw new ArgumentNullException(nameof(stamina));
            _heading = Mathf.Repeat(heading, 360f);
        }

        /// <summary>Forward speed in m/s; negative when backing up.</summary>
        public float Speed => _speed;

        /// <summary>Yaw in degrees, [0, 360).</summary>
        public float Heading => _heading;

        public bool IsGalloping { get; private set; }
        /// <summary>Moving forward at the slow driving walk (arreo).</summary>
        public bool IsDriving { get; private set; }
        public HorseStamina Stamina { get; }

        public void Tick(HorseIntent intent, float deltaTime)
        {
            bool wantsGallop = intent.Gallop && intent.Throttle > 0f && Stamina.CanGallop;
            IsDriving = intent.SlowWalk && intent.Throttle > 0f && !wantsGallop;
            float target = _gait.TargetSpeed(intent.Throttle, wantsGallop, IsDriving);

            _speed = _gait.NextSpeed(_speed, target, braking: intent.Throttle < 0f, deltaTime);
            _heading = Mathf.Repeat(_heading + intent.Steer * _gait.TurnRate(_speed) * deltaTime, 360f);

            IsGalloping = wantsGallop && _speed > _gait.WalkSpeed;
            Stamina.Tick(IsGalloping, deltaTime);
        }

        /// <summary>Drops the speed to what physics actually achieved when the horse is blocked.</summary>
        public void LimitToMeasuredSpeed(float measuredSpeed)
        {
            if (Mathf.Abs(measuredSpeed) < Mathf.Abs(_speed) * BlockedSpeedRatio)
                _speed = measuredSpeed;
        }
    }
}
