using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// Host-side check of a client's throw. What the client sees is ~100 ms in the past, so a
    /// cow the client lassoed may already have moved on the host. The claimed cow counts if it
    /// was under the loop where the client saw it (host rewound to the client's view), or within
    /// an extra margin of where it is now. Otherwise whatever is under the loop now is caught.
    /// </summary>
    public static class LassoValidator
    {
        /// <param name="claimed">Index (in <paramref name="currentHeads"/>) of the cow the client aimed at, or -1.</param>
        /// <param name="hasClaimedView">Whether <paramref name="claimedHeadAsSeen"/> (the rewound head) is known.</param>
        /// <returns>Index of the caught cow, or -1 for a miss.</returns>
        public static int ResolveCatch(Vector3 landing, float radius, float margin, int claimed, bool hasClaimedView,
            Vector3 claimedHeadAsSeen, IReadOnlyList<Vector3> currentHeads)
        {
            if (claimed >= 0 && claimed < currentHeads.Count)
            {
                if (hasClaimedView && FlatDistance(landing, claimedHeadAsSeen) <= radius)
                    return claimed;
                if (FlatDistance(landing, currentHeads[claimed]) <= radius + margin)
                    return claimed;
            }
            return LassoThrow.FindCatch(landing, currentHeads, radius);
        }

        /// <summary>How far back to rewind for a client rendering the world at <paramref name="clientViewTime"/>.</summary>
        public static float RewindSeconds(float serverNow, float clientViewTime, float maxRewind) =>
            Mathf.Clamp(serverNow - clientViewTime, 0f, maxRewind);

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
