using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>
    /// Moves the horse's Rigidbody from <see cref="HorseLocomotion"/>. It never reads input:
    /// whoever rides it calls <see cref="SetIntent"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HorseController : MonoBehaviour
    {
        [SerializeField] private HorseSettings _settings;

        private Rigidbody _rigidbody;
        private HorseLocomotion _locomotion;
        private HorseIntent _intent;
        private Vector3 _externalVelocity;

        public HorseLocomotion Locomotion => _locomotion;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_settings != null)
                Configure(_settings);
        }

        public void Configure(HorseSettings settings)
        {
            _settings = settings;
            _locomotion = settings.CreateLocomotion(transform.eulerAngles.y);
        }

        public void SetIntent(HorseIntent intent) => _intent = intent;

        /// <summary>Velocity forced on the horse from outside (a rope dragging it), on top of its own.</summary>
        public void SetExternalVelocity(Vector3 velocity) => _externalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        private void FixedUpdate()
        {
            // A kinematic horse is someone else's, moved by the network: never push it.
            if (_locomotion == null || _rigidbody.isKinematic)
                return;

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 forward = _rigidbody.rotation * Vector3.forward;
            Vector3 ownVelocity = new Vector3(velocity.x, 0f, velocity.z) - _externalVelocity;
            _locomotion.LimitToMeasuredSpeed(Vector3.Dot(ownVelocity, forward));

            _locomotion.Tick(_intent, Time.fixedDeltaTime);

            Quaternion rotation = Quaternion.Euler(0f, _locomotion.Heading, 0f);
            Vector3 horizontal = rotation * Vector3.forward * _locomotion.Speed + _externalVelocity;
            _rigidbody.MoveRotation(rotation);
            _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }
    }
}
