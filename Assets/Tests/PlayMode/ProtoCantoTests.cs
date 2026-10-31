#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Arreo.Herd;
using Arreo.Net;
using Arreo.Voice;
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
    /// The cabrestero's canto with two real players over Tugboat (rule 4). This Editor hosts with
    /// its own test canto; the Network Test Player joins as a client with another one, stands
    /// south of the herd and sings (ProtoCantoBot). The client's recording has to reach the host
    /// intact (it travels compressed), the host's has to reach the client, and the host's herd has
    /// to calm down where the canto is heard, and only there.
    /// </summary>
    [PrebuildSetup(typeof(NetworkTestPlayerBuild))]
    public class ProtoCantoTests
    {
        private const string ScenePath = "Assets/_Project/Prototype/Proto_Sabana.unity";
        // 30 m south of the herd's center (45, 55), where the client stands and sings.
        private const string SingerSpot = "45,25";
        private static readonly Vector3 SingerPosition = new Vector3(45f, 0f, 25f);

        private NetworkManager _network;
        private string _saveFolder;
        private string _hostProfile;
        private string _clientProfile;
        private byte[] _hostCanto;
        private byte[] _clientCanto;
        private Process _client;
        private string _report;
        private string _clientLog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _saveFolder = TestSaves.UseFolder();
            _hostProfile = TestVoice.UseFolder();
            _hostCanto = TestVoice.WriteCanto(_hostProfile, seconds: 6f, hz: 330f);
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterProtoCanto"));
            yield return SceneManager.UnloadSceneAsync(proto);
            TestSaves.Delete(_saveFolder);
            TestVoice.Delete(_hostProfile);
            TestVoice.Delete(_clientProfile);
        }

        [UnityTest]
        public IEnumerator ClientCanto_ReachesTheHost_AndCalmsTheCowsThatHearIt()
        {
            LogAssert.ignoreFailingMessages = true;
            StartClient();

            for (float t = 0f; t < 60f && NetworkRider.All.Count < 2 && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.All.Count, Is.EqualTo(2), $"the built client did not join (log: {_clientLog})");
            NetworkCanto canto = NetworkRider.All.First(rider => !rider.IsOwner).Canto;
            ActiveHerd herd = NetworkHerd.Instance.Herd;
            Assert.That(herd.CalmedByCanto, Is.EqualTo(0), "the herd is calm before anyone sings");

            // Once the client stands at its spot, a few cows go well inside earshot and a few well out of it
            // (the herd itself bunches up about 30 m away, where the canto is fading).
            for (float t = 0f; t < 20f && FlatDistance(canto.transform.position, SingerPosition) > 1f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(FlatDistance(canto.transform.position, SingerPosition), Is.LessThan(1f), "the client never reached its spot");
            int[] nearCows = { 0, 1, 2, 3 };
            int[] farCows = { 4, 5, 6 };
            // The near ones go on the far side of the singer, 40 m from the herd: past the 30 m a calm cow
            // looks for herd mates in, or they would rightly walk back to it.
            PlaceCows(herd, nearCows, SingerPosition + new Vector3(0f, 0f, -10f));
            PlaceCows(herd, farCows, SingerPosition + new Vector3(0f, 0f, -70f));

            // The recording comes in parts, a few a second; the host checks it before keeping it.
            for (float t = 0f; t < 30f && !canto.HostHasOwnCanto && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(canto.HostHasOwnCanto, Is.True, $"the client's canto never reached the host (log: {_clientLog})");
            Assert.That(canto.Canto, Is.EqualTo(TestVoice.Compressed(_clientCanto)), "the host rebuilt a different canto");
            Assert.That(canto.Canto.Length, Is.LessThan(_clientCanto.Length / 3), "the canto did not travel compressed");

            // The client holds the canto button: the host's herd hears it.
            for (float t = 0f; t < 15f && !canto.IsSinging && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(canto.IsSinging, Is.True, "the client's singing never reached the host");
            yield return new WaitForSeconds(3f);

            Vector3 singer = canto.transform.position;
            CantoCalm calm = herd.Settings.CreateCantoCalm();
            Debug.Log($"[ProtoCantoTests] singer at {singer}: {herd.CalmedByCanto}/{herd.PresentCount} calmed; " +
                      string.Join(", ", Enumerable.Range(0, herd.Count).Select(i => $"{FlatDistance(herd.Positions[i], singer):0} m {herd.CalmFromCanto[i]:0.00}")));
            foreach (int i in nearCows)
            {
                float distance = FlatDistance(herd.Positions[i], singer);
                Assert.That(calm.Hearing(distance), Is.EqualTo(1f), $"cow {i} wandered out of full earshot ({distance:0.0} m)");
                Assert.That(herd.CalmFromCanto[i], Is.GreaterThan(0.9f), $"cow {i} at {distance:0.0} m hears the canto but isn't calm");
            }
            foreach (int i in farCows)
            {
                float distance = FlatDistance(herd.Positions[i], singer);
                Assert.That(calm.Hearing(distance), Is.EqualTo(0f), $"cow {i} wandered into earshot ({distance:0.0} m)");
                Assert.That(herd.CalmFromCanto[i], Is.EqualTo(0f), $"cow {i} at {distance:0.0} m is out of earshot but calm");
            }
            Assert.That(herd.CalmedByCanto, Is.InRange(nearCows.Length, herd.PresentCount - farCows.Length));

            // The client lets go after a few seconds.
            for (float t = 0f; t < 15f && canto.IsSinging && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(canto.IsSinging, Is.False, "the client stopped singing but the host still hears it");

            // The host's own canto went the other way; the client reports what it got and quits.
            for (float t = 0f; t < 40f && !_client.HasExited; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(File.Exists(_report), Is.True, $"the client wrote no report (log: {_clientLog})");
            Dictionary<string, string> seen = File.ReadAllLines(_report)
                .Select(line => line.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]);
            Debug.Log($"[ProtoCantoTests] client: {string.Join(", ", seen)}");
            Assert.That(seen["timedOut"], Is.EqualTo("False"), "the client gave up waiting for the host's canto");
            Assert.That(seen["ownAccepted"], Is.EqualTo("True"));
            byte[] hostCompressed = TestVoice.Compressed(_hostCanto);
            Assert.That(int.Parse(seen["hostCantoBytes"]), Is.EqualTo(hostCompressed.Length), "the client got a different canto from the host");
            Assert.That(uint.Parse(seen["hostCantoChecksum"]), Is.EqualTo(CantoParts.Checksum(hostCompressed)));
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // Host: stands these cows still in a small square around center.
        private static void PlaceCows(ActiveHerd herd, int[] cows, Vector3 center)
        {
            for (int n = 0; n < cows.Length; n++)
            {
                Vector3 position = center + new Vector3((n % 2) * 3f - 1.5f, 0.05f, (n / 2) * 3f - 1.5f);
                Rigidbody cow = herd.Cows[cows[n]];
                cow.transform.position = position;
                cow.position = position;
                cow.linearVelocity = Vector3.zero;
            }
        }

        private void StartClient()
        {
            string folder = Path.Combine(Path.GetTempPath(), "ArreoTests");
            Directory.CreateDirectory(folder);
            _report = Path.Combine(folder, "canto-client.txt");
            _clientLog = Path.Combine(folder, "canto-client.log");
            if (File.Exists(_report))
                File.Delete(_report);
            _clientProfile = TestVoice.NewFolder();
            _clientCanto = TestVoice.WriteCanto(_clientProfile, seconds: 7f, hz: 220f);

            _client = Process.Start(new ProcessStartInfo
            {
                FileName = NetworkTestPlayerBuild.ExePath,
                Arguments = $"-batchmode -nographics -arreoNet client -arreoProfileDir \"{_clientProfile}\" " +
                            $"-arreoGoTo {SingerSpot} -arreoCantoTest \"{_report}\" -logFile \"{_clientLog}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
    }
}
#endif
