using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine.Scripting;

namespace PuzzleStudio.Core.Modes
{
    /// <summary>
    /// Memory (pairs): every cell is a card lying face down. A turned card shows its own piece of the picture and a
    /// symbol (number, letter or colored outline); each symbol is on exactly two cards. Two equal symbols stay face up
    /// and their two pieces of the picture are done; two different ones are turned back (after a short delay, or at
    /// the next tap). The level is won when the whole picture is uncovered. One attempt (two cards) = one move.
    /// An odd board has one free card in the middle, face up from the start.
    /// </summary>
    [Preserve]
    [PuzzleMode(ModeIds.Memory, Shape = GridShape.Grid, MinGrid = 2, MaxGrid = 10)]
    public sealed class MemoryMode : PuzzleModeBase, ICardMode
    {
        int[] _symbol = Array.Empty<int>();
        bool[] _matched = Array.Empty<bool>();
        int _first = -1, _second = -1;

        public override string Id => ModeIds.Memory;
        public override bool CanUndo => false;
        public override DragStyle DragStyle => DragStyle.None;
        protected override void ClearHistory() { }

        public MemorySymbols Symbols => Settings.MemorySymbols;
        public int PairCount { get; private set; }
        public int Attempts { get; private set; }
        public bool HasMismatch => _second >= 0;
        /// <summary>The free card of an odd board, or -1.</summary>
        public int FreeCell { get; private set; } = -1;

        public bool IsFaceUp(int cell) => InRange(cell) && (_matched[cell] || cell == _first || cell == _second);
        public bool IsMatched(int cell) => InRange(cell) && _matched[cell];
        public int SymbolOf(int cell) => InRange(cell) ? _symbol[cell] : -1;

        bool InRange(int cell) => cell >= 0 && cell < _symbol.Length;

        public override void Setup(BoardLayout board, ModeSettings settings)
        {
            base.Setup(board, settings);
            _symbol = new int[board.CellCount];
            _matched = new bool[board.CellCount];
            _first = _second = -1;
            PairCount = board.CellCount / 2;
            Attempts = 0;
        }

        public override void Shuffle(int seed)
        {
            int n = Board.CellCount;
            FreeCell = n % 2 == 1 ? Board.Cell(Board.Cols / 2, Board.Rows / 2) : -1;
            PairCount = n / 2;

            // Pairs 0, 0, 1, 1, 2, 2… dealt in random order on the cells (the free card excepted).
            var order = ShuffleService.Permutation(PairCount * 2, new SeededRandom(seed), 0f);
            for (int cell = 0, k = 0; cell < n; cell++)
            {
                bool free = cell == FreeCell;
                _symbol[cell] = free ? -1 : order[k++] / 2;
                _matched[cell] = free;
            }
            _first = _second = -1;
            Attempts = 0;

            // Pieces never move: each card shows the piece of the picture that belongs to its own cell.
            var identity = new int[n];
            for (int i = 0; i < n; i++) identity[i] = i;
            ApplyArrangement(identity);
        }

        protected override bool IsDone(PieceState p) => _matched.Length > p.Cell && _matched[p.Cell];

        public override bool CanPick(int cell) => InRange(cell) && !_matched[cell] && cell != _first && cell != _second;

        public override MoveResult HandleInput(in PuzzleInput input)
        {
            switch (input.Kind)
            {
                case InputKind.Tap:
                case InputKind.RotateCw:
                    return Flip(input.Cell);
                default:
                    return MoveResult.Ignored;
            }
        }

        MoveResult Flip(int cell)
        {
            if (!InRange(cell)) return MoveResult.Ignored;
            if (HasMismatch)
            {
                // Playing on: the two wrong cards are turned back first.
                bool wasOpen = cell == _first || cell == _second;
                ResolveMismatch();
                if (wasOpen) return MoveResult.Moved;
            }
            if (_matched[cell] || cell == _first) return MoveResult.Ignored;

            if (_first < 0)
            {
                _first = cell;
                Commit(new PuzzleMove(cell, -1, isUndo: false, count: 0));
                return MoveResult.Moved;
            }

            int other = _first;
            Attempts++;
            if (_symbol[other] == _symbol[cell])
            {
                _matched[other] = _matched[cell] = true;
                _first = -1;
            }
            else
            {
                _second = cell;
            }
            Commit(new PuzzleMove(cell, other, isUndo: false, count: 1));
            return MoveResult.Moved;
        }

        public bool ResolveMismatch()
        {
            if (_second < 0) return false;
            int a = _first, b = _second;
            _first = _second = -1;
            Commit(new PuzzleMove(a, b, isUndo: false, count: 0));
            return true;
        }

        public override bool Undo() => false;

        public override void ForceSolve()
        {
            for (int i = 0; i < _matched.Length; i++) _matched[i] = true;
            _first = _second = -1;
            Commit(new PuzzleMove(0, -1, isUndo: false));
        }

        /// <summary>Shows a matching pair: the partner of the card face up, or else any pair not found yet.</summary>
        public override Hint GetHint()
        {
            if (_first >= 0 && !HasMismatch)
            {
                int partner = PartnerOf(_first);
                if (partner >= 0) return new Hint(_first, _first, partner);
            }
            for (int cell = 0; cell < _symbol.Length; cell++)
            {
                if (_matched[cell]) continue;
                int partner = PartnerOf(cell);
                if (partner >= 0) return new Hint(cell, cell, partner);
            }
            return Hint.None;
        }

        public int PartnerOf(int cell)
        {
            if (!InRange(cell) || _symbol[cell] < 0) return -1;
            for (int i = 0; i < _symbol.Length; i++)
                if (i != cell && _symbol[i] == _symbol[cell]) return i;
            return -1;
        }

        /// <summary>
        /// About what a player with a perfect memory needs: ≈ 1.6 attempts per pair (each new card has to be seen once,
        /// lucky matches make up for some of it).
        /// </summary>
        public override int GetParMoves() => Math.Max(1, (int)Math.Ceiling(PairCount * 1.6));
    }
}
