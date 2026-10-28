using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// Classic boids rules on the ground plane (y is ignored). Each returns a vector for the
    /// agent at <c>self</c>; neighbors are the other agents within <c>radius</c>.
    /// </summary>
    public static class BoidRules
    {
        /// <summary>Push away from neighbors that are too close, stronger the closer they are.</summary>
        public static Vector3 Separation(int self, IReadOnlyList<Vector3> positions, float radius)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == self)
                    continue;

                Vector3 away = Flatten(positions[self] - positions[i]);
                float distance = away.magnitude;
                if (distance < 0.0001f || distance >= radius)
                    continue;

                push += away / distance * (1f - distance / radius);
            }
            return push;
        }

        /// <summary>Average velocity of the neighbors.</summary>
        public static Vector3 Alignment(int self, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> velocities, float radius)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == self || Flatten(positions[i] - positions[self]).sqrMagnitude > radius * radius)
                    continue;

                sum += Flatten(velocities[i]);
                count++;
            }
            return count == 0 ? Vector3.zero : sum / count;
        }

        /// <summary>Offset from this agent to the center of its neighbors.</summary>
        public static Vector3 Cohesion(int self, IReadOnlyList<Vector3> positions, float radius)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == self || Flatten(positions[i] - positions[self]).sqrMagnitude > radius * radius)
                    continue;

                sum += positions[i];
                count++;
            }
            return count == 0 ? Vector3.zero : Flatten(sum / count - positions[self]);
        }

        internal static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
