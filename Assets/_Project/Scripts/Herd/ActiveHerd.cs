using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Active herd: spawns the cows and drives every cow's Rigidbody from <see cref="HerdSteering"/>
    /// each physics step. Cows keep their Rigidbody so a rope can pull them later.
    /// </summary>
    public sealed class ActiveHerd : MonoBehaviour
    {
        [SerializeField] private HerdSettings _settings;
        [SerializeField] private Rigidbody _cowPrefab;
        [Tooltip("The rider the cows are afraid of.")]
        [SerializeField] private Rigidbody _rider;
        [SerializeField, Range(1, 100)] private int _count = 20;
        [SerializeField, Min(1f)] private float _spawnRadius = 12f;
        [Tooltip("Spawn the cows on Start. Off when only the host should have them: then the host calls SpawnHerd.")]
        [SerializeField] private bool _spawnOnStart = true;

        private readonly List<Rigidbody> _cows = new List<Rigidbody>();
        private Vector3[] _positions = new Vector3[0];
        private Vector3[] _velocities = new Vector3[0];
        private float[] _yaws = new float[0];
        private HerdSteering _steering;
        private Stampede _stampede;
        private System.Func<IReadOnlyList<RiderSample>> _riderSource;

        public int Count => _cows.Count;
        public IReadOnlyList<Vector3> Positions => _positions;
        public IReadOnlyList<float> Yaws => _yaws;
        public IReadOnlyList<Rigidbody> Cows => _cows;

        /// <summary>Cows here are kinematic copies moved by the host's snapshots (network clients).</summary>
        public bool IsPuppet { get; private set; }

        /// <summary>Every rider the cows fear, asked each physics step (all players' horses in a network game).</summary>
        public void SetRiderSource(System.Func<IReadOnlyList<RiderSample>> source) => _riderSource = source;

        /// <summary>
        /// Network clients: spawns <paramref name="count"/> kinematic copies with no AI, to be
        /// placed by <see cref="ApplyPoses"/>. Does nothing if the herd already exists.
        /// </summary>
        public void SpawnPuppets(int count)
        {
            if (_steering != null || IsPuppet)
                return;
            IsPuppet = true;
            for (int i = 0; i < count; i++)
            {
                Rigidbody cow = Instantiate(_cowPrefab, transform.position, Quaternion.identity, transform);
                cow.isKinematic = true;
                cow.interpolation = RigidbodyInterpolation.None;
                _cows.Add(cow);
            }
            _positions = new Vector3[count];
            _velocities = new Vector3[count];
            _yaws = new float[count];
        }

        public void ApplyPoses(IReadOnlyList<Vector3> positions, IReadOnlyList<float> yaws)
        {
            int count = Mathf.Min(_cows.Count, positions.Count);
            for (int i = 0; i < count; i++)
            {
                _cows[i].transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0f, yaws[i], 0f));
                _positions[i] = positions[i];
                _yaws[i] = yaws[i];
            }
        }

        /// <summary>How strong the current barajustada is, 0 (calm) to 1 (full panic).</summary>
        public float StampedeIntensity => _stampede != null ? _stampede.Intensity : 0f;

        /// <summary>Barajustada: the whole herd bolts away from <paramref name="origin"/>.</summary>
        public void TriggerStampede(Vector3 origin) => _stampede?.Trigger(origin);

        /// <summary>Overrides the herd size; only has an effect before Start.</summary>
        public void SetCount(int count) => _count = count;

        /// <summary>Sets up a herd created from code; call before Start.</summary>
        public void Configure(HerdSettings settings, Rigidbody cowPrefab, Rigidbody rider, int count)
        {
            _settings = settings;
            _cowPrefab = cowPrefab;
            _rider = rider;
            _count = count;
        }

        /// <summary>The rider the cows are afraid of (a networked player's horse only exists at runtime).</summary>
        public void SetRider(Rigidbody rider) => _rider = rider;

        private void Start()
        {
            if (_spawnOnStart)
                SpawnHerd();
        }

        /// <summary>Spawns the cows once; later calls do nothing.</summary>
        public void SpawnHerd()
        {
            if (_steering != null || IsPuppet)
                return;

            _steering = _settings.CreateSteering();
            _stampede = _settings.CreateStampede();
            var random = new System.Random(11);
            for (int i = 0; i < _count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)random.NextDouble()) * _spawnRadius;
                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                _cows.Add(Instantiate(_cowPrefab, position, rotation, transform));
            }
            _positions = new Vector3[_cows.Count];
            _velocities = new Vector3[_cows.Count];
            _yaws = new float[_cows.Count];
            ReadState();
        }

        private void FixedUpdate()
        {
            if (_steering == null)
                return;

            ReadState();

            IReadOnlyList<RiderSample> riders = _riderSource != null ? _riderSource() : SingleRider();
            float dt = Time.fixedDeltaTime;
            _stampede.Tick(dt);
            float panic = _stampede.Intensity;
            float acceleration = Mathf.Lerp(_settings.Acceleration, _settings.PanicAcceleration, panic);

            for (int i = 0; i < _cows.Count; i++)
            {
                Vector3 desired = _steering.DesiredVelocity(i, _positions, _velocities, riders);
                if (panic > 0f)
                    desired = Vector3.Lerp(desired, _steering.StampedeVelocity(i, _positions, _stampede.Origin, 1f), panic);
                if (desired.sqrMagnitude < _settings.MinMoveSpeed * _settings.MinMoveSpeed)
                    desired = Vector3.zero;

                Rigidbody cow = _cows[i];
                Vector3 velocity = cow.linearVelocity;
                Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), desired, acceleration * dt);
                cow.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);

                if (horizontal.sqrMagnitude > 0.04f)
                {
                    Quaternion facing = Quaternion.LookRotation(horizontal);
                    cow.MoveRotation(Quaternion.RotateTowards(cow.rotation, facing, _settings.TurnSpeed * dt));
                }
            }
        }

        private readonly RiderSample[] _single = new RiderSample[1];

        // Offline (tests, single player): the one rider set in the inspector or by SetRider.
        private IReadOnlyList<RiderSample> SingleRider()
        {
            if (_rider == null)
                return System.Array.Empty<RiderSample>();
            Vector3 velocity = _rider.linearVelocity;
            _single[0] = new RiderSample(_rider.position, new Vector2(velocity.x, velocity.z).magnitude);
            return _single;
        }

        private void ReadState()
        {
            for (int i = 0; i < _cows.Count; i++)
            {
                _positions[i] = _cows[i].position;
                _velocities[i] = _cows[i].linearVelocity;
                _yaws[i] = _cows[i].rotation.eulerAngles.y;
            }
        }
    }
}
