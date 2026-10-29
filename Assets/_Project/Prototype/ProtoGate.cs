using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 3 prototype): corral gate on a hinge. The rider opens or closes it with
    /// Interact when close enough. The hinge needs a kinematic Rigidbody so the gate shoves cows.
    /// </summary>
    public sealed class ProtoGate : MonoBehaviour
    {
        [SerializeField] private Rigidbody _hinge;
        [SerializeField] private Transform _rider;
        [SerializeField] private InputActionReference _interact;
        [SerializeField] private float _openYaw = -120f;
        [SerializeField] private float _reach = 15f;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float _swingSpeed = 120f;

        private float _closedYaw;
        private float _currentYaw;

        public bool IsOpen { get; private set; } = true;
        public bool RiderInReach => _rider != null && Vector3.Distance(_rider.position, _hinge.position) <= _reach;

        /// <summary>The rider who can work the gate (the host's own rider until the gate is networked).</summary>
        public void Bind(Transform rider) => _rider = rider;

        private void Awake()
        {
            _closedYaw = _hinge.rotation.eulerAngles.y;
            _currentYaw = IsOpen ? _closedYaw + _openYaw : _closedYaw;
            _hinge.transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        private void OnEnable() => _interact.action.Enable();

        private void Update()
        {
            if (RiderInReach && _interact.action.WasPressedThisFrame())
                IsOpen = !IsOpen;
        }

        private void FixedUpdate()
        {
            float target = IsOpen ? _closedYaw + _openYaw : _closedYaw;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, target, _swingSpeed * Time.fixedDeltaTime);
            _hinge.MoveRotation(Quaternion.Euler(0f, _currentYaw, 0f));
        }
    }
}
