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

        /// <summary>The horse the rope is tied to (the host's own rider this week).</summary>
        public void Bind(HorseController horse) => _horse = horse;

        private void FixedUpdate()
        {
            if (_horse != null)
                _horse.SetExternalVelocity(_rope.DragVelocity);
        }
    }
}
