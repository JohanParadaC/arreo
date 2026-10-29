using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>Tuning values for how a horse feels to ride. One asset per breed later on.</summary>
    [CreateAssetMenu(menuName = "Arreo/Horse Settings", fileName = "HorseSettings")]
    public sealed class HorseSettings : ScriptableObject
    {
        [Header("Speed (m/s)")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 4f;
        [SerializeField, Min(0.1f)] private float _gallopSpeed = 12f;
        [SerializeField, Min(0f)] private float _backSpeed = 1.5f;
        [Tooltip("Slow driving walk (arreo) while the slow-walk key is held. Keep it close to the herd's flight-zone speed so you push cattle without running into them.")]
        [SerializeField, Min(0.1f)] private float _driveSpeed = 2f;

        [Header("Inertia (m/s per second)")]
        [Tooltip("How fast it gains speed.")]
        [SerializeField, Min(0.1f)] private float _acceleration = 3.5f;
        [Tooltip("How fast it slows down when you let go.")]
        [SerializeField, Min(0.1f)] private float _coastDeceleration = 2.5f;
        [Tooltip("How fast it slows down when you pull back.")]
        [SerializeField, Min(0.1f)] private float _braking = 9f;

        [Header("Turning (degrees per second)")]
        [SerializeField, Min(0f)] private float _turnRateStanding = 110f;
        [SerializeField, Min(0f)] private float _turnRateAtGallop = 40f;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float _maxStamina = 100f;
        [Tooltip("Stamina lost per second at a gallop. Max / drain = seconds of gallop.")]
        [SerializeField, Min(0f)] private float _staminaDrainPerSecond = 7f;
        [Tooltip("Stamina regained per second when not galloping.")]
        [SerializeField, Min(0f)] private float _staminaRecoveryPerSecond = 12f;
        [Tooltip("Fraction of stamina needed to gallop again after running out.")]
        [SerializeField, Range(0.05f, 1f)] private float _recoveryThreshold = 0.35f;

        public HorseLocomotion CreateLocomotion(float heading) => new HorseLocomotion(
            new HorseGait(_walkSpeed, _gallopSpeed, _backSpeed, _acceleration, _coastDeceleration,
                _braking, _turnRateStanding, _turnRateAtGallop, _driveSpeed),
            new HorseStamina(_maxStamina, _staminaDrainPerSecond, _staminaRecoveryPerSecond, _recoveryThreshold),
            heading);
    }
}
