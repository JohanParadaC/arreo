using System;
using System.Globalization;
using System.IO;
using Arreo.Net;
using Arreo.Voice;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 10 prototype): the client half of the two-player canto test
    /// (ProtoCantoTests). Off unless started with -arreoCantoTest &lt;report file&gt;. Once the
    /// host has this player's recorded canto, it sings (as if holding V) for a few seconds. When
    /// the host's own canto has arrived here too, it writes what it saw to the report and quits.
    /// Pair it with -arreoProfileDir (whose canto.wav it sends) and -arreoGoTo (where it sings).
    /// </summary>
    public sealed class ProtoCantoBot : MonoBehaviour
    {
        private const float GiveUpSeconds = 120f;
        private const float SingSeconds = 8f;

        private string _reportPath;
        private float _elapsed;
        private float _singingSince = -1f;
        private bool _sang;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoCantoTest");
            if (index < 0 || index + 1 >= args.Length)
            {
                enabled = false;
                return;
            }
            _reportPath = args[index + 1];
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            NetworkCanto own = NetworkRider.Local != null ? NetworkRider.Local.Canto : null;
            if (_elapsed > GiveUpSeconds)
            {
                Finish(own, timedOut: true);
                return;
            }
            if (own == null || !own.IsSpawned)
                return;

            if (!_sang)
            {
                if (_singingSince < 0f && own.HostHasOwnCanto)
                {
                    own.Hold(true);
                    _singingSince = _elapsed;
                }
                if (_singingSince >= 0f && _elapsed - _singingSince >= SingSeconds)
                {
                    own.Hold(false);
                    _sang = true;
                }
                return;
            }

            if (HostCanto() != null)
                Finish(own, timedOut: false);
        }

        // The host's rider is the one this machine doesn't own (two-player test).
        private static byte[] HostCanto()
        {
            foreach (NetworkRider rider in NetworkRider.All)
                if (!rider.IsOwner && rider.Canto != null)
                    return rider.Canto.Canto;
            return null;
        }

        private void Finish(NetworkCanto own, bool timedOut)
        {
            enabled = false;
            byte[] host = HostCanto();
            string text = string.Format(CultureInfo.InvariantCulture,
                "ownAccepted={0}\nsang={1}\nhostCantoBytes={2}\nhostCantoChecksum={3}\ntimedOut={4}\n",
                own != null && own.HostHasOwnCanto, _sang, host?.Length ?? 0, host != null ? CantoParts.Checksum(host) : 0u, timedOut);
            File.WriteAllText(_reportPath, text);
            Debug.Log("[ProtoCantoBot] " + text.Replace('\n', ' '));
            Application.Quit();
        }
    }
}
