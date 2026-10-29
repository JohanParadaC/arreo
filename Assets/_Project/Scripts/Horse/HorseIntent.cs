using UnityEngine;

namespace Arreo.Horse
{
    /// <summary>
    /// What the rider asks the horse to do this tick. The horse only ever sees this, never the
    /// keyboard, so the same value can later come from the network.
    /// </summary>
    public readonly struct HorseIntent
    {
        public static readonly HorseIntent None = default;

        public HorseIntent(float throttle, float steer, bool gallop, bool slowWalk = false)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Gallop = gallop;
            SlowWalk = slowWalk;
        }

        /// <summary>1 = forward, 0 = let go, -1 = brake and then back up.</summary>
        public float Throttle { get; }

        /// <summary>-1 = left, 1 = right.</summary>
        public float Steer { get; }

        public bool Gallop { get; }

        /// <summary>Held for the slow driving walk (arreo); a gallop overrides it.</summary>
        public bool SlowWalk { get; }
    }
}
