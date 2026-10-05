using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Data
{
    [Serializable]
    public sealed class GameplayConfig
    {
        public string defaultMode = ModeIds.SwapTiles;
        public DifficultyCurve difficultyCurve = DifficultyCurve.Progressive;
        /// <summary>Grid "size" N for the first / last level (Progressive). Non-square images get N adapted to their ratio.</summary>
        public int minGrid = 3;
        public int maxGrid = 7;
        /// <summary>Grid size used when difficultyCurve = Fixed (or as fallback for Custom).</summary>
        public int fixedGrid = 4;
        public IntRange stripsCount = new IntRange { min = 4, max = 12 };
        public StripsOrientation stripsOrientation = StripsOrientation.Vertical;
        public List<int> rotateSteps = new List<int> { 90, 180, 270 };
        public bool lockCorrectPieces = true;
        /// <summary>Memory mode: symbol shown on the cards (a level can override it).</summary>
        public MemorySymbols memorySymbols = MemorySymbols.Numbers;
        /// <summary>Minimum ratio of misplaced pieces after a shuffle (0..1).</summary>
        public float minMisplacedRatio = 0.8f;
        public UnlockRule unlockRule = UnlockRule.Sequential;
        public int starsToUnlockPerLevel = 2;
        public bool showTimer = true;
        public bool showMoves = true;
        public bool allowPreview = true;
        public bool allowHints = true;
        public bool allowUndo = true;
        public int maxHintsPerLevel = 3;
        public StarRules starRules = new StarRules();
    }

    [Serializable]
    public sealed class IntRange
    {
        public int min;
        public int max;
    }

    [Serializable]
    public sealed class StarRules
    {
        /// <summary>moves &lt;= par * factor → 3 stars.</summary>
        public float threeStarsMoveFactor = 1.2f;
        /// <summary>moves &lt;= par * factor → 2 stars, else 1.</summary>
        public float twoStarsMoveFactor = 2.0f;
        /// <summary>If true, using at least one hint caps the result at 2 stars.</summary>
        public bool hintCostsStar = true;
    }
}
