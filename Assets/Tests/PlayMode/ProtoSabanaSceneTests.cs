#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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
            yield return new WaitForSeconds(1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
        public IEnumerator PlayingWhileAiming_LogsNoErrors()
        {
            // We assert on the collected errors ourselves, with a readable list.
            LogAssert.ignoreFailingMessages = true;
            HoldAim(true);
            yield return new WaitForSeconds(3f);
            HoldAim(false);
            yield return new WaitForSeconds(0.5f);

            Assert.That(_errors, Is.Empty, "Console errors while playing:\n" + string.Join("\n", _errors));
        }

        [UnityTest]
        public IEnumerator Aiming_MovesTheRealCameraCloserAndNarrowsTheView()
        {
            LogAssert.ignoreFailingMessages = true;
            Transform rider = GameObject.Find("Horse").transform;
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

        private void HoldAim(bool held)
        {
            var state = new MouseState();
            if (held)
                state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, state);
        }

        private void CollectErrors(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add($"{type}: {message}\n{stackTrace}");
        }
    }
}
#endif
