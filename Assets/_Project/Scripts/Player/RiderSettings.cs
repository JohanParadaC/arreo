using UnityEngine;

namespace Arreo.Player
{
    /// <summary>Tuning values for the rider being thrown off the horse.</summary>
    [CreateAssetMenu(menuName = "Arreo/Rider Settings", fileName = "RiderSettings")]
    public sealed class RiderSettings : ScriptableObject
    {
        [Header("Fall")]
        [Tooltip("Only a rope snapping at or above this tension (N) throws the rider off. A stampeding cow pulls about 3600 N; galloping away from a caught cow peaks around 5000 N.")]
        [SerializeField, Min(0f)] private float _fallTension = 4000f;
        [Tooltip("Launch speed (m/s) when the rope snaps right at the fall tension.")]
        [SerializeField, Min(0f)] private float _launchSpeed = 6f;
        [Tooltip("Extra launch speed (m/s) per newton above the fall tension. Higher = harder snaps throw much farther.")]
        [SerializeField, Min(0f)] private float _launchSpeedPerNewton = 0.002f;
        [SerializeField, Min(0f)] private float _maxLaunchSpeed = 14f;
        [Tooltip("Degrees above the ground the rider flies off at. Higher = more air time, lands closer.")]
        [SerializeField, Range(0f, 80f)] private float _launchAngle = 35f;
        [Tooltip("Spin (rad/s) given to the body when thrown. Higher = more tumbling.")]
        [SerializeField, Min(0f)] private float _tumbleSpin = 8f;
        [Tooltip("Seconds lying on the ground before getting up.")]
        [SerializeField, Min(0f)] private float _getUpSeconds = 2.2f;
        [Tooltip("Seconds to walk back and climb into the saddle.")]
        [SerializeField, Min(0.05f)] private float _remountSeconds = 0.8f;

        public float TumbleSpin => _tumbleSpin;

        public RiderFall CreateFall() => new RiderFall(_fallTension, _launchSpeed, _launchSpeedPerNewton, _maxLaunchSpeed,
            _launchAngle, _getUpSeconds, _remountSeconds);
    }
}
