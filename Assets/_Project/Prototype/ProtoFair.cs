using System.Collections.Generic;
using Arreo.Herd;
using Arreo.Net;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (week 7 prototype): the fair pen in the pueblo. A player at its gate presses
    /// Interact to sell every cow inside; the host checks they really are at the gate, takes those
    /// cows out of the herd (D29: sold for good) and pays the price per head into the seller's
    /// wallet, which while money is shared is the hato's (D75). Everyone sees how many cows are in
    /// the pen and the last sale. Nothing here knows the transport: it's all FishNet RPCs and SyncVars.
    /// </summary>
    public sealed class ProtoFair : NetworkBehaviour
    {
        [SerializeField] private NetworkHerd _herd;
        [SerializeField] private Corral _pen;
        [Tooltip("Where a rider stands to sell: the pen's gate.")]
        [SerializeField] private Transform _salePoint;
        [SerializeField] private NetworkEconomy _economy;
        [SerializeField] private InputActionReference _interact;
        [Tooltip("How close (m) to the sale point a rider must be to sell.")]
        [SerializeField, Min(1f)] private float _reach = 15f;

        private readonly SyncVar<int> _inPen = new SyncVar<int>();
        private readonly List<int> _inside = new List<int>();

        public static ProtoFair Instance { get; private set; }

        /// <summary>Cows in the fair pen right now (counted by the host).</summary>
        public int InPen => _inPen.Value;
        public long PricePerHead => _economy.Settings.PricePerHead;
        public Vector3 SalePoint => _salePoint.position;
        public bool LocalRiderAtSalePoint => NetworkRider.Local != null && InReach(NetworkRider.Local.transform.position);

        /// <summary>The last sale, as every machine heard it (Time.time when it arrived, or -1).</summary>
        public float LastSaleTime { get; private set; } = -1f;
        public int LastSaleHead { get; private set; }
        public long LastSaleTotal { get; private set; }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _interact.action.Enable();
        }

        private void Update()
        {
            if (IsClientInitialized && NetworkRider.Local != null && NetworkRider.Local.Mount.HasControl
                && LocalRiderAtSalePoint && _interact.action.WasPressedThisFrame())
                RequestSale();

            if (IsServerInitialized)
            {
                ActiveHerd herd = _herd.Herd;
                int inPen = herd.Count > 0 ? _pen.CountInside(herd.Positions, herd.Present) : 0;
                if (_inPen.Value != inPen)
                    _inPen.Value = inPen;
            }
        }

        /// <summary>Asks the host to sell what's in the pen (what Interact at the gate does).</summary>
        public void RequestSale() => SellRequest();

        private bool InReach(Vector3 riderPosition) => Vector3.Distance(riderPosition, _salePoint.position) <= _reach;

        [ServerRpc(RequireOwnership = false)]
        private void SellRequest(NetworkConnection sender = null)
        {
            if (!SenderAtSalePoint(sender))
                return;
            ActiveHerd herd = _herd.Herd;
            if (_pen.CollectInside(herd.Positions, herd.Present, _inside) == 0)
                return;

            int head = _inside.Count;
            long total = _economy.Settings.SaleTotal(head);
            _herd.ServerRemove(_inside);
            _economy.ServerEarn(sender, total);
            _inPen.Value = 0;
            ObserversSold(head, total);
        }

        // Only a rider who is actually at the gate can sell.
        private bool SenderAtSalePoint(NetworkConnection sender)
        {
            foreach (NetworkRider rider in NetworkRider.All)
                if (rider.Owner == sender && InReach(rider.transform.position))
                    return true;
            return false;
        }

        [ObserversRpc]
        private void ObserversSold(int head, long total)
        {
            LastSaleTime = Time.time;
            LastSaleHead = head;
            LastSaleTotal = total;
        }
    }
}
