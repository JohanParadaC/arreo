using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>Tuning values for how a herd behaves around a rider.</summary>
    [CreateAssetMenu(menuName = "Arreo/Herd Settings", fileName = "HerdSettings")]
    public sealed class HerdSettings : ScriptableObject
    {
        [Header("Boids")]
        [Tooltip("Cows closer than this push each other apart (m).")]
        [SerializeField, Min(0.1f)] private float _separationRadius = 2.5f;
        [Tooltip("Cows within this distance count as herd mates for alignment and cohesion (m).")]
        [SerializeField, Min(0.1f)] private float _neighborRadius = 12f;
        [SerializeField, Min(0f)] private float _separationStrength = 3f;
        [Tooltip("Fraction of herd mates' velocity each cow copies. Higher = fear spreads through the herd.")]
        [SerializeField, Range(0f, 1f)] private float _alignmentWeight = 0.6f;
        [Tooltip("m/s per meter away from the herd's center. Higher = tighter herd.")]
        [SerializeField, Min(0f)] private float _cohesionStrength = 0.15f;

        [Header("Fear of the rider")]
        [Tooltip("Distance at which a slow rider starts scaring cows (m).")]
        [SerializeField, Min(0.1f)] private float _fearRadius = 15f;
        [Tooltip("At gallop the fear radius is multiplied by this.")]
        [SerializeField, Min(1f)] private float _gallopRadiusMultiplier = 2f;
        [Tooltip("How much a rider standing still scares, compared to a galloping one.")]
        [SerializeField, Range(0f, 1f)] private float _stillIntensity = 0.5f;
        [Tooltip("Rider speed treated as full gallop (m/s). Match the horse's gallop speed.")]
        [SerializeField, Min(0.1f)] private float _gallopReferenceSpeed = 12f;
        [Tooltip("Rider speed where fear starts turning into panic and the herd scatters (m/s).")]
        [SerializeField, Min(0f)] private float _panicStartSpeed = 6f;

        [Header("Flight zone")]
        [Tooltip("Personal space of a cow (m). As soon as the rider is this close, even still or at a slow walk, the cow walks away at least at Flight Zone Speed. It never causes panic: that stays reserved for the gallop.")]
        [SerializeField, Min(0f)] private float _flightZoneRadius = 8f;
        [Tooltip("Walking speed of a cow leaving its flight zone (m/s). Keep it a bit above the horse's drive speed so the herd stays ahead of a slow-walking rider.")]
        [SerializeField, Min(0f)] private float _flightZoneSpeed = 2.2f;

        [Header("Cow movement")]
        [Tooltip("Top speed of a scared cow (m/s).")]
        [SerializeField, Min(0.1f)] private float _fleeSpeed = 6f;
        [Tooltip("Top speed of a panicked cow (m/s).")]
        [SerializeField, Min(0.1f)] private float _panicSpeed = 8f;
        [SerializeField, Min(0.1f)] private float _acceleration = 5f;
        [Tooltip("Degrees per second.")]
        [SerializeField, Min(1f)] private float _turnSpeed = 180f;
        [Tooltip("Below this desired speed the cow just stands and grazes (m/s).")]
        [SerializeField, Min(0f)] private float _minMoveSpeed = 0.2f;

        public float Acceleration => _acceleration;
        public float TurnSpeed => _turnSpeed;
        public float MinMoveSpeed => _minMoveSpeed;

        public HerdSteering CreateSteering() => new HerdSteering(
            _separationRadius, _neighborRadius, _separationStrength, _alignmentWeight, _cohesionStrength,
            _fleeSpeed, _panicSpeed,
            new RiderFear(_fearRadius, _gallopRadiusMultiplier, _stillIntensity, _gallopReferenceSpeed, _panicStartSpeed, _flightZoneRadius),
            _flightZoneSpeed);
    }
}
