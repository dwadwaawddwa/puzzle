using System.Linq;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Modes;
using PuzzleStudio.Core.Puzzle;

namespace PuzzleStudio.Tests
{
    public class SwapTilesModeTests
    {
        static SwapTilesMode Create(int cols, int rows, int seed = 123, bool lockCorrect = true)
        {
            var mode = new SwapTilesMode();
            mode.Setup(new BoardLayout(cols, rows), new ModeSettings { LockCorrectPieces = lockCorrect, MinMisplacedRatio = 0.8f });
            mode.Shuffle(seed);
            return mode;
        }

        /// <summary>Greedy optimal solve: put the right piece on each wrong cell (n − cycles swaps).</summary>
        static int Solve(IPuzzleMode mode)
        {
            int moves = 0;
            for (int cell = 0; cell < mode.Layout.CellCount; cell++)
            {
                if (mode.PieceAt(cell) == cell) continue;
                int from = mode.Pieces[cell].Cell;
                Assert.AreEqual(MoveResult.Moved, mode.HandleInput(PuzzleInput.Drop(from, cell)));
                moves++;
            }
            return moves;
        }

        [Test]
        public void Registry_FindsSwapTiles()
        {
            Assert.IsTrue(PuzzleModeRegistry.IsRegistered(ModeIds.SwapTiles));
            Assert.IsInstanceOf<SwapTilesMode>(PuzzleModeRegistry.Create(ModeIds.SwapTiles));
        }

        [Test]
        public void Shuffle_IsNotSolved([Values(2, 3, 5, 10)] int size)
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.IsFalse(Create(size, size, seed).IsSolved());
        }

        [Test]
        public void Solving_RaisesOnSolvedOnce_AndTakesExactlyPar()
        {
            var mode = Create(4, 3);
            int solvedCount = 0, moveEvents = 0;
            mode.OnSolved += () => solvedCount++;
            mode.OnMove += _ => moveEvents++;
            int par = mode.GetParMoves();

            int moves = Solve(mode);

            Assert.IsTrue(mode.IsSolved());
            Assert.AreEqual(1, solvedCount);
            Assert.AreEqual(par, moves, "optimal solution length must equal par");
            Assert.AreEqual(moves, moveEvents);
        }

        [Test]
        public void TapTap_SelectsThenSwaps()
        {
            var mode = Create(3, 3, lockCorrect: false);
            int a = Enumerable.Range(0, 9).First(c => mode.PieceAt(c) != c);
            int b = mode.Pieces[a].Cell; // cell where piece "a" sits

            Assert.AreEqual(MoveResult.Selected, mode.HandleInput(PuzzleInput.Tap(a)));
            Assert.AreEqual(a, mode.SelectedCell);
            Assert.AreEqual(MoveResult.Moved, mode.HandleInput(PuzzleInput.Tap(b)));
            Assert.AreEqual(-1, mode.SelectedCell);
            Assert.AreEqual(a, mode.PieceAt(a), "piece a is now home");
        }

        [Test]
        public void TapSameCell_Deselects()
        {
            var mode = Create(3, 3);
            int a = Enumerable.Range(0, 9).First(c => mode.CanPick(c));
            mode.HandleInput(PuzzleInput.Tap(a));
            Assert.AreEqual(MoveResult.Deselected, mode.HandleInput(PuzzleInput.Tap(a)));
            Assert.AreEqual(-1, mode.SelectedCell);
        }

        [Test]
        public void CorrectPieces_LockAndRaiseEvent()
        {
            var mode = Create(3, 3, lockCorrect: true);
            int correctEvents = 0;
            mode.OnPieceCorrect += _ => correctEvents++;

            int target = Enumerable.Range(0, 9).First(c => mode.PieceAt(c) != c);
            int from = mode.Pieces[target].Cell;
            mode.HandleInput(PuzzleInput.Drop(from, target));

            Assert.IsTrue(mode.Pieces[target].Locked);
            Assert.IsFalse(mode.CanPick(target));
            Assert.GreaterOrEqual(correctEvents, 1);
            int other = Enumerable.Range(0, 9).First(c => c != target && mode.CanPick(c));
            Assert.AreEqual(MoveResult.Blocked, mode.HandleInput(PuzzleInput.Drop(target, other)));
        }

        [Test]
        public void Undo_RestoresPreviousArrangement()
        {
            var mode = Create(4, 4, lockCorrect: false);
            var before = Enumerable.Range(0, 16).Select(mode.PieceAt).ToArray();
            Assert.IsFalse(mode.CanUndo);

            mode.HandleInput(PuzzleInput.Drop(0, 5));
            mode.HandleInput(PuzzleInput.Drop(3, 9));
            Assert.IsTrue(mode.CanUndo);

            Assert.IsTrue(mode.Undo());
            Assert.IsTrue(mode.Undo());
            Assert.IsFalse(mode.Undo());
            CollectionAssert.AreEqual(before, Enumerable.Range(0, 16).Select(mode.PieceAt).ToArray());
        }

        [Test]
        public void Hint_PointsMisplacedPieceToItsHome()
        {
            var mode = Create(4, 4);
            var hint = mode.GetHint();
            Assert.IsTrue(hint.IsValid);
            Assert.AreEqual(hint.PieceId, mode.PieceAt(hint.FromCell));
            Assert.AreEqual(hint.PieceId, hint.ToCell);
            Assert.AreNotEqual(hint.FromCell, hint.ToCell);

            Solve(mode);
            Assert.IsFalse(mode.GetHint().IsValid);
        }

        [Test]
        public void SameSeed_SameShuffle()
        {
            var a = Create(5, 4, seed: 99);
            var b = Create(5, 4, seed: 99);
            for (int c = 0; c < 20; c++) Assert.AreEqual(a.PieceAt(c), b.PieceAt(c));
        }

        [Test]
        public void Stars_FollowRules()
        {
            var rules = new StarRules { threeStarsMoveFactor = 1.2f, twoStarsMoveFactor = 2f, hintCostsStar = true };
            Assert.AreEqual(3, StarCalculator.Compute(10, 10, 0, rules));
            Assert.AreEqual(3, StarCalculator.Compute(12, 10, 0, rules));
            Assert.AreEqual(2, StarCalculator.Compute(13, 10, 0, rules));
            Assert.AreEqual(2, StarCalculator.Compute(20, 10, 0, rules));
            Assert.AreEqual(1, StarCalculator.Compute(21, 10, 0, rules));
            Assert.AreEqual(2, StarCalculator.Compute(10, 10, 1, rules), "a hint caps at 2 stars");
        }
    }
}
