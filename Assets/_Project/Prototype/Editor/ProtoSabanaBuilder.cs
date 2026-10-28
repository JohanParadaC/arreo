using System.IO;
using System.Linq;
using Arreo.Horse;
using Arreo.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Arreo.Prototype.Editor
{
    /// <summary>
    /// DISPOSABLE (week 2 prototype): builds the Proto_Sabana test scene from code.
    /// Rebuilding overwrites the scene but keeps the HorseSettings asset, so tuning survives.
    /// </summary>
    public static class ProtoSabanaBuilder
    {
        private const string Root = "Assets/_Project/Prototype";
        private const string ScenePath = Root + "/Proto_Sabana.unity";
        private const string GeneratedFolder = Root + "/Generated";
        private const string SettingsFolder = "Assets/_Project/Data/Horse";
        private const string SettingsPath = SettingsFolder + "/HorseSettings_Proto.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const float GroundSize = 600f;
        private const float ObstacleRadius = 270f;
        private const float SpawnClearRadius = 15f;

        [MenuItem("Arreo/Prototype/Rebuild Proto_Sabana")]
        public static void Build()
        {
            EnsureFolder(GeneratedFolder);
            EnsureFolder(SettingsFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            HorseSettings settings = LoadOrCreateSettings();

            CreateGround();
            CreateObstacles();
            HorseController horse = CreateHorse(settings, out Transform cameraTarget);
            CreateCamera(cameraTarget);

            var hud = new GameObject("ProtoHud").AddComponent<ProtoHud>();
            SetReference(hud, "_horse", horse);

            Light sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProtoSabanaBuilder] Saved {ScenePath}");
        }

        private static HorseSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HorseSettings>(SettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<HorseSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.isStatic = true;

            Material material = CreateMaterial("Ground", Color.white);
            material.SetTexture("_BaseMap", CreateCheckerTexture());
            // One checker repeat (2x2 tiles) every 8 m, so each tile is 4 m: helps read speed.
            material.SetTextureScale("_BaseMap", Vector2.one * (GroundSize / 8f));
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateObstacles()
        {
            var random = new System.Random(7);
            var parent = new GameObject("Obstacles").transform;
            Material boxMaterial = CreateMaterial("Box", new Color(0.76f, 0.64f, 0.45f));
            Material rampMaterial = CreateMaterial("Ramp", new Color(0.55f, 0.36f, 0.22f));
            Material palmMaterial = CreateMaterial("Palm", new Color(0.33f, 0.42f, 0.2f));

            for (int i = 0; i < 120; i++)
            {
                Vector3 size = new Vector3(Range(random, 1f, 6f), Range(random, 0.5f, 4f), Range(random, 1f, 6f));
                Vector3 position = RandomGroundPoint(random) + Vector3.up * (size.y / 2f);
                CreateBlock("Box", parent, position, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), size, boxMaterial);
            }

            for (int i = 0; i < 25; i++)
            {
                float length = Range(random, 8f, 14f);
                float angle = Range(random, 10f, 20f);
                Vector3 position = RandomGroundPoint(random)
                    + Vector3.up * (Mathf.Sin(angle * Mathf.Deg2Rad) * length / 2f);
                Quaternion rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f) * Quaternion.Euler(-angle, 0f, 0f);
                CreateBlock("Ramp", parent, position, rotation, new Vector3(4f, 0.4f, length), rampMaterial);
            }

            // Tall moriche-like markers to judge distance and speed.
            for (int i = 0; i < 40; i++)
            {
                GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                palm.name = "Palm";
                palm.transform.SetParent(parent);
                palm.transform.position = RandomGroundPoint(random) + Vector3.up * 5f;
                palm.transform.localScale = new Vector3(0.5f, 5f, 0.5f);
                palm.GetComponent<Renderer>().sharedMaterial = palmMaterial;
                palm.isStatic = true;
            }
        }

        private static HorseController CreateHorse(HorseSettings settings, out Transform cameraTarget)
        {
            var root = new GameObject("Horse");

            var body = root.AddComponent<Rigidbody>();
            body.mass = 450f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2; // along Z, nose to tail
            collider.radius = 0.7f;
            collider.height = 2.6f;
            collider.center = new Vector3(0f, 0.7f, 0f);
            collider.sharedMaterial = CreateFrictionlessMaterial();

            var controller = root.AddComponent<HorseController>();
            SetReference(controller, "_settings", settings);

            Material coat = CreateMaterial("HorseCoat", new Color(0.45f, 0.26f, 0.14f));
            Material dark = CreateMaterial("HorseDark", new Color(0.12f, 0.08f, 0.06f));
            Material poncho = CreateMaterial("RiderPoncho", new Color(0.75f, 0.15f, 0.12f));
            Material hat = CreateMaterial("RiderHat", new Color(0.85f, 0.8f, 0.65f));

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            AddPart(visual, PrimitiveType.Cube, "Body", new Vector3(0f, 1.35f, 0f), new Vector3(0.75f, 0.8f, 2f), coat);
            AddPart(visual, PrimitiveType.Cube, "Neck", new Vector3(0f, 1.85f, 1.05f), new Vector3(0.4f, 0.9f, 0.45f), coat, new Vector3(30f, 0f, 0f));
            AddPart(visual, PrimitiveType.Cube, "Head", new Vector3(0f, 2.2f, 1.45f), new Vector3(0.35f, 0.35f, 0.7f), coat);
            AddPart(visual, PrimitiveType.Cube, "Tail", new Vector3(0f, 1.3f, -1.1f), new Vector3(0.15f, 0.7f, 0.15f), dark, new Vector3(-20f, 0f, 0f));
            foreach (float x in new[] { -0.25f, 0.25f })
            foreach (float z in new[] { -0.75f, 0.75f })
                AddPart(visual, PrimitiveType.Cube, "Leg", new Vector3(x, 0.5f, z), new Vector3(0.18f, 1f, 0.18f), dark);

            Transform rider = new GameObject("Rider").transform;
            rider.SetParent(root.transform, false);
            AddPart(rider, PrimitiveType.Capsule, "Torso", new Vector3(0f, 2.25f, 0.1f), new Vector3(0.5f, 0.5f, 0.4f), poncho);
            AddPart(rider, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.95f, 0.1f), Vector3.one * 0.3f, hat);
            AddPart(rider, PrimitiveType.Cylinder, "Sombrero", new Vector3(0f, 3.1f, 0.1f), new Vector3(0.65f, 0.03f, 0.65f), hat);

            cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.SetParent(root.transform, false);
            cameraTarget.localPosition = new Vector3(0f, 2.6f, 0f);

            var input = root.AddComponent<RiderInput>();
            SetReference(input, "_horse", controller);
            SetReference(input, "_move", FindAction("Player/Move"));
            SetReference(input, "_gallop", FindAction("Player/Sprint"));

            return controller;
        }

        private static void CreateCamera(Transform target)
        {
            Camera mainCamera = Camera.main;
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
            mainCamera.farClipPlane = 800f;

            var go = new GameObject("CM Rider Camera");
            var cinemachineCamera = go.AddComponent<CinemachineCamera>();
            cinemachineCamera.Target.TrackingTarget = target;
            cinemachineCamera.Lens.FieldOfView = 65f;

            var orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 7f;
            // Orbit is relative to the horse's heading, so the camera swings behind it as it turns.
            orbit.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.2f, 0.4f, 0.3f);
            orbit.TrackerSettings.RotationDamping = new Vector3(0f, 0.5f, 0f);

            InputAxis horizontal = orbit.HorizontalAxis;
            horizontal.Recentering.Enabled = true;
            horizontal.Recentering.Wait = 1f;
            horizontal.Recentering.Time = 0.5f;
            orbit.HorizontalAxis = horizontal;

            InputAxis vertical = orbit.VerticalAxis;
            vertical.Value = vertical.Center = 12f;
            vertical.Range = new Vector2(-10f, 50f);
            orbit.VerticalAxis = vertical;

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.1f, 0.1f);

            var axisController = go.AddComponent<CinemachineInputAxisController>();
            axisController.SynchronizeControllers();
            foreach (var axis in axisController.Controllers)
                Debug.Log($"[ProtoSabanaBuilder] Camera axis '{axis.Name}' -> {(axis.Input.InputAction != null ? axis.Input.InputAction.name : "NO ACTION")}, gain {axis.Input.Gain}");
        }

        private static InputActionReference FindAction(string path)
        {
            InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.name == path);
            if (reference == null)
                throw new System.InvalidOperationException($"Input action '{path}' not found in {InputActionsPath}.");
            return reference;
        }

        private static void CreateBlock(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, rotation);
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
        }

        private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Material material, Vector3 euler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Vector3 RandomGroundPoint(System.Random random)
        {
            Vector2 point;
            do
            {
                point = new Vector2(Range(random, -ObstacleRadius, ObstacleRadius), Range(random, -ObstacleRadius, ObstacleRadius));
            } while (point.magnitude < SpawnClearRadius);
            return new Vector3(point.x, 0f, point.y);
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static Material CreateMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            return SaveAsset(material, $"{GeneratedFolder}/{name}.mat");
        }

        private static PhysicsMaterial CreateFrictionlessMaterial()
        {
            // No friction: the controller drives velocity itself, ground friction would only fight it.
            var material = new PhysicsMaterial("HorseFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            return SaveAsset(material, $"{GeneratedFolder}/HorseFrictionless.asset");
        }

        private static Texture2D CreateCheckerTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size);
            var light = new Color(0.47f, 0.6f, 0.28f);
            var dark = new Color(0.4f, 0.53f, 0.24f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, (x < size / 2) == (y < size / 2) ? light : dark);

            string path = $"{GeneratedFolder}/GroundChecker.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
