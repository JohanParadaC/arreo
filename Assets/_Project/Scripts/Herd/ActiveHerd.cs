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

        private readonly List<Rigidbody> _cows = new List<Rigidbody>();
        private Vector3[] _positions = new Vector3[0];
        private Vector3[] _velocities = new Vector3[0];
        private HerdSteering _steering;
        private Stampede _stampede;

        public int Count => _cows.Count;
        public IReadOnlyList<Vector3> Positions => _positions;

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

        private void Start()
        {
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
            ReadState();
        }

        private void FixedUpdate()
        {
            if (_steering == null)
                return;

            ReadState();

            Vector3 riderPosition = _rider != null ? _rider.position : new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 riderVelocity = _rider != null ? _rider.linearVelocity : Vector3.zero;
            float riderSpeed = new Vector2(riderVelocity.x, riderVelocity.z).magnitude;
            float dt = Time.fixedDeltaTime;
            _stampede.Tick(dt);
            float panic = _stampede.Intensity;
            float acceleration = Mathf.Lerp(_settings.Acceleration, _settings.PanicAcceleration, panic);

            for (int i = 0; i < _cows.Count; i++)
            {
                Vector3 desired = _steering.DesiredVelocity(i, _positions, _velocities, riderPosition, riderSpeed);
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

        private void ReadState()
        {
            for (int i = 0; i < _cows.Count; i++)
            {
                _positions[i] = _cows[i].position;
                _velocities[i] = _cows[i].linearVelocity;
            }
        }
    }
}
