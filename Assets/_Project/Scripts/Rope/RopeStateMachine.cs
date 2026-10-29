using UnityEngine;

namespace Arreo.Rope
{
    public enum RopeState
    {
        /// <summary>Ready on the saddle.</summary>
        Coiled,
        /// <summary>The loop is flying.</summary>
        Throwing,
        /// <summary>A cow is caught and the rope is slack.</summary>
        Lassoed,
        /// <summary>A cow is caught and pulling.</summary>
        Taut,
        /// <summary>Let go or missed; the rope is being coiled back.</summary>
        Released,
        /// <summary>Snapped; a new rope is ready after a moment.</summary>
        Broken,
    }

    /// <summary>
    /// Rope life cycle. Too much tension for too long snaps it: the break clock only runs while
    /// tension stays above the limit and resets as soon as it drops below.
    /// </summary>
    public sealed class RopeStateMachine
    {
        private readonly float _tautTension;
        private readonly float _breakTension;
        private readonly float _breakSeconds;
        private readonly float _retractSeconds;
        private readonly float _brokenSeconds;
        private float _breakTimer;
        private float _strainPeak;
        private float _stateTime;

        public RopeStateMachine(float tautTension, float breakTension, float breakSeconds, float retractSeconds, float brokenSeconds)
        {
            _tautTension = tautTension;
            _breakTension = breakTension;
            _breakSeconds = breakSeconds;
            _retractSeconds = retractSeconds;
            _brokenSeconds = brokenSeconds;
        }

        public RopeState State { get; private set; } = RopeState.Coiled;

        /// <summary>How close the rope is to snapping, 0 to 1.</summary>
        public float BreakProgress => Mathf.Clamp01(_breakTimer / _breakSeconds);

        public bool IsHoldingCow => State == RopeState.Lassoed || State == RopeState.Taut;

        /// <summary>Highest tension (N) during the strain that last snapped the rope.</summary>
        public float SnapTension { get; private set; }

        public bool Throw()
        {
            if (State != RopeState.Coiled)
                return false;
            Enter(RopeState.Throwing);
            return true;
        }

        public void Land(bool caught)
        {
            if (State == RopeState.Throwing)
                Enter(caught ? RopeState.Lassoed : RopeState.Released);
        }

        public void Release()
        {
            if (State == RopeState.Throwing || IsHoldingCow)
                Enter(RopeState.Released);
        }

        public void Tick(float deltaTime, float tension)
        {
            _stateTime += deltaTime;
            switch (State)
            {
                case RopeState.Lassoed:
                case RopeState.Taut:
                    State = tension >= _tautTension ? RopeState.Taut : RopeState.Lassoed;
                    bool straining = tension >= _breakTension;
                    _breakTimer = straining ? _breakTimer + deltaTime : 0f;
                    _strainPeak = straining ? Mathf.Max(_strainPeak, tension) : 0f;
                    if (_breakTimer >= _breakSeconds)
                    {
                        SnapTension = _strainPeak;
                        Enter(RopeState.Broken);
                    }
                    break;
                case RopeState.Released when _stateTime >= _retractSeconds:
                case RopeState.Broken when _stateTime >= _brokenSeconds:
                    Enter(RopeState.Coiled);
                    break;
            }
        }

        private void Enter(RopeState state)
        {
            State = state;
            _stateTime = 0f;
            _breakTimer = 0f;
            _strainPeak = 0f;
        }
    }
}
