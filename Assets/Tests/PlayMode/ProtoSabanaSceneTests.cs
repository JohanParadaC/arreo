#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Arreo.Herd;
using Arreo.Net;
using FishNet.Managing;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Plays the real Proto_Sabana scene. Catches what unit tests can't: missing references in the
    /// generated scene, scripts throwing every frame, and the real camera not reacting to aiming.
    /// </summary>
    public class ProtoSabanaSceneTests
    {
        private const string ScenePath = "Assets/_Project/Prototype/Proto_Sabana.unity";

        private readonly List<string> _errors = new List<string>();
        private Mouse _mouse;
        private NetworkManager _network;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _errors.Clear();
            Application.logMessageReceived += CollectErrors;
            // Batchmode has no focused window, and by default the Input System ignores devices then.
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            _mouse = InputSystem.AddDevice<Mouse>();

            // Host a game ("Crear partida") and wait for this machine's own rider.
            _network = Object.FindFirstObjectByType<NetworkManager>();
            Object.FindFirstObjectByType<Arreo.Prototype.ProtoNetworkMenu>().Host();
            for (float t = 0f; t < 10f && NetworkRider.Local == null; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(NetworkRider.Local, Is.Not.Null, "hosting did not spawn the local rider");
            yield return new WaitForSeconds(1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_network != null)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return null;
            }
            Application.logMessageReceived -= CollectErrors;
            LogAssert.ignoreFailingMessages = false;
            if (_mouse != null)
                InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInputBehavior;

            // Unload the scene so later tests don't run on top of its horse, herd and obstacles.
            Scene proto = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterProtoSabana"));
            yield return SceneManager.UnloadSceneAsync(proto);
        }

        [UnityTest]
        public IEnumerator PlayingWhileAiming_LogsNoErrorsOrWarnings()
        {
            // We assert on the collected errors ourselves, with a readable list.
            LogAssert.ignoreFailingMessages = true;
            HoldAim(true);
            yield return new WaitForSeconds(3f);
            HoldAim(false);
            yield return new WaitForSeconds(0.5f);

            Assert.That(_errors, Is.Empty, "Console errors or warnings while playing:\n" + string.Join("\n", _errors));
        }

        [UnityTest]
        public IEnumerator Aiming_MovesTheRealCameraCloserAndNarrowsTheView()
        {
            LogAssert.ignoreFailingMessages = true;
            Transform rider = NetworkRider.Local.transform;
            Camera camera = Camera.main;
            float normalDistance = Vector3.Distance(camera.transform.position, rider.position);
            float normalFieldOfView = camera.fieldOfView;

            HoldAim(true);
            yield return new WaitForSeconds(0.8f);

            float aimDistance = Vector3.Distance(camera.transform.position, rider.position);
            float aimFieldOfView = camera.fieldOfView;
            HoldAim(false);
            Debug.Log($"[ProtoSabanaSceneTests] camera distance {normalDistance:0.0} -> {aimDistance:0.0} m, FOV {normalFieldOfView:0} -> {aimFieldOfView:0}");

            Assert.That(aimDistance, Is.LessThan(normalDistance - 2f), "the camera did not move closer");
            Assert.That(aimFieldOfView, Is.LessThan(normalFieldOfView - 10f), "the view did not narrow");
        }

        [UnityTest]
        public IEnumerator ObstacleBetweenCameraAndRider_PullsTheCameraInFrontOfIt()
        {
            LogAssert.ignoreFailingMessages = true;
            Transform target = NetworkRider.Local.CameraTarget;
            Camera camera = Camera.main;
            Vector3 toCamera = camera.transform.position - target.position;
            float normalDistance = toCamera.magnitude;

            // A wall halfway between the rider and the camera, square to the view, like a ramp in the way.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(target.position + toCamera * 0.5f, Quaternion.LookRotation(toCamera));
            wall.transform.localScale = new Vector3(6f, 6f, 0.3f);
            yield return new WaitForSeconds(1f);

            float blockedDistance = Vector3.Distance(camera.transform.position, target.position);
            float cameraHeight = camera.transform.position.y;
            float wallTop = wall.GetComponent<Collider>().bounds.max.y;
            bool blocked = Physics.Linecast(camera.transform.position, target.position, out RaycastHit hit)
                && !hit.collider.CompareTag("Player");
            Object.Destroy(wall);
            Debug.Log($"[ProtoSabanaSceneTests] camera distance with a wall in the way {normalDistance:0.0} -> {blockedDistance:0.0} m, height {cameraHeight:0.0} m (wall top {wallTop:0.0} m)");

            Assert.That(blockedDistance, Is.LessThan(normalDistance * 0.5f), "the camera did not move in front of the wall");
            // Only the Ground layer counts as terrain, so the wall must not lift the camera over its top.
            Assert.That(cameraHeight, Is.LessThan(wallTop), "the camera climbed over the wall");
            Assert.That(blocked, Is.False, $"the camera still can't see the rider (blocked by {(blocked ? hit.collider.name : "")})");
        }

        [UnityTest]
        public IEnumerator Hosting_SpawnsOneOwnedRider_TheCameraFollowsIt_AndTheHostRunsTheHerd()
        {
            LogAssert.ignoreFailingMessages = true;
            NetworkRider rider = NetworkRider.Local;
            yield return null;

            Assert.That(Object.FindObjectsByType<NetworkRider>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(rider.IsOwner, Is.True);
            Assert.That(rider.Role.ReadsInput && rider.Role.RunsHostSystems, Is.True);
            Assert.That(rider.Input.enabled && rider.Horse.enabled, Is.True, "the owner must read input and move its horse");
            Assert.That(rider.CompareTag("Player"), Is.True, "only the local rider is tagged for the camera to see through");
            Assert.That(GameObject.Find("CM Rider Camera").GetComponent<CinemachineCamera>().Target.TrackingTarget,
                Is.EqualTo(rider.CameraTarget), "the camera does not follow the local rider");
            Assert.That(Object.FindFirstObjectByType<ActiveHerd>().Count, Is.GreaterThan(0), "the host did not spawn the herd");
            Assert.That(_errors, Is.Empty, "Console errors or warnings while hosting:\n" + string.Join("\n", _errors));
        }

        [UnityTest]
        public IEnumerator Hosting_NetworksTheHerdRopeAndCorral_AndBStartsABarajustada()
        {
            LogAssert.ignoreFailingMessages = true;
            NetworkRider rider = NetworkRider.Local;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.That(NetworkHerd.Instance, Is.Not.Null, "the herd is not a spawned network object");
                Assert.That(NetworkHerd.Instance.LastSnapshotBytes, Is.GreaterThan(0), "the host sent no herd snapshot");
                Assert.That(rider.Rope, Is.Not.Null, "the player prefab has no networked rope");
                Assert.That(rider.Rope.IsConfigured, Is.True, "the host's rope is not tied to its saddle");
                Arreo.Prototype.NetworkWorld world = Arreo.Prototype.NetworkWorld.Instance;
                Assert.That(world, Is.Not.Null, "the corral and gate are not a spawned network object");
                Assert.That(world.Total, Is.EqualTo(NetworkHerd.Instance.Herd.Count), "the corral counter does not know the herd");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.3f);
                Assert.That(world.StampedeIntensity, Is.GreaterThan(0f), "B did not start a barajustada");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
            Assert.That(_errors, Is.Empty, "Console errors or warnings while hosting:\n" + string.Join("\n", _errors));
        }

        private void HoldAim(bool held)
        {
            var state = new MouseState();
            if (held)
                state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, state);
        }

        private void CollectErrors(string message, string stackTrace, LogType type)
        {
            // Warnings count too: a warning every frame (like setting the velocity of a kinematic
            // body) hides real problems in the console just as well as an error.
            if (type != LogType.Log)
                _errors.Add($"{type}: {message}\n{stackTrace}");
        }
    }
}
#endif
