using System.IO;
using System.Text;

namespace PuzzleStudio.Core.Save
{
    /// <summary>Progress + settings for one game, stored in &lt;root&gt;/&lt;GameName&gt;/.</summary>
    public sealed class SaveSystem
    {
        public readonly string Directory;
        public readonly JsonFileStore<PlayerProgress> ProgressStore;
        public readonly JsonFileStore<SettingsData> SettingsStore;

        public PlayerProgress Progress { get; private set; }
        public SettingsData Settings { get; private set; }

        public SaveSystem(string rootDirectory, string gameName)
        {
            Directory = Path.Combine(rootDirectory, SanitizeFolderName(gameName));
            ProgressStore = new JsonFileStore<PlayerProgress>(Path.Combine(Directory, "progress.json"));
            SettingsStore = new JsonFileStore<SettingsData>(Path.Combine(Directory, "settings.json"));
        }

        public void LoadAll()
        {
            Progress = ProgressStore.Load();
            Settings = SettingsStore.Load();
        }

        public void SaveProgress() => ProgressStore.Save(Progress);
        public void SaveSettings() => SettingsStore.Save(Settings);

        public void ResetProgress()
        {
            ProgressStore.Delete();
            Progress = new PlayerProgress();
            SaveProgress();
        }

        public static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "PuzzleGame";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            string s = sb.ToString().TrimEnd('.', ' ');
            return s.Length == 0 ? "PuzzleGame" : s;
        }
    }
}
