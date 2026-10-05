using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Save;

namespace PuzzleStudio.Core.Steam
{
    /// <summary>The achievements of a game: the custom list from the pack, or a set generated from the levels.</summary>
    public static class AchievementGenerator
    {
        static readonly Regex ValidId = new Regex("^[A-Za-z0-9_]+$");

        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 128 && ValidId.IsMatch(id);

        /// <summary>The list the game uses (empty when achievements are turned off).</summary>
        public static List<AchievementDef> Effective(GamePackData pack)
        {
            if (pack == null || !pack.steam.achievementsEnabled) return new List<AchievementDef>();
            return pack.steam.achievements.Count > 0 ? pack.steam.achievements : Auto(pack);
        }

        public static bool IsCustom(GamePackData pack) => pack.steam.achievements.Count > 0;

        /// <summary>A sensible set for any game, scaled to its number of levels.</summary>
        public static List<AchievementDef> Auto(GamePackData pack)
        {
            var list = new List<AchievementDef>();
            int n = pack.levels.Count;
            if (n == 0) return list;

            void Add(string id, AchievementRule rule, float value, string name, string description) =>
                list.Add(new AchievementDef { id = id, rule = rule, value = value, name = name, description = description });

            Add("ACH_FIRST_PUZZLE", AchievementRule.LevelsCompleted, 1, "First Puzzle", "Complete your first puzzle.");
            if (n >= 8) Add("ACH_QUARTER", AchievementRule.PercentCompleted, 25, "Warming Up", "Complete a quarter of the puzzles.");
            if (n >= 4) Add("ACH_HALF", AchievementRule.PercentCompleted, 50, "Halfway There", "Complete half of the puzzles.");
            if (n >= 8) Add("ACH_THREE_QUARTERS", AchievementRule.PercentCompleted, 75, "Almost Done", "Complete three quarters of the puzzles.");
            if (n >= 2) Add("ACH_ALL_PUZZLES", AchievementRule.PercentCompleted, 100, "Puzzle Master", "Complete every puzzle.");
            Add("ACH_PERFECT", AchievementRule.PerfectLevel, 0, "Perfect Moves", "Solve a puzzle in the par number of moves or fewer.");
            if (pack.gameplay.allowHints) Add("ACH_NO_HINT", AchievementRule.NoHintLevel, 0, "On My Own", "Solve a puzzle without using a hint.");
            if (pack.gameplay.allowPreview) Add("ACH_NO_PREVIEW", AchievementRule.NoPreviewLevel, 0, "Sharp Memory", "Solve a puzzle without looking at the picture.");
            Add("ACH_FAST", AchievementRule.FastLevel, 60, "Speed Solver", "Solve a puzzle in under a minute.");
            Add("ACH_ALL_STARS", AchievementRule.AllThreeStars, 0, "Star Collector", "Get 3 stars on every puzzle.");
            return list;
        }

        /// <summary>True when <paramref name="def"/> still has the generated English texts (the game can then translate them).</summary>
        public static bool HasDefaultTexts(GamePackData pack, AchievementDef def)
        {
            foreach (var a in Auto(pack))
                if (a.id == def.id) return a.name == def.name && a.description == def.description;
            return false;
        }
    }

    /// <summary>Decides which achievements a player has earned. Everything is derived from the saved progress,
    /// so achievements earned offline (or before Steam was set up) are recovered at the next start.</summary>
    public static class AchievementTracker
    {
        public static bool IsSatisfied(AchievementDef def, GamePackData pack, PlayerProgress p)
        {
            int n = pack.levels.Count;
            if (n == 0 || def == null) return false;
            switch (def.rule)
            {
                case AchievementRule.LevelsCompleted:
                    return ProgressRules.CompletedCount(pack, p) >= Math.Max(1, (int)Math.Ceiling(def.value));
                case AchievementRule.PercentCompleted:
                {
                    float percent = Math.Min(100f, Math.Max(1f, def.value));
                    return ProgressRules.CompletedCount(pack, p) * 100f >= percent * n - 0.001f;
                }
                case AchievementRule.AllThreeStars:
                    foreach (var level in pack.levels)
                        if (p.StarsOf(level.id) < 3) return false;
                    return true;
                case AchievementRule.NoPreviewLevel: return Any(pack, p, r => r.completedWithoutPreview);
                case AchievementRule.NoHintLevel: return Any(pack, p, r => r.completedWithoutHint);
                case AchievementRule.FastLevel: return Any(pack, p, r => r.bestTime <= Math.Max(1f, def.value));
                case AchievementRule.PerfectLevel: return Any(pack, p, r => r.perfect);
                default: return false;
            }
        }

        /// <summary>Adds every newly earned achievement to the progress and returns them (in list order).</summary>
        public static List<AchievementDef> Unlock(GamePackData pack, PlayerProgress p)
        {
            var unlocked = new List<AchievementDef>();
            p.achievements ??= new List<string>();
            foreach (var def in AchievementGenerator.Effective(pack))
            {
                if (string.IsNullOrEmpty(def.id) || p.achievements.Contains(def.id)) continue;
                if (!IsSatisfied(def, pack, p)) continue;
                p.achievements.Add(def.id);
                unlocked.Add(def);
            }
            return unlocked;
        }

        /// <summary>(current, target) for counting achievements ("4 / 12"), or (0, 0) when there is nothing to count.</summary>
        public static (int current, int target) ProgressOf(AchievementDef def, GamePackData pack, PlayerProgress p)
        {
            int n = pack.levels.Count;
            if (n == 0) return (0, 0);
            switch (def.rule)
            {
                case AchievementRule.LevelsCompleted:
                {
                    int target = Math.Max(1, (int)Math.Ceiling(def.value));
                    return (Math.Min(target, ProgressRules.CompletedCount(pack, p)), target);
                }
                case AchievementRule.PercentCompleted:
                {
                    int target = (int)Math.Ceiling(Math.Min(100f, Math.Max(1f, def.value)) * n / 100f - 0.001f);
                    target = Math.Max(1, target);
                    return (Math.Min(target, ProgressRules.CompletedCount(pack, p)), target);
                }
                case AchievementRule.AllThreeStars:
                {
                    int done = 0;
                    foreach (var level in pack.levels) if (p.StarsOf(level.id) >= 3) done++;
                    return (done, n);
                }
                default: return (0, 0);
            }
        }

        static bool Any(GamePackData pack, PlayerProgress p, Func<LevelRecord, bool> test)
        {
            foreach (var level in pack.levels)
            {
                var r = p.Get(level.id);
                if (r != null && r.completed && test(r)) return true;
            }
            return false;
        }
    }
}
