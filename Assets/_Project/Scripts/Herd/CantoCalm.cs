using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Herd
{
    /// <summary>
    /// The cabrestero's canto (rule 4) as the herd feels it. A cow hears a singing rider fully up
    /// to <c>fullRadius</c>, then less and less until <c>radius</c>. What it hears turns into calm
    /// (0 to 1) that builds up over <c>riseSeconds</c> and lingers, fading over <c>fadeSeconds</c>
    /// once the song stops. A calm cow fears riders less (that also takes panic away) and keeps
    /// closer to its herd mates, looking for them farther away. In a barajustada its panic runs
    /// its course faster, so it stops bolting sooner and comes back to the herd.
    /// </summary>
    public sealed class CantoCalm
    {
        private readonly float _radius;
        private readonly float _fullRadius;
        private readonly float _fearReduction;
        private readonly float _cohesionBoost;
        private readonly float _riseSeconds;
        private readonly float _fadeSeconds;
        private readonly float _stampedeShortening;
        private readonly float _regroupRadiusBoost;

        /// <param name="fearReduction">Share of the fear a fully calm cow no longer feels (0-1).</param>
        /// <param name="cohesionBoost">Extra cohesion of a fully calm cow: 1.5 = 2.5 times as strong.</param>
        /// <param name="stampedeShortening">Share of a barajustada's panic a fully calm cow skips (0-1): 0.5 = it lasts half as long.</param>
        /// <param name="regroupRadiusBoost">How much farther a fully calm cow looks for herd mates: 1.5 = 2.5 times the neighbor radius.</param>
        public CantoCalm(float radius, float fullRadius, float fearReduction, float cohesionBoost, float riseSeconds, float fadeSeconds,
            float stampedeShortening, float regroupRadiusBoost)
        {
            _radius = radius;
            _fullRadius = Mathf.Min(fullRadius, radius);
            _fearReduction = fearReduction;
            _cohesionBoost = cohesionBoost;
            _riseSeconds = riseSeconds;
            _fadeSeconds = fadeSeconds;
            // Never all of it: a cow that heard the canto still bolts at the thunder for a moment.
            _stampedeShortening = Mathf.Clamp(stampedeShortening, 0f, 0.9f);
            _regroupRadiusBoost = regroupRadiusBoost;
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

        /// <summary>How fast a cow's own barajustada clock runs (see <see cref="Stampede.IntensityAt"/>): 2 = its panic lasts half as long.</summary>
        public float StampedeTimeScale(float calm) => 1f / (1f - _stampedeShortening * Mathf.Clamp01(calm));

        /// <summary>The neighbor radius a cow looks for herd mates in, as a multiple: farther when calm, to regroup a scattered herd.</summary>
        public float CohesionRadiusScale(float calm) => 1f + _regroupRadiusBoost * Mathf.Clamp01(calm);
    }
}
