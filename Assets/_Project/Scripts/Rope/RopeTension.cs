using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// Rope tension in newtons from how much the rope is stretched past its rest length, and how
    /// fast that tension drags the horse once it beats what the horse can hold.
    /// </summary>
    public sealed class RopeTension
    {
        private readonly float _stiffness;
        private readonly float _holdForce;
        private readonly float _dragSpeedPerNewton;
        private readonly float _maxDragSpeed;

        /// <param name="stiffness">Newtons per meter of stretch of the whole rope.</param>
        /// <param name="holdForce">Pull the horse resists without being dragged (N).</param>
        /// <param name="dragSpeedPerNewton">Drag speed (m/s) per newton above the hold force.</param>
        public RopeTension(float stiffness, float holdForce, float dragSpeedPerNewton, float maxDragSpeed)
        {
            _stiffness = stiffness;
            _holdForce = holdForce;
            _dragSpeedPerNewton = dragSpeedPerNewton;
            _maxDragSpeed = maxDragSpeed;
        }

        public static float PathLength(IReadOnlyList<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        public float FromLength(float pathLength, float restLength) => Mathf.Max(0f, pathLength - restLength) * _stiffness;

        public float DragSpeed(float tension) => Mathf.Clamp((tension - _holdForce) * _dragSpeedPerNewton, 0f, _maxDragSpeed);
    }
}
