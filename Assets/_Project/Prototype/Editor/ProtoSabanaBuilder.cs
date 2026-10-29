using System.IO;
using System.Linq;
using Arreo.Herd;
using Arreo.Horse;
using Arreo.Player;
using Arreo.Rope;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Arreo.Prototype.Editor
{
    /// <summary>
    /// DISPOSABLE (weeks 2-4 prototype): builds the Proto_Sabana test scene from code.
    /// Rebuilding overwrites the scene but keeps the settings assets, so tuning survives.
    /// </summary>
    public static class ProtoSabanaBuilder
    {
        private const string Root = "Assets/_Project/Prototype";
        private const string ScenePath = Root + "/Proto_Sabana.unity";
        private const string GeneratedFolder = Root + "/Generated";
        private const string HorseSettingsFolder = "Assets/_Project/Data/Horse";
        private const string HorseSettingsPath = HorseSettingsFolder + "/HorseSettings_Proto.asset";
        private const string HerdSettingsFolder = "Assets/_Project/Data/Herd";
        private const string HerdSettingsPath = HerdSettingsFolder + "/HerdSettings_Proto.asset";
        private const string RopeSettingsFolder = "Assets/_Project/Data/Rope";
        private const string RopeSettingsPath = RopeSettingsFolder + "/RopeSettings_Proto.asset";
        private const string RiderSettingsFolder = "Assets/_Project/Data/Rider";
        private const string RiderSettingsPath = RiderSettingsFolder + "/RiderSettings_Proto.asset";
        private const string CameraSettingsFolder = "Assets/_Project/Data/Camera";
        private const string CameraSettingsPath = CameraSettingsFolder + "/CameraSettings_Proto.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string BenchmarkPlayerPath = "Build/Benchmark/Arreo.exe";

        private const float GroundSize = 600f;
        private const float ObstacleRadius = 270f;
        private const float SpawnClearRadius = 15f;

        private static readonly Vector3 HerdCenter = new Vector3(45f, 0f, 55f);
        private const float HerdClearRadius = 25f;
        private static readonly Vector3 CorralCenter = new Vector3(-55f, 0f, -55f);
        private const float CorralSize = 24f;
        private const float GateWidth = 8f;
        private const float CorralClearRadius = 42f;

        [MenuItem("Arreo/Prototype/Rebuild Proto_Sabana")]
        public static void Build()
        {
            EnsureFolder(GeneratedFolder);
            EnsureFolder(HorseSettingsFolder);
            EnsureFolder(HerdSettingsFolder);
            EnsureFolder(RopeSettingsFolder);
            EnsureFolder(RiderSettingsFolder);
            EnsureFolder(CameraSettingsFolder);

            // Load every asset only after NewScene: it unloads unused assets, and a settings asset
            // loaded before it came back as a dead reference (that left ProtoCameraRig without settings).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cameraSettings = LoadOrCreate<CameraSettings>(CameraSettingsPath);
            PhysicsMaterial frictionless = CreateFrictionlessMaterial();
            Light sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateGround();
            CreateBorders();
            CreateObstacles();
            HorseController horse = CreateHorse(LoadOrCreate<HorseSettings>(HorseSettingsPath), frictionless, out Transform cameraTarget);
            CinemachineCamera riderCamera = CreateCamera(cameraTarget, cameraSettings);
            ActiveHerd herd = CreateHerd(horse, frictionless);
            Corral corral = CreateCorral(horse.transform, out ProtoGate gate);
            RopeSimulation rope = CreateRope(horse);
            RiderMount mount = CreateRider(horse, rope, out LassoInput lasso);
            CreateRopeCamera(riderCamera, cameraTarget, rope, lasso, mount, cameraSettings);

            var thunder = new GameObject("ProtoThunder").AddComponent<ProtoThunder>();
            SetReference(thunder, "_herd", herd);
            SetReference(thunder, "_sun", sun);

            var drive = new GameObject("ProtoDrive").AddComponent<ProtoDrive>();
            SetReference(drive, "_herd", herd);
            SetReference(drive, "_corral", corral);
            SetReference(drive, "_horse", horse);

            var hud = new GameObject("ProtoHud").AddComponent<ProtoHud>();
            SetReference(hud, "_horse", horse);
            SetReference(hud, "_drive", drive);
            SetReference(hud, "_gate", gate);
            SetReference(hud, "_rope", rope);
            SetReference(hud, "_herd", herd);
            SetReference(hud, "_lasso", lasso);
            SetReference(hud, "_mount", mount);
            SetReference(hud, "_riderInput", horse.GetComponent<RiderInput>());

            var benchmark = new GameObject("ProtoBenchmark").AddComponent<ProtoBenchmark>();
            SetReference(benchmark, "_herd", herd);
            SetReference(benchmark, "_horse", horse);
            SetReference(benchmark, "_riderInput", horse.GetComponent<RiderInput>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProtoSabanaBuilder] Saved {ScenePath}");
        }

        [MenuItem("Arreo/Prototype/Build FPS Benchmark Player")]
        public static void BuildBenchmarkPlayer()
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BenchmarkPlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[ProtoSabanaBuilder] Benchmark player build: {report.summary.result} -> {BenchmarkPlayerPath}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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

        private static void CreateBorders()
        {
            var parent = new GameObject("Borders").transform;
            Material material = CreateMaterial("Border", new Color(0.3f, 0.3f, 0.35f));
            float half = GroundSize / 2f;
            CreateBlock("Border", parent, new Vector3(0f, 1.5f, half), Quaternion.identity, new Vector3(GroundSize, 3f, 1f), material);
            CreateBlock("Border", parent, new Vector3(0f, 1.5f, -half), Quaternion.identity, new Vector3(GroundSize, 3f, 1f), material);
            CreateBlock("Border", parent, new Vector3(half, 1.5f, 0f), Quaternion.identity, new Vector3(1f, 3f, GroundSize), material);
            CreateBlock("Border", parent, new Vector3(-half, 1.5f, 0f), Quaternion.identity, new Vector3(1f, 3f, GroundSize), material);
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

        private static HorseController CreateHorse(HorseSettings settings, PhysicsMaterial frictionless, out Transform cameraTarget)
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
            collider.sharedMaterial = frictionless;

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
            SetReference(input, "_slowWalk", FindAction("Player/SlowWalk"));

            return controller;
        }

        private static CinemachineCamera CreateCamera(Transform target, CameraSettings settings)
        {
            Camera mainCamera = Camera.main;
            var brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, settings.BlendSeconds);
            mainCamera.farClipPlane = 800f;

            var go = new GameObject("CM Rider Camera");
            var cinemachineCamera = go.AddComponent<CinemachineCamera>();
            cinemachineCamera.Target.TrackingTarget = target;
            cinemachineCamera.Lens.FieldOfView = settings.FieldOfView;
            cinemachineCamera.Priority.Value = 10;

            var orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = settings.Distance;
            // Orbit is relative to the horse's heading, so the camera swings behind it as it turns.
            orbit.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            orbit.TrackerSettings.PositionDamping = settings.PositionDamping;
            orbit.TrackerSettings.RotationDamping = new Vector3(0f, settings.TurnDamping, 0f);

            InputAxis horizontal = orbit.HorizontalAxis;
            horizontal.Recentering.Enabled = true;
            horizontal.Recentering.Wait = settings.RecenterWait;
            horizontal.Recentering.Time = settings.RecenterTime;
            orbit.HorizontalAxis = horizontal;

            InputAxis vertical = orbit.VerticalAxis;
            vertical.Value = vertical.Center = settings.StartPitch;
            vertical.Range = settings.PitchRange;
            orbit.VerticalAxis = vertical;

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.1f, 0.1f);
            AddDecollider(go, settings);

            var axisController = go.AddComponent<CinemachineInputAxisController>();
            axisController.SynchronizeControllers();
            foreach (var axis in axisController.Controllers)
                Debug.Log($"[ProtoSabanaBuilder] Camera axis '{axis.Name}' -> {(axis.Input.InputAction != null ? axis.Input.InputAction.name : "NO ACTION")}, gain {axis.Input.Gain}");
            return cinemachineCamera;
        }

        private static void CreateRopeCamera(CinemachineCamera riderCamera, Transform riderTarget, RopeSimulation rope,
            LassoInput lasso, RiderMount mount, CameraSettings settings)
        {
            // Rider plus the caught head; the rope camera looks at the pair and zooms to fit both.
            var group = new GameObject("RopeTargetGroup").AddComponent<CinemachineTargetGroup>();
            group.AddMember(riderTarget, 1f, 1.5f);

            var go = new GameObject("CM Rope Camera");
            var ropeCamera = go.AddComponent<CinemachineCamera>();
            ropeCamera.Target.TrackingTarget = riderTarget;
            ropeCamera.Target.CustomLookAtTarget = true;
            ropeCamera.Target.LookAtTarget = group.transform;
            ropeCamera.Lens.FieldOfView = settings.RopeFieldOfView;
            ropeCamera.Priority.Value = 0;

            var orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = settings.RopeDistance;
            // World space: ProtoCameraRig turns the horizontal axis so the camera sits behind the rider facing the catch.
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = Vector3.one * settings.RopePositionDamping;
            InputAxis vertical = orbit.VerticalAxis;
            vertical.Value = vertical.Center = settings.RopePitch;
            orbit.VerticalAxis = vertical;

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.3f, 0.3f);
            var framing = go.AddComponent<CinemachineGroupFraming>();
            framing.FramingSize = settings.FramingSize;
            framing.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.ZoomOnly;
            framing.Damping = settings.FramingDamping;
            CinemachineDecollider ropeDecollider = AddDecollider(go, settings);

            var rig = new GameObject("ProtoCameraRig").AddComponent<ProtoCameraRig>();
            SetReference(rig, "_settings", settings);
            SetReference(rig, "_brain", Camera.main.GetComponent<CinemachineBrain>());
            SetReference(rig, "_ropeFraming", framing);
            SetReferences(rig, "_decolliders", riderCamera.GetComponent<CinemachineDecollider>(), ropeDecollider);
            SetReference(rig, "_riderCamera", riderCamera);
            SetReference(rig, "_riderOrbit", riderCamera.GetComponent<CinemachineOrbitalFollow>());
            SetReference(rig, "_riderComposer", riderCamera.GetComponent<CinemachineRotationComposer>());
            SetReference(rig, "_ropeCamera", ropeCamera);
            SetReference(rig, "_ropeOrbit", orbit);
            SetReference(rig, "_group", group);
            SetReference(rig, "_riderTarget", riderTarget);
            SetReference(rig, "_rope", rope);
            SetReference(rig, "_lasso", lasso);
            SetReference(rig, "_mount", mount);
        }

        private static CinemachineDecollider AddDecollider(GameObject camera, CameraSettings settings)
        {
            // Keeps the camera out of the horse, cattle, obstacles and ground. Resolved from the rider
            // (follow target) so a rope camera looking at the herd isn't pulled in by cows near the catch.
            var decollider = camera.AddComponent<CinemachineDecollider>();
            decollider.CameraRadius = settings.CameraRadius;
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = true,
                ObstacleLayers = Physics.DefaultRaycastLayers,
                UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings { Enabled = true, YOffset = 0f },
                Damping = settings.CollisionDamping,
                SmoothingTime = 0.1f,
            };
            return decollider;
        }

        private static RiderMount CreateRider(HorseController horse, RopeSimulation rope, out LassoInput lasso)
        {
            Rigidbody horseBody = horse.GetComponent<Rigidbody>();
            var settings = LoadOrCreate<RiderSettings>(RiderSettingsPath);

            var mount = horse.gameObject.AddComponent<RiderMount>();
            SetReference(mount, "_settings", settings);
            SetReference(mount, "_horse", horse);
            SetReference(mount, "_rope", rope);
            SetReference(mount, "_riderModel", horse.transform.Find("Rider"));
            SetReference(mount, "_clothes", AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedFolder}/RiderPoncho.mat"));
            SetReference(mount, "_skin", AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedFolder}/RiderHat.mat"));
            SetReference(horse.GetComponent<RiderInput>(), "_mount", mount);

            lasso = horse.gameObject.AddComponent<LassoInput>();
            SetReference(lasso, "_rope", rope);
            SetReference(lasso, "_mount", mount);
            SetReference(lasso, "_horse", horseBody);
            SetReference(lasso, "_aim", FindAction("Player/Aim"));
            SetReference(lasso, "_throw", FindAction("Player/Attack"));
            SetReference(lasso, "_release", FindAction("Player/ReleaseRope"));
            return mount;
        }

        private static ActiveHerd CreateHerd(HorseController horse, PhysicsMaterial frictionless)
        {
            var go = new GameObject("Herd");
            go.transform.position = HerdCenter;
            var herd = go.AddComponent<ActiveHerd>();
            SetReference(herd, "_settings", LoadOrCreate<HerdSettings>(HerdSettingsPath));
            SetReference(herd, "_cowPrefab", CreateCowPrefab(frictionless));
            SetReference(herd, "_rider", horse.GetComponent<Rigidbody>());
            return herd;
        }

        private static Rigidbody CreateCowPrefab(PhysicsMaterial frictionless)
        {
            var root = new GameObject("Cow_Proto");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 400f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.55f;
            collider.height = 2.1f;
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.sharedMaterial = frictionless;

            // Cebu-like llanero cattle: pale coat, hump, dark horns.
            Material coat = CreateMaterial("CowCoat", new Color(0.86f, 0.84f, 0.78f));
            Material dark = CreateMaterial("CowDark", new Color(0.2f, 0.18f, 0.16f));
            AddPart(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.7f, 1.7f), coat);
            AddPart(root.transform, PrimitiveType.Cube, "Hump", new Vector3(0f, 1.38f, 0.45f), new Vector3(0.4f, 0.3f, 0.4f), coat);
            AddPart(root.transform, PrimitiveType.Cube, "Head", new Vector3(0f, 1.05f, 1.05f), new Vector3(0.35f, 0.4f, 0.55f), coat);
            AddPart(root.transform, PrimitiveType.Cube, "Horns", new Vector3(0f, 1.32f, 1.05f), new Vector3(0.7f, 0.08f, 0.08f), dark);
            foreach (float x in new[] { -0.22f, 0.22f })
            foreach (float z in new[] { -0.6f, 0.6f })
                AddPart(root.transform, PrimitiveType.Cube, "Leg", new Vector3(x, 0.3f, z), new Vector3(0.15f, 0.6f, 0.15f), dark);

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.1f, 1.15f);
            var lassoTarget = root.AddComponent<LassoTarget>();
            SetReference(lassoTarget, "_head", head);

            string path = $"{GeneratedFolder}/Cow_Proto.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Rigidbody>();
        }

        private static RopeSimulation CreateRope(HorseController horse)
        {
            Rigidbody horseBody = horse.GetComponent<Rigidbody>();

            // Simulation and drawing live on separate objects: from week 6 only the host simulates.
            var rope = new GameObject("RopeSimulation").AddComponent<RopeSimulation>();
            SetReference(rope, "_settings", LoadOrCreate<RopeSettings>(RopeSettingsPath));
            SetReference(rope, "_saddleBody", horseBody);

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            SaveAsset(material, $"{GeneratedFolder}/Rope.mat");
            var view = new GameObject("RopeView").AddComponent<RopeView>();
            SetReference(view, "_rope", rope);
            SetReference(view, "_line", CreateLine(view.transform, "RopeLine", material, 0.045f));
            SetReference(view, "_loop", CreateLine(view.transform, "RopeLoop", material, 0.04f));

            var link = new GameObject("ProtoRopeLink").AddComponent<ProtoRopeLink>();
            SetReference(link, "_rope", rope);
            SetReference(link, "_horse", horse);
            return rope;
        }

        private static LineRenderer CreateLine(Transform parent, string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.positionCount = 0;
            return line;
        }

        private static Corral CreateCorral(Transform rider, out ProtoGate gate)
        {
            var root = new GameObject("Corral").transform;
            root.position = CorralCenter;
            var corral = root.gameObject.AddComponent<Corral>();
            Material wood = CreateMaterial("Fence", new Color(0.42f, 0.3f, 0.2f));

            float half = CorralSize / 2f;
            float gapHalf = GateWidth / 2f;
            float sideLength = half - gapHalf;
            var fence = new Vector3(0.3f, 1.6f, 1f);

            void Wall(Vector3 localCenter, float yaw, float length) => CreateBlock("Fence", root,
                CorralCenter + localCenter + Vector3.up * 0.8f, Quaternion.Euler(0f, yaw, 0f),
                new Vector3(fence.x, fence.y, length), wood);

            Wall(new Vector3(0f, 0f, -half), 90f, CorralSize);
            Wall(new Vector3(-half, 0f, 0f), 0f, CorralSize);
            Wall(new Vector3(half, 0f, 0f), 0f, CorralSize);
            // North side: two stretches with the gate opening between them.
            Wall(new Vector3(-(gapHalf + sideLength / 2f), 0f, half), 90f, sideLength);
            Wall(new Vector3(gapHalf + sideLength / 2f, 0f, half), 90f, sideLength);

            // Wing fences opening out from the gate like a funnel.
            const float wingLength = 16f;
            const float wingAngle = 35f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 start = new Vector3(side * gapHalf, 0f, half);
                Vector3 direction = Quaternion.Euler(0f, side * wingAngle, 0f) * Vector3.forward;
                Wall(start + direction * (wingLength / 2f), side * wingAngle, wingLength);
            }

            // Gate: a kinematic hinge at the west edge of the opening; closed, the panel spans it.
            var hinge = new GameObject("GateHinge");
            hinge.transform.SetParent(root, false);
            hinge.transform.position = CorralCenter + new Vector3(-gapHalf, 0f, half);
            var hingeBody = hinge.AddComponent<Rigidbody>();
            hingeBody.isKinematic = true;
            hingeBody.interpolation = RigidbodyInterpolation.Interpolate;
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "GatePanel";
            panel.transform.SetParent(hinge.transform, false);
            panel.transform.localPosition = new Vector3(gapHalf, 0.8f, 0f);
            panel.transform.localScale = new Vector3(GateWidth, 1.6f, 0.2f);
            panel.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Gate", new Color(0.62f, 0.45f, 0.28f));

            gate = hinge.AddComponent<ProtoGate>();
            SetReference(gate, "_hinge", hingeBody);
            SetReference(gate, "_rider", rider);
            SetReference(gate, "_interact", FindAction("Player/Interact"));

            return corral;
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
            Vector3 point;
            do
            {
                point = new Vector3(Range(random, -ObstacleRadius, ObstacleRadius), 0f, Range(random, -ObstacleRadius, ObstacleRadius));
            } while (point.magnitude < SpawnClearRadius
                     || Vector3.Distance(point, HerdCenter) < HerdClearRadius
                     || Vector3.Distance(point, CorralCenter) < CorralClearRadius);
            return point;
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
            // No friction: controllers drive velocity themselves, ground friction would only fight them.
            var material = new PhysicsMaterial("Frictionless")
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
            // Unity's == also catches destroyed objects, which would otherwise save as an empty field.
            if (value == null)
                throw new System.InvalidOperationException($"{target.GetType().Name}.{field} would be left empty.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (property.objectReferenceValue == null)
                throw new System.InvalidOperationException($"{target.GetType().Name}.{field} did not keep its reference.");
        }

        private static void SetReferences(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized array '{field}'.");
            for (int i = 0; i < values.Length; i++)
                if (values[i] == null)
                    throw new System.InvalidOperationException($"{target.GetType().Name}.{field}[{i}] would be left empty.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
