using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>Marks the inside of a corral; the area follows this transform's position and yaw.</summary>
    public sealed class Corral : MonoBehaviour
    {
        [Tooltip("Inside width (x) and depth (z) in meters.")]
        [SerializeField] private Vector2 _size = new Vector2(23f, 23f);

        private CorralArea _area;

        private void Awake() => _area = new CorralArea(transform.position, _size, transform.eulerAngles.y);

        public int CountInside(IReadOnlyList<Vector3> positions) => _area.Count(positions);

        /// <summary>Cows still in the herd that are inside (a sold cow keeps its index but doesn't count).</summary>
        public int CountInside(IReadOnlyList<Vector3> positions, IReadOnlyList<bool> present) => _area.CountPresent(positions, present);

        public int CollectInside(IReadOnlyList<Vector3> positions, IReadOnlyList<bool> present, List<int> inside) =>
            _area.CollectInside(positions, present, inside);

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.DrawWireCube(Vector3.up, new Vector3(_size.x, 2f, _size.y));
        }
    }
}
