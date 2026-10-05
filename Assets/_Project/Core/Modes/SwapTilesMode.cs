using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine.Scripting;

namespace PuzzleStudio.Core.Modes
{
    /// <summary>
    /// Grid of shuffled tiles. Tap two tiles (or drag one onto another) to swap them.
    /// Correct tiles can lock in place. Par = minimum number of swaps (n − cycles).
    /// </summary>
    [Preserve]
    [PuzzleMode(ModeIds.SwapTiles, Shape = GridShape.Grid, MinGrid = 2)]
    public sealed class SwapTilesMode : PuzzleModeBase
    {
        readonly Stack<(int a, int b)> _history = new Stack<(int, int)>();
        int _par;

        public override string Id => ModeIds.SwapTiles;
        public override bool CanUndo => _history.Count > 0;
        protected override bool SupportsLocking => true;

        protected override void ClearHistory() => _history.Clear();

        public override void Shuffle(int seed)
        {
            var rng = new SeededRandom(seed);
            var perm = ShuffleService.Permutation(Board.CellCount, rng, Settings.MinMisplacedRatio);
            _par = ShuffleService.MinSwaps(perm);
            ApplyArrangement(perm);
        }

        public override MoveResult HandleInput(in PuzzleInput input)
        {
            switch (input.Kind)
            {
                case InputKind.Cancel:
                    if (SelectedCell < 0) return MoveResult.Ignored;
                    SelectedCell = -1;
                    return MoveResult.Deselected;

                case InputKind.Tap:
                    if (!IsCell(input.Cell)) return MoveResult.Ignored;
                    if (!CanPick(input.Cell)) return MoveResult.Blocked;
                    if (SelectedCell < 0) { SelectedCell = input.Cell; return MoveResult.Selected; }
                    if (SelectedCell == input.Cell) { SelectedCell = -1; return MoveResult.Deselected; }
                    int from = SelectedCell;
                    SelectedCell = -1;
                    DoSwap(from, input.Cell);
                    return MoveResult.Moved;

                case InputKind.DragDrop:
                    if (!IsCell(input.Cell) || !IsCell(input.TargetCell) || input.Cell == input.TargetCell)
                        return MoveResult.Ignored;
                    if (!CanPick(input.Cell) || !CanPick(input.TargetCell)) return MoveResult.Blocked;
                    SelectedCell = -1;
                    DoSwap(input.Cell, input.TargetCell);
                    return MoveResult.Moved;

                default:
                    return MoveResult.Ignored;
            }
        }

        void DoSwap(int cellA, int cellB)
        {
            int pieceA = CellToPiece[cellA], pieceB = CellToPiece[cellB];
            SwapCells(cellA, cellB);
            _history.Push((cellA, cellB));
            Commit(new PuzzleMove(pieceA, pieceB, isUndo: false));
        }

        public override bool Undo()
        {
            if (_history.Count == 0 || IsSolved()) return false;
            var (a, b) = _history.Pop();
            int pieceA = CellToPiece[a], pieceB = CellToPiece[b];
            SwapCells(a, b);
            SelectedCell = -1;
            Commit(new PuzzleMove(pieceA, pieceB, isUndo: true));
            return true;
        }

        public override Hint GetHint()
        {
            foreach (var p in PiecesById)
                if (!p.IsCorrect) return new Hint(p.Id, p.Cell, p.Id);
            return Hint.None;
        }

        public override int GetParMoves() => _par < 1 ? 1 : _par;

        bool IsCell(int cell) => cell >= 0 && cell < CellToPiece.Length;
    }
}
