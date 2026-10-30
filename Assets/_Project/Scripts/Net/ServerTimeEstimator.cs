using System.Collections.Generic;

namespace Arreo.Net
{
    /// <summary>
    /// Client-side estimate of the host's clock from snapshot timestamps: the least-delayed of
    /// the recent packets gives the offset. A link that stays slower is adopted once the old,
    /// faster samples leave the window.
    /// </summary>
    public sealed class ServerTimeEstimator
    {
        private readonly Queue<float> _offsets = new Queue<float>();
        private readonly int _windowSize;

        public ServerTimeEstimator(int windowSize) => _windowSize = windowSize;

        public bool IsReady => _offsets.Count > 0;

        public void Observe(float serverTime, float localReceiveTime)
        {
            _offsets.Enqueue(localReceiveTime - serverTime);
            while (_offsets.Count > _windowSize)
                _offsets.Dequeue();
        }

        public float ServerNow(float localTime)
        {
            float best = float.MaxValue;
            foreach (float offset in _offsets)
                if (offset < best)
                    best = offset;
            return localTime - best;
        }
    }
}
