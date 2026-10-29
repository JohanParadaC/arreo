using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>What a rope looks like from outside: all a view (or a network client) needs to draw it.</summary>
    public readonly struct RopeSnapshot
    {
        public RopeSnapshot(Vector3 start, Vector3 end, float tension01, RopeState state)
        {
            Start = start;
            End = end;
            Tension01 = tension01;
            State = state;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }

        /// <summary>Tension relative to the break limit; 1 = at the limit.</summary>
        public float Tension01 { get; }

        public RopeState State { get; }
    }

    /// <summary>
    /// Rope simulation: the throw, a verlet rope from the saddle to whatever was caught (any
    /// <see cref="LassoTarget"/>: a cow today, a cuatrero or another player later), the pull as one
    /// spring between the two ends, and the rope's state. Draws nothing; from week 6 it runs on the host only.
    /// </summary>
    public sealed class RopeSimulation : MonoBehaviour
    {
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        [SerializeField] private RopeSettings _settings;
        [Tooltip("The horse's Rigidbody; the rope is tied to its saddle.")]
        [SerializeField] private Rigidbody _saddleBody;

        private readonly List<Vector3> _heads = new List<Vector3>();
        private readonly List<LassoTarget> _candidates = new List<LassoTarget>();
        private RopeStateMachine _machine;
        private RopeTension _tension;
        private VerletRope _verlet;
        private LassoTarget _caught;
        private float _restLength;
        private float _smoothedTension;
        private Vector3 _throwStart;
        private Vector3 _throwTarget;
        private float _flightTime;
        private float _flightElapsed;
        private Vector3 _end;
        private Vector3 _letGoEnd;
        private float _letGoElapsed;

        public RopeState State => _machine != null ? _machine.State : RopeState.Coiled;
        public float Tension => _smoothedTension;
        public float BreakProgress => _machine != null ? _machine.BreakProgress : 0f;
        public LassoTarget Caught => _caught;
        public float RestLength => _restLength;

        /// <summary>Rope points while something is caught (empty otherwise).</summary>
        public IReadOnlyList<Vector3> Points => _caught != null ? _verlet.Points : System.Array.Empty<Vector3>();

        /// <summary>Velocity the rope drags the horse with this step (zero unless the target out-pulls it).</summary>
        public Vector3 DragVelocity { get; private set; }

        public Vector3 SaddlePoint => _saddleBody.transform.TransformPoint(_settings.SaddleAnchor);

        public RopeSnapshot Snapshot => new RopeSnapshot(SaddlePoint, _end, _smoothedTension / _settings.BreakTension, State);

        private void Awake()
        {
            if (_settings != null && _saddleBody != null)
                Configure(_settings, _saddleBody);
        }

        /// <summary>Sets up a rope created from code.</summary>
        public void Configure(RopeSettings settings, Rigidbody saddleBody)
        {
            _settings = settings;
            _saddleBody = saddleBody;
            _machine = settings.CreateStateMachine();
            _tension = settings.CreateTension();
            _verlet = settings.CreateVerletRope();
            _end = _letGoEnd = SaddlePoint;
        }

        public float EstimateFlightTime(Vector3 target) =>
            LassoThrow.FlightTime(Vector3.Distance(SaddlePoint, target), _settings.ThrowSpeed, _settings.MinFlightTime);

        /// <summary>Throws the loop toward a point; it lands at most Max Range away.</summary>
        public bool Throw(Vector3 target)
        {
            if (_machine == null || !_machine.Throw())
                return false;

            _throwStart = SaddlePoint;
            _throwTarget = LassoThrow.ClampToRange(_throwStart, target, _settings.MaxRange);
            _flightTime = EstimateFlightTime(_throwTarget);
            _flightElapsed = 0f;
            return true;
        }

        public void Release()
        {
            if (_machine == null || (!_machine.IsHoldingCow && State != RopeState.Throwing))
                return;
            _machine.Release();
            LetGo();
        }

        /// <summary>Ties the rope to a target right away, skipping the throw.</summary>
        public void Attach(LassoTarget target)
        {
            if (State == RopeState.Coiled)
                _machine.Throw();
            _caught = target;
            _machine.Land(caught: true);

            Vector3 start = SaddlePoint;
            Vector3 end = target.HeadPosition;
            float distance = Vector3.Distance(start, end);
            _restLength = Mathf.Max(distance, Mathf.Min(distance + _settings.Slack, _settings.MaxLength));
            _verlet.Reset(start, end, _restLength);
            _smoothedTension = 0f;
            _end = end;
        }

        private void FixedUpdate()
        {
            if (_machine == null)
                return;

            float dt = Time.fixedDeltaTime;
            DragVelocity = Vector3.zero;

            switch (State)
            {
                case RopeState.Throwing:
                    UpdateThrow(dt);
                    break;
                case RopeState.Lassoed:
                case RopeState.Taut:
                    UpdateHeld(dt);
                    break;
                default:
                    _smoothedTension = 0f;
                    _machine.Tick(dt, 0f);
                    _letGoElapsed += dt;
                    _end = Vector3.Lerp(_letGoEnd, SaddlePoint, Mathf.Clamp01(_letGoElapsed / _settings.RetractSeconds));
                    break;
            }
        }

        private void UpdateThrow(float dt)
        {
            _flightElapsed += dt;
            float t = Mathf.Clamp01(_flightElapsed / _flightTime);
            _end = LassoThrow.ArcPoint(_throwStart, _throwTarget, _settings.ArcHeight, t);
            if (t < 1f)
                return;

            LassoTarget target = FindCatch(_throwTarget);
            if (target != null)
            {
                Attach(target);
            }
            else
            {
                _machine.Land(caught: false);
                LetGo();
            }
        }

        private void UpdateHeld(float dt)
        {
            if (_caught == null || !_caught.isActiveAndEnabled)
            {
                Release();
                return;
            }

            Vector3 start = SaddlePoint;
            Vector3 end = _caught.HeadPosition;
            _end = end;
            _verlet.Step(start, end, dt, _settings.GroundHeight);

            Vector3 along = end - start;
            float distance = along.magnitude;
            Vector3 direction = distance > 0.0001f ? along / distance : Vector3.zero;
            Rigidbody body = _caught.Body;
            float separationSpeed = Vector3.Dot(body.GetPointVelocity(end) - _saddleBody.GetPointVelocity(start), direction);
            float pull = _tension.PullForce(distance, _restLength, separationSpeed);

            // The rope only pulls the target back toward the saddle; the horse side is the drag rule.
            body.AddForce(-direction * pull, ForceMode.Force);

            float blend = _settings.TensionSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-dt / _settings.TensionSmoothing);
            _smoothedTension = Mathf.Lerp(_smoothedTension, pull, blend);

            _machine.Tick(dt, _smoothedTension);
            if (State == RopeState.Broken)
            {
                LetGo();
                return;
            }

            Vector3 towardTarget = new Vector3(direction.x, 0f, direction.z);
            DragVelocity = towardTarget.normalized * _tension.DragSpeed(_smoothedTension);
        }

        private LassoTarget FindCatch(Vector3 landing)
        {
            _candidates.Clear();
            _heads.Clear();
            int count = Physics.OverlapSphereNonAlloc(landing, _settings.CatchRadius + 2f, OverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                LassoTarget target = OverlapBuffer[i].GetComponentInParent<LassoTarget>();
                if (target == null || _candidates.Contains(target))
                    continue;
                _candidates.Add(target);
                _heads.Add(target.HeadPosition);
            }

            int index = LassoThrow.FindCatch(landing, _heads, _settings.CatchRadius);
            return index >= 0 ? _candidates[index] : null;
        }

        private void LetGo()
        {
            _letGoEnd = _end;
            _letGoElapsed = 0f;
            _caught = null;
            _smoothedTension = 0f;
        }
    }
}
