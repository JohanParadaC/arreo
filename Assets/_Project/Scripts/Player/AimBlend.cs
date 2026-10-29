using UnityEngine;

namespace Arreo.Player
{
    /// <summary>
    /// Hold-to-aim: while held, the aim weight blends from 0 to 1 over the blend time (and back
    /// when released). The camera reads <see cref="Eased"/> to zoom over the shoulder smoothly.
    /// </summary>
    public sealed class AimBlend
    {
        private readonly float _blendSeconds;

        public AimBlend(float blendSeconds) => _blendSeconds = blendSeconds;

        public bool IsAiming { get; private set; }

        /// <summary>0 = normal camera, 1 = fully over the shoulder; changes linearly.</summary>
        public float Weight { get; private set; }

        /// <summary>Weight with ease-in and ease-out, for camera moves.</summary>
        public float Eased => Weight * Weight * (3f - 2f * Weight);

        public void Tick(float deltaTime, bool held)
        {
            IsAiming = held;
            float target = held ? 1f : 0f;
            Weight = _blendSeconds <= 0f ? target : Mathf.MoveTowards(Weight, target, deltaTime / _blendSeconds);
        }
    }
}
