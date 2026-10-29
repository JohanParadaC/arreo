using UnityEngine;

namespace Arreo.Net
{
    /// <summary>Where each player's horse appears: side by side, alternating right and left.</summary>
    public static class SpawnLayout
    {
        public static Vector3 PositionFor(int playerIndex, Vector3 origin, float spacing)
        {
            if (playerIndex <= 0)
                return origin;

            int slot = (playerIndex + 1) / 2;
            float side = playerIndex % 2 == 1 ? 1f : -1f;
            return origin + new Vector3(side * slot * spacing, 0f, 0f);
        }
    }
}
