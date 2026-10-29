using Arreo.Herd;
using Arreo.Net;
using Arreo.Rope;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 5 prototype): when this machine's own rider spawns, points the camera and
    /// HUD at it. On the host it also ties the rope, herd, corral clock and gate to the host's
    /// rider: those run only on the host until they're networked in week 6.
    /// </summary>
    public sealed class ProtoLocalRiderBinder : MonoBehaviour
    {
        [SerializeField] private ProtoCameraRig _cameraRig;
        [SerializeField] private ProtoHud _hud;
        [SerializeField] private ProtoBenchmark _benchmark;
        [SerializeField] private RopeSimulation _rope;
        [SerializeField] private ProtoRopeLink _ropeLink;
        [SerializeField] private ActiveHerd _herd;
        [SerializeField] private ProtoDrive _drive;
        [SerializeField] private ProtoGate _gate;

        private void OnEnable()
        {
            NetworkRider.LocalSpawned += Bind;
            if (NetworkRider.Local != null)
                Bind(NetworkRider.Local);
        }

        private void OnDisable() => NetworkRider.LocalSpawned -= Bind;

        private void Bind(NetworkRider rider)
        {
            _cameraRig.Bind(rider.CameraTarget, rider.Lasso, rider.Mount);
            _hud.Bind(rider.Horse, rider.Lasso, rider.Mount, rider.Input);
            _benchmark.Bind(rider.Horse, rider.Input);

            if (!rider.Role.RunsHostSystems)
                return;

            _rope.BindSaddle(rider.Body);
            rider.Lasso.BindRope(_rope);
            rider.Mount.BindRope(_rope);
            _ropeLink.Bind(rider.Horse);
            _herd.SetRider(rider.Body);
            _herd.SpawnHerd();
            _drive.Bind(rider.Horse);
            _gate.Bind(rider.transform);
        }
    }
}
