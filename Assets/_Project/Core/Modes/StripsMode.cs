using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine.Scripting;

namespace PuzzleStudio.Core.Modes
{
    /// <summary>
    /// The picture is cut into N strips (vertical or horizontal) in a shuffled order. Moving a strip inserts it at
    /// the target position and the others shift (like reordering a list).
    /// Par = minimum number of insertions = N − longest increasing subsequence.
    /// </summary>
    [Preserve]
    [PuzzleMode(ModeIds.Strips, Shape = GridShape.Strips, MinGrid = 1)]
    public sealed class StripsMode : PuzzleModeBase
    {
        readonly Stack<(int from, int to)> _history = new Stack<(int, int)>();
        int _par;

        public override string Id => ModeIds.Strips;
        public override bool CanUndo => _history.Count > 0;
        public override DragStyle DragStyle => DragStyle.Insert;
        protected override void ClearHistory() => _history.Clear();

        public override void Shuffle(int seed)
        {
            var rng = new SeededRandom(seed);
            var perm = ShuffleService.Permutation(Board.CellCount, rng, Settings.MinMisplacedRatio);
            _par = MinInsertions(perm);
            ApplyArrangement(perm);
        }

        public override MoveResult HandleInput(in PuzzleInput input)
        {
            int n = CellToPiece.Length;
            switch (input.Kind)
            {
                case InputKind.Cancel:
                    if (SelectedCell < 0) return MoveResult.Ignored;
                    SelectedCell = -1;
                    return MoveResult.Deselected;

                case InputKind.Tap:
                    if (input.Cell < 0 || input.Cell >= n) return MoveResult.Ignored;
                    if (SelectedCell < 0) { SelectedCell = input.Cell; return MoveResult.Selected; }
                    if (SelectedCell == input.Cell) { SelectedCell = -1; return MoveResult.Deselected; }
                    int from = SelectedCell;
                    SelectedCell = -1;
                    DoInsert(from, input.Cell);
                    return MoveResult.Moved;

                case InputKind.DragDrop:
                    if (input.Cell < 0 || input.TargetCell < 0 || input.Cell >= n || input.TargetCell >= n || input.Cell == input.TargetCell)
                        return MoveResult.Ignored;
                    SelectedCell = -1;
                    DoInsert(input.Cell, input.TargetCell);
                    return MoveResult.Moved;

                default:
                    return MoveResult.Ignored;
            }
        }

        void DoInsert(int from, int to)
        {
            int piece = CellToPiece[from];
            Insert(from, to);
            _history.Push((from, to));
            Commit(new PuzzleMove(piece, -1, isUndo: false));
        }

        /// <summary>Takes the strip at <paramref name="from"/> and inserts it at <paramref name="to"/>.</summary>
        void Insert(int from, int to)
        {
            int piece = CellToPiece[from];
            if (from < to)
                for (int c = from; c < to; c++) { CellToPiece[c] = CellToPiece[c + 1]; PiecesById[CellToPiece[c]].Cell = c; }
            else
                for (int c = from; c > to; c--) { CellToPiece[c] = CellToPiece[c - 1]; PiecesById[CellToPiece[c]].Cell = c; }
            CellToPiece[to] = piece;
            PiecesById[piece].Cell = to;
        }

        public override bool Undo()
        {
            if (_history.Count == 0 || IsSolved()) return false;
            var (from, to) = _history.Pop();
            int piece = CellToPiece[to];
            Insert(to, from);
            SelectedCell = -1;
            Commit(new PuzzleMove(piece, -1, isUndo: true));
            return true;
        }

        public override Hint GetHint()
        {
            foreach (var p in PiecesById)
                if (!p.IsCorrect) return new Hint(p.Id, p.Cell, p.Id);
            return Hint.None;
        }

        public override int GetParMoves() => _par < 1 ? 1 : _par;

        /// <summary>Display cell of a strip while another one is dragged from <paramref name="from"/> over <paramref name="to"/>.</summary>
        public static int PreviewCell(int cell, int from, int to)
        {
            if (from < to && cell > from && cell <= to) return cell - 1;
            if (from > to && cell >= to && cell < from) return cell + 1;
            return cell;
        }

        /// <summary>N − length of the longest increasing subsequence (pieces already in relative order stay).</summary>
        public static int MinInsertions(int[] arrangement)
        {
            var tails = new List<int>();
            foreach (int v in arrangement)
            {
                int lo = 0, hi = tails.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (tails[mid] < v) lo = mid + 1; else hi = mid;
                }
                if (lo == tails.Count) tails.Add(v); else tails[lo] = v;
            }
            return arrangement.Length - tails.Count;
        }
    }
}
