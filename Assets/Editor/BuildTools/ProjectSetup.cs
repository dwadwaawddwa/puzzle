using System.IO;
using PuzzleStudio.Game.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.EditorTools.BuildTools
{
    /// <summary>
    /// Generates every asset that would otherwise need manual editor work: UI panel settings, the piece material,
    /// the scenes and the build/player settings. Safe to run repeatedly.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenesDir = "Assets/_Project/Scenes";
        public const string BootScene = ScenesDir + "/Boot.unity";
        public const string GameScene = ScenesDir + "/Game.unity";
        public const string StudioScene = ScenesDir + "/Studio.unity";
        const string PanelSettingsPath = "Assets/_Project/Resources/UI/GamePanelSettings.asset";
        const string ThemePath = "Assets/_Project/Resources/UI/RuntimeTheme.tss";
        const string StudioPanelSettingsPath = "Assets/_Project/Resources/UI/StudioPanelSettings.asset";
        const string StudioThemePath = "Assets/_Project/Resources/UI/StudioTheme.tss";
        const string MaterialPath = "Assets/_Project/Resources/Materials/Piece.mat";

        [MenuItem("Build/Setup/Regenerate Project Assets", priority = 100)]
        public static void Run()
        {
            CreatePanelSettings();
            CreateStudioPanelSettings();
            CreatePieceMaterial();
            CreateMaterial("Assets/_Project/Resources/Materials/Background.mat", "PuzzleStudio/Background");
            CreateMaterial("Assets/_Project/Resources/Materials/Particle.mat", "PuzzleStudio/Particle");
            CreateScenes();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Project assets regenerated.");
        }

        /// <summary>Batchmode entry: -executeMethod PuzzleStudio.EditorTools.BuildTools.ProjectSetup.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void CreatePanelSettings()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            }

            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            bool create = ps == null;
            if (create) ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f; // match height: same convention as the orthographic camera
            ps.clearColor = false;
            ps.sortingOrder = 0;
            if (create) AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            EditorUtility.SetDirty(ps);
            if (theme == null) Debug.LogWarning("[Setup] RuntimeTheme.tss not imported yet; run setup again.");
        }

        static void CreateStudioPanelSettings()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(StudioThemePath);
            if (theme == null)
            {
                AssetDatabase.ImportAsset(StudioThemePath, ImportAssetOptions.ForceUpdate);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(StudioThemePath);
            }
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(StudioPanelSettingsPath);
            bool create = ps == null;
            if (create) ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.scale = 1f;
            ps.clearColor = false;
            ps.sortingOrder = 0;
            if (create) AssetDatabase.CreateAsset(ps, StudioPanelSettingsPath);
            EditorUtility.SetDirty(ps);
            if (theme == null) Debug.LogWarning("[Setup] StudioTheme.tss not imported yet; run setup again.");
        }

        static void CreatePieceMaterial()
        {
            var shader = Shader.Find("PuzzleStudio/Piece");
            if (shader == null) { Debug.LogError("[Setup] Shader PuzzleStudio/Piece not found."); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Piece" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
        }

        static void CreateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[Setup] Shader {shaderName} not found."); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Creates only the missing scenes, so builds don't rewrite them (no Git noise).</summary>
        static void CreateScenes()
        {
            Directory.CreateDirectory(ScenesDir);
            CreateSceneIfMissing<BootLoader>(BootScene, "Boot");
            CreateSceneIfMissing<GameRoot>(GameScene, "GameRoot");
            CreateSceneIfMissing<PuzzleStudio.Studio.App.StudioApp>(StudioScene, "Studio");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(GameScene, true),
                new EditorBuildSettingsScene(StudioScene, false),
            };
        }

        static void CreateSceneIfMissing<T>(string path, string rootName) where T : Component
        {
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(rootName).AddComponent<T>();
            EditorSceneManager.SaveScene(scene, path);
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "PuzzleStudio";
            PlayerSettings.productName = "PuzzleGame";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
        }
    }
}
