using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Timestamped herd snapshots. Clients sample it a little in the past to draw the herd
    /// smoothly between snapshots; the host samples it in the past to see the herd the way a
    /// lagging client saw it (lag compensation). Never extrapolates past the newest snapshot.
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

        public SnapshotBuffer(float keepSeconds) => _keepSeconds = keepSeconds;

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
                Copy(_frames[_frames.Count - 1], positions, yaws);
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
