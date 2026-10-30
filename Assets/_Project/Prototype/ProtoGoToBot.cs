using System;
using System.Globalization;
using Arreo.Horse;
using Arreo.Net;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 8 prototype): for the save tests (ProtoSaveTests). Off unless started with
    /// -arreoGoTo &lt;x,z&gt;: once this machine's rider spawns, it jumps there and stands still, so
    /// the test knows where the client was when the host saved.
    /// </summary>
    public sealed class ProtoGoToBot : MonoBehaviour
    {
        private Vector3 _target;
        private bool _arrived;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoGoTo");
            string[] parts = index >= 0 && index + 1 < args.Length ? args[index + 1].Split(',') : null;
            if (parts == null || parts.Length != 2)
            {
                enabled = false;
                return;
            }
            _target = new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture), 0f, float.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        private void Update()
        {
            NetworkRider local = NetworkRider.Local;
            if (local == null)
                return;
            local.Input.enabled = false;
            local.Horse.SetIntent(new HorseIntent(throttle: 0f, steer: 0f, gallop: false));
            if (_arrived)
                return;
            // The owner moves its own horse (D74); the host sees it there a moment later.
            local.Horse.Teleport(_target, 0f);
            _arrived = true;
        }
    }
}
