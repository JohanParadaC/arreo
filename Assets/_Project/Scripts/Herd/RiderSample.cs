using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>One rider as the herd sees it: where they are and how fast they move (m/s).</summary>
    public readonly struct RiderSample
    {
        public RiderSample(Vector3 position, float speed)
        {
            Position = position;
            Speed = speed;
        }

        public Vector3 Position { get; }
        public float Speed { get; }
    }
}
