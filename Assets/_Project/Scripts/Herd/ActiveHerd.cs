using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Active herd: spawns the cows and drives every cow's Rigidbody from <see cref="HerdSteering"/>
    /// each physics step. Cows keep their Rigidbody so a rope can pull them later. A cow that
    /// leaves the herd (sold, D29: for good) is hidden but keeps its index, so the network ids of
    /// the others don't shift; the herd only steers the cows still in it.
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

        private const int SpawnSeed = 11;

        private readonly List<Rigidbody> _cows = new List<Rigidbody>();
        private Vector3[] _positions = new Vector3[0];
        private Vector3[] _velocities = new Vector3[0];
        private float[] _yaws = new float[0];
        private bool[] _present = new bool[0];
        // The cows still in the herd, packed together for the steering rules (sold ones would still pull their neighbors).
        private readonly List<int> _steerIndices = new List<int>();
        private readonly List<Vector3> _steerPositions = new List<Vector3>();
        private readonly List<Vector3> _steerVelocities = new List<Vector3>();
        private HerdSteering _steering;
        private Stampede _stampede;
        private System.Func<IReadOnlyList<RiderSample>> _riderSource;

        public int Count => _cows.Count;
        public IReadOnlyList<Vector3> Positions => _positions;
        public IReadOnlyList<float> Yaws => _yaws;
        public IReadOnlyList<Rigidbody> Cows => _cows;

        /// <summary>Whether each cow (by index) is still in the herd.</summary>
        public IReadOnlyList<bool> Present => _present;

        /// <summary>Cows still in the herd; <see cref="Count"/> also counts the ones that left.</summary>
        public int PresentCount { get; private set; }

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
            AllocateState(count);
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
            var random = new System.Random(SpawnSeed);
            for (int i = 0; i < _count; i++)
            {
                SpawnPose(random, out Vector3 position, out Quaternion rotation);
                _cows.Add(Instantiate(_cowPrefab, position, rotation, transform));
            }
            AllocateState(_cows.Count);
            ReadState();
        }

        /// <summary>
        /// Host: the cow leaves the herd for good (D29), sold or lost. It's hidden, a rope on it lets
        /// go, and the others stop herding with it. Does nothing if it already left.
        /// </summary>
        public void Remove(int index) => SetPresent(index, false);

        /// <summary>Network clients: shows or hides a cow the way the host's snapshot says.</summary>
        public void SetPresent(int index, bool present)
        {
            if (_present[index] == present)
                return;
            _present[index] = present;
            PresentCount += present ? 1 : -1;
            _cows[index].gameObject.SetActive(present);
        }

        /// <summary>
        /// Host (prototype debug): brings every cow back, standing still where the herd first
        /// spawned, as if it had never been driven or sold.
        /// </summary>
        public void Restock()
        {
            if (_steering == null)
                return;

            var random = new System.Random(SpawnSeed);
            for (int i = 0; i < _cows.Count; i++)
            {
                SetPresent(i, true);
                SpawnPose(random, out Vector3 position, out Quaternion rotation);
                Rigidbody cow = _cows[i];
                cow.transform.SetPositionAndRotation(position, rotation);
                cow.position = position;
                cow.rotation = rotation;
                cow.linearVelocity = Vector3.zero;
            }
            ReadState();
        }

        private void SpawnPose(System.Random random, out Vector3 position, out Quaternion rotation)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * _spawnRadius;
            position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        }

        private void AllocateState(int count)
        {
            _positions = new Vector3[count];
            _velocities = new Vector3[count];
            _yaws = new float[count];
            _present = new bool[count];
            for (int i = 0; i < count; i++)
                _present[i] = true;
            PresentCount = count;
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

            _steerIndices.Clear();
            _steerPositions.Clear();
            _steerVelocities.Clear();
            for (int i = 0; i < _cows.Count; i++)
            {
                if (!_present[i])
                    continue;
                _steerIndices.Add(i);
                _steerPositions.Add(_positions[i]);
                _steerVelocities.Add(_velocities[i]);
            }

            for (int j = 0; j < _steerIndices.Count; j++)
            {
                int i = _steerIndices[j];
                Vector3 desired = _steering.DesiredVelocity(j, _steerPositions, _steerVelocities, riders);
                if (panic > 0f)
                    desired = Vector3.Lerp(desired, _steering.StampedeVelocity(j, _steerPositions, _stampede.Origin, 1f), panic);
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

        // A cow that left keeps its last pose.
        private void ReadState()
        {
            for (int i = 0; i < _cows.Count; i++)
            {
                if (!_present[i])
                    continue;
                _positions[i] = _cows[i].position;
                _velocities[i] = _cows[i].linearVelocity;
                _yaws[i] = _cows[i].rotation.eulerAngles.y;
            }
        }
    }
}
