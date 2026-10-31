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

        [Header("Barajustada (stampede)")]
        [Tooltip("Seconds the whole herd bolts at full panic after a scare like thunder.")]
        [SerializeField, Min(0f)] private float _stampedeHoldSeconds = 5f;
        [Tooltip("Seconds the panic takes to fade out afterwards, so the herd can be gathered again.")]
        [SerializeField, Min(0.1f)] private float _stampedeFadeSeconds = 6f;
        [Tooltip("Acceleration of a stampeding cow (m/s²). Its pull on a rope is mass × this: 400 kg × 9 ≈ 3600 N.")]
        [SerializeField, Min(0.1f)] private float _panicAcceleration = 9f;

        [Header("Canto del cabrestero (rule 4)")]
        [Tooltip("Cows closer than this to a singing rider hear the canto at full strength (m).")]
        [SerializeField, Min(0f)] private float _cantoFullRadius = 20f;
        [Tooltip("Beyond this distance from the singer a cow doesn't hear the canto at all (m). Between the two radii it hears less and less.")]
        [SerializeField, Min(0.1f)] private float _cantoRadius = 35f;
        [Tooltip("Share of the fear of riders a cow that fully hears the canto no longer feels (0-1). Panic comes from fear, so it drops too: 0.75 = a galloping rider scares like one at a quarter of the fear.")]
        [SerializeField, Range(0f, 1f)] private float _cantoFearReduction = 0.75f;
        [Tooltip("Extra cohesion of a cow that fully hears the canto: 1.5 = it keeps to its herd mates 2.5 times as strongly, so the herd bunches up.")]
        [SerializeField, Min(0f)] private float _cantoCohesionBoost = 1.5f;
        [Tooltip("Seconds of hearing the canto it takes a cow to be fully calm.")]
        [SerializeField, Min(0.01f)] private float _cantoRiseSeconds = 1.5f;
        [Tooltip("Seconds the calm takes to fade once the canto stops or the cow is out of earshot: the herd stays calm for a while after the song.")]
        [SerializeField, Min(0.01f)] private float _cantoFadeSeconds = 6f;

        public float Acceleration => _acceleration;
        public float PanicAcceleration => _panicAcceleration;

        public Stampede CreateStampede() => new Stampede(_stampedeHoldSeconds, _stampedeFadeSeconds);
        public float TurnSpeed => _turnSpeed;
        public float MinMoveSpeed => _minMoveSpeed;

        public HerdSteering CreateSteering() => new HerdSteering(
            _separationRadius, _neighborRadius, _separationStrength, _alignmentWeight, _cohesionStrength,
            _fleeSpeed, _panicSpeed,
            new RiderFear(_fearRadius, _gallopRadiusMultiplier, _stillIntensity, _gallopReferenceSpeed, _panicStartSpeed, _flightZoneRadius),
            _flightZoneSpeed, CreateCantoCalm());

        public CantoCalm CreateCantoCalm() => new CantoCalm(_cantoRadius, _cantoFullRadius, _cantoFearReduction,
            _cantoCohesionBoost, _cantoRiseSeconds, _cantoFadeSeconds);
    }
}
