using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Steam;

namespace PuzzleStudio.Game.Steam
{
    /// <summary>
    /// Unlocks achievements from the saved progress and forwards them to Steam.
    /// Works without Steam too: unlocked ids are kept in the save and listed in the game menu.
    /// </summary>
    public sealed class AchievementService
    {
        readonly GamePackData _pack;
        readonly SaveSystem _save;
        readonly ISteamService _steam;
        readonly bool _persist;

        public event Action<AchievementDef> Unlocked;
        public List<AchievementDef> All { get; }

        public AchievementService(GamePackData pack, SaveSystem save, ISteamService steam, bool persist = true)
        {
            _pack = pack;
            _save = save;
            _steam = steam ?? new NullSteamService();
            _persist = persist;
            All = AchievementGenerator.Effective(pack);
        }

        public bool IsUnlocked(AchievementDef def) => _save.Progress.HasAchievement(def.id);
        public int UnlockedCount => All.FindAll(IsUnlocked).Count;

        /// <summary>At startup: re-sends local achievements to Steam (earned offline) and silently unlocks any already earned.</summary>
        public void SyncOnStart()
        {
            var newly = AchievementTracker.Unlock(_pack, _save.Progress);
            if (newly.Count > 0 && _persist) _save.SaveProgress();
            foreach (var def in All)
                if (_save.Progress.HasAchievement(def.id)) _steam.UnlockAchievement(def.id);
        }

        /// <summary>After a level is solved: unlocks what was just earned.</summary>
        public void Check()
        {
            var newly = AchievementTracker.Unlock(_pack, _save.Progress);
            if (newly.Count == 0) return;
            if (_persist) _save.SaveProgress();
            foreach (var def in newly)
            {
                _steam.UnlockAchievement(def.id);
                Unlocked?.Invoke(def);
            }
        }

        /// <summary>Name shown in the game: translated when the achievement still has its generated text.</summary>
        public string NameOf(AchievementDef def, LocalizationService loc) => Text(def, loc, "name", def.name);
        public string DescriptionOf(AchievementDef def, LocalizationService loc) => Text(def, loc, "desc", def.description);

        string Text(AchievementDef def, LocalizationService loc, string field, string fallback)
        {
            string key = $"ach.{def.id}.{field}";
            if (loc != null && loc.Has(key) && AchievementGenerator.HasDefaultTexts(_pack, def)) return loc.T(key);
            return string.IsNullOrWhiteSpace(fallback) ? def.id : fallback;
        }
    }
}
