using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// Rope as a chain of points moved by verlet integration. Links only stop two points from
    /// getting farther apart than one segment, so the rope can go slack but not stretch. Both ends
    /// are pinned to where the rope is tied; the ground is a floor the points can't go below.
    /// Plain C#, no Rigidbodies: it can't jitter against heavy bodies the way jointed segments did.
    /// </summary>
    public sealed class VerletRope
    {
        private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
        // Share of sideways motion a point keeps per step while it lies on the ground.
        private const float GroundFriction = 0.5f;

        private readonly Vector3[] _points;
        private readonly Vector3[] _previous;
        private readonly bool[] _onGround;
        private readonly int _iterations;
        private readonly float _damping;

        /// <param name="damping">Share of each point's velocity lost per step (air drag), 0 to 1.</param>
        public VerletRope(int pointCount, int iterations, float damping)
        {
            _points = new Vector3[Mathf.Max(2, pointCount)];
            _previous = new Vector3[_points.Length];
            _onGround = new bool[_points.Length];
            _iterations = Mathf.Max(1, iterations);
            _damping = Mathf.Clamp01(damping);
        }

        public IReadOnlyList<Vector3> Points => _points;
        public float SegmentLength { get; private set; }

        public void Reset(Vector3 start, Vector3 end, float restLength)
        {
            SegmentLength = restLength / (_points.Length - 1);
            for (int i = 0; i < _points.Length; i++)
                _points[i] = _previous[i] = Vector3.Lerp(start, end, i / (_points.Length - 1f));
        }

        public void Step(Vector3 start, Vector3 end, float deltaTime, float groundHeight)
        {
            int last = _points.Length - 1;
            _points[0] = _previous[0] = start;
            _points[last] = _previous[last] = end;

            Vector3 gravityStep = Gravity * (deltaTime * deltaTime);
            for (int i = 1; i < last; i++)
            {
                Vector3 velocity = (_points[i] - _previous[i]) * (1f - _damping);
                _previous[i] = _points[i];
                _points[i] += velocity + gravityStep;
                _onGround[i] = false;
            }

            for (int iteration = 0; iteration < _iterations; iteration++)
            {
                for (int i = 0; i < last; i++)
                    SatisfyLink(i, i + 1, last);
                for (int i = 1; i < last; i++)
                    KeepAboveGround(i, groundHeight);
            }

            // Ground contact is settled once, after the links: resetting velocity inside the loop
            // let a link lift the point a few millimetres afterwards, which read as a hop next step.
            for (int i = 1; i < last; i++)
            {
                if (!_onGround[i])
                    continue;
                _previous[i].y = _points[i].y;
                _previous[i].x = Mathf.Lerp(_points[i].x, _previous[i].x, GroundFriction);
                _previous[i].z = Mathf.Lerp(_points[i].z, _previous[i].z, GroundFriction);
            }
        }

        private void SatisfyLink(int a, int b, int last)
        {
            Vector3 delta = _points[b] - _points[a];
            float distance = delta.magnitude;
            if (distance <= SegmentLength || distance < 0.000001f)
                return;

            // Pinned ends don't move; the free point takes the whole correction.
            float weightA = a == 0 ? 0f : 1f;
            float weightB = b == last ? 0f : 1f;
            float totalWeight = weightA + weightB;
            if (totalWeight == 0f)
                return;

            Vector3 correction = delta * ((distance - SegmentLength) / distance / totalWeight);
            _points[a] += correction * weightA;
            _points[b] -= correction * weightB;
        }

        private void KeepAboveGround(int i, float groundHeight)
        {
            if (_points[i].y > groundHeight)
                return;

            _points[i].y = groundHeight;
            _onGround[i] = true;
        }
    }
}
