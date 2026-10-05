using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PuzzleStudio.EditorTools.BuildTools
{
    /// <summary>
    /// Build menu + batchmode entry points (see build.bat):
    ///   -executeMethod PuzzleStudio.EditorTools.BuildTools.BuildMenu.BuildTemplateBatch
    /// </summary>
    public static class BuildMenu
    {
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string TemplateDir => Path.Combine(ProjectRoot, "Build", "Template");
        public const string TemplateExeName = "Game.exe";

        [MenuItem("Build/Player Template", priority = 1)]
        public static void BuildTemplateMenu()
        {
            bool ok = BuildTemplate();
            if (ok) EditorUtility.RevealInFinder(Path.Combine(TemplateDir, TemplateExeName));
        }

        public static void BuildTemplateBatch() => ExitWith(BuildTemplate());

        /// <summary>Builds the generic data-driven game (Boot + Game scenes, no content) to Build/Template/Game.exe.</summary>
        public static bool BuildTemplate()
        {
            ProjectSetup.Run();

            var backend = IsIl2CppInstalled() ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
            Debug.Log($"[Build] Scripting backend: {backend}");

            if (Directory.Exists(TemplateDir)) Directory.Delete(TemplateDir, true);
            Directory.CreateDirectory(TemplateDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.BootScene, ProjectSetup.GameScene },
                locationPathName = Path.Combine(TemplateDir, TemplateExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? $"[Build] Player Template OK ({report.summary.totalSize / (1024 * 1024)} MB) → {options.locationPathName}"
                : $"[Build] Player Template FAILED: {report.summary.result} ({report.summary.totalErrors} errors)");

            // The template must not ship a "do not ship" folder from IL2CPP builds.
            foreach (var dir in Directory.GetDirectories(TemplateDir, "*DoNotShip*"))
                Directory.Delete(dir, true);
            return ok;
        }

        public static bool IsIl2CppInstalled()
        {
            string engines = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "windowsstandalonesupport", "Variations");
            return Directory.Exists(Path.Combine(engines, "win64_player_nondevelopment_il2cpp"));
        }

        public static string StudioDir => Path.Combine(ProjectRoot, "Build", "PuzzleStudio");

        [MenuItem("Build/Studio", priority = 2)]
        public static void BuildStudioMenu()
        {
            if (BuildStudio()) EditorUtility.RevealInFinder(Path.Combine(StudioDir, "PuzzleStudio.exe"));
        }

        [MenuItem("Build/All", priority = 3)]
        public static void BuildAllMenu()
        {
            if (BuildTemplate() && BuildStudio()) EditorUtility.RevealInFinder(Path.Combine(StudioDir, "PuzzleStudio.exe"));
        }

        public static void BuildStudioBatch() => ExitWith(BuildStudio());
        public static void BuildAllBatch() => ExitWith(BuildTemplate() && BuildStudio());

        /// <summary>
        /// Builds PuzzleStudio.exe (Studio scene) to Build/PuzzleStudio, then copies the Player Template
        /// into PuzzleStudio/Template and the sample packs into PuzzleStudio/Samples.
        /// </summary>
        public static bool BuildStudio()
        {
            if (!File.Exists(Path.Combine(TemplateDir, TemplateExeName)) && !BuildTemplate()) return false;
            ProjectSetup.Run();

            PlayerSettings.productName = "PuzzleStudio";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            bool ok;
            try
            {
                string studioExe = Path.Combine(StudioDir, "PuzzleStudio.exe");
                if (Directory.Exists(StudioDir)) Directory.Delete(StudioDir, true);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.StudioScene },
                    locationPathName = studioExe,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log(ok ? $"[Build] Studio OK → {studioExe}" : $"[Build] Studio FAILED: {report.summary.result}");
            }
            finally
            {
                ProjectSetup.ConfigurePlayerSettings(); // back to the game's settings
            }
            if (!ok) return false;

            CopyDirectory(TemplateDir, Path.Combine(StudioDir, "Template"));
            CopyDirectory(Path.Combine(ProjectRoot, "SamplePacks"), Path.Combine(StudioDir, "Samples"));
            Debug.Log("[Build] Template and samples copied into the Studio folder.");
            return true;
        }

        static void CopyDirectory(string src, string dst)
        {
            if (!Directory.Exists(src)) return;
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(dst, f.Substring(src.Length).TrimStart('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f, target, true);
            }
        }

        [MenuItem("Build/Generate Sample Packs", priority = 50)]
        public static void GenerateSamplesMenu() => SamplePackGenerator.GenerateAll();

        public static void GenerateAudioBatch() => DefaultAudioGenerator.GenerateBatch();

        public static void GenerateSamplesBatch()
        {
            try { SamplePackGenerator.GenerateAll(); ExitWith(true); }
            catch (Exception e) { Debug.LogException(e); ExitWith(false); }
        }

        static void ExitWith(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
