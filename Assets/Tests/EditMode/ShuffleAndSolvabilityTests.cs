using NUnit.Framework;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Tests
{
    public class ShuffleAndSolvabilityTests
    {
        [Test]
        public void Permutation_IsDeterministicForSameSeed()
        {
            var a = ShuffleService.Permutation(25, new SeededRandom(42), 0.8f);
            var b = ShuffleService.Permutation(25, new SeededRandom(42), 0.8f);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Permutation_DiffersForDifferentSeeds()
        {
            var a = ShuffleService.Permutation(25, new SeededRandom(1), 0.8f);
            var b = ShuffleService.Permutation(25, new SeededRandom(2), 0.8f);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void Permutation_IsValidNeverSolvedAndMisplacedEnough(
            [Values(2, 3, 4, 9, 16, 49, 100, 144)] int n,
            [Values(0f, 0.5f, 0.8f, 1f)] float ratio)
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var p = ShuffleService.Permutation(n, new SeededRandom(seed), ratio);
                CollectionAssert.AreEquivalent(Range(n), p, "must be a permutation");
                Assert.IsFalse(ShuffleService.IsIdentity(p), "must never be solved");
                Assert.GreaterOrEqual(ShuffleService.CountMisplaced(p), ShuffleService.RequiredMisplaced(n, ratio));
            }
        }

        [Test]
        public void MinSwaps_MatchesCycleStructure()
        {
            Assert.AreEqual(0, ShuffleService.MinSwaps(new[] { 0, 1, 2, 3 }));
            Assert.AreEqual(1, ShuffleService.MinSwaps(new[] { 1, 0, 2, 3 }));
            Assert.AreEqual(2, ShuffleService.MinSwaps(new[] { 1, 2, 0, 3 }));      // one 3-cycle
            Assert.AreEqual(2, ShuffleService.MinSwaps(new[] { 1, 0, 3, 2 }));      // two 2-cycles
        }

        [Test]
        public void Sliding_SolvedStateIsSolvable()
        {
            var layout = new BoardLayout(4, 4);
            Assert.IsTrue(SolvabilityChecker.IsSlidingSolvable(Range(16), layout, 15));
        }

        [Test]
        public void Sliding_SwappingTwoTilesMakesItUnsolvable([Values(3, 4)] int size)
        {
            var layout = new BoardLayout(size, size);
            int n = size * size;
            var p = Range(n);
            (p[0], p[1]) = (p[1], p[0]);
            Assert.IsFalse(SolvabilityChecker.IsSlidingSolvable(p, layout, n - 1));
        }

        [Test]
        public void Sliding_LegalMovesKeepSolvability()
        {
            var layout = new BoardLayout(4, 3);
            int n = 12, hole = n - 1;
            var p = Range(n);
            var rng = new SeededRandom(7);
            int holeCell = hole;
            for (int i = 0; i < 500; i++)
            {
                int col = layout.Col(holeCell), row = layout.Row(holeCell);
                int dir = rng.Next(4);
                int nc = col + (dir == 0 ? 1 : dir == 1 ? -1 : 0);
                int nr = row + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                if (!layout.InBounds(nc, nr)) continue;
                int target = layout.Cell(nc, nr);
                (p[holeCell], p[target]) = (p[target], p[holeCell]);
                holeCell = target;
                Assert.IsTrue(SolvabilityChecker.IsSlidingSolvable(p, layout, hole));
            }
        }

        static int[] Range(int n)
        {
            var r = new int[n];
            for (int i = 0; i < n; i++) r[i] = i;
            return r;
        }
    }
}
