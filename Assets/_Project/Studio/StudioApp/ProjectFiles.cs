using System;
using System.IO;
using System.Security.Cryptography;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;

namespace PuzzleStudio.Studio.App
{
    /// <summary>
    /// Copies files the user picks (theme pictures, logos, fonts, music, sounds) into the project's pack.
    /// Each file gets a name made of its role and its content ("theme/background_3fa2c1d0.png"): a new picture never
    /// overwrites the old one, so undo can bring it back. Unused files go to ".trash" when the project is opened.
    /// </summary>
    public static class ProjectFiles
    {
        /// <param name="baseName">Role of the file, e.g. "background", "decor_left", "logo", "font_title".</param>
        /// <returns>Pack-relative path, e.g. "theme/background_3fa2c1d0.png".</returns>
        public static string ImportThemeFile(GamePackData pack, string source, string baseName) =>
            Import(pack, source, PackPaths.ThemeDir, baseName);

        /// <returns>Pack-relative path, e.g. "audio/music_menu_7b21e09a.ogg".</returns>
        public static string ImportAudioFile(GamePackData pack, string source, string baseName) =>
            Import(pack, source, PackPaths.AudioDir, baseName);

        static string Import(GamePackData pack, string source, string folder, string baseName)
        {
            string dir = Path.Combine(pack.RootPath, folder);
            Directory.CreateDirectory(dir);
            string ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext == ".jpeg") ext = ".jpg";
            string name = $"{baseName}_{ContentHash(source)}{ext}";
            string dst = Path.Combine(dir, name);
            if (!File.Exists(dst)) File.Copy(source, dst, false);
            return $"{folder}/{name}";
        }

        /// <summary>8 hex characters of the file's SHA-1.</summary>
        public static string ContentHash(string path)
        {
            using (var sha = SHA1.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream), 0, 4).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Kept for compatibility: files are no longer deleted during a session (undo), see
        /// <see cref="StudioProject.MoveUnusedFilesToTrash"/>.</summary>
        public static void RemoveThemeFile(GamePackData pack, string relative) { }
    }
}
