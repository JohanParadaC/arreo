using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Combines the boids rules and fear of the rider into the velocity (m/s, on the ground)
    /// a cow wants. Panic fades cohesion and alignment and boosts separation, so a galloping
    /// rider scatters the herd while a walking one moves it as a group. The cabrestero's canto
    /// (<see cref="CantoCalm"/>) works the other way: less fear, more cohesion.
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
        private readonly CantoCalm _canto;
        // Reused so the single-rider overload doesn't allocate every call.
        private readonly RiderSample[] _singleRider = new RiderSample[1];

        /// <param name="separationStrength">m/s of push from a neighbor touching this cow.</param>
        /// <param name="alignmentWeight">Fraction of the neighbors' average velocity this cow copies.</param>
        /// <param name="cohesionStrength">m/s per meter away from the neighbors' center.</param>
        /// <param name="fleeSpeed">Top speed when fully scared.</param>
        /// <param name="panicSpeed">Top speed when fully panicked.</param>
        public HerdSteering(float separationRadius, float neighborRadius, float separationStrength,
            float alignmentWeight, float cohesionStrength, float fleeSpeed, float panicSpeed, RiderFear fear, float flightZoneSpeed,
            CantoCalm canto = null)
        {
            _canto = canto;
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
            _singleRider[0] = new RiderSample(riderPosition, riderSpeed);
            return DesiredVelocity(self, positions, velocities, _singleRider);
        }

        /// <summary>
        /// Every rider pushes the cow away (D73: everyone drives the same herd); the most
        /// frightening one decides how panicked it gets.
        /// </summary>
        public Vector3 DesiredVelocity(int self, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> velocities,
            IReadOnlyList<RiderSample> riders) => DesiredVelocity(self, positions, velocities, riders, 0f);

        /// <param name="cantoCalm">
        /// How calm the cabrestero's canto has made this cow (0-1, see <see cref="CantoCalm"/>): it
        /// fears riders less, so it also panics less, and sticks closer to its herd mates.
        /// </param>
        public Vector3 DesiredVelocity(int self, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> velocities,
            IReadOnlyList<RiderSample> riders, float cantoCalm)
        {
            float fearScale = _canto != null ? _canto.FearScale(cantoCalm) : 1f;
            float cohesionScale = _canto != null ? _canto.CohesionScale(cantoCalm) : 1f;
            Vector3 position = positions[self];
            Vector3 flee = Vector3.zero;
            float panic = 0f;
            for (int i = 0; i < riders.Count; i++)
            {
                RiderSample rider = riders[i];
                float distance = BoidRules.Flatten(position - rider.Position).magnitude;
                float fear = _fear.Fear(distance, rider.Speed) * fearScale;
                panic = Mathf.Max(panic, _fear.Panic(fear, rider.Speed));
                // The flight zone only sets a minimum walking pace; it adds no panic.
                float fleeSpeed = Mathf.Max(fear * _fleeSpeed, _fear.InFlightZone(distance) ? _flightZoneSpeed : 0f);
                flee += RiderFear.FleeDirection(position, rider.Position) * fleeSpeed;
            }
            float calm = 1f - panic;

            Vector3 velocity =
                BoidRules.Separation(self, positions, _separationRadius) * (_separationStrength * (1f + panic))
                + BoidRules.Alignment(self, positions, velocities, _neighborRadius) * (_alignmentWeight * calm)
                + BoidRules.Cohesion(self, positions, _neighborRadius) * (_cohesionStrength * calm * cohesionScale)
                + flee;

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
