using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>Score-keeping for one attempt at a level (moves, time, hints, preview, stars).</summary>
    public sealed class GameSession
    {
        public readonly int LevelIndex;
        public readonly IPuzzleMode Mode;
        public readonly int Par;

        public int Moves { get; private set; }
        public float Elapsed { get; private set; }
        public int HintsUsed { get; private set; }
        public bool UsedPreview { get; private set; }
        public bool IsRunning { get; private set; } = true;
        public bool IsPaused { get; set; }
        public bool IsSolved { get; private set; }

        public GameSession(int levelIndex, IPuzzleMode mode)
        {
            LevelIndex = levelIndex;
            Mode = mode;
            Par = mode.GetParMoves();
        }

        public void Tick(float dt)
        {
            if (IsRunning && !IsPaused) Elapsed += dt;
        }

        public void CountMove(int count = 1) { if (IsRunning && count > 0) Moves += count; }
        public void CountHint() => HintsUsed++;
        public void MarkPreviewUsed() => UsedPreview = true;

        public void Finish()
        {
            IsRunning = false;
            IsSolved = true;
        }

        public int Stars(StarRules rules) => StarCalculator.Compute(Moves, Par, HintsUsed, rules);

        public static string FormatTime(float seconds)
        {
            int s = (int)seconds;
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
        }
    }
}
