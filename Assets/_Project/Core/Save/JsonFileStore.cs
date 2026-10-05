using System;
using System.IO;
using PuzzleStudio.Core.Pack;

namespace PuzzleStudio.Core.Save
{
    /// <summary>
    /// Crash-safe JSON file: writes to .tmp then swaps, keeps the previous good version as .bak,
    /// and falls back to .bak (then to a fresh object) if the main file is corrupt.
    /// </summary>
    public sealed class JsonFileStore<T> where T : class, new()
    {
        public readonly string Path;
        public string BackupPath => Path + ".bak";
        string TempPath => Path + ".tmp";

        /// <summary>Set after Load when the main file was unreadable.</summary>
        public bool RecoveredFromBackup { get; private set; }
        public bool StartedFresh { get; private set; }
        public event Action<string> OnWarning;

        public JsonFileStore(string path) { Path = path; }

        public T Load()
        {
            RecoveredFromBackup = false;
            StartedFresh = false;

            if (TryRead(Path, out T value)) return value;

            bool mainExisted = File.Exists(Path);
            if (mainExisted)
            {
                OnWarning?.Invoke($"Save file is corrupt, trying backup: {Path}");
                TryMove(Path, Path + ".corrupt");
            }

            if (TryRead(BackupPath, out value))
            {
                RecoveredFromBackup = true;
                Save(value);
                return value;
            }

            StartedFresh = true;
            if (mainExisted) OnWarning?.Invoke("Backup unavailable, starting with fresh data.");
            return new T();
        }

        public void Save(T value)
        {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(TempPath, PackJson.Serialize(value));
            if (File.Exists(Path))
            {
                // Only keep a backup that is itself readable.
                if (TryRead(Path, out T _)) File.Copy(Path, BackupPath, overwrite: true);
                File.Delete(Path);
            }
            File.Move(TempPath, Path);
        }

        public void Delete()
        {
            foreach (var p in new[] { Path, BackupPath, TempPath })
                if (File.Exists(p)) File.Delete(p);
        }

        static bool TryRead(string path, out T value)
        {
            value = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                value = PackJson.Deserialize<T>(json);
                return value != null;
            }
            catch (Exception) { return false; }
        }

        static void TryMove(string from, string to)
        {
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(from, to);
            }
            catch (IOException) { }
        }
    }
}
