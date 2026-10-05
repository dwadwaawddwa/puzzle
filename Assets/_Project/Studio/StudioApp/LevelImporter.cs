using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Studio.App
{
    /// <summary>Copies images into the project's pack/levels folder and creates one level per image.</summary>
    public static class LevelImporter
    {
        public sealed class Result
        {
            public readonly List<LevelConfig> Added = new List<LevelConfig>();
            public readonly List<string> Skipped = new List<string>();
        }

        /// <summary>Expands folders (non-recursive... then one level deep) into image files, natural-sorted.</summary>
        public static List<string> CollectImages(IEnumerable<string> paths)
        {
            var files = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    var inFolder = new List<string>();
                    foreach (var f in Directory.GetFiles(p, "*.*", SearchOption.AllDirectories))
                        if (ImageHeaderReader.IsSupportedExtension(f)) inFolder.Add(f);
                    inFolder.Sort(NaturalCompare);
                    files.AddRange(inFolder);
                }
                else if (File.Exists(p)) files.Add(p);
            }
            return files;
        }

        public static Result Import(GamePackData pack, IEnumerable<string> paths)
        {
            var result = new Result();
            string levelsDir = Path.Combine(pack.RootPath, PackPaths.LevelsDir);
            Directory.CreateDirectory(levelsDir);

            foreach (var src in CollectImages(paths))
            {
                if (!ImageHeaderReader.IsSupportedExtension(src) || !ImageHeaderReader.TryReadSize(src, out _, out _))
                {
                    result.Skipped.Add(src);
                    continue;
                }

                string fileName = UniqueFileName(levelsDir, Path.GetFileName(src), src);
                string dst = Path.Combine(levelsDir, fileName);
                if (!SamePath(src, dst)) File.Copy(src, dst, overwrite: true);

                var level = new LevelConfig
                {
                    id = UniqueId(pack, "lvl_" + Slug(Path.GetFileNameWithoutExtension(fileName))),
                    image = $"{PackPaths.LevelsDir}/{fileName}",
                    name = PrettyName(Path.GetFileNameWithoutExtension(src)),
                };
                pack.levels.Add(level);
                result.Added.Add(level);
            }
            return result;
        }

        /// <summary>Removes a level. Its picture stays on disk until the project is reopened (undo needs it).</summary>
        public static void Remove(GamePackData pack, LevelConfig level) => pack.levels.Remove(level);

        /// <summary>Moves the level at <paramref name="from"/> so it ends up at index <paramref name="to"/>.</summary>
        public static bool Move(GamePackData pack, int from, int to)
        {
            var levels = pack.levels;
            if (from < 0 || from >= levels.Count) return false;
            to = Math.Max(0, Math.Min(levels.Count - 1, to));
            if (from == to) return false;
            var level = levels[from];
            levels.RemoveAt(from);
            levels.Insert(to, level);
            return true;
        }

        public static LevelConfig Duplicate(GamePackData pack, LevelConfig level)
        {
            var copy = new LevelConfig
            {
                id = UniqueId(pack, level.id + "_copy"),
                image = level.image,
                name = level.name + " (copy)",
                mode = level.mode,
                grid = level.grid == null ? null : new GridOverride { cols = level.grid.cols, rows = level.grid.rows, strips = level.grid.strips },
                crop = new CropRect(level.crop.x, level.crop.y, level.crop.w, level.crop.h),
            };
            pack.levels.Insert(pack.levels.IndexOf(level) + 1, copy);
            return copy;
        }

        public static void SortByFileName(GamePackData pack) =>
            pack.levels.Sort((a, b) => NaturalCompare(Path.GetFileName(a.image), Path.GetFileName(b.image)));

        /// <summary>"sunset_hills-02" → "Sunset Hills 02".</summary>
        public static string PrettyName(string fileName)
        {
            string s = Regex.Replace(fileName ?? "", @"[_\-\.]+", " ").Trim();
            s = Regex.Replace(s, @"\s+", " ");
            if (s.Length == 0) return "Level";
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        /// <summary>Compares strings with numbers in numeric order ("img2" &lt; "img10").</summary>
        public static int NaturalCompare(string a, string b)
        {
            var ra = Regex.Split(a ?? "", @"(\d+)");
            var rb = Regex.Split(b ?? "", @"(\d+)");
            for (int i = 0; i < Math.Min(ra.Length, rb.Length); i++)
            {
                int c;
                if (long.TryParse(ra[i], out long na) && long.TryParse(rb[i], out long nb)) c = na.CompareTo(nb);
                else c = string.Compare(ra[i], rb[i], StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
            }
            return ra.Length.CompareTo(rb.Length);
        }

        static string Slug(string s)
        {
            s = Regex.Replace((s ?? "").ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
            return s.Length == 0 ? "level" : s.Length > 32 ? s.Substring(0, 32) : s;
        }

        static string UniqueId(GamePackData pack, string baseId)
        {
            string id = baseId;
            for (int i = 2; pack.IndexOfLevel(id) >= 0; i++) id = $"{baseId}_{i}";
            return id;
        }

        static string UniqueFileName(string dir, string fileName, string source)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".jpeg") ext = ".jpg";
            name = Regex.Replace(name, @"[^\w\-\. ]", "_");
            string candidate = name + ext;
            for (int i = 2; File.Exists(Path.Combine(dir, candidate)) && !SamePath(source, Path.Combine(dir, candidate)); i++)
                candidate = $"{name}_{i}{ext}";
            return candidate;
        }

        static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
