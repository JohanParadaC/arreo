using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Measures how smooth a remote horse looks from positions sampled every frame: jumps
    /// (faster than a horse could move) and stalls (frozen for a few frames mid-run).
    /// </summary>
    public static class MotionSmoothness
    {
        // Below this a frame counts as not moving (m/s).
        private const float StillSpeed = 0.05f;
        // A stall only counts if the horse was actually moving just before (m/s).
        private const float MovingSpeed = 1f;
        private const int MinStallFrames = 2;

        public readonly struct Report
        {
            public Report(int jumps, int stalls, float maxSpeed, float maxSpeedChange)
            {
                Jumps = jumps;
                Stalls = stalls;
                MaxSpeed = maxSpeed;
                MaxSpeedChange = maxSpeedChange;
            }

            public int Jumps { get; }
            public int Stalls { get; }
            public float MaxSpeed { get; }

            /// <summary>Biggest change in speed between two frames (m/s).</summary>
            public float MaxSpeedChange { get; }
        }

        /// <summary>
        /// Uses the real time of each sample: frames vary in length, and dividing by an average
        /// frame time turned every slow frame into a false "jump".
        /// </summary>
        public static Report Analyze(IReadOnlyList<Vector3> samples, IReadOnlyList<float> times, float maxPlausibleSpeed) =>
            Analyze(samples, i => times[i] - times[i - 1], maxPlausibleSpeed);

        public static Report Analyze(IReadOnlyList<Vector3> samples, float frameSeconds, float maxPlausibleSpeed) =>
            Analyze(samples, _ => frameSeconds, maxPlausibleSpeed);

        private static Report Analyze(IReadOnlyList<Vector3> samples, System.Func<int, float> secondsBefore, float maxPlausibleSpeed)
        {
            int jumps = 0;
            int stalls = 0;
            float maxSpeed = 0f;
            float maxSpeedChange = 0f;
            float previousSpeed = -1f;
            float speedBeforeStill = 0f;
            int stillFrames = 0;

            for (int i = 1; i < samples.Count; i++)
            {
                float seconds = secondsBefore(i);
                if (seconds <= 0f)
                    continue;
                float speed = Vector3.Distance(samples[i - 1], samples[i]) / seconds;

                if (speed > maxPlausibleSpeed)
                    jumps++;
                else
                    maxSpeed = Mathf.Max(maxSpeed, speed);
                if (previousSpeed >= 0f)
                    maxSpeedChange = Mathf.Max(maxSpeedChange, Mathf.Abs(speed - previousSpeed));

                if (speed < StillSpeed)
                {
                    if (stillFrames == 0)
                        speedBeforeStill = Mathf.Max(previousSpeed, 0f);
                    stillFrames++;
                }
                else
                {
                    if (stillFrames >= MinStallFrames && speedBeforeStill > MovingSpeed)
                        stalls++;
                    stillFrames = 0;
                }
                previousSpeed = speed;
            }

            return new Report(jumps, stalls, maxSpeed, maxSpeedChange);
        }
    }
}
