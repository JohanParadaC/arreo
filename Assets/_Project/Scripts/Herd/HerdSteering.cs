using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Combines the boids rules and fear of the rider into the velocity (m/s, on the ground)
    /// a cow wants. Panic fades cohesion and alignment and boosts separation, so a galloping
    /// rider scatters the herd while a walking one moves it as a group.
    /// </summary>
    public sealed class HerdSteering
    {
        private readonly float _separationRadius;
        private readonly float _neighborRadius;
        private readonly float _separationStrength;
        private readonly float _alignmentWeight;
        private readonly float _cohesionStrength;
        private readonly float _fleeSpeed;
        private readonly float _panicSpeed;
        private readonly RiderFear _fear;
        private readonly float _flightZoneSpeed;

        /// <param name="separationStrength">m/s of push from a neighbor touching this cow.</param>
        /// <param name="alignmentWeight">Fraction of the neighbors' average velocity this cow copies.</param>
        /// <param name="cohesionStrength">m/s per meter away from the neighbors' center.</param>
        /// <param name="fleeSpeed">Top speed when fully scared.</param>
        /// <param name="panicSpeed">Top speed when fully panicked.</param>
        public HerdSteering(float separationRadius, float neighborRadius, float separationStrength,
            float alignmentWeight, float cohesionStrength, float fleeSpeed, float panicSpeed, RiderFear fear, float flightZoneSpeed)
        {
            _separationRadius = separationRadius;
            _neighborRadius = neighborRadius;
            _separationStrength = separationStrength;
            _alignmentWeight = alignmentWeight;
            _cohesionStrength = cohesionStrength;
            _fleeSpeed = fleeSpeed;
            _panicSpeed = panicSpeed;
            _fear = fear;
            _flightZoneSpeed = flightZoneSpeed;
        }

        public Vector3 DesiredVelocity(int self, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> velocities,
            Vector3 riderPosition, float riderSpeed)
        {
            Vector3 position = positions[self];
            float distance = BoidRules.Flatten(position - riderPosition).magnitude;
            float fear = _fear.Fear(distance, riderSpeed);
            float panic = _fear.Panic(fear, riderSpeed);
            float calm = 1f - panic;
            // The flight zone only sets a minimum walking pace; it adds no panic.
            float fleeSpeed = Mathf.Max(fear * _fleeSpeed, _fear.InFlightZone(distance) ? _flightZoneSpeed : 0f);

            Vector3 velocity =
                BoidRules.Separation(self, positions, _separationRadius) * (_separationStrength * (1f + panic))
                + BoidRules.Alignment(self, positions, velocities, _neighborRadius) * (_alignmentWeight * calm)
                + BoidRules.Cohesion(self, positions, _neighborRadius) * (_cohesionStrength * calm)
                + RiderFear.FleeDirection(position, riderPosition) * fleeSpeed;

            return Vector3.ClampMagnitude(velocity, Mathf.Lerp(_fleeSpeed, _panicSpeed, panic));
        }

        /// <summary>Barajustada: bolt straight away from the scare at panic speed, scaled by intensity.</summary>
        public Vector3 StampedeVelocity(int self, IReadOnlyList<Vector3> positions, Vector3 origin, float intensity)
        {
            if (intensity <= 0f)
                return Vector3.zero;

            Vector3 velocity = RiderFear.FleeDirection(positions[self], origin) * _panicSpeed
                + BoidRules.Separation(self, positions, _separationRadius) * (_separationStrength * 2f);
            return Vector3.ClampMagnitude(velocity, _panicSpeed) * intensity;
        }
    }
}
