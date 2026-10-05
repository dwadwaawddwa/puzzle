using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using UnityEngine;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>
    /// Builds a ready-to-ship game folder from the Player Template:
    /// copy template → rename exe/_Data → inject icon → write the optimized Game Pack → optional zip.
    /// Runs as a coroutine so the Studio can show progress.
    /// </summary>
    public sealed class GameExporter
    {
        public const int MaxImageSize = 2048;

        public sealed class Result
        {
            public bool Success;
            public string Error;
            public string GameDir;
            public string ExePath;
            public string ZipPath;
            public string IcoPath;
            public string SteamworksDir;
            public ValidationReport Report;
        }

        public float Progress { get; private set; }
        public string Status { get; private set; } = "";
        public Result Outcome { get; private set; }

        readonly StudioProject _project;
        readonly Transform _host;

        /// <param name="host">Parent of the off-screen renderer used for the Steam images.</param>
        public GameExporter(StudioProject project, Transform host)
        {
            _project = project;
            _host = host;
        }

        public IEnumerator Run()
        {
            Outcome = new Result();
            var export = _project.File.export;
            var pack = _project.Pack;

            Step(0.02f, "Validating…");
            Outcome.Report = PackValidator.Validate(pack);
            if (Outcome.Report.HasErrors) { Fail("The game has errors. Fix them first (see Validate)."); yield break; }
            if (!StudioPaths.TemplateAvailable) { Fail($"Player Template not found in {StudioPaths.TemplateDir}."); yield break; }
            yield return null;

            string exeName = _project.ExeName;
            string outRoot = _project.ExportRoot;
            string gameDir = Path.Combine(outRoot, exeName);
            Outcome.GameDir = gameDir;

            // 1. Fresh copy of the template (only ever deletes a previous export of this game).
            Step(0.05f, "Copying the Player Template…");
            if (Directory.Exists(gameDir))
            {
                bool previousExport = File.Exists(Path.Combine(gameDir, exeName + ".exe")) || Directory.GetFileSystemEntries(gameDir).Length == 0;
                if (!previousExport) { Fail($"{gameDir} already exists and is not a previous export of this game. Choose another output folder."); yield break; }
                Directory.Delete(gameDir, true);
            }
            var files = Directory.GetFiles(StudioPaths.TemplateDir, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i].Substring(StudioPaths.TemplateDir.Length).TrimStart('\\', '/');
                if (rel.StartsWith(StudioPaths.TemplateData + Path.DirectorySeparatorChar + "StreamingAssets")) continue;
                string dst = Path.Combine(gameDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(files[i], dst, true);
                if (i % 40 == 0) { Step(0.05f + 0.35f * i / files.Length, $"Copying the Player Template… {i}/{files.Length}"); yield return null; }
            }

            // 2. Rename Game.exe / Game_Data.
            Step(0.42f, "Renaming the executable…");
            string exePath = Path.Combine(gameDir, exeName + ".exe");
            string dataDir = Path.Combine(gameDir, exeName + "_Data");
            if (!string.Equals(exeName, "Game", StringComparison.OrdinalIgnoreCase))
            {
                File.Move(Path.Combine(gameDir, StudioPaths.TemplateExe), exePath);
                Directory.Move(Path.Combine(gameDir, StudioPaths.TemplateData), dataDir);
            }
            Outcome.ExePath = exePath;
            yield return null;

            // 3. Icon.
            Step(0.48f, "Creating the icon…");
            string iconError = null;
            try
            {
                var icon = BuildIcon(out bool rounded);
                if (icon != null)
                {
                    var images = IconBuilder.Build(icon, rounded);
                    UnityEngine.Object.Destroy(icon);
                    IconInjector.Inject(exePath, images);
                    Outcome.IcoPath = Path.Combine(outRoot, exeName + ".ico");
                    File.WriteAllBytes(Outcome.IcoPath, IconBuilder.BuildIcoFile(images));
                }
            }
            catch (Exception e)
            {
                iconError = e.Message;
                Debug.LogWarning($"[Export] Icon injection failed: {e}");
            }
            yield return null;

            // 4. Game Pack → <Name>_Data/StreamingAssets/GamePack.
            string packDst = Path.Combine(dataDir, "StreamingAssets", PackPaths.StreamingFolder);
            Directory.CreateDirectory(packDst);
            var copy = GamePackWriter.Clone(pack);
            for (int i = 0; i < copy.levels.Count; i++)
            {
                Step(0.55f + 0.3f * i / Math.Max(1, copy.levels.Count), $"Optimizing images… {i + 1}/{copy.levels.Count}");
                yield return null;
                CopyImage(pack.Resolve(copy.levels[i].image), Path.Combine(packDst, copy.levels[i].image));
            }
            Step(0.86f, "Writing game data…");
            foreach (var folder in new[] { PackPaths.ThemeDir, PackPaths.AudioDir, PackPaths.LocaleDir })
                CopyDirectory(Path.Combine(pack.RootPath, folder), Path.Combine(packDst, folder));
            copy.RootPath = packDst;
            GamePackWriter.WriteJson(copy, packDst);
            yield return null;

            // 5. Steam.
            long appId = pack.game.steamAppId;
            if (export.steamAppIdTxt && appId > 0)
                File.WriteAllText(Path.Combine(gameDir, "steam_appid.txt"), appId.ToString());
            if (export.steamFiles)
            {
                Step(0.87f, "Creating the Steamworks folder…");
                var renderer = new UiImageRenderer(_host);
                try
                {
                    var steps = CoroutineUtil.Flatten(SteamworksExporter.Run(_project, outRoot, exeName, renderer, s => Status = s));
                    while (steps.MoveNext()) yield return steps.Current;
                }
                finally
                {
                    renderer.Dispose();
                }
                Outcome.SteamworksDir = SteamworksExporter.FolderFor(outRoot, exeName);
            }

            // 6. Zip.
            if (export.zip)
            {
                Step(0.9f, "Creating the zip…");
                yield return null;
                string zip = gameDir + ".zip";
                if (File.Exists(zip)) File.Delete(zip);
                ZipFile.CreateFromDirectory(gameDir, zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);
                Outcome.ZipPath = zip;
            }

            Step(1f, iconError == null ? "Export complete." : $"Export complete (icon not changed: {iconError}).");
            Outcome.Success = true;
            if (export.openFolder) FileDialogs.Reveal(exePath);
        }

        Texture2D BuildIcon(out bool rounded)
        {
            rounded = false;
            string custom = _project.ResolveProjectPath(_project.File.export.icon);
            if (custom != null && File.Exists(custom)) return TextureLoader.Load(custom, 1024, false);
            // Default: the first level's picture with rounded corners.
            rounded = true;
            if (_project.Pack.levels.Count == 0) return null;
            return TextureLoader.Load(_project.Pack.Resolve(_project.Pack.levels[0].image), 1024, false);
        }

        static void CopyImage(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (!ImageHeaderReader.TryReadSize(src, out int w, out int h) || Math.Max(w, h) <= MaxImageSize)
            {
                File.Copy(src, dst, true);
                return;
            }
            var tex = TextureLoader.Load(src, MaxImageSize, false);
            bool jpg = dst.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || dst.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
            File.WriteAllBytes(dst, jpg ? tex.EncodeToJPG(92) : tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
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

        void Step(float progress, string status)
        {
            Progress = progress;
            Status = status;
        }

        void Fail(string error)
        {
            Outcome.Success = false;
            Outcome.Error = error;
            Status = error;
            Progress = 0f;
        }
    }
}
