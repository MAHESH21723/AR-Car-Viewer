using System;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CarViewer.Editor
{
    /// <summary>
    /// Batch-mode project setup and Android build.
    /// Run headless with: -executeMethod CarViewer.Editor.CarViewerBuild.SetupProject (or .BuildAndroid)
    /// </summary>
    public static class CarViewerBuild
    {
        const string Root = "Assets/CarViewer";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string IconDir = Root + "/Icon";
        const string OutputApk = "Builds/Android/ARCarViewer-1.0.0.apk";

        // ------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------

        public static void SetupProject()
        {
            try
            {
                ImportIcons();
                var font = CreateFont();
                var reticleMat = CreateTextureMaterial("Reticle", MakeReticleTexture(), Color.white);
                var shadowMat = CreateTextureMaterial("BlobShadow", MakeShadowTexture(), Color.white);
                var planePrefab = CreatePlanePrefab();
                BuildScene(font, reticleMat, shadowMat, planePrefab);
                ConfigurePlayerSettings();
                ConfigureArCore();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[CarViewer] SetupProject finished.");
            }
            catch (Exception e)
            {
                Debug.LogError("[CarViewer] SetupProject failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAndroid()
        {
            try
            {
                SetupProject();
                Directory.CreateDirectory(Path.GetDirectoryName(OutputApk));
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.development = false;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputApk,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"[CarViewer] Build result: {summary.result}, size {summary.totalSize / (1024 * 1024)} MB, errors {summary.totalErrors}");
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[CarViewer] BuildAndroid failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------
        // Assets
        // ------------------------------------------------------------------

        static void ImportIcons()
        {
            SetTexture(IconDir + "/Logo.png", TextureImporterType.Sprite);
            SetTexture(IconDir + "/AppIcon.png", TextureImporterType.Default);
            SetTexture(IconDir + "/AdaptiveForeground.png", TextureImporterType.Default);
            SetTexture(IconDir + "/AdaptiveBackground.png", TextureImporterType.Default);
        }

        static void SetTexture(string path, TextureImporterType type)
        {
            if (!File.Exists(path)) { Debug.LogWarning("[CarViewer] Missing " + path); return; }
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = type;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        static TMP_FontAsset CreateFont()
        {
            const string ttf = Root + "/Fonts/Rajdhani-Bold.ttf";
            const string assetPath = Root + "/Fonts/Rajdhani-Bold SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            AssetDatabase.ImportAsset(ttf);
            var source = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            var fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            fa.name = "Rajdhani-Bold SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.material.name = "Rajdhani-Bold SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = "Rajdhani-Bold SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);

            var chars = new System.Text.StringBuilder();
            for (char c = (char)32; c < 127; c++) chars.Append(c);
            fa.TryAddCharacters(chars.ToString(), out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Texture2D MakeReticleTexture()
        {
            const string path = Root + "/Textures/Reticle.png";
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.40f) / 0.018f);
                    float inner = Mathf.Clamp01(1f - Mathf.Abs(r - 0.30f) / 0.008f) * 0.6f;
                    float dot = Mathf.Clamp01(1f - r / 0.03f);
                    bool tick = (Mathf.Abs(dx) < 0.012f || Mathf.Abs(dy) < 0.012f) && r > 0.30f && r < 0.46f;
                    float a = Mathf.Max(Mathf.Max(ring, inner), Mathf.Max(dot, tick ? 1f : 0f));
                    tex.SetPixel(x, y, new Color(0.13f, 0.83f, 0.93f, a));
                }
            }
            return SaveTexture(tex, path);
        }

        static Texture2D MakeShadowTexture()
        {
            const string path = Root + "/Textures/BlobShadow.png";
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f) * 0.55f;
                    tex.SetPixel(x, y, new Color(0, 0, 0, a));
                }
            }
            return SaveTexture(tex, path);
        }

        static Texture2D SaveTexture(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material CreateTextureMaterial(string name, Texture2D tex, Color tint)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tex;
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CreateFlatMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject CreatePlanePrefab()
        {
            const string path = Root + "/Prefabs/ARPlaneVisual.prefab";
            Directory.CreateDirectory(Root + "/Prefabs");
            var fill = CreateFlatMaterial("PlaneFill", new Color(0.13f, 0.83f, 0.93f, 0.22f));
            var line = CreateFlatMaterial("PlaneLine", new Color(0.13f, 0.83f, 0.93f, 0.9f));

            var go = new GameObject("ARPlaneVisual");
            go.AddComponent<ARPlane>();
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fill;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<ARPlaneMeshVisualizer>();
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = line;
            lr.widthMultiplier = 0.008f;
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------
        // Scene
        // ------------------------------------------------------------------

        static void BuildScene(TMP_FontAsset font, Material reticleMat, Material shadowMat, GameObject planePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.7f);

            // AR session
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();

            // XR Origin > Camera Offset > AR Camera
            var originGo = new GameObject("XR Origin");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);
            var camGo = new GameObject("AR Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(offset.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            camGo.AddComponent<AudioListener>();
            var camMgr = camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            camGo.AddComponent<CarViewer.XRCameraPoseDriver>();

            var origin = originGo.AddComponent<XROrigin>();
            origin.Origin = originGo;
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = cam;
            var planeMgr = originGo.AddComponent<ARPlaneManager>();
            planeMgr.planePrefab = planePrefab;
            planeMgr.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            var raycastMgr = originGo.AddComponent<ARRaycastManager>();

            // Reticle
            var reticle = new GameObject("Reticle");
            var rq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(rq.GetComponent<Collider>());
            rq.transform.SetParent(reticle.transform, false);
            rq.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rq.transform.localScale = Vector3.one * 0.24f;
            rq.GetComponent<MeshRenderer>().sharedMaterial = reticleMat;
            reticle.SetActive(false);

            // Car root
            var carGo = new GameObject("Car");
            var car = carGo.AddComponent<CarViewer.CarModel>();
            car.shadowMaterial = shadowMat;

            // Placement + app
            var appGo = new GameObject("App");
            var placement = appGo.AddComponent<CarViewer.ARPlacementController>();
            placement.raycastManager = raycastMgr;
            placement.planeManager = planeMgr;
            placement.arCamera = cam;
            placement.reticle = reticle.transform;
            placement.car = car;

            var voice = appGo.AddComponent<CarViewer.VoiceTour>();
            voice.narration = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/external asset/ElevenLabs_Text_to_Speech_audio.mp3");

            var app = appGo.AddComponent<CarViewer.ARCarApp>();
            app.session = session;
            app.cameraManager = camMgr;
            app.placement = placement;
            app.car = car;
            app.voice = voice;
            app.font = font;
            app.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - FREE Racing Car/Prefabs (Meshes Only)/Free Racing Car.prefab");
            app.logo = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/Logo.png");
            app.paints = BuildPaints();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>The five textures that ship with the car; names come from each texture's dominant hue.</summary>
        static CarViewer.CarPaint[] BuildPaints()
        {
            var list = new System.Collections.Generic.List<CarViewer.CarPaint>();
            for (int i = 1; i <= 5; i++)
            {
                string path = $"Assets/ARCADE - FREE Racing Car/Textures/Color Variations/AFRC_Tex_Col{i}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                var colour = DominantColour(path);
                string n = i == 1 ? "Yellow" : ColourName(colour);
                foreach (var p in list) if (p.name == n) n += " 2";
                list.Add(new CarViewer.CarPaint { name = n, swatch = colour, texture = tex });
            }
            return list.ToArray();
        }

        static Color DominantColour(string path)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            var px = t.GetPixels32();
            float bestSat = -1f;
            Color best = Color.white;
            // Pick the most saturated pixel region (the paint), ignoring tyres / glass.
            for (int i = 0; i < px.Length; i += 7)
            {
                Color c = px[i];
                Color.RGBToHSV(c, out _, out float s, out float v);
                float score = s * v;
                if (score > bestSat) { bestSat = score; best = c; }
            }
            UnityEngine.Object.DestroyImmediate(t);
            best.a = 1f;
            return best;
        }

        static string ColourName(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out _);
            if (s < 0.15f) return "Silver";
            float deg = h * 360f;
            if (deg < 15 || deg >= 345) return "Red";
            if (deg < 45) return "Orange";
            if (deg < 70) return "Yellow";
            if (deg < 160) return "Green";
            if (deg < 200) return "Cyan";
            if (deg < 260) return "Blue";
            if (deg < 330) return "Purple";
            return "Pink";
        }

        // ------------------------------------------------------------------
        // Player settings
        // ------------------------------------------------------------------

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "AR Car Viewer";
            PlayerSettings.companyName = "Maheshwaran";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maheshwaran.arcarviewer");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.androidIsGame = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;

            // Icons
            var app = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AppIcon.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AdaptiveForeground.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/AdaptiveBackground.png");
            if (app != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { app }, IconKind.Application);
            if (fg != null && bg != null)
            {
                var kind = AndroidPlatformIconKind.Adaptive;
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons) icon.SetTextures(bg, fg);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            else Debug.LogWarning("[CarViewer] Adaptive icon textures missing; using the default icon only.");
        }

        static void ConfigureArCore()
        {
            var settings = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            settings.requirement = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Required;
            EditorUtility.SetDirty(settings);
        }
    }
}
