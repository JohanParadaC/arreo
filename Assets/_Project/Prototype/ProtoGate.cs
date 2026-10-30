using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 3-6 prototype): corral gate on a hinge. Any player near it can ask to
    /// open or close it (Interact); the host decides and every machine swings it the same way
    /// (<see cref="NetworkWorld"/>). The hinge needs a kinematic Rigidbody so the gate shoves cows.
    /// </summary>
    public sealed class ProtoGate : MonoBehaviour
    {
        [SerializeField] private Rigidbody _hinge;
        [SerializeField] private float _openYaw = -120f;
        [Tooltip("How close (m) a rider must be to work the gate.")]
        [SerializeField] private float _reach = 15f;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float _swingSpeed = 120f;

        private float _closedYaw;
        private float _currentYaw;

        public bool IsOpen { get; private set; } = true;

        public bool InReach(Vector3 riderPosition) => Vector3.Distance(riderPosition, _hinge.position) <= _reach;

        public void SetOpen(bool open) => IsOpen = open;

        private void Awake()
        {
            _closedYaw = _hinge.rotation.eulerAngles.y;
            _currentYaw = IsOpen ? _closedYaw + _openYaw : _closedYaw;
            _hinge.transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        private void FixedUpdate()
        {
            float target = IsOpen ? _closedYaw + _openYaw : _closedYaw;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, target, _swingSpeed * Time.fixedDeltaTime);
            _hinge.MoveRotation(Quaternion.Euler(0f, _currentYaw, 0f));
        }
    }
}
