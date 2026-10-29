using Arreo.Rope;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Player
{
    /// <summary>
    /// Hold to aim (the camera moves over the shoulder), throw the lasso where the camera's center
    /// points, and let go on demand. Aiming at something catchable leads the throw to where its
    /// head will be when the loop arrives. From week 6 these become requests to the host.
    /// </summary>
    public sealed class LassoInput : MonoBehaviour
    {
        private const float AimDistance = 80f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private RiderSettings _settings;
        [Tooltip("While the rider is down they can't aim, throw or let go.")]
        [SerializeField] private RiderMount _mount;
        [Tooltip("The rider's horse, ignored by the aim ray.")]
        [SerializeField] private Rigidbody _horse;
        [SerializeField] private InputActionReference _aim;
        [SerializeField] private InputActionReference _throw;
        [SerializeField] private InputActionReference _release;

        private AimBlend _aimBlend;

        /// <summary>Aiming state for the camera and the crosshair.</summary>
        public AimBlend Aim => _aimBlend;

        private void Awake() => _aimBlend = _settings.CreateAim();

        private void OnEnable()
        {
            _aim.action.Enable();
            _throw.action.Enable();
            _release.action.Enable();
        }

        private void Update()
        {
            bool hasControl = _mount == null || _mount.HasControl;
            _aimBlend.Tick(Time.deltaTime, hasControl && _aim.action.IsPressed());
            if (!hasControl)
                return;

            if (_release.action.WasPressedThisFrame())
                _rope.Release();
            else if (_throw.action.WasPressedThisFrame() && Camera.main != null)
                _rope.Throw(AimPoint(Camera.main));
        }

        private Vector3 AimPoint(Camera camera)
        {
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

            Vector3 head = target.HeadPosition;
            return head + target.Body.linearVelocity * _rope.EstimateFlightTime(head);
        }
    }
}
