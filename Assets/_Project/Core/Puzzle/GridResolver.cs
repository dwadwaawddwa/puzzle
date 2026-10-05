using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>Everything needed to start one level, resolved from global settings + level overrides.</summary>
    public sealed class LevelSetup
    {
        public string ModeId;
        public BoardLayout Layout;
        public ModeSettings Settings;
        public int Seed;
    }

    /// <summary>Turns game.json settings into a concrete grid for a level (difficulty curve, ratio, overrides).</summary>
    public static class GridResolver
    {
        public const int MaxGrid = 12;
        public const int MaxStrips = 24;

        /// <param name="texWidth">Source image width in pixels (before crop).</param>
        public static LevelSetup Resolve(GamePackData pack, int levelIndex, int texWidth, int texHeight)
        {
            var level = pack.levels[levelIndex];
            var g = pack.gameplay;
            string modeId = string.IsNullOrEmpty(level.mode) ? g.defaultMode : level.mode;
            if (!PuzzleModeRegistry.IsRegistered(modeId)) modeId = ModeIds.SwapTiles;
            var info = PuzzleModeRegistry.InfoOf(modeId) ?? new PuzzleModeAttribute(modeId);

            var crop = level.crop ?? new CropRect();
            float aspect = ImageSlicer.CroppedAspect(texWidth, texHeight, crop);
            float t = pack.levels.Count > 1 ? (float)levelIndex / (pack.levels.Count - 1) : 0f;

            int cols, rows;
            if (info.Shape == GridShape.Strips)
            {
                int strips = level.grid?.strips ?? StripsFor(g, t);
                strips = Clamp(strips, 2, MaxStrips);
                bool vertical = g.stripsOrientation == StripsOrientation.Vertical;
                cols = vertical ? strips : 1;
                rows = vertical ? 1 : strips;
            }
            else
            {
                int n = Math.Max(info.MinGrid, (int)Math.Round(GridSizeFor(g, t) * info.GridFactor));
                (cols, rows) = ColsRowsFor(n, aspect, info.MinGrid);
                if (level.grid != null && (level.grid.cols.HasValue || level.grid.rows.HasValue))
                {
                    if (level.grid.cols.HasValue && level.grid.rows.HasValue)
                    {
                        cols = level.grid.cols.Value;
                        rows = level.grid.rows.Value;
                    }
                    else if (level.grid.cols.HasValue)
                    {
                        cols = level.grid.cols.Value;
                        rows = (int)Math.Round(cols / aspect);
                    }
                    else
                    {
                        rows = level.grid.rows.Value;
                        cols = (int)Math.Round(rows * aspect);
                    }
                    cols = Clamp(cols, info.MinGrid, MaxGrid);
                    rows = Clamp(rows, info.MinGrid, MaxGrid);
                }
                cols = Clamp(cols, info.MinGrid, Math.Min(MaxGrid, info.MaxGrid));
                rows = Clamp(rows, info.MinGrid, Math.Min(MaxGrid, info.MaxGrid));
            }

            if (info.SquareCells)
            {
                crop = ImageSlicer.SquareCellCrop(texWidth, texHeight, crop, cols, rows);
                aspect = ImageSlicer.CroppedAspect(texWidth, texHeight, crop);
            }

            return new LevelSetup
            {
                ModeId = modeId,
                Layout = new BoardLayout(cols, rows, crop, aspect),
                Settings = new ModeSettings
                {
                    ModeId = modeId,
                    LockCorrectPieces = g.lockCorrectPieces,
                    MinMisplacedRatio = g.minMisplacedRatio,
                    StripsOrientation = g.stripsOrientation,
                    RotateSteps = g.rotateSteps,
                },
                Seed = level.seed ?? StableHash.Fnv1a(level.id),
            };
        }

        /// <summary>Grid "size" N for a level at progress t (0 = first level, 1 = last).</summary>
        public static int GridSizeFor(GameplayConfig g, float t)
        {
            switch (g.difficultyCurve)
            {
                case DifficultyCurve.Progressive:
                    return Clamp((int)Math.Round(g.minGrid + (g.maxGrid - g.minGrid) * t), 2, MaxGrid);
                default:
                    return Clamp(g.fixedGrid, 2, MaxGrid);
            }
        }

        public static int StripsFor(GameplayConfig g, float t)
        {
            var r = g.stripsCount;
            return g.difficultyCurve == DifficultyCurve.Progressive
                ? (int)Math.Round(r.min + (r.max - r.min) * t)
                : r.min;
        }

        /// <summary>Picks cols × rows ≈ N × N cells whose shape is as square as possible for the image ratio.</summary>
        public static (int cols, int rows) ColsRowsFor(int n, float aspect, int minGrid = 2)
        {
            double s = Math.Sqrt(Math.Max(0.05, aspect));
            int cols = Clamp((int)Math.Round(n * s), minGrid, MaxGrid);
            int rows = Clamp((int)Math.Round(n / s), minGrid, MaxGrid);
            return (cols, rows);
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
