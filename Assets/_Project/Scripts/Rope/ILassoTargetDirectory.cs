using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>
    /// Host-side list of what a lasso can catch, by network id, with a short history so a client's
    /// throw can be checked against where the target was when the client saw it.
    /// </summary>
    public interface ILassoTargetDirectory
    {
        /// <summary>Heads now, indexed by target id.</summary>
        void GetCurrentHeads(List<Vector3> heads);

        /// <summary>Where a target's head was <paramref name="secondsAgo"/> seconds ago, if still remembered.</summary>
        bool TryGetHeadAt(int targetId, float secondsAgo, out Vector3 head);

        LassoTarget GetTarget(int targetId);
    }
}
