using System.Collections.Generic;

namespace Arreo.Core
{
    /// <summary>Frame times of a measured run: average FPS, percentiles and how many frames missed a budget.</summary>
    public sealed class FrameTimeStats
    {
        private readonly List<float> _milliseconds = new List<float>();
        private readonly List<float> _sorted = new List<float>();
        private double _total;
        private bool _sortedIsCurrent;

        public int Count => _milliseconds.Count;

        /// <summary>Frames per second over the whole run (frames / total time), not the average of each frame's FPS.</summary>
        public float AverageFps => _total > 0d ? (float)(Count / (_total / 1000d)) : 0f;

        public float Max => Count > 0 ? Sorted()[Count - 1] : 0f;

        public void Add(float milliseconds)
        {
            _milliseconds.Add(milliseconds);
            _total += milliseconds;
            _sortedIsCurrent = false;
        }

        /// <summary>Frame time (ms) that this fraction of frames stays under, by nearest rank (0.99 = the 99th of 100).</summary>
        public float Percentile(float fraction)
        {
            if (Count == 0)
                return 0f;
            // 0.99f is a hair above 0.99, so without the nudge 0.99 x 100 would round up to rank 100.
            int rank = (int)System.Math.Ceiling((double)fraction * Count - 1e-4);
            return Sorted()[UnityEngine.Mathf.Clamp(rank - 1, 0, Count - 1)];
        }

        /// <summary>Fraction (0-1) of frames that took longer than <paramref name="milliseconds"/>.</summary>
        public float ShareSlowerThan(float milliseconds)
        {
            if (Count == 0)
                return 0f;
            int slow = 0;
            foreach (float ms in _milliseconds)
                if (ms > milliseconds)
                    slow++;
            return (float)slow / Count;
        }

        private List<float> Sorted()
        {
            if (!_sortedIsCurrent)
            {
                _sorted.Clear();
                _sorted.AddRange(_milliseconds);
                _sorted.Sort();
                _sortedIsCurrent = true;
            }
            return _sorted;
        }
    }
}
