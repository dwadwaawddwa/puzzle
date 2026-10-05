using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Steam;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Studio.App;
using UnityEngine;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>
    /// "&lt;Name&gt;_Steamworks" folder next to an export: everything to type or upload on the Steamworks partner site
    /// (achievement icons + list, Rich Presence files, store images, screenshots, SteamPipe upload script, README).
    /// </summary>
    public static class SteamworksExporter
    {
        public const string Readme = "README_STEAMWORKS.txt";

        public static string FolderFor(string exportRoot, string exeName) => Path.Combine(exportRoot, exeName + "_Steamworks");

        /// <summary>Project folder holding the editable Steam art (store images, screenshots).</summary>
        public static string ProjectSteamDir(StudioProject project) => Path.Combine(project.Root, "steam");
        public static string ProjectStoreDir(StudioProject project) => Path.Combine(ProjectSteamDir(project), "store");
        public static string ProjectScreenshotsDir(StudioProject project) => Path.Combine(ProjectSteamDir(project), "screenshots");

        public static IEnumerator Run(StudioProject project, string exportRoot, string exeName, UiImageRenderer renderer, Action<string> status)
        {
            var pack = project.Pack;
            string dir = FolderFor(exportRoot, exeName);
            // Only ever replaces a folder this exporter made.
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, Readme))) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            // Achievements: list + icons.
            var achievements = AchievementGenerator.Effective(pack);
            string achDir = Path.Combine(dir, "achievements");
            Directory.CreateDirectory(achDir);
            File.WriteAllText(Path.Combine(achDir, "achievements.txt"), SteamworksFiles.AchievementsTable(achievements));
            if (achievements.Count > 0)
                yield return SteamArt.RenderAchievementIcons(pack, achievements, achDir, renderer, status);

            // Rich Presence localization files (one per language the game has).
            if (pack.steam.richPresence)
            {
                status?.Invoke("Rich Presence files…");
                string rpDir = Path.Combine(dir, "rich_presence");
                Directory.CreateDirectory(rpDir);
                foreach (var code in Languages(pack))
                {
                    string steamLang = SteamworksFiles.SteamLanguage(code);
                    if (steamLang == null) continue;
                    var loc = GameBootstrap.LoadLocalization(pack, code);
                    File.WriteAllText(Path.Combine(rpDir, $"rich_presence_{steamLang}.vdf"), SteamworksFiles.RichPresenceVdf(steamLang, loc.T));
                }
            }

            // Store images: the project's copies (editable by the user), generated when missing.
            string storeSrc = ProjectStoreDir(project);
            bool missing = false;
            foreach (var spec in SteamArt.StoreImages)
                if (!File.Exists(Path.Combine(storeSrc, spec.File))) missing = true;
            if (missing)
                yield return SteamArt.RenderStoreImages(pack, project.File.steamCoverLevel, storeSrc, renderer, status);
            var storeFiles = new List<string>();
            CopyFolder(storeSrc, Path.Combine(dir, "store"), storeFiles);
            CopyFolder(ProjectScreenshotsDir(project), Path.Combine(dir, "screenshots"), null);

            // SteamPipe.
            long appId = pack.game.steamAppId;
            if (appId > 0)
            {
                string pipeDir = Path.Combine(dir, "steampipe");
                Directory.CreateDirectory(pipeDir);
                string buildFile = $"app_build_{appId}.vdf";
                string contentRoot = $"..\\..\\{exeName}\\";
                File.WriteAllText(Path.Combine(pipeDir, buildFile),
                    SteamworksFiles.AppBuildVdf(appId, pack.steam.EffectiveDepotId(appId), $"{pack.game.title} {pack.game.version}", contentRoot));
                File.WriteAllText(Path.Combine(pipeDir, "upload.bat"), SteamworksFiles.UploadBat(buildFile));
            }

            File.WriteAllText(Path.Combine(dir, Readme), SteamworksFiles.Guide(pack, achievements, exeName, storeFiles));
            yield return null;
        }

        static IEnumerable<string> Languages(GamePackData pack)
        {
            var list = new List<string> { "en" };
            if (Resources.Load<TextAsset>("Localization/fr") != null) list.Add("fr");
            if (!string.IsNullOrEmpty(pack.RootPath))
            {
                string dir = Path.Combine(pack.RootPath, Core.Pack.PackPaths.LocaleDir);
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (!list.Contains(code)) list.Add(code);
                    }
            }
            return list;
        }

        static void CopyFolder(string src, string dst, List<string> names)
        {
            if (!Directory.Exists(src)) return;
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
                names?.Add(Path.GetFileName(f));
            }
        }
    }
}
