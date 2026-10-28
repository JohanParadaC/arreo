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

        private void FixedUpdate()
        {
            if (_locomotion == null)
                return;

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 forward = _rigidbody.rotation * Vector3.forward;
            _locomotion.LimitToMeasuredSpeed(Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), forward));

            _locomotion.Tick(_intent, Time.fixedDeltaTime);

            Quaternion rotation = Quaternion.Euler(0f, _locomotion.Heading, 0f);
            Vector3 horizontal = rotation * Vector3.forward * _locomotion.Speed;
            _rigidbody.MoveRotation(rotation);
            _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }
    }
}
