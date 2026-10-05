using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Modes;
using PuzzleStudio.Core.Puzzle;

namespace PuzzleStudio.Tests
{
    public class ModesTests
    {
        static T Make<T>(int cols, int rows, int seed = 5, bool lockCorrect = true) where T : IPuzzleMode, new()
        {
            var m = new T();
            m.Setup(new BoardLayout(cols, rows), new ModeSettings { LockCorrectPieces = lockCorrect, MinMisplacedRatio = 0.8f });
            m.Shuffle(seed);
            return m;
        }

        static int[] Arrangement(IPuzzleMode m) => Enumerable.Range(0, m.Layout.CellCount).Select(m.PieceAt).ToArray();

        [Test]
        public void Registry_HasAllFourModes()
        {
            foreach (var id in new[] { ModeIds.SwapTiles, ModeIds.Strips, ModeIds.Sliding, ModeIds.Rotate })
                Assert.IsTrue(PuzzleModeRegistry.IsRegistered(id), id);
            Assert.IsTrue(PuzzleModeRegistry.InfoOf(ModeIds.Rotate).SquareCells);
            Assert.AreEqual(GridShape.Strips, PuzzleModeRegistry.InfoOf(ModeIds.Strips).Shape);
        }

        // ---------------------------------------------------------------- strips

        [Test]
        public void Strips_InsertShiftsTheOthers()
        {
            var m = new StripsMode();
            m.Setup(new BoardLayout(5, 1), new ModeSettings());
            // Identity, then move strip at 0 to 3: [1,2,3,0,4]
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Drop(0, 3)));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0, 4 }, Arrangement(m));
            foreach (var p in m.Pieces) Assert.AreEqual(p.Id, m.PieceAt(p.Cell), "lookup tables in sync");
            Assert.IsTrue(m.Undo());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, Arrangement(m));
        }

        [Test]
        public void Strips_ParIsMinimumInsertionsAndHintsSolveWithinPar([Values(4, 8, 12)] int n)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var m = Make<StripsMode>(n, 1, seed);
                Assert.IsFalse(m.IsSolved());
                Assert.AreEqual(StripsMode.MinInsertions(Arrangement(m)), m.GetParMoves());
                int moves = 0;
                while (!m.IsSolved() && moves < 100)
                {
                    var h = m.GetHint();
                    m.HandleInput(PuzzleInput.Drop(h.FromCell, h.ToCell));
                    moves++;
                }
                Assert.IsTrue(m.IsSolved());
                Assert.LessOrEqual(moves, n - 1, "hint strategy needs at most n-1 insertions");
            }
        }

        [Test]
        public void Strips_PreviewCellMatchesRealInsert()
        {
            int n = 7;
            for (int from = 0; from < n; from++)
                for (int to = 0; to < n; to++)
                {
                    if (from == to) continue;
                    var m = new StripsMode();
                    m.Setup(new BoardLayout(n, 1), new ModeSettings());
                    var before = m.Pieces.Select(p => p.Cell).ToArray();
                    m.HandleInput(PuzzleInput.Drop(from, to));
                    for (int id = 0; id < n; id++)
                    {
                        if (before[id] == from) continue;
                        Assert.AreEqual(m.Pieces[id].Cell, StripsMode.PreviewCell(before[id], from, to), $"{from}->{to} piece {id}");
                    }
                }
        }

        // ---------------------------------------------------------------- sliding

        [Test]
        public void Sliding_ShuffleIsSolvableAndNotSolved([Values(2, 3, 4, 5)] int size)
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var m = Make<SlidingMode>(size, size, seed);
                Assert.IsFalse(m.IsSolved());
                Assert.IsTrue(SolvabilityChecker.IsSlidingSolvable(Arrangement(m), m.Layout, size * size - 1));
                Assert.IsTrue(m.Pieces[size * size - 1].IsEmpty);
                Assert.GreaterOrEqual(m.GetParMoves(), 1);
            }
        }

        [Test]
        public void Sliding_3x2_BreadthFirstSearchFindsASolution()
        {
            var m = Make<SlidingMode>(3, 2, 11);
            // BFS over single-tile moves through the public API: proves the shuffled state is reachable/solvable.
            var start = string.Join(",", Arrangement(m));
            var seen = new HashSet<string> { start };
            var queue = new Queue<int[]>();
            queue.Enqueue(Arrangement(m));
            bool solved = false;
            var layout = m.Layout;
            while (queue.Count > 0 && !solved)
            {
                var a = queue.Dequeue();
                int gap = System.Array.IndexOf(a, 5);
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int c = layout.Col(gap) + dc, r = layout.Row(gap) + dr;
                    if (!layout.InBounds(c, r)) continue;
                    var b = (int[])a.Clone();
                    int other = layout.Cell(c, r);
                    (b[gap], b[other]) = (b[other], b[gap]);
                    if (b.SequenceEqual(Enumerable.Range(0, 6))) { solved = true; break; }
                    if (seen.Add(string.Join(",", b))) queue.Enqueue(b);
                }
            }
            Assert.IsTrue(solved);
        }

        [Test]
        public void Sliding_TapSlidesWholeRowAndUndoRestores()
        {
            var m = new SlidingMode();
            m.Setup(new BoardLayout(4, 4), new ModeSettings());
            // Solved board, gap at 15 (bottom-right). Tap cell 12 (bottom-left): 3 tiles slide right.
            int moves = 0;
            m.OnMove += mv => { if (!mv.IsUndo) moves += mv.Count; };
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Tap(12)));
            Assert.AreEqual(3, moves);
            Assert.AreEqual(12, m.EmptyCell);
            CollectionAssert.AreEqual(new[] { 15, 12, 13, 14 }, new[] { m.PieceAt(12), m.PieceAt(13), m.PieceAt(14), m.PieceAt(15) });
            Assert.AreEqual(MoveResult.Blocked, m.HandleInput(PuzzleInput.Tap(5)), "not in line with the gap");
            Assert.IsTrue(m.Undo());
            Assert.IsTrue(m.IsSolved());
        }

        [Test]
        public void Sliding_ArrowMovesTileIntoGap()
        {
            var m = new SlidingMode();
            m.Setup(new BoardLayout(3, 3), new ModeSettings());
            // Gap at 8. "Right" = the tile on the left of the gap (cell 7) moves right.
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Direction(1, 0)));
            Assert.AreEqual(7, m.EmptyCell);
            Assert.AreEqual(MoveResult.Blocked, m.HandleInput(PuzzleInput.Direction(0, -1)), "nothing below the gap");
        }

        [Test]
        public void Sliding_HintReducesTheDistanceOnAverage()
        {
            var m = Make<SlidingMode>(3, 3, 4);
            int start = m.LowerBound();
            for (int i = 0; i < 40 && !m.IsSolved(); i++)
            {
                var h = m.GetHint();
                Assert.IsTrue(h.IsValid);
                Assert.AreEqual(m.EmptyCell, h.ToCell);
                m.HandleInput(PuzzleInput.Tap(h.FromCell));
            }
            Assert.Less(m.LowerBound(), start + 1);
        }

        // ---------------------------------------------------------------- rotate

        [Test]
        public void Rotate_ShuffleTurnsEnoughTilesAndParIsMinimalClicks()
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var m = Make<RotateMode>(4, 4, seed);
                Assert.IsFalse(m.IsSolved());
                int turned = m.Pieces.Count(p => p.Rotation != 0);
                Assert.GreaterOrEqual(turned, ShuffleService.RequiredMisplaced(16, 0.8f));
                int par = m.Pieces.Sum(p => p.Rotation == 3 ? 1 : p.Rotation);
                Assert.AreEqual(par, m.GetParMoves());
                foreach (var p in m.Pieces) Assert.AreEqual(p.Id, p.Cell, "tiles never move");
            }
        }

        [Test]
        public void Rotate_ClicksTurnAndLockAndUndo()
        {
            var m = Make<RotateMode>(3, 3, 2);
            var p = m.Pieces.First(x => x.Rotation == 1 || x.Rotation == 3);
            int before = p.Rotation;
            // Turn toward 0 by the shortest way.
            var input = before == 3 ? PuzzleInput.Tap(p.Cell) : PuzzleInput.RotateCcw(p.Cell);
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(input));
            Assert.AreEqual(0, p.Rotation);
            Assert.IsTrue(p.Locked);
            Assert.AreEqual(MoveResult.Blocked, m.HandleInput(PuzzleInput.Tap(p.Cell)));
            Assert.IsTrue(m.Undo());
            Assert.AreEqual(before, p.Rotation);
            Assert.IsFalse(p.Locked);
        }

        [Test]
        public void Rotate_SolvingWithParClicks()
        {
            var m = Make<RotateMode>(5, 5, 9);
            int clicks = 0;
            foreach (var p in m.Pieces.ToList())
            {
                while (p.Rotation != 0)
                {
                    m.HandleInput(p.Rotation == 3 ? PuzzleInput.Tap(p.Cell) : PuzzleInput.RotateCcw(p.Cell));
                    clicks++;
                }
            }
            Assert.IsTrue(m.IsSolved());
            Assert.AreEqual(m.GetParMoves(), clicks);
        }

        // ---------------------------------------------------------------- grid resolver per mode

        [Test]
        public void GridResolver_RespectsModeShapes()
        {
            var pack = new GamePackData();
            pack.gameplay.difficultyCurve = DifficultyCurve.Fixed;
            pack.gameplay.fixedGrid = 9;
            pack.gameplay.stripsCount = new IntRange { min = 7, max = 7 };
            pack.levels.Add(new LevelConfig { id = "strips", mode = ModeIds.Strips });
            pack.levels.Add(new LevelConfig { id = "slide", mode = ModeIds.Sliding });
            pack.levels.Add(new LevelConfig { id = "rot", mode = ModeIds.Rotate });

            var strips = GridResolver.Resolve(pack, 0, 1920, 1080);
            Assert.AreEqual((7, 1), (strips.Layout.Cols, strips.Layout.Rows));
            pack.gameplay.stripsOrientation = StripsOrientation.Horizontal;
            strips = GridResolver.Resolve(pack, 0, 1920, 1080);
            Assert.AreEqual((1, 7), (strips.Layout.Cols, strips.Layout.Rows));

            var slide = GridResolver.Resolve(pack, 1, 1000, 1000);
            Assert.LessOrEqual(slide.Layout.Cols, 6, "sliding is capped at 6");

            var rot = GridResolver.Resolve(pack, 2, 1920, 1080);
            float cellW = 1920 * rot.Layout.Crop.w / rot.Layout.Cols, cellH = 1080 * rot.Layout.Crop.h / rot.Layout.Rows;
            Assert.AreEqual(cellW, cellH, 0.5f, "rotate uses square cells");
        }
    }
}
