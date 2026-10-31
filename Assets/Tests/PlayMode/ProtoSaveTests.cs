#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Arreo.Core;
using Arreo.Herd;
using Arreo.Net;
using FishNet.Managing;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Saving and continuing in the real Proto_Sabana. The two-player test plays a game with a
    /// built client over Tugboat (like ProtoFairNetworkTests), saves, closes everything, then
    /// continues the game with the client joining again, and checks it all came back.
    /// </summary>
    [PrebuildSetup(typeof(NetworkTestPlayerBuild))]
    public class ProtoSaveTests
    {
        private const string ScenePath = "Assets/_Project/Prototype/Proto_Sabana.unity";
        private static readonly Vector3 ClientSpot = new Vector3(30f, 0f, -40f);
        private static readonly Vector3 HostSpot = new Vector3(-20f, 0f, 25f);

        private NetworkManager _network;
        private Process _client;
        private string _clientLog;
        private string _saveFolder;
        private string _voiceFolder;

        [SetUp]
        public void SetUp()
        {
            _saveFolder = TestSaves.NewFolder();
            _voiceFolder = TestVoice.NewFolder();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return Close();
            TestSaves.Delete(_saveFolder);
            TestVoice.Delete(_voiceFolder);
        }

        [UnityTest]
        public IEnumerator SaveWithTwoPlayers_CloseAndContinue_EverythingIsWhereItWas()
        {
            LogAssert.ignoreFailingMessages = true;

            // First session: a new game, a client that rides to a known spot, a sale, a later hour.
            yield return Open(continueGame: false);
            StartClient($"-arreoGoTo {ClientSpot.x},{ClientSpot.z}");
            yield return WaitForTwoRiders();
            NetworkRider client = NetworkRider.All.First(rider => !rider.IsOwner);
            for (float t = 0f; t < 20f && Distance2D(client.transform.position, ClientSpot) > 1f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(Distance2D(client.transform.position, ClientSpot), Is.LessThan(1f), "the client never reached its spot");

            ActiveHerd herd = NetworkHerd.Instance.Herd;
            Arreo.Prototype.ProtoFair fair = Arreo.Prototype.ProtoFair.Instance;
            ProtoSabanaSceneTests.PutCowsInTheFairPen(herd, 2);
            ProtoSabanaSceneTests.MoveLocalRiderTo(fair.SalePoint);
            yield return new WaitForSeconds(0.5f);
            fair.RequestSale();
            yield return new WaitForSeconds(0.5f);
            Assert.That(NetworkEconomy.Instance.HatoBalance, Is.GreaterThan(0), "the sale before saving did not happen");
            ProtoSabanaSceneTests.MoveLocalRiderTo(HostSpot);
            NetworkClock.Instance.ServerSet(3, 14.5f);
            yield return new WaitForSeconds(0.5f);

            NetworkSave save = Object.FindFirstObjectByType<NetworkSave>();
            Assert.That(save.SaveNow(), Is.True, "saving (F5) failed");

            // Close: the client quits, the host stops (and saves again by itself), the scene goes away.
            yield return Close();
            SaveLoadResult onDisk = new SaveStore(_saveFolder).Load();
            Assert.That(onDisk.Status, Is.EqualTo(SaveLoadStatus.Loaded), onDisk.Problem);
            SaveData saved = onDisk.Data;
            Assert.That(saved.players.Select(p => p.id), Is.EqualTo(new[] { "player1", "player2" }));
            Assert.That(Distance2D(saved.players[1].position, ClientSpot), Is.LessThan(1f), "the client's place was not saved");
            Assert.That(Distance2D(saved.players[0].position, HostSpot), Is.LessThan(1f), "the host's place was not saved");
            Assert.That(saved.cows.Count(cow => !cow.inHerd), Is.EqualTo(2), "the sold cows were not saved as sold");
            Assert.That(saved.clock.day, Is.EqualTo(3));

            // Second session: continue, and the same client joins again.
            yield return Open(continueGame: true);
            // Time runs and cows graze once the game is back, so check them before waiting for the client.
            herd = NetworkHerd.Instance.Herd;
            NetworkRider host = NetworkRider.Local;
            Debug.Log($"[ProtoSaveTests] continued: day {NetworkClock.Instance.Day} {NetworkClock.Instance.Hour:0.00}h, " +
                      $"{NetworkEconomy.Instance.HatoBalance} pesos, {herd.PresentCount}/{herd.Count} cows, host at {host.transform.position}");
            Assert.That(NetworkClock.Instance.Day, Is.EqualTo(3), "the day changed");
            Assert.That(NetworkClock.Instance.Hour, Is.EqualTo(saved.clock.hour).Within(0.05f), "the hour changed");
            Assert.That(NetworkEconomy.Instance.HatoBalance, Is.EqualTo(saved.money.hato), "the money changed");
            Assert.That(herd.Count, Is.EqualTo(saved.cows.Count));
            for (int i = 0; i < herd.Count; i++)
            {
                Assert.That(herd.Present[i], Is.EqualTo(saved.cows[i].inHerd), $"cow {i} came back sold/unsold the wrong way");
                Assert.That(Distance2D(herd.Positions[i], saved.cows[i].position), Is.LessThan(0.5f), $"cow {i} is not where it was");
            }
            Assert.That(Distance2D(host.transform.position, saved.players[0].position), Is.LessThan(1f), "the host is not where it was");

            StartClient("");
            yield return WaitForTwoRiders();
            yield return new WaitForSeconds(1f);
            client = NetworkRider.All.First(rider => !rider.IsOwner);
            Debug.Log($"[ProtoSaveTests] the client is back at {client.transform.position}");
            Assert.That(Distance2D(client.transform.position, saved.players[1].position), Is.LessThan(1.5f), "the client is not where it was");
            Assert.That(NetworkHerd.Instance.Herd.PresentCount, Is.EqualTo(saved.cows.Count(cow => cow.inHerd)), "the client's joining changed the herd");
        }

        [UnityTest]
        public IEnumerator ContinueWithABrokenSave_WarnsKeepsACopyAndStartsANewGame()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(_saveFolder);
            File.WriteAllText(Path.Combine(_saveFolder, SaveStore.FileName), "{\"version\":2,\"clock\":{\"da");

            yield return Open(continueGame: true);

            NetworkSave save = Object.FindFirstObjectByType<NetworkSave>();
            Assert.That(NetworkEconomy.Instance.HatoBalance, Is.EqualTo(NetworkEconomy.Instance.Settings.StartingMoney), "it did not start a new game");
            Assert.That(NetworkClock.Instance.Day, Is.EqualTo(1));
            Assert.That(NetworkHerd.Instance.Herd.PresentCount, Is.EqualTo(NetworkHerd.Instance.Herd.Count));
            Assert.That(save.Notice, Does.Contain("dañado"), "the player was not told");
            Assert.That(Directory.GetFiles(_saveFolder, "arreo-save.bad-*.json"), Has.Length.EqualTo(1), "the broken file was not kept");
            Assert.That(save.HasSave, Is.False, "the broken file is still offered to continue");
        }

        private IEnumerator Open(bool continueGame)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            TestSaves.UseFolder(_saveFolder);
            TestVoice.UseFolder(_voiceFolder);
            _network = Object.FindFirstObjectByType<NetworkManager>();
            var menu = Object.FindFirstObjectByType<Arreo.Prototype.ProtoNetworkMenu>();
            if (continueGame)
                menu.Continue();
            else
                menu.Host();
            for (float t = 0f; t < 10f && (NetworkRider.Local == null || NetworkHerd.Instance == null || NetworkHerd.Instance.Herd.Count == 0); t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.Local, Is.Not.Null, "hosting did not spawn the local rider");
            // One more beat for the save to be applied.
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator Close()
        {
            if (_client != null)
            {
                if (!_client.HasExited)
                    _client.Kill();
                _client.Dispose();
                _client = null;
            }
            if (_network == null)
                yield break;
            _network.ClientManager.StopConnection();
            _network.ServerManager.StopConnection(true);
            _network = null;
            yield return null;
            Scene proto = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterProtoSave" + Time.frameCount));
            yield return SceneManager.UnloadSceneAsync(proto);
        }

        private IEnumerator WaitForTwoRiders()
        {
            for (float t = 0f; t < 60f && NetworkRider.All.Count < 2 && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.All.Count, Is.EqualTo(2), $"the built client did not join (log: {_clientLog})");
        }

        private void StartClient(string extraArguments)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ArreoTests");
            Directory.CreateDirectory(folder);
            _clientLog = Path.Combine(folder, "save-client.log");
            _client = Process.Start(new ProcessStartInfo
            {
                FileName = NetworkTestPlayerBuild.ExePath,
                Arguments = $"-batchmode -nographics -arreoNet client {extraArguments} -logFile \"{_clientLog}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }

        private static float Distance2D(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
#endif
