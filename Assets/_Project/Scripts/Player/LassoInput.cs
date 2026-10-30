using Arreo.Rope;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Player
{
    /// <summary>
    /// Hold to aim (the camera moves over the shoulder), throw the lasso where the camera's center
    /// points, and let go on demand. Aiming at something catchable leads the throw to where its
    /// head will be when the loop arrives, and names it so the host can check the catch where this
    /// player saw it. In a network game the throw is a request to the host.
    /// </summary>
    public sealed class LassoInput : MonoBehaviour
    {
        private const float AimDistance = 80f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [Tooltip("Who carries out the throw: a RopeSimulation offline, a networked rope online (anything implementing ILassoActions).")]
        [SerializeField] private MonoBehaviour _actions;
        [Tooltip("While the rider is down they can't aim, throw or let go.")]
        [SerializeField] private RiderMount _mount;
        [Tooltip("The rider's horse, ignored by the aim ray.")]
        [SerializeField] private Rigidbody _horse;
        [SerializeField] private InputActionReference _aim;
        [SerializeField] private InputActionReference _throw;
        [SerializeField] private InputActionReference _release;

        /// <summary>Aim held (and the rider in control); the camera and the crosshair follow it.</summary>
        public bool IsAiming { get; private set; }

        private ILassoActions Actions => _actions as ILassoActions;

        /// <summary>Gives this rider the lasso to throw with (the rider spawns before it's known).</summary>
        public void BindRope(MonoBehaviour actions) => _actions = actions;

        private void OnEnable()
        {
            _aim.action.Enable();
            _throw.action.Enable();
            _release.action.Enable();
        }

        private void OnDisable() => IsAiming = false;

        private void Update()
        {
            bool hasControl = _mount == null || _mount.HasControl;
            IsAiming = hasControl && _aim.action.IsPressed();
            ILassoActions actions = Actions;
            if (!hasControl || actions == null)
                return;

            if (_release.action.WasPressedThisFrame())
            {
                actions.Release();
            }
            else if (_throw.action.WasPressedThisFrame() && Camera.main != null)
            {
                Vector3 point = AimPoint(Camera.main, actions, out int targetId);
                actions.Throw(point, targetId);
            }
        }

        private Vector3 AimPoint(Camera camera, ILassoActions actions, out int targetId)
        {
            targetId = -1;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, Hits, AimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            RaycastHit? nearest = null;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].rigidbody == _horse)
                    continue;
                if (nearest == null || Hits[i].distance < nearest.Value.distance)
                    nearest = Hits[i];
            }

            if (nearest == null)
            {
                Vector3 far = ray.GetPoint(AimDistance);
                return new Vector3(far.x, 0f, far.z);
            }

            LassoTarget target = nearest.Value.collider.GetComponentInParent<LassoTarget>();
            if (target == null)
                return nearest.Value.point;

            targetId = target.TargetId;
            Vector3 head = target.HeadPosition;
            return head + target.Velocity * actions.EstimateFlightTime(head);
        }
    }
}
