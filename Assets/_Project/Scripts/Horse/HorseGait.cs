using System;
using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>
    /// Speed and turning rules of a horse: target speed per gait, inertia and how
    /// turning gets harder the faster it goes. Stateless; speeds in m/s, turn rates in deg/s.
    /// </summary>
    public sealed class HorseGait
    {
        private readonly float _backSpeed;
        private readonly float _acceleration;
        private readonly float _coastDeceleration;
        private readonly float _braking;
        private readonly float _turnRateStanding;
        private readonly float _turnRateAtGallop;

        public HorseGait(float walkSpeed, float gallopSpeed, float backSpeed, float acceleration,
            float coastDeceleration, float braking, float turnRateStanding, float turnRateAtGallop, float driveSpeed)
        {
            if (walkSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(walkSpeed), walkSpeed, "Must be greater than zero.");
            if (gallopSpeed <= walkSpeed)
                throw new ArgumentException("Gallop must be faster than walk.", nameof(gallopSpeed));
            if (driveSpeed <= 0f || driveSpeed > walkSpeed)
                throw new ArgumentException("Drive speed must be greater than zero and no faster than walk.", nameof(driveSpeed));

            WalkSpeed = walkSpeed;
            DriveSpeed = driveSpeed;
            GallopSpeed = gallopSpeed;
            _backSpeed = backSpeed;
            _acceleration = acceleration;
            _coastDeceleration = coastDeceleration;
            _braking = braking;
            _turnRateStanding = turnRateStanding;
            _turnRateAtGallop = turnRateAtGallop;
        }

        public float WalkSpeed { get; }
        public float GallopSpeed { get; }
        public float DriveSpeed { get; }

        /// <summary>Speed the horse wants to reach. Negative throttle backs up; a gallop overrides driving.</summary>
        public float TargetSpeed(float throttle, bool galloping, bool driving = false)
        {
            if (throttle < 0f)
                return throttle * _backSpeed;

            return throttle * (galloping ? GallopSpeed : driving ? DriveSpeed : WalkSpeed);
        }

        /// <summary>Moves the current speed toward the target, limited by acceleration or deceleration.</summary>
        public float NextSpeed(float current, float target, bool braking, float deltaTime)
        {
            bool slowingDown = current > 0f ? target < current : current < 0f && target > current;
            float rate = !slowingDown ? _acceleration
                : braking ? _braking
                : _coastDeceleration;

            return Mathf.MoveTowards(current, target, rate * deltaTime);
        }

        /// <summary>Maximum turn rate at the given speed: quick when standing, wide arcs at a gallop.</summary>
        public float TurnRate(float speed)
        {
            float t = Mathf.Clamp01(Mathf.Abs(speed) / GallopSpeed);
            return Mathf.Lerp(_turnRateStanding, _turnRateAtGallop, t);
        }
    }
}
