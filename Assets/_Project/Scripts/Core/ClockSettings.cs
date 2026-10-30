using UnityEngine;

namespace Arreo.Core
{
    /// <summary>How long a game day lasts and when a new game starts (D14: a day is 12-20 real minutes).</summary>
    [CreateAssetMenu(menuName = "Arreo/Clock Settings", fileName = "ClockSettings")]
    public sealed class ClockSettings : ScriptableObject
    {
        [Tooltip("Real minutes per game day.")]
        [SerializeField, Range(1f, 60f)] private float _realMinutesPerDay = 15f;
        [Tooltip("Hour a new game starts at.")]
        [SerializeField, Range(0f, 23.99f)] private float _startHour = 6f;

        public float RealSecondsPerDay => _realMinutesPerDay * 60f;
        public float StartHour => _startHour;

        public GameClock CreateClock(int day, float hour) => new GameClock(RealSecondsPerDay, day, hour);

        public GameClock CreateNewGameClock() => CreateClock(1, _startHour);
    }
}
