using Arreo.Horse;
using Arreo.Rope;
using UnityEngine;

namespace Arreo.Player
{
    /// <summary>
    /// Keeps the rider on the horse, or throws them off as a ragdoll when the rope snaps hard
    /// enough. While down there's no control and the horse pulls up; after a few seconds the
    /// rider gets up and climbs back on. From week 6 the host decides the fall.
    /// </summary>
    public sealed class RiderMount : MonoBehaviour
    {
        [SerializeField] private RiderSettings _settings;
        [SerializeField] private HorseController _horse;
        [SerializeField] private RopeSimulation _rope;
        [Tooltip("The rider model sitting on the horse; hidden while the ragdoll flies.")]
        [SerializeField] private Transform _riderModel;
        [Tooltip("Where the rider's torso sits, in the horse's local space (the ragdoll starts there).")]
        [SerializeField] private Vector3 _torsoOffset = new Vector3(0f, 2.25f, 0.1f);
        [SerializeField] private Material _clothes;
        [SerializeField] private Material _skin;

        private RiderFall _fall;
        private RiderRagdoll _ragdoll;
        private Rigidbody _horseBody;
        private Vector3 _modelLocalPosition;
        private Vector3 _getUpPoint;

        public bool HasControl => _fall == null || _fall.HasControl;
        public RiderState State => _fall != null ? _fall.State : RiderState.Mounted;

        private void Awake()
        {
            if (_settings != null)
                Initialize();
        }

        /// <summary>Sets up a rider created from code.</summary>
        public void Configure(RiderSettings settings, HorseController horse, RopeSimulation rope, Transform riderModel,
            Material clothes, Material skin)
        {
            _settings = settings;
            _horse = horse;
            _rope = rope;
            _riderModel = riderModel;
            _clothes = clothes;
            _skin = skin;
            Initialize();
        }

        private void Initialize()
        {
            _fall = _settings.CreateFall();
            _horseBody = _horse.GetComponent<Rigidbody>();
            _modelLocalPosition = _riderModel.localPosition;
            _rope.Snapped -= OnRopeSnapped;
            _rope.Snapped += OnRopeSnapped;
        }

        private void OnDestroy()
        {
            if (_rope != null)
                _rope.Snapped -= OnRopeSnapped;
        }

        private void OnRopeSnapped(float snapTension, Vector3 pullDirection)
        {
            if (!_fall.TryFall(snapTension, pullDirection, out Vector3 launch))
                return;

            Vector3 torso = _horse.transform.TransformPoint(_torsoOffset);
            _ragdoll = RiderRagdoll.Spawn(torso, _horse.transform.rotation, launch + _horseBody.linearVelocity,
                _settings.TumbleSpin, _clothes, _skin, _horse.GetComponentsInChildren<Collider>());
            // Same tag as the horse, so whatever sees through this player (the camera) sees through the ragdoll too.
            foreach (Collider part in _ragdoll.GetComponentsInChildren<Collider>())
                part.tag = _horse.tag;
            _riderModel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_fall == null)
                return;

            RiderState before = _fall.State;
            _fall.Tick(Time.deltaTime);
            RiderState now = _fall.State;

            if (before == RiderState.Fallen && now == RiderState.Remounting)
                GetUp();
            if (now == RiderState.Remounting)
                WalkBackToTheSaddle(_fall.RemountProgress);
            if (before == RiderState.Remounting && now == RiderState.Mounted)
                _riderModel.localPosition = _modelLocalPosition;

            if (!HasControl)
                PullUpTheHorse();
        }

        private void GetUp()
        {
            _getUpPoint = _ragdoll != null ? _ragdoll.Torso.position : _horse.transform.TransformPoint(_torsoOffset);
            if (_ragdoll != null)
                Destroy(_ragdoll.gameObject);
            _riderModel.gameObject.SetActive(true);
        }

        private void WalkBackToTheSaddle(float progress)
        {
            // Model placed so its torso stands upright where the ragdoll landed, then arcs onto the horse.
            Vector3 standing = _getUpPoint + Vector3.up * 1.1f - _horse.transform.rotation * _torsoOffset;
            Vector3 seat = _horse.transform.TransformPoint(_modelLocalPosition);
            _riderModel.position = Vector3.Lerp(standing, seat, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.8f);
        }

        private void PullUpTheHorse()
        {
            HorseLocomotion locomotion = _horse.Locomotion;
            bool stillMoving = locomotion != null && locomotion.Speed > 0.1f;
            _horse.SetIntent(stillMoving ? new HorseIntent(-1f, 0f, gallop: false) : HorseIntent.None);
        }
    }
}
