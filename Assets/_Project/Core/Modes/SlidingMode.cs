using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine.Scripting;

namespace PuzzleStudio.Core.Modes
{
    /// <summary>
    /// Sliding puzzle ("taquin"): the bottom-right tile is removed, tiles in line with the gap slide into it
    /// (a whole row/column at once). Shuffled by a random walk of legal moves, so it is always solvable.
    /// Par ≈ 1.7 × (Manhattan distance + linear conflicts), a realistic target for a human.
    /// </summary>
    [Preserve]
    [PuzzleMode(ModeIds.Sliding, Shape = GridShape.Grid, MinGrid = 3, MaxGrid = 6, GridFactor = 0.75f)]
    public sealed class SlidingMode : PuzzleModeBase
    {
        /// <summary>Each entry = the gap positions visited during one action (to undo it).</summary>
        readonly Stack<List<int>> _history = new Stack<List<int>>();
        int _empty;
        int _par;

        public override string Id => ModeIds.Sliding;
        public override bool CanUndo => _history.Count > 0;
        public override DragStyle DragStyle => DragStyle.Slide;
        protected override void ClearHistory() => _history.Clear();

        public int EmptyCell => PiecesById[_empty].Cell;

        public override void Setup(BoardLayout board, ModeSettings settings)
        {
            base.Setup(board, settings);
            _empty = board.CellCount - 1;
            PiecesById[_empty].IsEmpty = true;
        }

        public override void Shuffle(int seed)
        {
            var rng = new SeededRandom(seed);
            int n = Board.CellCount;
            var arrangement = new int[n];
            for (int i = 0; i < n; i++) arrangement[i] = i;
            int gap = n - 1, previous = -1;
            int required = ShuffleService.RequiredMisplaced(n - 1, Settings.MinMisplacedRatio);
            int steps = 0, minSteps = n * 25, maxSteps = n * 200;

            while (steps < maxSteps && (steps < minSteps || Misplaced(arrangement) < required))
            {
                var options = Neighbours(gap);
                options.Remove(previous);
                int next = options[rng.Next(options.Count)];
                (arrangement[gap], arrangement[next]) = (arrangement[next], arrangement[gap]);
                previous = gap;
                gap = next;
                steps++;
            }
            ApplyArrangement(arrangement);
            _par = Math.Max(1, (int)Math.Ceiling(LowerBound() * 1.7));
        }

        public override bool CanPick(int cell)
        {
            if (cell < 0 || cell >= CellToPiece.Length || cell == EmptyCell) return false;
            int e = EmptyCell;
            return Board.Row(cell) == Board.Row(e) || Board.Col(cell) == Board.Col(e);
        }

        public override MoveResult HandleInput(in PuzzleInput input)
        {
            switch (input.Kind)
            {
                case InputKind.Tap:
                case InputKind.DragDrop:
                    return SlideFrom(input.Cell);
                case InputKind.Direction:
                {
                    // The tile moves in (dx, dy) into the gap, so it comes from the opposite side of the gap.
                    int e = EmptyCell;
                    int col = Board.Col(e) - input.Cell, row = Board.Row(e) - input.TargetCell;
                    if (!Board.InBounds(col, row)) return MoveResult.Blocked;
                    return SlideFrom(Board.Cell(col, row));
                }
                default:
                    return MoveResult.Ignored;
            }
        }

        MoveResult SlideFrom(int cell)
        {
            if (!CanPick(cell)) return cell == EmptyCell ? MoveResult.Ignored : MoveResult.Blocked;
            var path = new List<int> { EmptyCell };
            int first = -1, count = 0;
            while (EmptyCell != cell)
            {
                int e = EmptyCell;
                int dc = Math.Sign(Board.Col(cell) - Board.Col(e)), dr = Math.Sign(Board.Row(cell) - Board.Row(e));
                int next = Board.Cell(Board.Col(e) + dc, Board.Row(e) + dr);
                if (first < 0) first = CellToPiece[next];
                SwapCells(e, next);
                path.Add(next);
                count++;
            }
            _history.Push(path);
            Commit(new PuzzleMove(first, -1, isUndo: false, count));
            return MoveResult.Moved;
        }

        public override bool Undo()
        {
            if (_history.Count == 0 || IsSolved()) return false;
            var path = _history.Pop();
            for (int i = path.Count - 1; i > 0; i--) SwapCells(path[i], path[i - 1]);
            Commit(new PuzzleMove(CellToPiece[path[path.Count - 1]], -1, isUndo: true, path.Count - 1));
            return true;
        }

        /// <summary>Greedy hint: the single-tile move that lowers the distance estimate the most (never undoes the last move).</summary>
        public override Hint GetHint()
        {
            if (IsSolved()) return Hint.None;
            int e = EmptyCell;
            int lastGap = _history.Count > 0 && _history.Peek().Count > 1 ? _history.Peek()[_history.Peek().Count - 2] : -1;
            int bestCell = -1, bestScore = int.MaxValue;
            foreach (var cell in Neighbours(e))
            {
                if (cell == lastGap && Neighbours(e).Count > 1) continue;
                SwapCells(e, cell);
                int score = LowerBound();
                SwapCells(cell, e);
                if (score < bestScore) { bestScore = score; bestCell = cell; }
            }
            return bestCell < 0 ? Hint.None : new Hint(CellToPiece[bestCell], bestCell, e);
        }

        public override int GetParMoves() => _par;

        // ------------------------------------------------------------------ helpers

        List<int> Neighbours(int cell)
        {
            var list = new List<int>(4);
            int c = Board.Col(cell), r = Board.Row(cell);
            if (c > 0) list.Add(cell - 1);
            if (c < Board.Cols - 1) list.Add(cell + 1);
            if (r > 0) list.Add(cell - Board.Cols);
            if (r < Board.Rows - 1) list.Add(cell + Board.Cols);
            return list;
        }

        int Misplaced(int[] arrangement)
        {
            int m = 0;
            for (int i = 0; i < arrangement.Length; i++)
                if (arrangement[i] != i && arrangement[i] != _empty) m++;
            return m;
        }

        /// <summary>Manhattan distance of every tile + 2 × linear conflicts (admissible lower bound).</summary>
        public int LowerBound()
        {
            int sum = 0;
            for (int cell = 0; cell < CellToPiece.Length; cell++)
            {
                int p = CellToPiece[cell];
                if (p == _empty) continue;
                sum += Board.Manhattan(cell, p);
            }
            // Linear conflicts in rows
            for (int r = 0; r < Board.Rows; r++)
                for (int a = 0; a < Board.Cols; a++)
                {
                    int pa = CellToPiece[Board.Cell(a, r)];
                    if (pa == _empty || Board.Row(pa) != r) continue;
                    for (int b = a + 1; b < Board.Cols; b++)
                    {
                        int pb = CellToPiece[Board.Cell(b, r)];
                        if (pb != _empty && Board.Row(pb) == r && Board.Col(pb) < Board.Col(pa)) sum += 2;
                    }
                }
            // Linear conflicts in columns
            for (int c = 0; c < Board.Cols; c++)
                for (int a = 0; a < Board.Rows; a++)
                {
                    int pa = CellToPiece[Board.Cell(c, a)];
                    if (pa == _empty || Board.Col(pa) != c) continue;
                    for (int b = a + 1; b < Board.Rows; b++)
                    {
                        int pb = CellToPiece[Board.Cell(c, b)];
                        if (pb != _empty && Board.Col(pb) == c && Board.Row(pb) < Board.Row(pa)) sum += 2;
                    }
                }
            return sum;
        }
    }
}
