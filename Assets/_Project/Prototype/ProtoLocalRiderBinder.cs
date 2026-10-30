using Arreo.Net;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 5-6 prototype): when this machine's own rider spawns, points the camera,
    /// HUD and benchmark at it and its rope. The herd, ropes, corral and gate are networked
    /// (<see cref="NetworkHerd"/>, <see cref="NetworkRope"/>, <see cref="NetworkWorld"/>).
    /// </summary>
    public sealed class ProtoLocalRiderBinder : MonoBehaviour
    {
        [SerializeField] private ProtoCameraRig _cameraRig;
        [SerializeField] private ProtoHud _hud;
        [SerializeField] private ProtoBenchmark _benchmark;

        private void OnEnable()
        {
            NetworkRider.LocalSpawned += Bind;
            if (NetworkRider.Local != null)
                Bind(NetworkRider.Local);
        }

        private void OnDisable() => NetworkRider.LocalSpawned -= Bind;

        private void Bind(NetworkRider rider)
        {
            _cameraRig.Bind(rider.CameraTarget, rider.Lasso, rider.Mount, rider.Rope);
            _hud.Bind(rider.Horse, rider.Lasso, rider.Mount, rider.Input, rider.Rope);
            _benchmark.Bind(rider.Horse, rider.Input);
        }
    }
}
