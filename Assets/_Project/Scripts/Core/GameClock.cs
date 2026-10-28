using System;

namespace Arreo.Core
{
    /// <summary>
    /// In-game day and time of day. Plain C# so it can be tested without a scene;
    /// the host owns the authoritative instance and advances it.
    /// </summary>
    public sealed class GameClock
    {
        public const float HoursPerDay = 24f;

        private readonly float _gameHoursPerRealSecond;
        private int _day;
        private float _hour;

        /// <summary>Raised once for every new day reached, with that day's number.</summary>
        public event Action<int> DayStarted;

        public GameClock(float realSecondsPerDay, int startDay = 1, float startHour = 0f)
        {
            if (realSecondsPerDay <= 0f)
                throw new ArgumentOutOfRangeException(nameof(realSecondsPerDay), realSecondsPerDay, "Must be greater than zero.");
            if (startDay < 1)
                throw new ArgumentOutOfRangeException(nameof(startDay), startDay, "Days start at 1.");
            if (startHour < 0f || startHour >= HoursPerDay)
                throw new ArgumentOutOfRangeException(nameof(startHour), startHour, "Must be in [0, 24).");

            _gameHoursPerRealSecond = HoursPerDay / realSecondsPerDay;
            _day = startDay;
            _hour = startHour;
        }

        public int Day => _day;

        /// <summary>Hour of the current day, in [0, 24).</summary>
        public float Hour => _hour;

        /// <summary>Hour mapped to [0, 1), handy for driving the sun and lighting.</summary>
        public float DayProgress => _hour / HoursPerDay;

        public void Advance(float realSeconds)
        {
            if (realSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(realSeconds), realSeconds, "Time cannot go backwards.");

            _hour += realSeconds * _gameHoursPerRealSecond;

            while (_hour >= HoursPerDay)
            {
                _hour -= HoursPerDay;
                _day++;
                DayStarted?.Invoke(_day);
            }
        }
    }
}
