using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>Shared state and bookkeeping for grid-based modes (piece positions, locks, events, undo).</summary>
    public abstract class PuzzleModeBase : IPuzzleMode
    {
        protected BoardLayout Board;
        protected ModeSettings Settings;
        protected PieceState[] PiecesById = Array.Empty<PieceState>();
        protected int[] CellToPiece = Array.Empty<int>();
        bool[] _correctCache = Array.Empty<bool>();
        bool _solvedRaised;

        public abstract string Id { get; }
        public BoardLayout Layout => Board;
        public IReadOnlyList<PieceState> Pieces => PiecesById;
        public int SelectedCell { get; protected set; } = -1;
        public abstract bool CanUndo { get; }
        public virtual DragStyle DragStyle => DragStyle.Swap;

        public event Action<PuzzleMove> OnMove;
        public event Action<int> OnPieceCorrect;
        public event Action OnSolved;

        /// <summary>Whether correctly placed pieces get locked when <see cref="ModeSettings.LockCorrectPieces"/> is on.</summary>
        protected virtual bool SupportsLocking => false;

        /// <summary>Whether a piece is done (in place and upright; Memory: its pair was found). Drives OnPieceCorrect and IsSolved.</summary>
        protected virtual bool IsDone(PieceState p) => !p.IsEmpty && p.IsCorrect;

        public virtual void Setup(BoardLayout board, ModeSettings settings)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Settings = settings ?? new ModeSettings();
            int n = board.CellCount;
            PiecesById = new PieceState[n];
            CellToPiece = new int[n];
            _correctCache = new bool[n];
            for (int i = 0; i < n; i++)
            {
                PiecesById[i] = new PieceState { Id = i, Cell = i };
                CellToPiece[i] = i;
            }
            SelectedCell = -1;
            _solvedRaised = false;
            ClearHistory();
        }

        public abstract void Shuffle(int seed);
        public abstract MoveResult HandleInput(in PuzzleInput input);
        public abstract Hint GetHint();
        public abstract int GetParMoves();
        public abstract bool Undo();
        protected abstract void ClearHistory();

        public virtual void ForceSolve()
        {
            for (int i = 0; i < CellToPiece.Length; i++)
            {
                CellToPiece[i] = i;
                PiecesById[i].Cell = i;
                PiecesById[i].Rotation = 0;
            }
            SelectedCell = -1;
            ClearHistory();
            Commit(new PuzzleMove(0, -1, isUndo: false));
        }

        public virtual bool CanPick(int cell) =>
            cell >= 0 && cell < CellToPiece.Length && !PiecesById[CellToPiece[cell]].Locked && !PiecesById[CellToPiece[cell]].IsEmpty;

        public int PieceAt(int cell) => CellToPiece[cell];

        public virtual bool IsSolved()
        {
            foreach (var p in PiecesById)
                if (!p.IsEmpty && !IsDone(p)) return false;
            return true;
        }

        public int CountCorrect()
        {
            int c = 0;
            foreach (var p in PiecesById) if (!p.IsEmpty && IsDone(p)) c++;
            return c;
        }

        /// <summary>Applies an arrangement (cell → piece) produced by a shuffle and resets tracking.</summary>
        protected void ApplyArrangement(int[] cellToPiece)
        {
            for (int cell = 0; cell < cellToPiece.Length; cell++)
            {
                CellToPiece[cell] = cellToPiece[cell];
                PiecesById[cellToPiece[cell]].Cell = cell;
            }
            SelectedCell = -1;
            _solvedRaised = false;
            ClearHistory();
            RefreshLocksAndCache(raiseEvents: false);
        }

        /// <summary>Moves piece ids between cells (keeps both lookup tables in sync).</summary>
        protected void SwapCells(int cellA, int cellB)
        {
            int a = CellToPiece[cellA], b = CellToPiece[cellB];
            CellToPiece[cellA] = b;
            CellToPiece[cellB] = a;
            PiecesById[a].Cell = cellB;
            PiecesById[b].Cell = cellA;
        }

        /// <summary>Call after every state change: updates locks, raises OnPieceCorrect / OnMove / OnSolved.</summary>
        protected void Commit(PuzzleMove move)
        {
            RefreshLocksAndCache(raiseEvents: !move.IsUndo);
            OnMove?.Invoke(move);
            if (!_solvedRaised && IsSolved())
            {
                _solvedRaised = true;
                SelectedCell = -1;
                OnSolved?.Invoke();
            }
        }

        void RefreshLocksAndCache(bool raiseEvents)
        {
            bool lockOn = SupportsLocking && Settings.LockCorrectPieces;
            for (int i = 0; i < PiecesById.Length; i++)
            {
                var p = PiecesById[i];
                bool correct = !p.IsEmpty && IsDone(p);
                p.Locked = lockOn && correct;
                if (raiseEvents && correct && !_correctCache[i]) OnPieceCorrect?.Invoke(i);
                _correctCache[i] = correct;
            }
        }
    }
}
