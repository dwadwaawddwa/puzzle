using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Save
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public int version = 1;
        public Dictionary<string, LevelRecord> levels = new Dictionary<string, LevelRecord>();
        public string lastPlayedLevelId = null;
        public long totalPlaySeconds = 0;
        /// <summary>Unlocked achievement ids (kept locally, also when Steam is not running).</summary>
        public List<string> achievements = new List<string>();

        public LevelRecord Get(string levelId) =>
            levelId != null && levels.TryGetValue(levelId, out var r) ? r : null;

        public bool IsCompleted(string levelId) => Get(levelId)?.completed == true;
        public int StarsOf(string levelId) => Get(levelId)?.stars ?? 0;
        public bool HasAchievement(string id) => achievements != null && achievements.Contains(id);

        /// <summary>Merges a finished attempt and returns what improved.</summary>
        /// <param name="perfect">Solved in the par number of moves or fewer.</param>
        public RecordResult Submit(string levelId, int stars, float seconds, int moves, bool usedPreview, bool usedHint, bool perfect = false)
        {
            var result = new RecordResult();
            if (!levels.TryGetValue(levelId, out var r))
            {
                r = new LevelRecord();
                levels[levelId] = r;
                result.FirstCompletion = true;
            }
            else if (!r.completed) result.FirstCompletion = true;

            if (r.completed && seconds < r.bestTime) result.NewBestTime = true;
            if (r.completed && moves < r.bestMoves) result.NewBestMoves = true;
            if (stars > r.stars) result.MoreStars = true;

            r.bestTime = r.completed ? Math.Min(r.bestTime, seconds) : seconds;
            r.bestMoves = r.completed ? Math.Min(r.bestMoves, moves) : moves;
            r.stars = Math.Max(r.stars, stars);
            r.completed = true;
            r.timesCompleted++;
            if (!usedPreview) r.completedWithoutPreview = true;
            if (!usedHint) r.completedWithoutHint = true;
            if (perfect) r.perfect = true;
            return result;
        }
    }

    [Serializable]
    public sealed class LevelRecord
    {
        public bool completed;
        public int stars;
        public float bestTime;
        public int bestMoves;
        public int timesCompleted;
        public bool completedWithoutPreview;
        public bool completedWithoutHint;
        public bool perfect;
    }

    public struct RecordResult
    {
        public bool FirstCompletion;
        public bool NewBestTime;
        public bool NewBestMoves;
        public bool MoreStars;
    }

    public enum DisplayMode { Fullscreen, Borderless, Windowed }

    [Serializable]
    public sealed class SettingsData
    {
        public int version = 1;
        public float masterVolume = 1f;
        public float musicVolume = -1f;   // -1 = use the pack default
        public float sfxVolume = -1f;
        public DisplayMode displayMode = DisplayMode.Borderless;
        public int resolutionWidth = 0;   // 0 = native
        public int resolutionHeight = 0;
        public bool vSync = true;
        public int fpsLimit = 0;          // 0 = unlimited (when vSync is off)
        public string language = null;   // null = pack default
        public float uiScale = 1f;
        public bool colorblindMode = false;
        public bool reduceMotion = false;
        /// <summary>Gamepad rumble on snaps and victories.</summary>
        public bool vibration = true;
        /// <summary>Replaces the theme fonts with the plainest built-in font (Inter).</summary>
        public bool readableFont = false;
        /// <summary>Relaxed play: hide the timer during levels.</summary>
        public bool showTimer = true;
        /// <summary>Steam Deck defaults (larger interface) were applied once.</summary>
        public bool deckDefaultsApplied = false;
    }

    /// <summary>First launch on a Steam Deck (7" screen, 1280 × 800): a larger interface.</summary>
    public static class DeckDefaults
    {
        public const float UiScale = 1.15f;

        /// <returns>True when the settings were changed (save them).</returns>
        public static bool Apply(SettingsData s, bool isSteamDeck)
        {
            if (!isSteamDeck || s.deckDefaultsApplied) return false;
            s.deckDefaultsApplied = true;
            if (Math.Abs(s.uiScale - 1f) < 0.01f) s.uiScale = UiScale;
            return true;
        }
    }
}
