using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>Tuning values for throwing the lasso and how the rope holds, pulls and snaps.</summary>
    [CreateAssetMenu(menuName = "Arreo/Rope Settings", fileName = "RopeSettings")]
    public sealed class RopeSettings : ScriptableObject
    {
        [Header("Throw")]
        [Tooltip("Farthest the loop can land from the rider, along the ground (m). Aiming farther makes it fall short.")]
        [SerializeField, Min(1f)] private float _maxRange = 12f;
        [Tooltip("How fast the loop flies (m/s). Lower = more time to aim ahead of a running cow.")]
        [SerializeField, Min(1f)] private float _throwSpeed = 20f;
        [SerializeField, Min(0.05f)] private float _minFlightTime = 0.25f;
        [Tooltip("How high the loop's arc rises above the straight line (m).")]
        [SerializeField, Min(0f)] private float _arcHeight = 2.5f;
        [Tooltip("How close to a head the loop must land to catch it (m). Bigger = more forgiving.")]
        [SerializeField, Min(0.1f)] private float _catchRadius = 1.4f;

        [Header("Rope (verlet)")]
        [Tooltip("Number of rope points. More = smoother shape, a bit more CPU. It doesn't change the pull.")]
        [SerializeField, Range(4, 64)] private int _pointCount = 17;
        [Tooltip("Constraint passes per physics step. Higher = the rope keeps its length better.")]
        [SerializeField, Range(1, 40)] private int _constraintIterations = 12;
        [Tooltip("Air damping of the rope points (0-1 per step). Higher = calmer, less swinging.")]
        [SerializeField, Range(0f, 0.5f)] private float _pointDamping = 0.05f;
        [Tooltip("Height of the ground the rope lies on (m).")]
        [SerializeField] private float _groundHeight = 0f;
        [Tooltip("Extra rope given out on top of the distance to the target when it's caught (m).")]
        [SerializeField, Min(0f)] private float _slack = 1f;
        [SerializeField, Min(1f)] private float _maxLength = 15f;

        [Header("Pull (spring between the two ends)")]
        [Tooltip("Newtons per meter the rope stretches past its length. Higher = stiffer rope, less rubber band.")]
        [SerializeField, Min(1f)] private float _stiffness = 10000f;
        [Tooltip("Newtons per m/s the ends move apart while taut. Higher = less bounce when the rope snaps tight.")]
        [SerializeField, Min(0f)] private float _ropeDamping = 2000f;

        [Header("Tension")]
        [Tooltip("Above this tension (N) the rope counts as taut.")]
        [SerializeField, Min(0f)] private float _tautTension = 300f;
        [Tooltip("Tension (N) the rope can't hold for long. A fleeing cow pulls about 2000 N, a stampeding one about 3600 N.")]
        [SerializeField, Min(1f)] private float _breakTension = 2400f;
        [Tooltip("Seconds of non-stop tension above Break Tension before the rope snaps.")]
        [SerializeField, Min(0.05f)] private float _breakSeconds = 1.2f;
        [Tooltip("Time constant (s) to smooth the tension reading. Higher = steadier but slower to react.")]
        [SerializeField, Min(0f)] private float _tensionSmoothing = 0.1f;

        [Header("Horse")]
        [Tooltip("Pull (N) the horse and rider resist without being dragged.")]
        [SerializeField, Min(0f)] private float _horseHoldForce = 1500f;
        [Tooltip("Drag speed (m/s) per newton of pull above the hold force.")]
        [SerializeField, Min(0f)] private float _dragSpeedPerNewton = 0.002f;
        [SerializeField, Min(0f)] private float _maxDragSpeed = 3f;
        [Tooltip("Where the rope is tied on the horse, in its local space (m).")]
        [SerializeField] private Vector3 _saddleAnchor = new Vector3(0f, 1.7f, 0.2f);

        [Header("Timing")]
        [Tooltip("Seconds to coil the rope back after letting go or missing.")]
        [SerializeField, Min(0.05f)] private float _retractSeconds = 1f;
        [Tooltip("Seconds until a new rope is ready after one snaps.")]
        [SerializeField, Min(0.05f)] private float _brokenSeconds = 1.5f;

        public float MaxRange => _maxRange;
        public float ThrowSpeed => _throwSpeed;
        public float MinFlightTime => _minFlightTime;
        public float ArcHeight => _arcHeight;
        public float CatchRadius => _catchRadius;
        public float GroundHeight => _groundHeight;
        public float Slack => _slack;
        public float MaxLength => _maxLength;
        public float BreakTension => _breakTension;
        public float TensionSmoothing => _tensionSmoothing;
        public Vector3 SaddleAnchor => _saddleAnchor;
        public float RetractSeconds => _retractSeconds;

        public RopeStateMachine CreateStateMachine() =>
            new RopeStateMachine(_tautTension, _breakTension, _breakSeconds, _retractSeconds, _brokenSeconds);

        public RopeTension CreateTension() =>
            new RopeTension(_stiffness, _ropeDamping, _horseHoldForce, _dragSpeedPerNewton, _maxDragSpeed);

        public VerletRope CreateVerletRope() => new VerletRope(_pointCount, _constraintIterations, _pointDamping);
    }
}
