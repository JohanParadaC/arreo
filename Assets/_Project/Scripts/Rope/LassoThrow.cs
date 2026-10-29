using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>Throw geometry: range, the loop's arc and whether it lands over a head.</summary>
    public static class LassoThrow
    {
        /// <summary>A target beyond reach falls short at max range (measured along the ground).</summary>
        public static Vector3 ClampToRange(Vector3 origin, Vector3 target, float maxRange)
        {
            Vector3 flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            if (flat.magnitude <= maxRange)
                return target;

            Vector3 reach = flat.normalized * maxRange;
            return new Vector3(origin.x + reach.x, target.y, origin.z + reach.z);
        }

        /// <summary>Point along a parabolic arc that peaks <c>apexHeight</c> above the straight line.</summary>
        public static Vector3 ArcPoint(Vector3 start, Vector3 end, float apexHeight, float t) =>
            Vector3.Lerp(start, end, t) + Vector3.up * (4f * apexHeight * t * (1f - t));

        public static float FlightTime(float distance, float throwSpeed, float minTime) => Mathf.Max(distance / throwSpeed, minTime);

        /// <summary>Index of the head closest to where the loop landed (along the ground), or -1 if none is close enough.</summary>
        public static int FindCatch(Vector3 landing, IReadOnlyList<Vector3> heads, float catchRadius)
        {
            int best = -1;
            float bestSqr = catchRadius * catchRadius;
            for (int i = 0; i < heads.Count; i++)
            {
                float dx = heads[i].x - landing.x;
                float dz = heads[i].z - landing.z;
                float sqr = dx * dx + dz * dz;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }
    }
}
