using System;
using System.Globalization;
using System.IO;
using Arreo.Horse;
using Arreo.Net;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 7 prototype): the client half of the two-player sale test
    /// (ProtoFairNetworkTests). Off unless started with -arreoSellTest &lt;report file&gt;. Once the
    /// host has cows in the fair pen, this rider jumps to the pen's gate and asks to sell, like
    /// pressing Interact there. When the hato's money arrives it writes what this machine sees
    /// (money, cows left in the herd) to the report and quits.
    /// </summary>
    public sealed class ProtoSaleBot : MonoBehaviour
    {
        private const float GiveUpSeconds = 90f;
        private const float RetrySeconds = 1.5f;
        // After the money arrives, time for the snapshot that hides the sold cows.
        private const float SettleSeconds = 1f;

        private string _reportPath;
        private float _elapsed;
        private bool _atGate;
        private float _lastRequest = float.MinValue;
        private int _requests;
        private float _paidAt = -1f;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoSellTest");
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
            if (_elapsed > GiveUpSeconds)
            {
                Finish(timedOut: true);
                return;
            }

            NetworkRider local = NetworkRider.Local;
            ProtoFair fair = ProtoFair.Instance;
            NetworkEconomy economy = NetworkEconomy.Instance;
            if (local == null || fair == null || economy == null)
                return;

            local.Input.enabled = false;
            local.Horse.SetIntent(new HorseIntent(throttle: 0f, steer: 0f, gallop: false));
            if (fair.InPen == 0 && !_atGate)
                return;

            if (!_atGate)
            {
                // The owner moves its own horse (D74), so the host sees it at the gate a moment later.
                Rigidbody body = local.Body;
                body.linearVelocity = Vector3.zero;
                body.position = fair.SalePoint;
                local.transform.position = fair.SalePoint;
                _atGate = true;
                _lastRequest = _elapsed;
                return;
            }

            if (economy.HatoBalance == 0)
            {
                if (_elapsed - _lastRequest >= RetrySeconds)
                {
                    _lastRequest = _elapsed;
                    _requests++;
                    fair.RequestSale();
                }
                return;
            }

            if (_paidAt < 0f)
                _paidAt = _elapsed;
            else if (_elapsed - _paidAt >= SettleSeconds)
                Finish(timedOut: false);
        }

        private void Finish(bool timedOut)
        {
            enabled = false;
            NetworkEconomy economy = NetworkEconomy.Instance;
            NetworkHerd herd = NetworkHerd.Instance;
            string text = string.Format(CultureInfo.InvariantCulture,
                "balance={0}\npresentCows={1}\nrequests={2}\ntimedOut={3}\n",
                economy != null ? economy.HatoBalance : 0, herd != null ? herd.Herd.PresentCount : -1, _requests, timedOut);
            File.WriteAllText(_reportPath, text);
            Debug.Log("[ProtoSaleBot] " + text.Replace('\n', ' '));
            Application.Quit();
        }
    }
}
