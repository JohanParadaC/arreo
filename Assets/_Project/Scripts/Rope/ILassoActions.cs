using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// What a rider can ask of their lasso. Offline it's the rope simulation itself; in a network
    /// game it's a request to the host, who validates and simulates (D74).
    /// </summary>
    public interface ILassoActions
    {
        /// <param name="targetId">Network id of what the rider aimed at (<see cref="LassoTarget.TargetId"/>), or -1.</param>
        void Throw(Vector3 target, int targetId);

        void Release();

        float EstimateFlightTime(Vector3 target);
    }
}
