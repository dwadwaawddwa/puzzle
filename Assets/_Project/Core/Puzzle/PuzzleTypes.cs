using System.Collections.Generic;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>Grid geometry of a board. Cell index = row * Cols + col, row 0 at the top.</summary>
    public sealed class BoardLayout
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly CropRect Crop;
        /// <summary>Width / height of the cropped image (in pixels).</summary>
        public readonly float ImageAspect;

        public BoardLayout(int cols, int rows, CropRect crop = null, float imageAspect = 1f)
        {
            Cols = cols < 1 ? 1 : cols;
            Rows = rows < 1 ? 1 : rows;
            Crop = crop ?? new CropRect();
            ImageAspect = imageAspect <= 0 ? 1f : imageAspect;
        }

        public int CellCount => Cols * Rows;
        public int Col(int cell) => cell % Cols;
        public int Row(int cell) => cell / Cols;
        public int Cell(int col, int row) => row * Cols + col;
        public bool InBounds(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows;

        public int Manhattan(int a, int b) =>
            System.Math.Abs(Col(a) - Col(b)) + System.Math.Abs(Row(a) - Row(b));
    }

    /// <summary>Rules for one level, resolved from game.json (global + per-level overrides).</summary>
    public sealed class ModeSettings
    {
        public string ModeId = ModeIds.SwapTiles;
        public bool LockCorrectPieces = true;
        public float MinMisplacedRatio = 0.8f;
        public StripsOrientation StripsOrientation = StripsOrientation.Vertical;
        /// <summary>Allowed initial rotations in degrees (Rotate mode).</summary>
        public List<int> RotateSteps = new List<int> { 90, 180, 270 };
        /// <summary>Card symbols (Memory mode).</summary>
        public MemorySymbols MemorySymbols = MemorySymbols.Numbers;
    }

    public sealed class PieceState
    {
        /// <summary>Piece id = index of its home cell.</summary>
        public int Id;
        /// <summary>Current cell.</summary>
        public int Cell;
        /// <summary>Clockwise quarter turns (0..3). 0 = upright.</summary>
        public int Rotation;
        public bool Locked;
        /// <summary>The hole of a sliding puzzle (never drawn).</summary>
        public bool IsEmpty;

        public bool IsCorrect => Cell == Id && Rotation == 0;
    }

    public enum InputKind { Tap, DragDrop, RotateCw, RotateCcw, Cancel, Direction }

    /// <summary>How the board reacts to dragging a piece.</summary>
    public enum DragStyle
    {
        /// <summary>The piece follows the pointer and swaps with the drop target.</summary>
        Swap,
        /// <summary>The piece follows the pointer; the others shift to make room (list reordering).</summary>
        Insert,
        /// <summary>No free drag: a drag gesture acts like a tap (sliding puzzle).</summary>
        Slide,
        /// <summary>No drag at all (rotation).</summary>
        None
    }

    public readonly struct PuzzleInput
    {
        public readonly InputKind Kind;
        public readonly int Cell;
        public readonly int TargetCell;

        public PuzzleInput(InputKind kind, int cell, int targetCell = -1)
        {
            Kind = kind;
            Cell = cell;
            TargetCell = targetCell;
        }

        public static PuzzleInput Tap(int cell) => new PuzzleInput(InputKind.Tap, cell);
        public static PuzzleInput Drop(int from, int to) => new PuzzleInput(InputKind.DragDrop, from, to);
        public static PuzzleInput RotateCw(int cell) => new PuzzleInput(InputKind.RotateCw, cell);
        public static PuzzleInput RotateCcw(int cell) => new PuzzleInput(InputKind.RotateCcw, cell);
        public static PuzzleInput Cancel() => new PuzzleInput(InputKind.Cancel, -1);
        /// <summary>Move a piece in a direction (dx, dy: -1/0/1, y down). Sliding: the tile moves into the gap.</summary>
        public static PuzzleInput Direction(int dx, int dy) => new PuzzleInput(InputKind.Direction, dx, dy);
    }

    public enum MoveResult { Ignored, Selected, Deselected, Moved, Blocked }

    public readonly struct PuzzleMove
    {
        public readonly bool IsUndo;
        /// <summary>Main piece that moved (for sounds/FX).</summary>
        public readonly int PrimaryPiece;
        /// <summary>Second piece involved (swap partner), or -1.</summary>
        public readonly int SecondaryPiece;
        /// <summary>Number of moves this action counts for (a sliding row of 3 tiles = 3; 0 = not a move, e.g. the first card of a pair).</summary>
        public readonly int Count;

        public PuzzleMove(int primary, int secondary, bool isUndo, int count = 1)
        {
            PrimaryPiece = primary;
            SecondaryPiece = secondary;
            IsUndo = isUndo;
            Count = count < 0 ? 0 : count;
        }
    }

    public readonly struct Hint
    {
        public readonly bool IsValid;
        public readonly int PieceId;
        public readonly int FromCell;
        public readonly int ToCell;

        public Hint(int pieceId, int fromCell, int toCell)
        {
            IsValid = true;
            PieceId = pieceId;
            FromCell = fromCell;
            ToCell = toCell;
        }

        public static Hint None => default;
    }
}
