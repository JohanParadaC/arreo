using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// Camera tuning for the prototype: rider camera, over-the-shoulder aim, rope framing, blends
    /// and collision. Lives in an asset so rebuilding the scene keeps it. Most values apply live.
    /// </summary>
    [CreateAssetMenu(menuName = "Arreo/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Rider camera")]
        [Tooltip("Distance from the rider (m).")]
        [SerializeField, Min(1f)] private float _distance = 7f;
        [SerializeField, Range(20f, 100f)] private float _fieldOfView = 65f;
        [Tooltip("Starting angle above the horizon (degrees). Applied when the scene is rebuilt.")]
        [SerializeField] private float _startPitch = 12f;
        [Tooltip("Lowest and highest angle the mouse can tilt to (degrees). Applied when the scene is rebuilt.")]
        [SerializeField] private Vector2 _pitchRange = new Vector2(-10f, 50f);
        [Tooltip("How much the camera lags behind the horse (s per axis: sideways, up, forward). Applied when the scene is rebuilt.")]
        [SerializeField] private Vector3 _positionDamping = new Vector3(0.2f, 0.4f, 0.3f);
        [Tooltip("How much the camera lags when the horse turns (s). Applied when the scene is rebuilt.")]
        [SerializeField, Min(0f)] private float _turnDamping = 0.5f;
        [Tooltip("Seconds without mouse input before the camera swings back behind the horse. Applied when the scene is rebuilt.")]
        [SerializeField, Min(0f)] private float _recenterWait = 1f;
        [Tooltip("Seconds the swing back behind the horse takes. Applied when the scene is rebuilt.")]
        [SerializeField, Min(0f)] private float _recenterTime = 0.5f;

        [Header("Aim (over the shoulder)")]
        [Tooltip("Seconds to move over the shoulder when aiming starts (and back when it ends).")]
        [SerializeField, Min(0f)] private float _aimBlendSeconds = 0.2f;
        [Tooltip("Distance from the rider while aiming (m). Closer than the normal distance, like a shooter, but far enough that the rider fills only about a quarter of the screen.")]
        [SerializeField, Min(0.5f)] private float _aimDistance = 3.5f;
        [Tooltip("Where the camera sits relative to the rider while aiming (m): x = to the right, y = up. Bigger x moves the rider further left so they don't cover the crosshair.")]
        [SerializeField] private Vector3 _shoulderOffset = new Vector3(1.6f, 0.35f, 0f);
        [Tooltip("Field of view while aiming. A bit narrower than the normal one reads as a zoom without making the rider fill the screen.")]
        [SerializeField, Range(20f, 100f)] private float _aimFieldOfView = 52f;

        [Header("Rope camera (something caught)")]
        [Tooltip("Distance from the rider (m) before zooming to fit the catch.")]
        [SerializeField, Min(1f)] private float _ropeDistance = 9f;
        [Tooltip("Angle above the horizon (degrees).")]
        [SerializeField] private float _ropePitch = 20f;
        [SerializeField, Range(20f, 100f)] private float _ropeFieldOfView = 60f;
        [Tooltip("Share of the screen the rider and the catch should fill together (0-1).")]
        [SerializeField, Range(0.1f, 1f)] private float _framingSize = 0.6f;
        [Tooltip("How gradually the zoom follows the catch (s).")]
        [SerializeField, Min(0f)] private float _framingDamping = 1f;
        [Tooltip("Seconds the camera takes to swing round to keep the catch in front.")]
        [SerializeField, Min(0f)] private float _swingSmoothTime = 0.35f;
        [Tooltip("How much the rope camera lags behind the rider (s). Applied when the scene is rebuilt.")]
        [SerializeField, Min(0f)] private float _ropePositionDamping = 0.3f;

        [Header("Transitions")]
        [Tooltip("Seconds the fade between the rider camera and the rope camera takes.")]
        [SerializeField, Min(0f)] private float _blendSeconds = 0.4f;

        [Header("Collision")]
        [Tooltip("How far the camera keeps from the horse, cattle, obstacles and ground (m).")]
        [SerializeField, Min(0.05f)] private float _cameraRadius = 0.4f;
        [Tooltip("How gradually the camera returns after being pushed out of something (s).")]
        [SerializeField, Min(0f)] private float _collisionDamping = 0.3f;

        public float Distance => _distance;
        public float FieldOfView => _fieldOfView;
        public float StartPitch => _startPitch;
        public Vector2 PitchRange => _pitchRange;
        public Vector3 PositionDamping => _positionDamping;
        public float TurnDamping => _turnDamping;
        public float RecenterWait => _recenterWait;
        public float RecenterTime => _recenterTime;
        public float AimBlendSeconds => _aimBlendSeconds;
        public float AimDistance => _aimDistance;
        public Vector3 ShoulderOffset => _shoulderOffset;
        public float AimFieldOfView => _aimFieldOfView;
        public float RopeDistance => _ropeDistance;
        public float RopePitch => _ropePitch;
        public float RopeFieldOfView => _ropeFieldOfView;
        public float FramingSize => _framingSize;
        public float FramingDamping => _framingDamping;
        public float SwingSmoothTime => _swingSmoothTime;
        public float RopePositionDamping => _ropePositionDamping;
        public float BlendSeconds => _blendSeconds;
        public float CameraRadius => _cameraRadius;
        public float CollisionDamping => _collisionDamping;
    }
}
