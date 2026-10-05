using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PuzzleStudio.Core.Data
{
    /// <summary>Root of game.json. Every field has a default so older/partial packs still load.</summary>
    [Serializable]
    public sealed class GamePackData
    {
        public const int CurrentVersion = 1;

        public int packVersion = CurrentVersion;
        public GameInfo game = new GameInfo();
        public GameplayConfig gameplay = new GameplayConfig();
        public ThemeConfig theme = new ThemeConfig();
        public AudioConfig audio = new AudioConfig();
        public SteamConfig steam = new SteamConfig();
        public LayoutConfig layout = new LayoutConfig();
        public List<LevelConfig> levels = new List<LevelConfig>();

        /// <summary>Absolute folder the pack was loaded from (not serialized).</summary>
        [JsonIgnore] public string RootPath;

        /// <summary>Resolves a pack-relative path ("levels/01.png") to an absolute path. Returns null for null/"default:" refs.</summary>
        public string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || PackPathsUtil.IsDefaultRef(relativePath)) return null;
            if (System.IO.Path.IsPathRooted(relativePath)) return relativePath;
            return string.IsNullOrEmpty(RootPath) ? relativePath : System.IO.Path.Combine(RootPath, relativePath);
        }

        public int IndexOfLevel(string id)
        {
            for (int i = 0; i < levels.Count; i++) if (levels[i].id == id) return i;
            return -1;
        }
    }

    public static class PackPathsUtil
    {
        public const string DefaultPrefix = "default:";
        public static bool IsDefaultRef(string value) =>
            value != null && value.StartsWith(DefaultPrefix, StringComparison.OrdinalIgnoreCase);
        public static string DefaultName(string value) =>
            IsDefaultRef(value) ? value.Substring(DefaultPrefix.Length) : value;
    }

    [Serializable]
    public sealed class GameInfo
    {
        public string title = "My Puzzle Game";
        public string subtitle = "";
        public string developer = "";
        public string version = "1.0.0";
        public long steamAppId = 0;
        public string defaultLanguage = "en";
        public string logo = null;
        public string devLogo = null;
        public List<CreditEntry> credits = new List<CreditEntry>();
    }

    [Serializable]
    public sealed class CreditEntry
    {
        public string role = "";
        public List<string> names = new List<string>();
    }

    [Serializable]
    public sealed class SteamConfig
    {
        public bool cloudEnabled = false;
        public bool richPresence = true;
        /// <summary>Empty = generated automatically from the level count.</summary>
        public List<AchievementDef> achievements = new List<AchievementDef>();
    }

    [Serializable]
    public sealed class AchievementDef
    {
        public string id = "";
        public string name = "";
        public string description = "";
        public AchievementRule rule = AchievementRule.LevelsCompleted;
        public float value = 1;
        public bool hidden = false;
    }
}
