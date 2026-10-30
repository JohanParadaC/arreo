using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>Rectangle on the ground (rotated by yaw) that counts as inside the corral.</summary>
    public sealed class CorralArea
    {
        private readonly Vector3 _center;
        private readonly Vector2 _halfSize;
        private readonly Quaternion _toLocal;

        /// <param name="size">Width along the corral's local x and depth along its local z, in meters.</param>
        public CorralArea(Vector3 center, Vector2 size, float yawDegrees)
        {
            _center = center;
            _halfSize = size / 2f;
            _toLocal = Quaternion.Euler(0f, -yawDegrees, 0f);
        }

        public bool Contains(Vector3 point)
        {
            Vector3 local = _toLocal * (point - _center);
            return Mathf.Abs(local.x) <= _halfSize.x && Mathf.Abs(local.z) <= _halfSize.y;
        }

        public int Count(IReadOnlyList<Vector3> points)
        {
            int count = 0;
            for (int i = 0; i < points.Count; i++)
                if (Contains(points[i]))
                    count++;
            return count;
        }

        /// <summary>Like <see cref="Count"/>, skipping points whose <paramref name="present"/> flag is off (sold cows).</summary>
        public int CountPresent(IReadOnlyList<Vector3> points, IReadOnlyList<bool> present)
        {
            int count = 0;
            for (int i = 0; i < points.Count; i++)
                if (present[i] && Contains(points[i]))
                    count++;
            return count;
        }

        /// <summary>Fills <paramref name="inside"/> with the indices of present points inside, and returns how many.</summary>
        public int CollectInside(IReadOnlyList<Vector3> points, IReadOnlyList<bool> present, List<int> inside)
        {
            inside.Clear();
            for (int i = 0; i < points.Count; i++)
                if (present[i] && Contains(points[i]))
                    inside.Add(i);
            return inside.Count;
        }
    }
}
