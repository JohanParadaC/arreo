using System.IO;
using System.Linq;
using Arreo.Economy;
using Arreo.Herd;
using Arreo.Horse;
using Arreo.Net;
using Arreo.Player;
using Arreo.Rope;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
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
    /// DISPOSABLE (weeks 2-7 prototype): builds the Proto_Sabana test scene from code.
    /// Rebuilding overwrites the scene but keeps the settings assets, so tuning survives.
    /// Since week 5 the horse and rider are a network player prefab spawned per player; scene
    /// objects that need the local rider get it at runtime from ProtoLocalRiderBinder.
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
        private const string EconomySettingsFolder = "Assets/_Project/Data/Economy";
        private const string EconomySettingsPath = EconomySettingsFolder + "/EconomySettings_Proto.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string BenchmarkPlayerPath = "Build/Benchmark/Arreo.exe";
        private const string NetworkTestPlayerPath = "Build/NetworkTest/Arreo.exe";
        private const string PlayerTag = "Player";
        private const string GroundLayerName = "Ground";

        private const float GroundSize = 600f;
        private const float ObstacleRadius = 270f;
        private const float SpawnClearRadius = 15f;

        private static readonly Vector3 HerdCenter = new Vector3(45f, 0f, 55f);
        private const float HerdClearRadius = 25f;
        private static readonly Vector3 CorralCenter = new Vector3(-55f, 0f, -55f);
        private const float CorralSize = 24f;
        private const float GateWidth = 8f;
        private const float CorralClearRadius = 42f;

        // Week 7: the pueblo, about 150 m from both the hato's corral and the herd, with the fair
        // pen's gate facing the herd (east).
        private static readonly Vector3 PuebloCenter = new Vector3(-100f, 0f, 90f);
        private const float FairPenYaw = 90f;
        private const float FairPenSize = 20f;
        private const float FairGateWidth = 8f;
        private const float PuebloClearRadius = 50f;

        [MenuItem("Arreo/Prototype/Rebuild Proto_Sabana")]
        public static void Build()
        {
            EnsureFolder(GeneratedFolder);
            EnsureFolder(HorseSettingsFolder);
            EnsureFolder(HerdSettingsFolder);
            EnsureFolder(RopeSettingsFolder);
            EnsureFolder(RiderSettingsFolder);
            EnsureFolder(CameraSettingsFolder);
            EnsureFolder(EconomySettingsFolder);

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
            NetworkObject playerPrefab = CreatePlayerPrefab(LoadOrCreate<HorseSettings>(HorseSettingsPath), frictionless);
            CinemachineCamera riderCamera = CreateCamera(cameraSettings);
            ActiveHerd herd = CreateHerd(frictionless);
            Corral corral = CreateCorral(out ProtoGate gate);
            Corral fairPen = CreatePueblo(out Transform salePoint);
            ProtoCameraRig cameraRig = CreateRopeCamera(riderCamera, cameraSettings);

            var thunder = new GameObject("ProtoThunder").AddComponent<ProtoThunder>();
            SetReference(thunder, "_herd", herd);
            SetReference(thunder, "_sun", sun);

            // Stampede, gate and corral count: any player asks, the host applies, everyone sees it.
            var worldObject = new GameObject("NetworkWorld");
            worldObject.AddComponent<NetworkObject>();
            var world = worldObject.AddComponent<NetworkWorld>();
            SetReference(world, "_herd", herd);
            SetReference(world, "_networkHerd", herd.GetComponent<NetworkHerd>());
            SetReference(world, "_corral", corral);
            SetReference(world, "_gate", gate);
            SetReference(world, "_thunder", thunder);
            SetReference(world, "_interact", FindAction("Player/Interact"));

            // The hato's money (the host's, D74) and the fair that pays into it.
            var economyObject = new GameObject("NetworkEconomy");
            economyObject.AddComponent<NetworkObject>();
            var economy = economyObject.AddComponent<NetworkEconomy>();
            SetReference(economy, "_settings", LoadOrCreate<EconomySettings>(EconomySettingsPath));

            var fairObject = new GameObject("ProtoFair");
            fairObject.AddComponent<NetworkObject>();
            var fair = fairObject.AddComponent<ProtoFair>();
            SetReference(fair, "_herd", herd.GetComponent<NetworkHerd>());
            SetReference(fair, "_pen", fairPen);
            SetReference(fair, "_salePoint", salePoint);
            SetReference(fair, "_economy", economy);
            SetReference(fair, "_interact", FindAction("Player/Interact"));

            var hud = new GameObject("ProtoHud").AddComponent<ProtoHud>();
            SetReference(hud, "_world", world);
            SetReference(hud, "_fair", fair);
            SetReference(hud, "_economy", economy);

            var benchmark = new GameObject("ProtoBenchmark").AddComponent<ProtoBenchmark>();
            SetReference(benchmark, "_herd", herd);

            NetworkManager networkManager = CreateNetwork(playerPrefab);
            var binder = new GameObject("ProtoLocalRiderBinder").AddComponent<ProtoLocalRiderBinder>();
            SetReference(binder, "_cameraRig", cameraRig);
            SetReference(binder, "_hud", hud);
            SetReference(binder, "_benchmark", benchmark);

            var bot = new GameObject("ProtoNetBot").AddComponent<ProtoNetBot>();
            SetReference(bot, "_networkManager", networkManager);
            SetReference(bot, "_herd", herd);
            new GameObject("ProtoSaleBot").AddComponent<ProtoSaleBot>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssignSceneIds(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProtoSabanaBuilder] Saved {ScenePath}");
        }

        // Scene NetworkObjects (herd, world) need unique scene ids, which FishNet only makes from the
        // inspector or its Reserialize menu; this is the same internal call, once the scene has its path.
        private static void AssignSceneIds(Scene scene)
        {
            System.Reflection.MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (create == null)
                throw new System.InvalidOperationException("FishNet's NetworkObject.CreateSceneId(Scene, bool, out int) was not found.");
            var arguments = new object[] { scene, true, 0 };
            create.Invoke(null, arguments);
            Debug.Log($"[ProtoSabanaBuilder] Scene ids assigned to {arguments[2]} network objects");
        }

        [MenuItem("Arreo/Prototype/Build FPS Benchmark Player")]
        public static void BuildBenchmarkPlayer() => BuildPlayer(BenchmarkPlayerPath, BuildOptions.None);

        // Development build: FishNet's latency/packet-loss simulator only runs in the Editor and development builds.
        [MenuItem("Arreo/Prototype/Build Network Test Player")]
        public static void BuildNetworkTestPlayer() => BuildPlayer(NetworkTestPlayerPath, BuildOptions.Development);

        private static void BuildPlayer(string path, BuildOptions options)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options,
            });
            Debug.Log($"[ProtoSabanaBuilder] Player build ({options}): {report.summary.result} -> {path}");
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
            // Its own layer, so whatever needs "the floor" (the camera's terrain check) doesn't
            // mistake ramps, palms or walls for it.
            ground.layer = GroundLayer();
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

            // The pueblo (week 7) is kept clear by skipping what would land on it, after drawing it
            // from the random sequence as before, so every other obstacle stays where it was.
            for (int i = 0; i < 120; i++)
            {
                Vector3 size = new Vector3(Range(random, 1f, 6f), Range(random, 0.5f, 4f), Range(random, 1f, 6f));
                Vector3 position = RandomGroundPoint(random) + Vector3.up * (size.y / 2f);
                Quaternion rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
                if (!InPueblo(position))
                    CreateBlock("Box", parent, position, rotation, size, boxMaterial);
            }

            for (int i = 0; i < 25; i++)
            {
                float length = Range(random, 8f, 14f);
                float angle = Range(random, 10f, 20f);
                Vector3 position = RandomGroundPoint(random)
                    + Vector3.up * (Mathf.Sin(angle * Mathf.Deg2Rad) * length / 2f);
                Quaternion rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f) * Quaternion.Euler(-angle, 0f, 0f);
                if (!InPueblo(position))
                    CreateBlock("Ramp", parent, position, rotation, new Vector3(4f, 0.4f, length), rampMaterial);
            }

            // Tall moriche-like markers to judge distance and speed.
            for (int i = 0; i < 40; i++)
            {
                Vector3 position = RandomGroundPoint(random) + Vector3.up * 5f;
                if (InPueblo(position))
                    continue;
                GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                palm.name = "Palm";
                palm.transform.SetParent(parent);
                palm.transform.position = position;
                palm.transform.localScale = new Vector3(0.5f, 5f, 0.5f);
                palm.GetComponent<Renderer>().sharedMaterial = palmMaterial;
                palm.isStatic = true;
            }
        }

        private static NetworkObject CreatePlayerPrefab(HorseSettings settings, PhysicsMaterial frictionless)
        {
            HorseController horse = CreateHorse(settings, frictionless, out Transform cameraTarget);
            GameObject root = horse.gameObject;
            RiderMount mount = AddRider(horse, out LassoInput lasso);

            // Owner moves it (D74); everyone else gets an interpolated, kinematic copy.
            var networkObject = root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            SetValue(networkTransform, "_componentConfiguration", (int)NetworkTransform.ComponentConfigurationType.Rigidbody);
            SetValue(networkTransform, "_clientAuthoritative", true);
            SetValue(networkTransform, "_sendToOwner", false);
            SetValue(networkTransform, "_synchronizeScale", false);
            SetValue(networkTransform, "_interpolation", 2);

            var rider = root.AddComponent<NetworkRider>();
            SetReference(rider, "_horse", horse);
            SetReference(rider, "_input", root.GetComponent<RiderInput>());
            SetReference(rider, "_lasso", lasso);
            SetReference(rider, "_mount", mount);
            SetReference(rider, "_cameraTarget", cameraTarget);

            // Each player's lasso rides with their horse; only the host simulates it.
            var rope = root.AddComponent<RopeSimulation>();
            SetReference(rope, "_settings", LoadOrCreate<RopeSettings>(RopeSettingsPath));
            SetReference(rope, "_saddleBody", root.GetComponent<Rigidbody>());
            var networkRope = root.AddComponent<NetworkRope>();
            SetReference(networkRope, "_rope", rope);
            SetReference(networkRope, "_rider", rider);
            SetReference(rider, "_rope", networkRope);
            CreateRopeView(root.transform, networkRope);

            // Off until NetworkRider knows whose horse this is: a remote copy never drives its body.
            horse.enabled = false;
            root.GetComponent<RiderInput>().enabled = false;

            string path = $"{GeneratedFolder}/Horse_Player.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        private static HorseController CreateHorse(HorseSettings settings, PhysicsMaterial frictionless, out Transform cameraTarget)
        {
            // Untagged: NetworkRider tags only the local rider as Player at runtime, so the camera
            // sees through your own horse but not other players'.
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

        // No tracking target yet: ProtoCameraRig points it at the local rider once that spawns.
        private static CinemachineCamera CreateCamera(CameraSettings settings)
        {
            Camera mainCamera = Camera.main;
            var brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, settings.BlendSeconds);
            mainCamera.farClipPlane = 800f;

            var go = new GameObject("CM Rider Camera");
            var cinemachineCamera = go.AddComponent<CinemachineCamera>();
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
            AddDeoccluder(go, settings);

            var axisController = go.AddComponent<CinemachineInputAxisController>();
            axisController.SynchronizeControllers();
            foreach (var axis in axisController.Controllers)
                Debug.Log($"[ProtoSabanaBuilder] Camera axis '{axis.Name}' -> {(axis.Input.InputAction != null ? axis.Input.InputAction.name : "NO ACTION")}, gain {axis.Input.Gain}");
            return cinemachineCamera;
        }

        private static ProtoCameraRig CreateRopeCamera(CinemachineCamera riderCamera, CameraSettings settings)
        {
            // Rider plus the caught head; the rope camera looks at the pair and zooms to fit both.
            // The local rider is added to the group (and tracked) at runtime by ProtoCameraRig.Bind.
            var group = new GameObject("RopeTargetGroup").AddComponent<CinemachineTargetGroup>();

            var go = new GameObject("CM Rope Camera");
            var ropeCamera = go.AddComponent<CinemachineCamera>();
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
            CinemachineDeoccluder ropeDeoccluder = AddDeoccluder(go, settings);

            var rig = new GameObject("ProtoCameraRig").AddComponent<ProtoCameraRig>();
            SetReference(rig, "_settings", settings);
            SetReference(rig, "_brain", Camera.main.GetComponent<CinemachineBrain>());
            SetReference(rig, "_ropeFraming", framing);
            SetReferences(rig, "_decolliders", riderCamera.GetComponent<CinemachineDecollider>(), ropeDecollider);
            SetReferences(rig, "_deoccluders", riderCamera.GetComponent<CinemachineDeoccluder>(), ropeDeoccluder);
            SetReference(rig, "_riderCamera", riderCamera);
            SetReference(rig, "_riderOrbit", riderCamera.GetComponent<CinemachineOrbitalFollow>());
            SetReference(rig, "_riderComposer", riderCamera.GetComponent<CinemachineRotationComposer>());
            SetReference(rig, "_ropeCamera", ropeCamera);
            SetReference(rig, "_ropeOrbit", orbit);
            SetReference(rig, "_group", group);
            return rig;
        }

        private static CinemachineDecollider AddDecollider(GameObject camera, CameraSettings settings)
        {
            // Keeps the camera above the ground and out of the horse, cattle and obstacles. Resolved
            // from the rider (follow target) so a rope camera looking at the herd isn't pulled in by
            // cows near the catch. Only the Ground layer is terrain: left on its default (every
            // Default-layer object), terrain lifted the camera over walls and switched off decollision.
            int groundMask = 1 << GroundLayer();
            var decollider = camera.AddComponent<CinemachineDecollider>();
            decollider.CameraRadius = settings.CameraRadius;
            decollider.TerrainResolution = new CinemachineDecollider.TerrainSettings
            {
                Enabled = true,
                TerrainLayers = groundMask,
                MaximumRaycast = 10f,
                Damping = 0.5f,
            };
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = true,
                ObstacleLayers = Physics.DefaultRaycastLayers & ~groundMask,
                UseFollowTarget = new CinemachineDecollider.DecollisionSettings.FollowTargetSettings { Enabled = true, YOffset = 0f },
                Damping = settings.CollisionDamping,
                SmoothingTime = 0.1f,
            };
            return decollider;
        }

        private static CinemachineDeoccluder AddDeoccluder(GameObject camera, CameraSettings settings)
        {
            // The decollider only keeps the camera out of things; this pulls it in front of a ramp or
            // palm that hides the rider. Line of sight to the rider (follow target), seeing through
            // everything tagged as the player: the horse and the ragdoll (RiderMount tags it too).
            var deoccluder = camera.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = Physics.DefaultRaycastLayers;
            deoccluder.IgnoreTag = PlayerTag;
            deoccluder.TransparentLayers = 0;
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = settings.OcclusionWait,
                CameraRadius = settings.CameraRadius,
                UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings { Enabled = true, YOffset = 0f },
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 4,
                SmoothingTime = 0f,
                Damping = settings.UnoccludedDamping,
                DampingWhenOccluded = settings.OccludedDamping,
            };
            return deoccluder;
        }

        // The lasso throws through the NetworkRope that NetworkRider binds at runtime (host or client).
        private static RiderMount AddRider(HorseController horse, out LassoInput lasso)
        {
            Rigidbody horseBody = horse.GetComponent<Rigidbody>();
            var settings = LoadOrCreate<RiderSettings>(RiderSettingsPath);

            var mount = horse.gameObject.AddComponent<RiderMount>();
            SetReference(mount, "_settings", settings);
            SetReference(mount, "_horse", horse);
            SetReference(mount, "_riderModel", horse.transform.Find("Rider"));
            SetReference(mount, "_clothes", AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedFolder}/RiderPoncho.mat"));
            SetReference(mount, "_skin", AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedFolder}/RiderHat.mat"));
            SetReference(horse.GetComponent<RiderInput>(), "_mount", mount);

            lasso = horse.gameObject.AddComponent<LassoInput>();
            SetReference(lasso, "_mount", mount);
            SetReference(lasso, "_horse", horseBody);
            SetReference(lasso, "_aim", FindAction("Player/Aim"));
            SetReference(lasso, "_throw", FindAction("Player/Attack"));
            SetReference(lasso, "_release", FindAction("Player/ReleaseRope"));
            return mount;
        }

        // The host spawns and simulates the cows when its server starts; clients get puppets from snapshots.
        private static ActiveHerd CreateHerd(PhysicsMaterial frictionless)
        {
            var go = new GameObject("Herd");
            go.transform.position = HerdCenter;
            var herd = go.AddComponent<ActiveHerd>();
            SetReference(herd, "_settings", LoadOrCreate<HerdSettings>(HerdSettingsPath));
            SetReference(herd, "_cowPrefab", CreateCowPrefab(frictionless));
            SetValue(herd, "_spawnOnStart", false);
            go.AddComponent<NetworkObject>();
            SetReference(go.AddComponent<NetworkHerd>(), "_herd", herd);
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

        // Every machine draws every rope from what its NetworkRope knows (the host's simulation or the sent state).
        private static void CreateRopeView(Transform parent, NetworkRope source)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            SaveAsset(material, $"{GeneratedFolder}/Rope.mat");
            var view = new GameObject("RopeView").AddComponent<RopeView>();
            view.transform.SetParent(parent, false);
            SetReference(view, "_source", source);
            SetReference(view, "_line", CreateLine(view.transform, "RopeLine", material, 0.045f));
            SetReference(view, "_loop", CreateLine(view.transform, "RopeLoop", material, 0.04f));
        }

        private static NetworkManager CreateNetwork(NetworkObject playerPrefab)
        {
            var go = new GameObject("NetworkManager");
            var networkManager = go.AddComponent<NetworkManager>();
            var transportManager = go.AddComponent<TransportManager>();
            var tugboat = go.AddComponent<Tugboat>();
            SetReference(transportManager, "Transport", tugboat);
            // Lives with the scene: a persistent manager would survive into the next play or test.
            SetValue(networkManager, "_dontDestroyOnLoad", false);

            var prefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>();
            prefabs.AddObject(playerPrefab, checkForDuplicates: true, initializeAdded: false);
            SetReference(networkManager, "_spawnablePrefabs", SaveAsset(prefabs, $"{GeneratedFolder}/NetworkPrefabs.asset"));

            var spawner = go.AddComponent<NetworkPlayerSpawner>();
            SetReference(spawner, "_networkManager", networkManager);
            SetReference(spawner, "_playerPrefab", playerPrefab);

            var menu = go.AddComponent<ProtoNetworkMenu>();
            SetReference(menu, "_networkManager", networkManager);
            return networkManager;
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

        private static Corral CreateCorral(out ProtoGate gate)
        {
            Material wood = CreateMaterial("Fence", new Color(0.42f, 0.3f, 0.2f));
            Transform root = CreatePen("Corral", CorralCenter, 0f, CorralSize, GateWidth, wood);
            var corral = root.gameObject.AddComponent<Corral>();
            float half = CorralSize / 2f;
            float gapHalf = GateWidth / 2f;

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

            return corral;
        }

        /// <summary>
        /// Square pen with a gap for the gate on its local north side (+z) and wing fences opening
        /// out from it like a funnel, under a root turned by <paramref name="yaw"/> so the gate can
        /// face any way.
        /// </summary>
        private static Transform CreatePen(string name, Vector3 center, float yaw, float size, float gateWidth, Material wood)
        {
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));

            float half = size / 2f;
            float gapHalf = gateWidth / 2f;
            float sideLength = half - gapHalf;
            var fence = new Vector3(0.3f, 1.6f, 1f);

            void Wall(Vector3 localCenter, float localYaw, float length) => CreateBlock("Fence", root,
                root.TransformPoint(localCenter + Vector3.up * 0.8f), root.rotation * Quaternion.Euler(0f, localYaw, 0f),
                new Vector3(fence.x, fence.y, length), wood);

            Wall(new Vector3(0f, 0f, -half), 90f, size);
            Wall(new Vector3(-half, 0f, 0f), 0f, size);
            Wall(new Vector3(half, 0f, 0f), 0f, size);
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
            return root;
        }

        // Week 7: a few houses and a church around the fair pen. The pen has no gate panel: the
        // cattle are sold from its opening, where a yellow post marks the sale point.
        private static Corral CreatePueblo(out Transform salePoint)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedFolder}/Fence.mat");
            Transform pen = CreatePen("FairPen", PuebloCenter, FairPenYaw, FairPenSize, FairGateWidth, wood);
            var fairPen = pen.gameObject.AddComponent<Corral>();
            SetValue(fairPen, "_size", new Vector2(FairPenSize - 1f, FairPenSize - 1f));

            float half = FairPenSize / 2f;
            salePoint = new GameObject("SalePoint").transform;
            salePoint.SetParent(pen, false);
            salePoint.localPosition = new Vector3(0f, 0f, half + 3f);
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "SalePost";
            post.transform.SetParent(pen, false);
            post.transform.localPosition = new Vector3(FairGateWidth / 2f + 2f, 1.5f, half + 3f);
            post.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
            post.GetComponent<Renderer>().sharedMaterial = CreateMaterial("SalePost", new Color(0.95f, 0.8f, 0.15f));

            var town = new GameObject("Pueblo").transform;
            Material walls = CreateMaterial("HouseWall", new Color(0.93f, 0.9f, 0.82f));
            Material roof = CreateMaterial("HouseRoof", new Color(0.66f, 0.3f, 0.18f));
            // Behind the pen and along its sides; the gate side stays open for the herd.
            var houses = new[]
            {
                new Vector3(-28f, 0f, -18f), new Vector3(-28f, 0f, 0f), new Vector3(-28f, 0f, 18f),
                new Vector3(-10f, 0f, 28f), new Vector3(8f, 0f, 28f), new Vector3(-10f, 0f, -28f), new Vector3(8f, 0f, -28f),
            };
            foreach (Vector3 offset in houses)
            {
                Vector3 at = PuebloCenter + offset;
                CreateBlock("House", town, at + Vector3.up * 1.75f, Quaternion.identity, new Vector3(6f, 3.5f, 5f), walls);
                CreateBlock("Roof", town, at + Vector3.up * 3.75f, Quaternion.identity, new Vector3(6.6f, 0.5f, 5.6f), roof);
            }
            Vector3 church = PuebloCenter + new Vector3(-42f, 0f, 0f);
            CreateBlock("Church", town, church + Vector3.up * 3f, Quaternion.identity, new Vector3(8f, 6f, 14f), walls);
            CreateBlock("Bell tower", town, church + new Vector3(0f, 6f, 5f), Quaternion.identity, new Vector3(3.5f, 12f, 3.5f), walls);
            return fairPen;
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

        private static bool InPueblo(Vector3 point) =>
            new Vector2(point.x - PuebloCenter.x, point.z - PuebloCenter.z).magnitude < PuebloClearRadius;

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

        /// <summary>Sets a private serialized bool, int, enum (by its int value) or Vector2 field.</summary>
        private static void SetValue(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            switch (value)
            {
                case bool flag:
                    property.boolValue = flag;
                    break;
                case int number when property.propertyType == SerializedPropertyType.Enum:
                    property.enumValueIndex = number;
                    break;
                case int number:
                    property.intValue = number;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                default:
                    throw new System.ArgumentException($"Unsupported value type {value.GetType().Name} for {field}.");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int GroundLayer()
        {
            int layer = LayerMask.NameToLayer(GroundLayerName);
            if (layer < 0)
                throw new System.InvalidOperationException($"Layer '{GroundLayerName}' is missing from Project Settings > Tags and Layers.");
            return layer;
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
