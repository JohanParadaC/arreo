using Arreo.Horse;
using Arreo.Rope;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 4 prototype): passes the rope's drag on to the horse, so Horse and Rope
    /// don't need to know each other. From week 6 the host does this.
    /// </summary>
    public sealed class ProtoRopeLink : MonoBehaviour
    {
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private HorseController _horse;

        private void FixedUpdate() => _horse.SetExternalVelocity(_rope.DragVelocity);
    }
}
