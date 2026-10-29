using Arreo.Player;
using Arreo.Rope;
using Unity.Cinemachine;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 4 prototype): camera moves on top of the rider camera. Aiming zooms over
    /// the shoulder; with something on the rope, a second camera sits behind the rider looking
    /// at the catch and zooms to keep both in frame.
    /// </summary>
    public sealed class ProtoCameraRig : MonoBehaviour
    {
        private const int RopeCameraLivePriority = 20;

        [SerializeField] private CinemachineCamera _riderCamera;
        [SerializeField] private CinemachineOrbitalFollow _riderOrbit;
        [SerializeField] private CinemachineRotationComposer _riderComposer;
        [SerializeField] private CinemachineCamera _ropeCamera;
        [SerializeField] private CinemachineOrbitalFollow _ropeOrbit;
        [SerializeField] private CinemachineTargetGroup _group;
        [SerializeField] private Transform _riderTarget;
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private LassoInput _lasso;
        [SerializeField] private RiderMount _mount;

        [Header("Aim (over the shoulder)")]
        [Tooltip("Camera distance while fully aiming (m).")]
        [SerializeField] private float _aimRadius = 3f;
        [Tooltip("How far right/up of the rider the camera looks past while aiming (m).")]
        [SerializeField] private Vector3 _shoulderOffset = new Vector3(0.75f, 0.15f, 0f);
        [SerializeField] private float _aimFieldOfView = 50f;

        [Header("Rope framing")]
        [Tooltip("Seconds the rope camera takes to swing around to keep the catch in front.")]
        [SerializeField] private float _swingSmoothTime = 0.35f;

        private float _baseRadius;
        private float _baseFieldOfView;
        private Transform _framedHead;
        private float _swingVelocity;

        private void Awake()
        {
            _baseRadius = _riderOrbit.Radius;
            _baseFieldOfView = _riderCamera.Lens.FieldOfView;
        }

        private void LateUpdate()
        {
            float aim = _lasso.Aim.Eased;
            Vector3 offset = Vector3.Lerp(Vector3.zero, _shoulderOffset, aim);
            _riderOrbit.Radius = Mathf.Lerp(_baseRadius, _aimRadius, aim);
            _riderOrbit.TargetOffset = offset;
            _riderComposer.TargetOffset = offset;
            _riderCamera.Lens.FieldOfView = Mathf.Lerp(_baseFieldOfView, _aimFieldOfView, aim);

            bool holding = _rope.State == RopeState.Lassoed || _rope.State == RopeState.Taut;
            LassoTarget caught = holding ? _rope.Caught : null;
            bool frameCatch = caught != null && !_lasso.Aim.IsAiming && _mount.HasControl;

            FrameHead(frameCatch ? caught.Head : null);
            if (frameCatch)
                SwingBehindRider(caught.HeadPosition);
            _ropeCamera.Priority.Value = frameCatch ? RopeCameraLivePriority : 0;
        }

        private void FrameHead(Transform head)
        {
            if (head == _framedHead)
                return;

            bool snapSwing = _framedHead == null;
            if (_framedHead != null)
                _group.RemoveMember(_framedHead);
            if (head != null)
                _group.AddMember(head, 1f, 1.5f);
            _framedHead = head;

            if (snapSwing && head != null)
                _swingVelocity = 0f;
        }

        private void SwingBehindRider(Vector3 target)
        {
            // World-space orbit: a horizontal value of yaw puts the camera behind the rider facing that yaw.
            Vector3 toTarget = target - _riderTarget.position;
            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            InputAxis axis = _ropeOrbit.HorizontalAxis;
            float swung = Mathf.SmoothDampAngle(axis.Value, yaw, ref _swingVelocity, _swingSmoothTime);
            axis.Value = Mathf.DeltaAngle(0f, swung);
            _ropeOrbit.HorizontalAxis = axis;
        }
    }
}
