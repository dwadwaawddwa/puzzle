using System;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Save
{
    /// <summary>Unlocking, "continue" and totals — pure rules shared by the game and the Studio.</summary>
    public static class ProgressRules
    {
        public static bool IsUnlocked(GamePackData pack, PlayerProgress progress, int index)
        {
            if (index <= 0) return true;
            if (index >= pack.levels.Count) return false;
            if (progress.IsCompleted(pack.levels[index].id)) return true;
            switch (pack.gameplay.unlockRule)
            {
                case UnlockRule.AllUnlocked:
                    return true;
                case UnlockRule.ByStars:
                    return TotalStars(pack, progress) >= StarsRequired(pack, index);
                default: // Sequential
                    return progress.IsCompleted(pack.levels[index - 1].id);
            }
        }

        /// <summary>Stars needed to open level <paramref name="index"/> with the ByStars rule.</summary>
        public static int StarsRequired(GamePackData pack, int index) =>
            Math.Max(0, index * Math.Max(0, pack.gameplay.starsToUnlockPerLevel));

        public static int CompletedCount(GamePackData pack, PlayerProgress progress)
        {
            int n = 0;
            foreach (var l in pack.levels) if (progress.IsCompleted(l.id)) n++;
            return n;
        }

        public static bool AllCompleted(GamePackData pack, PlayerProgress progress) =>
            pack.levels.Count > 0 && CompletedCount(pack, progress) == pack.levels.Count;

        public static bool HasStarted(GamePackData pack, PlayerProgress progress) => CompletedCount(pack, progress) > 0;

        public static int TotalStars(GamePackData pack, PlayerProgress progress)
        {
            int n = 0;
            foreach (var l in pack.levels) n += progress.StarsOf(l.id);
            return n;
        }

        public static float TotalBestTime(GamePackData pack, PlayerProgress progress)
        {
            float t = 0;
            foreach (var l in pack.levels) t += progress.Get(l.id)?.bestTime ?? 0f;
            return t;
        }

        /// <summary>The level "Continue" resumes: the last played one if unfinished, else the first unlocked unfinished one.</summary>
        public static int ContinueIndex(GamePackData pack, PlayerProgress progress)
        {
            int last = pack.IndexOfLevel(progress.lastPlayedLevelId);
            if (last >= 0 && !progress.IsCompleted(pack.levels[last].id) && IsUnlocked(pack, progress, last)) return last;
            for (int i = 0; i < pack.levels.Count; i++)
                if (!progress.IsCompleted(pack.levels[i].id) && IsUnlocked(pack, progress, i)) return i;
            return 0;
        }

        /// <summary>Next level to offer after finishing <paramref name="index"/>, or -1 if none is available.</summary>
        public static int NextPlayable(GamePackData pack, PlayerProgress progress, int index)
        {
            int next = index + 1;
            return next < pack.levels.Count && IsUnlocked(pack, progress, next) ? next : -1;
        }
    }
}
