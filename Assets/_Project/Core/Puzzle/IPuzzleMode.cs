using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>
    /// Pure puzzle logic (no rendering). To add a mode: create a class implementing this interface
    /// (usually by extending <see cref="PuzzleModeBase"/>) and tag it with <see cref="PuzzleModeAttribute"/>.
    /// </summary>
    public interface IPuzzleMode
    {
        string Id { get; }
        BoardLayout Layout { get; }
        IReadOnlyList<PieceState> Pieces { get; }
        /// <summary>Cell currently selected by a first tap, or -1.</summary>
        int SelectedCell { get; }
        bool CanUndo { get; }
        /// <summary>How dragging behaves for this mode (board feedback and input).</summary>
        DragStyle DragStyle { get; }

        void Setup(BoardLayout board, ModeSettings settings);
        /// <summary>Deterministic shuffle; the result is never solved and is always solvable.</summary>
        void Shuffle(int seed);
        MoveResult HandleInput(in PuzzleInput input);
        bool IsSolved();
        Hint GetHint();
        /// <summary>Reference number of moves used for star rating.</summary>
        int GetParMoves();
        bool Undo();
        /// <summary>Puts every piece in place at once (Studio preview of the victory, captures, tests).</summary>
        void ForceSolve();

        /// <summary>True if the piece on this cell can be picked up / dragged.</summary>
        bool CanPick(int cell);
        /// <summary>Piece id currently on a cell.</summary>
        int PieceAt(int cell);

        event Action<PuzzleMove> OnMove;
        event Action<int> OnPieceCorrect;
        event Action OnSolved;
    }

    public enum GridShape
    {
        /// <summary>cols × rows grid adapted to the image ratio.</summary>
        Grid,
        /// <summary>N strips (1 row or 1 column).</summary>
        Strips
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PuzzleModeAttribute : Attribute
    {
        public readonly string Id;
        public GridShape Shape { get; set; } = GridShape.Grid;
        /// <summary>Cells must be exactly square (pieces can rotate).</summary>
        public bool SquareCells { get; set; }
        /// <summary>Minimum cells per side.</summary>
        public int MinGrid { get; set; } = 2;
        /// <summary>Maximum cells per side (the difficulty curve is clamped to it).</summary>
        public int MaxGrid { get; set; } = 12;
        /// <summary>Multiplier of the difficulty curve's grid size (harder modes use smaller grids).</summary>
        public float GridFactor { get; set; } = 1f;

        public PuzzleModeAttribute(string id) { Id = id; }
    }
}
