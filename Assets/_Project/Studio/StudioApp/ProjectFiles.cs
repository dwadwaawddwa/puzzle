using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;

namespace PuzzleStudio.Studio.App
{
    /// <summary>Copies theme pictures (background, decorations, logo...) into the project's pack/theme folder.</summary>
    public static class ProjectFiles
    {
        /// <param name="baseName">Stable file name without extension, e.g. "background", "decor_left".</param>
        /// <returns>Pack-relative path, e.g. "theme/background.png".</returns>
        public static string ImportThemeFile(GamePackData pack, string source, string baseName)
        {
            string dir = Path.Combine(pack.RootPath, PackPaths.ThemeDir);
            Directory.CreateDirectory(dir);
            string ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext == ".jpeg") ext = ".jpg";
            string dst = Path.Combine(dir, baseName + ext);
            foreach (var old in Directory.GetFiles(dir, baseName + ".*"))
                if (!string.Equals(Path.GetFullPath(old), Path.GetFullPath(dst), System.StringComparison.OrdinalIgnoreCase))
                    File.Delete(old);
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(dst), System.StringComparison.OrdinalIgnoreCase))
                File.Copy(source, dst, true);
            return $"{PackPaths.ThemeDir}/{baseName}{ext}";
        }

        public static void RemoveThemeFile(GamePackData pack, string relative)
        {
            string path = pack.Resolve(relative);
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }
    }
}
