using System;
using Arreo.Core;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// The game's day and hour on the network (rule 5: the clock lives in the host). The host
    /// advances its <see cref="GameClock"/> and sends the day when it changes and the hour about
    /// once a second; clients run the hour on between updates so it doesn't tick in jumps.
    /// </summary>
    public sealed class NetworkClock : NetworkBehaviour
    {
        [SerializeField] private ClockSettings _settings;
        [Tooltip("How often (s) the host sends the hour.")]
        [SerializeField, Min(0.1f)] private float _syncInterval = 1f;

        private readonly SyncVar<int> _day = new SyncVar<int>(1);
        private readonly SyncVar<float> _hour = new SyncVar<float>();
        private GameClock _clock;
        private float _nextSync;
        private float _hourReceivedAt;

        /// <summary>The clock on this machine (at most one per scene).</summary>
        public static NetworkClock Instance { get; private set; }

        /// <summary>Host: raised when a new game day starts, with its number.</summary>
        public event Action<int> ServerDayStarted;

        public int Day => IsServerInitialized && _clock != null ? _clock.Day : _day.Value;

        /// <summary>Hour of the day, [0, 24).</summary>
        public float Hour
        {
            get
            {
                if (IsServerInitialized && _clock != null)
                    return _clock.Hour;
                float sinceUpdate = Mathf.Min(Time.time - _hourReceivedAt, _syncInterval * 2f);
                return Mathf.Repeat(_hour.Value + sinceUpdate * GameClock.HoursPerDay / _settings.RealSecondsPerDay, GameClock.HoursPerDay);
            }
        }

        private void Awake() => _hour.OnChange += (_, _, _) => _hourReceivedAt = Time.time;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Use(_settings.CreateNewGameClock());
        }

        /// <summary>Host: sets the day and hour (a loaded game, or a test).</summary>
        [Server]
        public void ServerSet(int day, float hour) => Use(_settings.CreateClock(day, hour));

        private void Use(GameClock clock)
        {
            if (_clock != null)
                _clock.DayStarted -= OnDayStarted;
            _clock = clock;
            _clock.DayStarted += OnDayStarted;
            _day.Value = clock.Day;
            _hour.Value = clock.Hour;
            _nextSync = Time.time + _syncInterval;
        }

        private void OnDayStarted(int day)
        {
            _day.Value = day;
            ServerDayStarted?.Invoke(day);
        }

        private void Update()
        {
            if (!IsServerInitialized || _clock == null)
                return;
            _clock.Advance(Time.deltaTime);
            if (Time.time >= _nextSync)
            {
                _nextSync = Time.time + _syncInterval;
                _hour.Value = _clock.Hour;
            }
        }
    }
}
