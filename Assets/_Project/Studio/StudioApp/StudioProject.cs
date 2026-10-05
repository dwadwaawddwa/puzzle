using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Studio.App
{
    /// <summary>project.json — Studio-only settings (export options, preview state). The game content is in pack/.</summary>
    [Serializable]
    public sealed class StudioProjectFile
    {
        public int version = 1;
        public ExportSettings export = new ExportSettings();
        public int previewLevel = 0;
        public string previewResolution = "1920x1080";
    }

    [Serializable]
    public sealed class ExportSettings
    {
        /// <summary>Executable name without ".exe". Empty = derived from the game title.</summary>
        public string exeName = "";
        /// <summary>Project-relative PNG used as the exe icon. Null = generated from the first level.</summary>
        public string icon = null;
        /// <summary>Absolute folder receiving exports. Null = Documents\PuzzleStudio Exports.</summary>
        public string outputDir = null;
        public bool zip = false;
        public bool openFolder = true;
    }

    /// <summary>
    /// A Studio project = a "&lt;Name&gt;.puzzleproj" folder holding project.json and the source Game Pack (pack/).
    /// </summary>
    public sealed class StudioProject
    {
        public const string Extension = ".puzzleproj";
        public const string ProjectFileName = "project.json";
        public const string PackFolder = "pack";

        public string Root { get; private set; }
        public StudioProjectFile File { get; private set; }
        public GamePackData Pack { get; private set; }

        public string PackDir => Path.Combine(Root, PackFolder);
        public string Name => Path.GetFileNameWithoutExtension(Root);

        public static StudioProject Create(string parentDir, string name)
        {
            string safe = SanitizeFileName(name, "MyPuzzleGame");
            string root = Path.Combine(parentDir, safe + Extension);
            if (Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length > 0)
                throw new IOException($"A project already exists at {root}");

            Directory.CreateDirectory(Path.Combine(root, PackFolder, PackPaths.LevelsDir));
            var pack = new GamePackData();
            pack.game.title = string.IsNullOrWhiteSpace(name) ? "My Puzzle Game" : name.Trim();
            pack.RootPath = Path.Combine(root, PackFolder);

            var project = new StudioProject { Root = root, File = new StudioProjectFile(), Pack = pack };
            project.Save();
            return project;
        }

        /// <summary>Creates a new project from an existing Game Pack folder (e.g. a sample pack), copying its files.</summary>
        public static StudioProject CreateFromPack(string packDir, string parentDir)
        {
            var source = GamePackLoader.LoadFromDirectory(packDir);
            packDir = source.RootPath.TrimEnd('\\', '/');
            string baseName = SanitizeFileName(source.game.title, "MyPuzzleGame");
            string root = Path.Combine(parentDir, baseName + Extension);
            for (int i = 2; Directory.Exists(root); i++) root = Path.Combine(parentDir, $"{baseName} {i}{Extension}");

            string dst = Path.Combine(root, PackFolder);
            foreach (var f in Directory.GetFiles(packDir, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(dst, f.Substring(packDir.Length).TrimStart('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                System.IO.File.Copy(f, target, true);
            }
            var project = Open(root);
            project.Save();
            return project;
        }

        /// <param name="path">The project folder, its project.json, or the pack's game.json.</param>
        public static StudioProject Open(string path)
        {
            string root = ResolveRoot(path);
            if (root == null) throw new IOException($"Not a Puzzle Studio project: {path}");

            string projectJson = Path.Combine(root, ProjectFileName);
            var file = System.IO.File.Exists(projectJson)
                ? PackJson.Deserialize<StudioProjectFile>(System.IO.File.ReadAllText(projectJson)) ?? new StudioProjectFile()
                : new StudioProjectFile();
            file.export ??= new ExportSettings();

            var pack = GamePackLoader.LoadFromDirectory(Path.Combine(root, PackFolder));
            return new StudioProject { Root = root, File = file, Pack = pack };
        }

        public static string ResolveRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p = Path.GetFullPath(path);
            if (System.IO.File.Exists(p))
            {
                string dir = Path.GetDirectoryName(p);
                if (Path.GetFileName(p).Equals(PackPaths.GameJson, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(dir).Equals(PackFolder, StringComparison.OrdinalIgnoreCase))
                    dir = Path.GetDirectoryName(dir);
                p = dir;
            }
            return System.IO.File.Exists(Path.Combine(p, PackFolder, PackPaths.GameJson)) ? p : null;
        }

        public void Save()
        {
            Directory.CreateDirectory(Root);
            GamePackWriter.WriteJson(Pack, PackDir);
            string tmp = Path.Combine(Root, ProjectFileName + ".tmp");
            System.IO.File.WriteAllText(tmp, PackJson.Serialize(File));
            string dst = Path.Combine(Root, ProjectFileName);
            if (System.IO.File.Exists(dst)) System.IO.File.Delete(dst);
            System.IO.File.Move(tmp, dst);
        }

        /// <summary>Absolute path of a project-relative file (export icon...).</summary>
        public string ResolveProjectPath(string relative) =>
            string.IsNullOrEmpty(relative) ? null : Path.IsPathRooted(relative) ? relative : Path.Combine(Root, relative);

        public string ExeName =>
            SanitizeFileName(string.IsNullOrWhiteSpace(File.export.exeName) ? Pack.game.title.Replace(" ", "") : File.export.exeName, "PuzzleGame");

        public static string DefaultExportRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PuzzleStudio Exports");

        public string ExportRoot => string.IsNullOrWhiteSpace(File.export.outputDir) ? DefaultExportRoot : File.export.outputDir;

        public static string DefaultProjectsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PuzzleStudio Projects");

        public static string SanitizeFileName(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            var invalid = Path.GetInvalidFileNameChars();
            var chars = new List<char>();
            foreach (char c in name.Trim()) chars.Add(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            string s = new string(chars.ToArray()).Trim().TrimEnd('.');
            return s.Length == 0 ? fallback : s;
        }
    }
}
