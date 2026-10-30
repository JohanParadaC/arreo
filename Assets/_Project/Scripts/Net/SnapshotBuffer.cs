using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Timestamped herd snapshots. Clients sample it a little in the past to draw the herd
    /// smoothly between snapshots; the host samples it in the past to see the herd the way a
    /// lagging client saw it (lag compensation). Past the newest snapshot it holds it, or, if allowed,
    /// carries the last motion on for a short while (a lost packet shouldn't freeze the herd).
    /// </summary>
    public sealed class SnapshotBuffer
    {
        private readonly struct Frame
        {
            public Frame(float time, Vector3[] positions, float[] yaws)
            {
                Time = time;
                Positions = positions;
                Yaws = yaws;
            }

            public float Time { get; }
            public Vector3[] Positions { get; }
            public float[] Yaws { get; }
        }

        private readonly List<Frame> _frames = new List<Frame>();
        private readonly float _keepSeconds;
        private readonly float _maxExtrapolation;

        /// <param name="maxExtrapolation">Seconds past the newest snapshot it may guess ahead (0 = never).</param>
        public SnapshotBuffer(float keepSeconds, float maxExtrapolation = 0f)
        {
            _keepSeconds = keepSeconds;
            _maxExtrapolation = maxExtrapolation;
        }

        public float OldestTime => _frames.Count > 0 ? _frames[0].Time : 0f;
        public float NewestTime => _frames.Count > 0 ? _frames[_frames.Count - 1].Time : 0f;
        public int Count => _frames.Count;

        public void Add(float time, IReadOnlyList<Vector3> positions, IReadOnlyList<float> yaws)
        {
            if (_frames.Count > 0 && time <= NewestTime)
                return;

            var copyPositions = new Vector3[positions.Count];
            var copyYaws = new float[yaws.Count];
            for (int i = 0; i < copyPositions.Length; i++)
                copyPositions[i] = positions[i];
            for (int i = 0; i < copyYaws.Length; i++)
                copyYaws[i] = yaws[i];
            _frames.Add(new Frame(time, copyPositions, copyYaws));

            while (_frames.Count > 1 && _frames[0].Time < time - _keepSeconds)
                _frames.RemoveAt(0);
        }

        public bool Sample(float time, List<Vector3> positions, List<float> yaws)
        {
            positions.Clear();
            yaws.Clear();
            if (_frames.Count == 0)
                return false;

            if (time <= _frames[0].Time || _frames.Count == 1)
            {
                Copy(_frames[0], positions, yaws);
                return true;
            }
            if (time >= NewestTime)
            {
                Frame last = _frames[_frames.Count - 1];
                Copy(last, positions, yaws);
                float ahead = Mathf.Min(time - last.Time, _maxExtrapolation);
                if (ahead > 0f)
                {
                    Frame before = _frames[_frames.Count - 2];
                    float scale = ahead / (last.Time - before.Time);
                    int moving = Mathf.Min(before.Positions.Length, positions.Count);
                    for (int i = 0; i < moving; i++)
                        positions[i] += (last.Positions[i] - before.Positions[i]) * scale;
                }
                return true;
            }

            int next = 1;
            while (_frames[next].Time < time)
                next++;
            Frame a = _frames[next - 1];
            Frame b = _frames[next];
            float t = (time - a.Time) / (b.Time - a.Time);
            int count = Mathf.Min(a.Positions.Length, b.Positions.Length);
            for (int i = 0; i < count; i++)
            {
                positions.Add(Vector3.Lerp(a.Positions[i], b.Positions[i], t));
                yaws.Add(Mathf.LerpAngle(a.Yaws[i], b.Yaws[i], t));
            }
            return true;
        }

        private static void Copy(Frame frame, List<Vector3> positions, List<float> yaws)
        {
            positions.AddRange(frame.Positions);
            yaws.AddRange(frame.Yaws);
        }
    }
}
