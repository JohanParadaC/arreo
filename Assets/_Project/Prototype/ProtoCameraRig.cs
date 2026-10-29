using Arreo.Player;
using Arreo.Rope;
using Unity.Cinemachine;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 4 prototype): camera moves on top of the rider camera. Aiming slides it
    /// over the shoulder (rider to the left, crosshair free); with something on the rope, a second
    /// camera sits behind the rider looking at the catch and zooms to keep both in frame.
    /// All values come from <see cref="CameraSettings"/>, read every frame so tuning applies live.
    /// </summary>
    public sealed class ProtoCameraRig : MonoBehaviour
    {
        private const int RopeCameraLivePriority = 20;

        [SerializeField] private CameraSettings _settings;
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private CinemachineCamera _riderCamera;
        [SerializeField] private CinemachineOrbitalFollow _riderOrbit;
        [SerializeField] private CinemachineRotationComposer _riderComposer;
        [SerializeField] private CinemachineCamera _ropeCamera;
        [SerializeField] private CinemachineOrbitalFollow _ropeOrbit;
        [SerializeField] private CinemachineGroupFraming _ropeFraming;
        [SerializeField] private CinemachineDecollider[] _decolliders;
        [SerializeField] private CinemachineDeoccluder[] _deoccluders;
        [SerializeField] private CinemachineTargetGroup _group;
        [SerializeField] private Transform _riderTarget;
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private LassoInput _lasso;
        [SerializeField] private RiderMount _mount;

        private AimBlend _aim;
        private float _aimBlendSeconds = -1f;
        private Transform _framedHead;
        private float _swingVelocity;

        private void Awake()
        {
            // One clear error naming the empty field, instead of a NullReferenceException every frame.
            var missing = new System.Collections.Generic.List<string>();
            if (_settings == null) missing.Add(nameof(_settings));
            if (_brain == null) missing.Add(nameof(_brain));
            if (_riderCamera == null) missing.Add(nameof(_riderCamera));
            if (_riderOrbit == null) missing.Add(nameof(_riderOrbit));
            if (_riderComposer == null) missing.Add(nameof(_riderComposer));
            if (_ropeCamera == null) missing.Add(nameof(_ropeCamera));
            if (_ropeOrbit == null) missing.Add(nameof(_ropeOrbit));
            if (_ropeFraming == null) missing.Add(nameof(_ropeFraming));
            if (_group == null) missing.Add(nameof(_group));
            if (_riderTarget == null) missing.Add(nameof(_riderTarget));
            if (_rope == null) missing.Add(nameof(_rope));
            if (_lasso == null) missing.Add(nameof(_lasso));
            if (_mount == null) missing.Add(nameof(_mount));
            if (missing.Count == 0)
                return;

            Debug.LogError($"ProtoCameraRig is missing: {string.Join(", ", missing)}. Rebuild the scene (Arreo > Prototype > Rebuild Proto_Sabana).", this);
            enabled = false;
        }

        private void LateUpdate()
        {
            // Recreate the blend if its time was tuned while playing.
            if (!Mathf.Approximately(_aimBlendSeconds, _settings.AimBlendSeconds))
            {
                _aimBlendSeconds = _settings.AimBlendSeconds;
                _aim = new AimBlend(_aimBlendSeconds);
            }
            _aim.Tick(Time.deltaTime, _lasso.IsAiming);

            ApplyRiderCamera(_aim.Eased);
            ApplyRopeCamera();
            ApplySharedSettings();
        }

        private void ApplyRiderCamera(float aim)
        {
            // Camera and look point both slide sideways, so the rider ends up left of the crosshair.
            Vector3 offset = _settings.ShoulderOffset * aim;
            _riderOrbit.Radius = Mathf.Lerp(_settings.Distance, _settings.AimDistance, aim);
            _riderOrbit.TargetOffset = offset;
            _riderComposer.TargetOffset = offset;
            _riderCamera.Lens.FieldOfView = Mathf.Lerp(_settings.FieldOfView, _settings.AimFieldOfView, aim);
        }

        private void ApplyRopeCamera()
        {
            bool holding = _rope.State == RopeState.Lassoed || _rope.State == RopeState.Taut;
            LassoTarget caught = holding ? _rope.Caught : null;
            bool frameCatch = caught != null && !_lasso.IsAiming && _mount.HasControl;

            FrameHead(frameCatch ? caught.Head : null);
            if (frameCatch)
                SwingBehindRider(caught.HeadPosition);

            _ropeOrbit.Radius = _settings.RopeDistance;
            InputAxis pitch = _ropeOrbit.VerticalAxis;
            pitch.Value = _settings.RopePitch;
            _ropeOrbit.VerticalAxis = pitch;
            _ropeCamera.Lens.FieldOfView = _settings.RopeFieldOfView;
            _ropeFraming.FramingSize = _settings.FramingSize;
            _ropeFraming.Damping = _settings.FramingDamping;
            _ropeCamera.Priority.Value = frameCatch ? RopeCameraLivePriority : 0;
        }

        private void ApplySharedSettings()
        {
            _brain.DefaultBlend.Time = _settings.BlendSeconds;
            foreach (CinemachineDecollider decollider in _decolliders)
            {
                decollider.CameraRadius = _settings.CameraRadius;
                decollider.Decollision.Damping = _settings.CollisionDamping;
            }
            foreach (CinemachineDeoccluder deoccluder in _deoccluders)
            {
                deoccluder.AvoidObstacles.CameraRadius = _settings.CameraRadius;
                deoccluder.AvoidObstacles.MinimumOcclusionTime = _settings.OcclusionWait;
                deoccluder.AvoidObstacles.DampingWhenOccluded = _settings.OccludedDamping;
                deoccluder.AvoidObstacles.Damping = _settings.UnoccludedDamping;
            }
        }

        private void FrameHead(Transform head)
        {
            if (head == _framedHead)
                return;

            if (_framedHead != null)
                _group.RemoveMember(_framedHead);
            if (head != null)
            {
                _group.AddMember(head, 1f, 1.5f);
                _swingVelocity = 0f;
            }
            _framedHead = head;
        }

        private void SwingBehindRider(Vector3 target)
        {
            // World-space orbit: a horizontal value of yaw puts the camera behind the rider facing that yaw.
            Vector3 toTarget = target - _riderTarget.position;
            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            InputAxis axis = _ropeOrbit.HorizontalAxis;
            float swung = Mathf.SmoothDampAngle(axis.Value, yaw, ref _swingVelocity, _settings.SwingSmoothTime);
            axis.Value = Mathf.DeltaAngle(0f, swung);
            _ropeOrbit.HorizontalAxis = axis;
        }
    }
}
