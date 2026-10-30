using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>Marks something the lasso can catch, and where its head is.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LassoTarget : MonoBehaviour
    {
        // Time constant (s) of the velocity estimate.
        private const float VelocitySmoothing = 0.1f;

        [SerializeField] private Transform _head;

        private Rigidbody _body;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        public Rigidbody Body => _body != null ? _body : _body = GetComponent<Rigidbody>();

        /// <summary>Id shared by host and clients (a cow's index in the herd); -1 if not networked.</summary>
        public int TargetId { get; set; } = -1;

        public Transform Head => _head != null ? _head : transform;
        public Vector3 HeadPosition => Head.position;

        /// <summary>
        /// Movement measured from position, not the Rigidbody: on clients targets are kinematic
        /// copies moved by snapshots, whose Rigidbody reports no velocity.
        /// </summary>
        public Vector3 Velocity { get; private set; }

        public void SetHead(Transform head) => _head = head;

        private void Update()
        {
            Vector3 position = transform.position;
            if (_hasLastPosition && Time.deltaTime > 0f)
            {
                Vector3 measured = (position - _lastPosition) / Time.deltaTime;
                Velocity = Vector3.Lerp(Velocity, measured, 1f - Mathf.Exp(-Time.deltaTime / VelocitySmoothing));
            }
            _lastPosition = position;
            _hasLastPosition = true;
        }
    }
}
