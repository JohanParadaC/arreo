using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// One rider as the herd sees it: where they are, how fast they move (m/s) and whether they
    /// are singing the canto (rule 4).
    /// </summary>
    public readonly struct RiderSample
    {
        public RiderSample(Vector3 position, float speed, bool singing = false)
        {
            Position = position;
            Speed = speed;
            Singing = singing;
        }

        public Vector3 Position { get; }
        public float Speed { get; }
        public bool Singing { get; }
    }
}
