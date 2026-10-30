#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Arreo.Herd;
using Arreo.Net;
using FishNet.Managing;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Two real players over Tugboat. This Editor hosts Proto_Sabana; a built copy of the game (the
    /// Network Test Player, rebuilt first when any game source is newer) joins it as a client in
    /// the background and sells at the fair (ProtoSaleBot). The client's sale has to change the
    /// hato's money the same way on both machines.
    /// </summary>
    [PrebuildSetup(typeof(NetworkTestPlayerBuild))]
    public class ProtoFairNetworkTests
    {
        private const string ScenePath = "Assets/_Project/Prototype/Proto_Sabana.unity";
        private const int CowsToSell = 3;

        private NetworkManager _network;
        private string _saveFolder;
        private Process _client;
        private string _report;
        private string _clientLog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _saveFolder = TestSaves.UseFolder();
            _network = Object.FindFirstObjectByType<NetworkManager>();
            Object.FindFirstObjectByType<Arreo.Prototype.ProtoNetworkMenu>().Host();
            for (float t = 0f; t < 10f && NetworkRider.Local == null; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.Local, Is.Not.Null, "hosting did not spawn the local rider");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null && !_client.HasExited)
                _client.Kill();
            _client?.Dispose();
            if (_network != null)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return null;
            }
            Scene proto = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterProtoFair"));
            yield return SceneManager.UnloadSceneAsync(proto);
            TestSaves.Delete(_saveFolder);
        }

        [UnityTest]
        public IEnumerator ClientSellsAtTheFair_TheHatoMoneyChangesOnBothMachines()
        {
            LogAssert.ignoreFailingMessages = true;
            StartClient();

            for (float t = 0f; t < 60f && NetworkRider.All.Count < 2 && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.All.Count, Is.EqualTo(2), $"the built client did not join (log: {_clientLog})");

            NetworkEconomy economy = NetworkEconomy.Instance;
            ActiveHerd herd = NetworkHerd.Instance.Herd;
            int total = herd.Count;
            long before = economy.HatoBalance;
            ProtoSabanaSceneTests.PutCowsInTheFairPen(herd, CowsToSell);

            // The client sees the cows in the pen, rides to the gate and asks to sell.
            for (float t = 0f; t < 30f && economy.HatoBalance == before && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            long expected = before + CowsToSell * economy.Settings.PricePerHead;
            Assert.That(economy.HatoBalance, Is.EqualTo(expected), $"the client's sale did not pay the hato on the host (log: {_clientLog})");
            Assert.That(herd.PresentCount, Is.EqualTo(total - CowsToSell), "the sold cows are still in the host's herd");

            for (float t = 0f; t < 20f && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(File.Exists(_report), Is.True, $"the client wrote no report (log: {_clientLog})");
            Dictionary<string, string> seen = File.ReadAllLines(_report)
                .Select(line => line.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]);
            Debug.Log($"[ProtoFairNetworkTests] host: {economy.HatoBalance} pesos, {herd.PresentCount} cows; client: {string.Join(", ", seen)}");

            Assert.That(seen["timedOut"], Is.EqualTo("False"), "the client gave up before seeing the money");
            Assert.That(long.Parse(seen["balance"]), Is.EqualTo(economy.HatoBalance), "the client sees different money than the host");
            Assert.That(int.Parse(seen["presentCows"]), Is.EqualTo(herd.PresentCount), "the client still shows the sold cows");
        }

        private void StartClient()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ArreoTests");
            Directory.CreateDirectory(folder);
            _report = Path.Combine(folder, "sale-client.txt");
            _clientLog = Path.Combine(folder, "sale-client.log");
            if (File.Exists(_report))
                File.Delete(_report);

            _client = Process.Start(new ProcessStartInfo
            {
                FileName = NetworkTestPlayerBuild.ExePath,
                Arguments = $"-batchmode -nographics -arreoNet client -arreoSellTest \"{_report}\" -logFile \"{_clientLog}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
    }

    /// <summary>
    /// Builds the Network Test Player (the same development build as Arreo > Prototype > Build
    /// Network Test Player) before the tests run, unless the last one is newer than every game source.
    /// </summary>
    public class NetworkTestPlayerBuild : IPrebuildSetup
    {
        private const string ScenePath = "Assets/_Project/Prototype/Proto_Sabana.unity";
        private const string BuildPath = "Build/NetworkTest/Arreo.exe";
        // Written after a build this helper made: the player's own files keep Unity's timestamps.
        private const string StampPath = "Build/NetworkTest/built-for-tests.txt";

        public static string ExePath => Path.GetFullPath(BuildPath);

        public void Setup()
        {
            if (File.Exists(ExePath) && File.Exists(StampPath) && File.GetLastWriteTimeUtc(StampPath) > NewestSource())
                return;
            Debug.Log("[NetworkTestPlayerBuild] Building the Network Test Player for the two-player tests");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                // Development: FishNet's latency simulator, which the same build is used for, needs it.
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"The Network Test Player build failed ({report.summary.result}); see the Editor log.");
            File.WriteAllText(StampPath, DateTime.UtcNow.ToString("o"));
        }

        // What goes into the player: the game's own assets and scripts, the input actions and the packages.
        private static DateTime NewestSource() =>
            Directory.EnumerateFiles("Assets/_Project", "*", SearchOption.AllDirectories)
                .Append("Assets/InputSystem_Actions.inputactions")
                .Append("Packages/manifest.json")
                .Max(File.GetLastWriteTimeUtc);
    }
}
#endif
