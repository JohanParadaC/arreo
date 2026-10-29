using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// How scared a cow is of a rider (0 to 1) from distance and rider speed. A faster rider is
    /// felt from farther away and scares more; above <c>panicStartSpeed</c> fear turns into
    /// panic, which breaks the herd apart instead of moving it as one.
    /// </summary>
    public sealed class RiderFear
    {
        private readonly float _fearRadius;
        private readonly float _gallopRadiusMultiplier;
        private readonly float _stillIntensity;
        private readonly float _gallopReferenceSpeed;
        private readonly float _panicStartSpeed;
        private readonly float _flightZoneRadius;

        public RiderFear(float fearRadius, float gallopRadiusMultiplier, float stillIntensity,
            float gallopReferenceSpeed, float panicStartSpeed, float flightZoneRadius)
        {
            _fearRadius = fearRadius;
            _gallopRadiusMultiplier = gallopRadiusMultiplier;
            _stillIntensity = stillIntensity;
            _gallopReferenceSpeed = gallopReferenceSpeed;
            _panicStartSpeed = panicStartSpeed;
            _flightZoneRadius = flightZoneRadius;
        }

        public float Fear(float distance, float riderSpeed)
        {
            float speed01 = Mathf.Clamp01(riderSpeed / _gallopReferenceSpeed);
            float radius = _fearRadius * Mathf.Lerp(1f, _gallopRadiusMultiplier, speed01);
            float proximity = Mathf.Clamp01(1f - distance / radius);
            return proximity * Mathf.Lerp(_stillIntensity, 1f, speed01);
        }

        /// <summary>Share of the fear that is panic: none at a walk, all of it at full gallop.</summary>
        public float Panic(float fear, float riderSpeed)
        {
            float gallop01 = Mathf.Clamp01((riderSpeed - _panicStartSpeed) / (_gallopReferenceSpeed - _panicStartSpeed));
            return fear * gallop01;
        }

        /// <summary>Rider is inside the cow's personal space: it walks away even from a still rider.</summary>
        public bool InFlightZone(float distance) => distance < _flightZoneRadius;

        public static Vector3 FleeDirection(Vector3 cow, Vector3 rider)
        {
            Vector3 away = BoidRules.Flatten(cow - rider);
            return away.sqrMagnitude < 0.000001f ? Vector3.zero : away.normalized;
        }
    }
}
