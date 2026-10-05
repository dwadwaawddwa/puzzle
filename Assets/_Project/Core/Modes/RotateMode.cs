using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine.Scripting;

namespace PuzzleStudio.Core.Modes
{
    /// <summary>
    /// Every tile is in its place but turned by 90°, 180° or 270°. Tap (left click) turns clockwise,
    /// right click counter-clockwise. Cells are made square by the grid resolver.
    /// Par = Σ min(clockwise turns, counter-clockwise turns).
    /// </summary>
    [Preserve]
    [PuzzleMode(ModeIds.Rotate, Shape = GridShape.Grid, SquareCells = true, MinGrid = 2, MaxGrid = 10)]
    public sealed class RotateMode : PuzzleModeBase
    {
        readonly Stack<(int cell, int delta)> _history = new Stack<(int, int)>();
        int _par;

        public override string Id => ModeIds.Rotate;
        public override bool CanUndo => _history.Count > 0;
        public override DragStyle DragStyle => DragStyle.None;
        protected override bool SupportsLocking => true;
        protected override void ClearHistory() => _history.Clear();

        public override void Shuffle(int seed)
        {
            var rng = new SeededRandom(seed);
            int n = Board.CellCount;
            var allowed = new List<int>();
            foreach (int deg in Settings.RotateSteps ?? new List<int> { 90, 180, 270 })
            {
                int q = ((deg / 90) % 4 + 4) % 4;
                if (q != 0 && !allowed.Contains(q)) allowed.Add(q);
            }
            if (allowed.Count == 0) allowed.AddRange(new[] { 1, 2, 3 });

            int required = ShuffleService.RequiredMisplaced(n, Settings.MinMisplacedRatio);
            int turned = required + rng.Next(n - required + 1);
            var order = ShuffleService.Permutation(n, rng, 0f);

            var identity = new int[n];
            for (int i = 0; i < n; i++) identity[i] = i;
            ApplyArrangement(identity);
            _par = 0;
            for (int i = 0; i < n; i++)
            {
                int q = i < turned ? allowed[rng.Next(allowed.Count)] : 0;
                PiecesById[order[i]].Rotation = q;
                _par += q == 3 ? 1 : q;
            }
            ApplyArrangement(identity); // refreshes locks / correct cache with the new rotations
        }

        public override MoveResult HandleInput(in PuzzleInput input)
        {
            switch (input.Kind)
            {
                case InputKind.Tap:
                case InputKind.RotateCw:
                    return Turn(input.Cell, +1);
                case InputKind.RotateCcw:
                    return Turn(input.Cell, -1);
                default:
                    return MoveResult.Ignored;
            }
        }

        MoveResult Turn(int cell, int delta)
        {
            if (cell < 0 || cell >= CellToPiece.Length) return MoveResult.Ignored;
            if (!CanPick(cell)) return MoveResult.Blocked;
            var piece = PiecesById[CellToPiece[cell]];
            piece.Rotation = (piece.Rotation + delta + 4) % 4;
            _history.Push((cell, delta));
            Commit(new PuzzleMove(piece.Id, -1, isUndo: false));
            return MoveResult.Moved;
        }

        public override bool Undo()
        {
            if (_history.Count == 0 || IsSolved()) return false;
            var (cell, delta) = _history.Pop();
            var piece = PiecesById[CellToPiece[cell]];
            piece.Rotation = (piece.Rotation - delta + 4) % 4;
            Commit(new PuzzleMove(piece.Id, -1, isUndo: true));
            return true;
        }

        public override Hint GetHint()
        {
            foreach (var p in PiecesById)
                if (p.Rotation != 0) return new Hint(p.Id, p.Cell, p.Cell);
            return Hint.None;
        }

        public override int GetParMoves() => _par < 1 ? 1 : _par;
    }
}
