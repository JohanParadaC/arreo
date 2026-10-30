using System;
using System.Collections.Generic;
using Arreo.Core;
using Arreo.Herd;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Arreo.Net
{
    /// <summary>
    /// Saving and continuing a game, on the host (rule 5; D32: the host keeps the progress). It
    /// copies the clock, the hato's money, every cow (in the herd or sold, and where) and every
    /// player's place into a <see cref="SaveData"/> and back. It saves by itself when a game day
    /// starts and when the host stops or the game closes. The file goes to
    /// Application.persistentDataPath, or the folder given with -arreoSaveDir.
    /// </summary>
    public sealed class NetworkSave : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkHerd _herd;
        [SerializeField] private NetworkEconomy _economy;
        [SerializeField] private NetworkClock _clock;

        private readonly List<bool> _present = new List<bool>();
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<float> _yaws = new List<float>();
        private SaveStore _store;
        // A loaded game waiting for the host's herd, money and clock to be up.
        private SaveData _continuing;
        private PlayerPlaces _places = new PlayerPlaces();
        // The host's systems are running, holding the right game.
        private bool _ready;
        private int _lastDay = 1;
        private float _lastHour;
        private long _lastMoney;

        public SaveStore Store => _store;
        public bool HasSave => _store.Exists;

        /// <summary>The last thing saving had to say (saved, couldn't read the file...), for the HUD.</summary>
        public string Notice { get; private set; } = "";
        /// <summary>Time.time of <see cref="Notice"/>, or -1.</summary>
        public float NoticeTime { get; private set; } = -1f;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-arreoSaveDir");
            UseFolder(index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath);
        }

        private void Start() => _networkManager.ServerManager.OnServerConnectionState += OnServerState;

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.ServerManager != null)
                _networkManager.ServerManager.OnServerConnectionState -= OnServerState;
        }

        /// <summary>Saves and continues in another folder (tests use a temporary one).</summary>
        public void UseFolder(string folder) => _store = new SaveStore(folder);

        /// <summary>
        /// Before hosting: reads the saved game so hosting continues it. Returns false, with a
        /// notice, if there's none or it couldn't be read; hosting then starts a new game, and a
        /// broken file is kept aside (see <see cref="SaveStore"/>).
        /// </summary>
        public bool PrepareContinue()
        {
            SaveLoadResult result = _store.Load();
            if (result.Status == SaveLoadStatus.Loaded)
            {
                _continuing = result.Data;
                _places = new PlayerPlaces(result.Data.players);
                Notify($"Continuando la partida: día {result.Data.clock.day}");
                return true;
            }
            PrepareNewGame();
            WarnIfUnreadable(result);
            return false;
        }

        /// <summary>Before hosting: the next game starts from scratch (it replaces the save the first time it saves).</summary>
        public void PrepareNewGame()
        {
            _continuing = null;
            _places = new PlayerPlaces();
        }

        /// <summary>Where a player was in the game being continued.</summary>
        public bool TryGetPlace(string playerId, out Vector3 position, out float yaw) => _places.TryGet(playerId, out position, out yaw);

        private void Update()
        {
            if (!_networkManager.IsServerStarted)
                return;
            if (!_ready)
            {
                if (_herd.Herd.Count == 0 || !_economy.IsServerInitialized || !_clock.IsServerInitialized)
                    return;
                if (_continuing != null)
                    Apply(_continuing);
                _continuing = null;
                _clock.ServerDayStarted += OnDayStarted;
                _ready = true;
            }

            // Kept every frame: when the game closes, the riders may already be gone and the
            // network objects reset, but the save still needs them.
            _lastDay = _clock.Day;
            _lastHour = _clock.Hour;
            _lastMoney = _economy.HatoBalance;
            foreach (NetworkRider rider in NetworkRider.All)
                if (!string.IsNullOrEmpty(rider.ServerPlayerId))
                    _places.Set(rider.ServerPlayerId, rider.transform.position, rider.transform.eulerAngles.y);
        }

        private void OnDayStarted(int day) => SaveNow();

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopping && args.ConnectionState != LocalConnectionState.Stopped)
                return;
            SaveNow();
            Stop();
        }

        private void OnApplicationQuit() => SaveNow();

        private void Stop()
        {
            if (_ready && _clock != null)
                _clock.ServerDayStarted -= OnDayStarted;
            _ready = false;
        }

        /// <summary>Host: writes the game as it is now. Does nothing before the game is up.</summary>
        public bool SaveNow()
        {
            if (!_ready)
                return false;
            try
            {
                SaveData save = Capture();
                _store.Save(save);
                Notify($"Partida guardada (día {save.clock.day}, {Clock(save.clock.hour)})");
                return true;
            }
            catch (Exception e)
            {
                Notify($"No se pudo guardar la partida: {e.Message}", warning: true);
                return false;
            }
        }

        /// <summary>Host (F9): back to the saved game: herd, money, clock and every connected player's place.</summary>
        public bool LoadNow()
        {
            if (!_ready)
                return false;
            SaveLoadResult result = _store.Load();
            if (result.Status != SaveLoadStatus.Loaded)
            {
                if (result.Status == SaveLoadStatus.Missing)
                    Notify("No hay partida guardada");
                WarnIfUnreadable(result);
                return false;
            }

            _places = new PlayerPlaces(result.Data.players);
            Apply(result.Data);
            foreach (NetworkRider rider in NetworkRider.All)
                if (_places.TryGet(rider.ServerPlayerId, out Vector3 position, out float yaw))
                    rider.ServerTeleport(position, yaw);
            Notify($"Partida cargada: día {result.Data.clock.day}, {Clock(result.Data.clock.hour)}");
            return true;
        }

        private SaveData Capture()
        {
            var save = new SaveData();
            bool live = _clock.IsServerInitialized && _economy.IsServerInitialized;
            save.clock.day = live ? _clock.Day : _lastDay;
            save.clock.hour = live ? _clock.Hour : _lastHour;
            save.money.hato = live ? _economy.HatoBalance : _lastMoney;
            ActiveHerd herd = _herd.Herd;
            for (int i = 0; i < herd.Count; i++)
                save.cows.Add(new CowSave { inHerd = herd.Present[i], position = herd.Positions[i], yaw = herd.Yaws[i] });
            save.players = _places.ToSave();
            return save;
        }

        private void Apply(SaveData save)
        {
            _clock.ServerSet(save.clock.day, save.clock.hour);
            _economy.ServerLoadHato(save.money.hato);
            _present.Clear();
            _positions.Clear();
            _yaws.Clear();
            foreach (CowSave cow in save.cows)
            {
                _present.Add(cow.inHerd);
                _positions.Add(cow.position);
                _yaws.Add(cow.yaw);
            }
            _herd.ServerRestore(_present, _positions, _yaws);
        }

        private void WarnIfUnreadable(SaveLoadResult result)
        {
            if (result.Status != SaveLoadStatus.Corrupt && result.Status != SaveLoadStatus.UnknownVersion)
                return;
            string why = result.Status == SaveLoadStatus.UnknownVersion ? "es de una versión que este juego no conoce" : "está dañado";
            Notify($"El guardado {why}; se guardó una copia en {result.BackupPath} y empieza una partida nueva. ({result.Problem})",
                warning: true);
        }

        private void Notify(string message, bool warning = false)
        {
            Notice = message;
            NoticeTime = Time.time;
            if (warning)
                Debug.LogWarning("[NetworkSave] " + message);
            else
                Debug.Log("[NetworkSave] " + message);
        }

        private static string Clock(float hour) => $"{(int)hour:00}:{(int)(hour % 1f * 60f):00}";
    }
}
