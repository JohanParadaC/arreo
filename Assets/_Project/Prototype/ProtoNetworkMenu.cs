using System;
using Arreo.Net;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using UnityEngine;

namespace Arreo.Prototype
{
    /// <summary>
    /// DISPOSABLE (weeks 5-8 prototype): minimal network menu. "Continuar partida" (if there's a
    /// save) hosts the saved game, "Crear partida" hosts a new one (server and client in this game),
    /// "Unirse" joins a host on this computer over Tugboat. A toggle simulates a bad connection.
    /// Command line: -arreoNet host|continue|client, -arreoLatency &lt;ms per direction&gt;,
    /// -arreoLoss &lt;0-1&gt;; the FPS benchmark hosts by itself.
    /// </summary>
    public sealed class ProtoNetworkMenu : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private NetworkSave _save;

        [Header("Simulated bad connection")]
        [Tooltip("Delay added to every packet this game sends (ms). Both players simulating 75 ms = 150 ms round trip (ping).")]
        [SerializeField, Min(0)] private long _latencyPerDirection = 75;
        [Tooltip("Share of unreliable packets this game drops (0-1). 0.02 = 2 %.")]
        [SerializeField, Range(0f, 1f)] private float _packetLoss = 0.02f;
        [SerializeField] private bool _simulateBadConnection;

        private GUIStyle _title;
        private bool _hasSave;
        private float _nextSaveCheck;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (TryGetArg(args, "-arreoLatency", out string latency))
            {
                _latencyPerDirection = long.Parse(latency);
                _simulateBadConnection = true;
            }
            if (TryGetArg(args, "-arreoLoss", out string loss))
            {
                _packetLoss = float.Parse(loss, System.Globalization.CultureInfo.InvariantCulture);
                _simulateBadConnection = true;
            }

            TryGetArg(args, "-arreoNet", out string role);
            if (role == "host" || Array.IndexOf(args, "-arreoBenchmark") >= 0)
                Host();
            else if (role == "continue")
                Continue();
            else if (role == "client")
                Join();
        }

        /// <summary>Hosts a new game ("Crear partida"); it replaces the saved one the first time it saves.</summary>
        public void Host()
        {
            _save.PrepareNewGame();
            StartHost();
        }

        /// <summary>
        /// Hosts the saved game ("Continuar partida"). With no readable save it warns, keeps a copy
        /// of the broken file and hosts a new game instead.
        /// </summary>
        public void Continue()
        {
            _save.PrepareContinue();
            StartHost();
        }

        private void StartHost()
        {
            ApplyConnectionSimulation();
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        public void Join()
        {
            ApplyConnectionSimulation();
            _networkManager.ClientManager.StartConnection("localhost");
        }

        private void ApplyConnectionSimulation()
        {
            LatencySimulator simulator = _networkManager.TransportManager.LatencySimulator;
            simulator.SetEnabled(_simulateBadConnection);
            simulator.SetLatency(_latencyPerDirection);
            simulator.SetPacketLoss(_packetLoss);
            if (_simulateBadConnection)
                // FishNet only simulates in the Editor and development builds; a release build ignores it.
                Debug.Log($"[ProtoNetworkMenu] Simulating {_latencyPerDirection} ms per direction and {_packetLoss:P0} packet loss" +
                          (Debug.isDebugBuild ? "" : " (IGNORED: release build)"));
        }

        private void OnGUI()
        {
            if (_networkManager.ClientManager.Started)
            {
                string role = _networkManager.IsServerStarted ? "anfitrión" : "cliente";
                string lag = _simulateBadConnection ? $" · simulando {_latencyPerDirection * 2} ms de ping, {_packetLoss:P0} pérdida" : "";
                GUI.Label(new Rect(Screen.width - 430, 10, 420, 25), $"En red como {role}{lag}");
                return;
            }

            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            // Checked each time the menu is drawn would mean a disk read per frame; once a second is plenty.
            if (Time.unscaledTime >= _nextSaveCheck)
            {
                _nextSaveCheck = Time.unscaledTime + 1f;
                _hasSave = _save.HasSave;
            }
            float extra = _hasSave ? 55f : 0f;
            var panel = new Rect(Screen.width / 2f - 200, Screen.height / 2f - 120 - extra / 2f, 400, 240 + extra);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 10, panel.width, 40), "Arreo", _title);
            if (_hasSave && GUI.Button(new Rect(panel.x + 50, panel.y + 65, 300, 45), "Continuar partida"))
                Continue();
            if (GUI.Button(new Rect(panel.x + 50, panel.y + 65 + extra, 300, 45), _hasSave ? "Crear partida nueva" : "Crear partida"))
                Host();
            if (GUI.Button(new Rect(panel.x + 50, panel.y + 120 + extra, 300, 45), "Unirse (localhost)"))
                Join();
            _simulateBadConnection = GUI.Toggle(new Rect(panel.x + 30, panel.y + 180 + extra, 360, 40), _simulateBadConnection,
                $" Simular mala conexión ({_latencyPerDirection * 2} ms de ping, {_packetLoss:P0} de pérdida)");
        }

        private static bool TryGetArg(string[] args, string name, out string value)
        {
            int index = Array.IndexOf(args, name);
            value = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            return value != null;
        }
    }
}
