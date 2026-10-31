using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// The cabrestero's canto (rule 4) as the herd feels it. A cow hears a singing rider fully up
    /// to <c>fullRadius</c>, then less and less until <c>radius</c>. What it hears turns into calm
    /// (0 to 1) that builds up over <c>riseSeconds</c> and lingers, fading over <c>fadeSeconds</c>
    /// once the song stops. A calm cow fears riders less (that also takes panic away) and keeps
    /// closer to its herd mates.
    /// </summary>
    public sealed class CantoCalm
    {
        private readonly float _radius;
        private readonly float _fullRadius;
        private readonly float _fearReduction;
        private readonly float _cohesionBoost;
        private readonly float _riseSeconds;
        private readonly float _fadeSeconds;

        /// <param name="fearReduction">Share of the fear a fully calm cow no longer feels (0-1).</param>
        /// <param name="cohesionBoost">Extra cohesion of a fully calm cow: 1.5 = 2.5 times as strong.</param>
        public CantoCalm(float radius, float fullRadius, float fearReduction, float cohesionBoost, float riseSeconds, float fadeSeconds)
        {
            _radius = radius;
            _fullRadius = Mathf.Min(fullRadius, radius);
            _fearReduction = fearReduction;
            _cohesionBoost = cohesionBoost;
            _riseSeconds = riseSeconds;
            _fadeSeconds = fadeSeconds;
        }

        /// <summary>How well a cow hears a canto sung this far away (m, on the ground), 0 to 1.</summary>
        public float Hearing(float distance)
        {
            if (distance <= _fullRadius)
                return 1f;
            if (distance >= _radius)
                return 0f;
            return 1f - (distance - _fullRadius) / (_radius - _fullRadius);
        }

        /// <summary>How well a cow here hears the closest rider who is singing.</summary>
        public float Hearing(Vector3 cow, IReadOnlyList<RiderSample> riders)
        {
            float hearing = 0f;
            for (int i = 0; i < riders.Count; i++)
                if (riders[i].Singing)
                    hearing = Mathf.Max(hearing, Hearing(BoidRules.Flatten(cow - riders[i].Position).magnitude));
            return hearing;
        }

        /// <summary>A cow's calm after <paramref name="deltaTime"/>: it rises toward what it hears, or fades.</summary>
        public float Step(float calm, float hearing, float deltaTime)
        {
            if (hearing > calm)
                return Mathf.Min(hearing, calm + deltaTime / _riseSeconds);
            return Mathf.Max(hearing, calm - deltaTime / _fadeSeconds);
        }

        public float FearScale(float calm) => 1f - _fearReduction * Mathf.Clamp01(calm);

        public float CohesionScale(float calm) => 1f + _cohesionBoost * Mathf.Clamp01(calm);
    }
}
