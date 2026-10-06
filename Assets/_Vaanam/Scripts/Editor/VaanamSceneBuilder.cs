using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vaanam.Core;
using Vaanam.Flight;
using Vaanam.Input;
using Vaanam.UI;
using Vaanam.World;

namespace Vaanam.Editor
{
    /// <summary>
    /// Builds the whole game scene from code: Vaanam > Build Main Scene.
    ///
    /// Why a builder instead of hand-placing objects: everything the scene
    /// needs is small and wiring it by script means the scene can always be
    /// regenerated, diffed and understood by reading one file. Run it again at
    /// any time; existing assets (materials, levels, palette) are reused so
    /// their GUIDs stay stable.
    ///
    /// It also makes sure the project really renders with URP: a fresh Unity
    /// project with the URP package installed still uses the built-in pipeline
    /// until a pipeline asset is assigned in Graphics settings.
    /// </summary>
    public static class VaanamSceneBuilder
    {
        private const string ScenesDir = "Assets/_Vaanam/Scenes";
        public const string ScenePath = ScenesDir + "/Vaanam.unity";
        private const string MatDir = "Assets/_Vaanam/Art/Materials";
        private const string ResDir = "Assets/_Vaanam/Resources";
        private const string SettingsDir = "Assets/_Vaanam/Settings";
        private const string LevelsDir = "Assets/_Vaanam/Levels";

        [MenuItem("Vaanam/Build Main Scene")]
        public static void BuildMainSceneMenu() => Debug.Log(BuildMainScene());

        [MenuItem("Vaanam/Setup/Copy MediaPipe Model to StreamingAssets")]
        public static void CopyModelMenu() => Debug.Log(CopyModel());

        public static string BuildMainScene()
        {
            foreach (string d in new[] { ScenesDir, MatDir, ResDir, SettingsDir, LevelsDir, "Assets/StreamingAssets" })
                Directory.CreateDirectory(d);

            // Open the new scene FIRST. Creating a scene unloads unused objects, and a
            // ScriptableObject created just before that can come back as a "fake null"
            // reference, which then serializes as nothing.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureUrp();
            CopyModel();
            VaanamPalette palette = BuildPalette();
            Material sky = BuildSkybox();
            LevelDefinition[] levels = BuildLevels();

            // ---- light & sky -------------------------------------------------------
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(50f, 35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 80f; RenderSettings.fogEndDistance = 320f;
            RenderSettings.fogColor = new Color(0.85f, 0.93f, 1f);
            RenderSettings.ambientMode = AmbientMode.Trilight;

            // ---- camera ------------------------------------------------------------
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FlightCamera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; cam.farClipPlane = 600f; cam.nearClipPlane = 0.3f; cam.fieldOfView = 62f;
            camGo.transform.position = new Vector3(0f, 10.5f, -12f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            // ---- game root ---------------------------------------------------------
            var game = new GameObject("Game");
            game.AddComponent<GameSession>();
            var timer = game.AddComponent<SessionTimer>();
            game.AddComponent<AudioDirector>();
            var voice = game.AddComponent<VoiceCommands>();
            var skyCtl = game.AddComponent<SkyController>();
            Set(skyCtl, "sun", sun); Set(skyCtl, "skyboxMaterial", sky);
            var flow = game.AddComponent<GameFlow>();

            // ---- pose tracking -----------------------------------------------------
            var poseGo = new GameObject("PoseProvider");
            poseGo.AddComponent<PoseProvider>();
            poseGo.AddComponent<KeyboardPoseSource>();
            // Added by name so this file compiles whether or not the MediaPipe package is present.
            Type mp = Type.GetType("Vaanam.Input.MediaPipePoseSource, Vaanam");
            if (mp != null) poseGo.AddComponent(mp);
            else Debug.LogWarning("[Vaanam] MediaPipePoseSource not compiled (define VAANAM_MEDIAPIPE missing). Keyboard only.");

            // ---- players -----------------------------------------------------------
            var playersRoot = new GameObject("Players");
            var players = new FlightController[2];
            for (int i = 0; i < 2; i++)
            {
                var p = new GameObject("Player " + (i + 1));
                p.transform.SetParent(playersRoot.transform, false);
                p.transform.position = new Vector3(i * 3f, 6f, 0f);
                var rb = p.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var col = p.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 1.1f;
                var fc = p.AddComponent<FlightController>();
                Set(fc, "playerIndex", i);
                var bv = p.AddComponent<BirdVisual>();
                Set(bv, "colorIndex", i);
                players[i] = fc;
                if (i == 1) p.SetActive(false);
            }
            camGo.GetComponent<FlightCamera>().SetPlayers(players);
            Set(camGo.GetComponent<FlightCamera>(), "players", players);

            // ---- level -------------------------------------------------------------
            var levelGo = new GameObject("LevelGenerator");
            var gen = levelGo.AddComponent<LevelGenerator>();
            Set(gen, "levels", levels);
            Set(gen, "sky", skyCtl);

            // ---- UI ----------------------------------------------------------------
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = UiChild<HudController>(canvasGo.transform, "HUD");
            Set(hud, "timer", timer);
            var board = UiChild<MessageBoard>(canvasGo.transform, "Message");
            var sunset = UiChild<SunsetScreen>(canvasGo.transform, "Sunset");
            UiChild<ParentPanel>(canvasGo.transform, "ParentPanel");

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // ---- wire GameFlow -----------------------------------------------------
            Set(flow, "players", players);
            Set(flow, "level", gen);
            Set(flow, "timer", timer);
            Set(flow, "sky", skyCtl);
            Set(flow, "flightCamera", camGo.GetComponent<FlightCamera>());
            Set(flow, "voice", voice);
            Set(flow, "board", board);
            Set(flow, "hud", hud);
            Set(flow, "sunsetScreen", sunset);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Scene built: " + ScenePath;
        }

        private static T UiChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        // ------------------------------------------------------------------ URP

        private static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;

            string rendererPath = SettingsDir + "/Vaanam Renderer.asset";
            string pipelinePath = SettingsDir + "/Vaanam URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 120f;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Vaanam] URP pipeline asset assigned: " + pipelinePath);
        }

        // ------------------------------------------------------------------ assets

        private static string CopyModel()
        {
            string dst = "Assets/StreamingAssets/pose_landmarker_lite.task";
            if (File.Exists(dst)) return "Model already present: " + dst;
            string src = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/pose_landmarker_lite.bytes";
            string full = Path.GetFullPath(src);
            if (!File.Exists(full)) return "MediaPipe model not found at " + src + " (is the plugin installed?)";
            Directory.CreateDirectory("Assets/StreamingAssets");
            File.Copy(full, dst, true);
            AssetDatabase.Refresh();
            return "Copied pose model to " + dst;
        }

        private static Material Mat(string key, bool emissive = false)
        {
            string path = $"{MatDir}/{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = VaanamPalette.MakeMaterial(key, VaanamPalette.DefaultColors[key], emissive);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static VaanamPalette BuildPalette()
        {
            string path = ResDir + "/VaanamPalette.asset";
            var p = AssetDatabase.LoadAssetAtPath<VaanamPalette>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VaanamPalette>(); AssetDatabase.CreateAsset(p, path); }

            p.birdPrimary = new[] { Mat("BirdOrange"), Mat("BirdBlue") };
            p.birdSecondary = new[] { Mat("BirdOrangeLight"), Mat("BirdBlueLight") };
            p.beak = Mat("Beak"); p.eye = Mat("Eye"); p.eyeWhite = Mat("EyeWhite");
            p.ringGold = Mat("RingGold", true); p.ringPassed = Mat("RingPassed", true); p.star = Mat("Star", true); p.kolam = Mat("Kolam", true);
            p.cloud = Mat("Cloud"); p.duckBar = Mat("DuckBar"); p.jumpWall = Mat("JumpWall");
            p.ground = Mat("Ground"); p.groundDark = Mat("GroundDark"); p.hill = Mat("Hill"); p.trunk = Mat("Trunk"); p.leaves = Mat("Leaves");
            p.water = Mat("Water"); p.house = Mat("House"); p.roof = Mat("Roof"); p.pot = Mat("Pot"); p.sugarcane = Mat("Sugarcane");
            p.sun = Mat("Sun", true); p.perch = Mat("Perch");
            p.kites = new[] { Mat("Kite1"), Mat("Kite2"), Mat("Kite3") };

            string particlePath = MatDir + "/Particle.mat";
            var particle = AssetDatabase.LoadAssetAtPath<Material>(particlePath);
            if (particle == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                particle = new Material(sh) { name = "Particle" };
                var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                if (tex != null) { if (particle.HasProperty("_BaseMap")) particle.SetTexture("_BaseMap", tex); particle.mainTexture = tex; }
                // Additive-looking soft particles: surface type transparent, blend additive where supported.
                if (particle.HasProperty("_Surface")) particle.SetFloat("_Surface", 1f);
                if (particle.HasProperty("_Blend")) particle.SetFloat("_Blend", 1f);
                if (particle.HasProperty("_SrcBlend")) particle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (particle.HasProperty("_DstBlend")) particle.SetFloat("_DstBlend", (float)BlendMode.One);
                if (particle.HasProperty("_ZWrite")) particle.SetFloat("_ZWrite", 0f);
                particle.renderQueue = 3000;
                particle.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                AssetDatabase.CreateAsset(particle, particlePath);
            }
            p.particle = particle;

            EditorUtility.SetDirty(p);
            return p;
        }

        private static Material BuildSkybox()
        {
            string path = MatDir + "/Sky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find("Skybox/Procedural");
            m = new Material(sh) { name = "Sky" };
            m.SetFloat("_SunSize", 0.06f); m.SetFloat("_SunSizeConvergence", 4f);
            m.SetFloat("_AtmosphereThickness", 1f); m.SetFloat("_Exposure", 1.25f);
            m.SetColor("_SkyTint", new Color(0.35f, 0.65f, 1f)); m.SetColor("_GroundColor", new Color(0.85f, 0.93f, 1f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static LevelDefinition[] BuildLevels()
        {
            var l1 = Level("Level 1 - Sunrise Fields", d =>
            {
                d.nameKey = "level_1"; d.theme = SceneryTheme.Fields; d.durationSeconds = 140f; d.forwardSpeed = 13f;
                d.ringChance = 0.85f; d.starChance = 0.9f; d.starsPerCluster = 3;
                d.cloudChance = 0f; d.duckBarChance = 0f; d.jumpWallChance = 0f; d.kiteChance = 0.1f;
                d.skyTop = new Color(0.40f, 0.70f, 1.0f); d.skyHorizon = new Color(0.95f, 0.90f, 0.80f);
                d.sunColor = new Color(1.0f, 0.95f, 0.85f); d.sunElevation = 35f; d.fogColor = new Color(0.92f, 0.90f, 0.82f);
            });
            var l2 = Level("Level 2 - Cloud Kingdom", d =>
            {
                d.nameKey = "level_2"; d.theme = SceneryTheme.Clouds; d.durationSeconds = 150f; d.forwardSpeed = 14.5f;
                d.ringChance = 0.7f; d.starChance = 0.85f; d.starsPerCluster = 3;
                d.cloudChance = 0.45f; d.duckBarChance = 0.3f; d.jumpWallChance = 0f; d.kiteChance = 0.05f;
                d.skyTop = new Color(0.30f, 0.55f, 1.0f); d.skyHorizon = new Color(0.80f, 0.90f, 1.0f);
                d.sunColor = Color.white; d.sunElevation = 60f; d.fogColor = new Color(0.85f, 0.92f, 1.0f);
                d.groundTint = new Color(0.85f, 0.95f, 1.0f);
            });
            var l3 = Level("Level 3 - Pongal Sky", d =>
            {
                d.nameKey = "level_3"; d.theme = SceneryTheme.Pongal; d.durationSeconds = 160f; d.forwardSpeed = 15.5f;
                d.ringChance = 0.75f; d.starChance = 0.9f; d.starsPerCluster = 4;
                d.cloudChance = 0.25f; d.duckBarChance = 0.3f; d.jumpWallChance = 0.3f; d.kiteChance = 0.6f; d.kolamRings = true;
                d.skyTop = new Color(0.45f, 0.65f, 1.0f); d.skyHorizon = new Color(1.0f, 0.88f, 0.70f);
                d.sunColor = new Color(1.0f, 0.90f, 0.70f); d.sunElevation = 45f; d.fogColor = new Color(1.0f, 0.90f, 0.75f);
                d.groundTint = new Color(1.0f, 0.95f, 0.80f);
            });
            return new[] { l1, l2, l3 };
        }

        private static LevelDefinition Level(string name, Action<LevelDefinition> fill)
        {
            string path = $"{LevelsDir}/{name}.asset";
            var d = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            bool fresh = d == null;
            if (fresh) d = ScriptableObject.CreateInstance<LevelDefinition>();
            if (fresh) fill(d);          // keep hand-tuned values on rebuild
            if (fresh) AssetDatabase.CreateAsset(d, path);
            return d;
        }

        /// <summary>
        /// Set a private [SerializeField] by reflection and mark the object dirty so
        /// the scene save picks it up. (SerializedProperty.objectReferenceValue was
        /// found to drop references to freshly created ScriptableObject assets.)
        /// </summary>
        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var f = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f == null) { Debug.LogWarning($"[Vaanam] No field '{field}' on {target.GetType().Name}"); return; }
            if (value is Array srcArray && f.FieldType.IsArray)
            {
                // Copy into an array of the exact field element type (e.g. LevelDefinition[]).
                var typed = Array.CreateInstance(f.FieldType.GetElementType(), srcArray.Length);
                Array.Copy(srcArray, typed, srcArray.Length);
                value = typed;
            }
            f.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
    }
}
