using System.Collections.Generic;
using Arreo.Voice;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// One rider's canto on the network (rule 4). When a player joins, their own recording (if
    /// they made one) goes to the host in parts, compressed to a quarter (<see cref="ImaAdpcm"/>);
    /// the host checks it and passes it on to everyone else, once per session, including players
    /// who join later. Holding the canto button asks
    /// the host to sing: every machine plays it in 3D from this rider (the fixed canto if there's
    /// no recording, or while it's still on its way), and the host's herd hears it.
    /// </summary>
    public sealed class NetworkCanto : NetworkBehaviour
    {
        [SerializeField] private NetworkRider _rider;
        [SerializeField] private VoiceSettings _settings;
        [Tooltip("Plays this rider's canto in 3D.")]
        [SerializeField] private AudioSource _voice;

        private readonly SyncVar<bool> _singing = new SyncVar<bool>();
        // Set by the host once this player's own recording arrived whole and passed the checks.
        private readonly SyncVar<bool> _hostHasOwnCanto = new SyncVar<bool>();

        private readonly List<Delivery> _deliveries = new List<Delivery>();
        private readonly HashSet<int> _deliveredTo = new HashSet<int>();
        private CantoRules _rules;
        private CantoAssembler _incoming;
        // The recording as it travels (IMA ADPCM, a quarter of the WAV): the owner's own, or the one that arrived. The host passes it on.
        private byte[] _canto;
        private List<CantoPart> _parts;
        private AudioClip _clip;
        // Owner: parts of its own canto sent so far.
        private int _uploaded;
        private float _nextUpload;
        private bool _localHeld;
        private bool _botHeld;
        private bool _warnedRejected;

        private sealed class Delivery
        {
            public NetworkConnection Connection;
            public int Next;
            public float NextAt;
        }

        /// <summary>The host says this rider is singing (the owner hears itself right away, before the host answers).</summary>
        public bool IsSinging => _singing.Value;

        /// <summary>The host got this player's own recording and checked it; everyone can see this.</summary>
        public bool HostHasOwnCanto => _hostHasOwnCanto.Value;

        /// <summary>
        /// This rider's recording as it travels over the network (compressed, <see cref="ImaAdpcm"/>), or null
        /// while this machine sings the fixed canto for them.
        /// </summary>
        public byte[] Canto => _canto;

        /// <summary>Owner: share of its own recording sent to the host (1 when there's nothing to send).</summary>
        public float UploadProgress => _parts == null || !IsOwner || IsServerInitialized ? 1f : (float)_uploaded / _parts.Count;

        /// <summary>This machine is playing this rider's canto now.</summary>
        public bool IsPlaying => _voice.isPlaying;

        /// <summary>Owner: sings as if the button were held (bots and tests).</summary>
        public void Hold(bool held) => _botHeld = held;

        private CantoRules Rules => _rules ??= _settings.CreateRules();

        public override void OnStartClient()
        {
            base.OnStartClient();
            _voice.playOnAwake = false;
            _voice.loop = true;
            _voice.spatialBlend = 1f;
            _voice.dopplerLevel = 0f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.minDistance = _settings.MinDistance;
            _voice.maxDistance = _settings.MaxDistance;
            if (IsOwner)
                ReadOwnCanto();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _voice.Stop();
            _deliveries.Clear();
        }

        private void OnDestroy()
        {
            if (_clip != null)
                Destroy(_clip);
        }

        // Owner: its own recording from the profile, played here and sent to the host (or, on the host, kept).
        private void ReadOwnCanto()
        {
            byte[] wav = VoiceProfile.Instance != null ? VoiceProfile.Instance.LoadOwnCanto() : null;
            if (wav == null)
                return;
            if (!Rules.Check(wav, out float[] samples, out string problem))
            {
                Debug.LogWarning($"[NetworkCanto] Your recorded canto can't be used ({problem}); singing the fixed one.");
                return;
            }
            // The owner hears its own recording as it was; the others get it compressed.
            Keep(ImaAdpcm.Encode(samples, Rules.SampleRate), samples);
            if (IsServerInitialized)
                ServerAccept();
            else
                _nextUpload = Time.time;
        }

        private void Keep(byte[] compressed, float[] samples)
        {
            _canto = compressed;
            _parts = CantoParts.Split(compressed, _settings.PartBytes);
            _clip = _settings.CreateClip($"Canto {OwnerId}", samples);
        }

        private void Update()
        {
            if (IsOwner)
                OwnerUpdate();
            if (IsServerInitialized)
                ServerUpdate();
            if (IsClientInitialized)
                PlayUpdate();
        }

        private void OwnerUpdate()
        {
            bool held = (_botHeld || (_rider.Input.enabled && _rider.Input.SingHeld)) && _rider.Mount.HasControl;
            if (held != _localHeld)
            {
                _localHeld = held;
                ServerSing(held);
            }

            // Once per session, a few parts a second so the game's own messages keep flowing.
            if (IsServerInitialized || _parts == null || _uploaded >= _parts.Count || Time.time < _nextUpload)
                return;
            CantoPart part = _parts[_uploaded++];
            _nextUpload += 1f / _settings.PartsPerSecond;
            ServerUploadPart(part.Index, part.Count, part.TotalBytes, part.Data);
        }

        [ServerRpc]
        private void ServerUploadPart(int index, int count, int totalBytes, byte[] data)
        {
            if (_hostHasOwnCanto.Value)
                return;
            _incoming ??= new CantoAssembler(Rules.MaxCompressedBytes, _settings.PartBytes);
            CantoPartResult result = _incoming.Add(index, count, totalBytes, data);
            if (result == CantoPartResult.Rejected)
            {
                // The assembler rejects every later part too: warn once.
                if (!_warnedRejected)
                    Debug.LogWarning($"[NetworkCanto] Player {OwnerId}'s canto rejected ({_incoming.Problem}); they'll sing the fixed one.");
                _warnedRejected = true;
                return;
            }
            if (result != CantoPartResult.Complete)
                return;

            if (!Rules.CheckCompressed(_incoming.Result, out float[] samples, out string problem))
            {
                Debug.LogWarning($"[NetworkCanto] Player {OwnerId}'s canto is not a canto ({problem}); they'll sing the fixed one.");
                return;
            }
            Keep(_incoming.Result, samples);
            _incoming = null;
            ServerAccept();
        }

        // Host: everyone watching this rider gets the canto; later arrivals get it from OnSpawnServer.
        [Server]
        private void ServerAccept()
        {
            _hostHasOwnCanto.Value = true;
            foreach (NetworkConnection connection in NetworkObject.Observers)
                Deliver(connection);
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (_hostHasOwnCanto.Value)
                Deliver(connection);
        }

        private void Deliver(NetworkConnection connection)
        {
            // The owner has its own; the host's own client shares the host's copy.
            if (connection == Owner || connection.IsLocalClient || _deliveredTo.Contains(connection.ClientId))
                return;
            foreach (Delivery delivery in _deliveries)
                if (delivery.Connection == connection)
                    return;
            _deliveries.Add(new Delivery { Connection = connection, NextAt = Time.time });
        }

        private void ServerUpdate()
        {
            if (_singing.Value && !_rider.Mount.HasControl)
                _singing.Value = false;

            for (int i = _deliveries.Count - 1; i >= 0; i--)
            {
                Delivery delivery = _deliveries[i];
                if (!delivery.Connection.IsActive)
                {
                    _deliveries.RemoveAt(i);
                    continue;
                }
                if (Time.time < delivery.NextAt)
                    continue;
                CantoPart part = _parts[delivery.Next++];
                delivery.NextAt += 1f / _settings.PartsPerSecond;
                TargetPart(delivery.Connection, part.Index, part.Count, part.TotalBytes, part.Data);
                if (delivery.Next < _parts.Count)
                    continue;
                _deliveredTo.Add(delivery.Connection.ClientId);
                _deliveries.RemoveAt(i);
            }
        }

        [TargetRpc]
        private void TargetPart(NetworkConnection target, int index, int count, int totalBytes, byte[] data)
        {
            if (_canto != null)
                return;
            _incoming ??= new CantoAssembler(Rules.MaxCompressedBytes, _settings.PartBytes);
            if (_incoming.Add(index, count, totalBytes, data) != CantoPartResult.Complete)
                return;
            if (Rules.CheckCompressed(_incoming.Result, out float[] samples, out string problem))
                Keep(_incoming.Result, samples);
            else
                Debug.LogWarning($"[NetworkCanto] Player {OwnerId}'s canto can't be played ({problem}); using the fixed one.");
            _incoming = null;
        }

        [ServerRpc]
        private void ServerSing(bool singing) => _singing.Value = singing && _rider.Mount.HasControl;

        private void PlayUpdate()
        {
            bool sing = IsOwner ? _localHeld : _singing.Value;
            _voice.mute = !IsOwner && VoiceProfile.Instance != null && VoiceProfile.Instance.MuteOtherCantos;
            if (sing)
            {
                if (!_voice.isPlaying)
                {
                    _voice.clip = _clip != null ? _clip : _settings.FixedCanto;
                    _voice.Play();
                }
                _voice.volume = _settings.Volume;
                return;
            }
            if (!_voice.isPlaying)
                return;
            // Let go of the button: a short fade instead of a click.
            float step = _settings.FadeOutSeconds > 0f ? _settings.Volume * Time.deltaTime / _settings.FadeOutSeconds : 1f;
            _voice.volume = Mathf.Max(0f, _voice.volume - step);
            if (_voice.volume <= 0f)
                _voice.Stop();
        }
    }
}
