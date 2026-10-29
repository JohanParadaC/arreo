using Arreo.Rope;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Player
{
    /// <summary>
    /// Throws the lasso where the camera's center points and lets go on demand. Aiming at
    /// something catchable leads the throw to where its head will be when the loop arrives.
    /// From week 6 these become requests to the host instead of direct calls.
    /// </summary>
    public sealed class LassoInput : MonoBehaviour
    {
        private const float AimDistance = 80f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [SerializeField] private RopeSimulation _rope;
        [Tooltip("The rider's horse, ignored by the aim ray.")]
        [SerializeField] private Rigidbody _horse;
        [SerializeField] private InputActionReference _throw;
        [SerializeField] private InputActionReference _release;

        private void OnEnable()
        {
            _throw.action.Enable();
            _release.action.Enable();
        }

        private void Update()
        {
            if (_release.action.WasPressedThisFrame())
                _rope.Release();
            else if (_throw.action.WasPressedThisFrame() && Camera.main != null)
                _rope.Throw(Aim(Camera.main));
        }

        private Vector3 Aim(Camera camera)
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
