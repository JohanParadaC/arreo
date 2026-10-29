using Arreo.Herd;
using Arreo.Horse;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 3 prototype): runs one drive into the corral. The clock starts the
    /// first time the horse moves.
    /// </summary>
    public sealed class ProtoDrive : MonoBehaviour
    {
        [SerializeField] private ActiveHerd _herd;
        [SerializeField] private Corral _corral;
        [SerializeField] private HorseController _horse;

        public HerdDrive Drive { get; private set; }

        /// <summary>The horse whose first move starts the clock (the host's own rider).</summary>
        public void Bind(HorseController horse) => _horse = horse;

        private void Update()
        {
            if (_horse == null)
                return;

            if (Drive == null)
            {
                if (_herd.Count == 0)
                    return;
                Drive = new HerdDrive(_herd.Count);
            }

            if (!Drive.HasStarted && _horse.Locomotion != null && Mathf.Abs(_horse.Locomotion.Speed) > 0.5f)
                Drive.Begin();

            Drive.Tick(Time.deltaTime, _corral.CountInside(_herd.Positions));
        }
    }
}
