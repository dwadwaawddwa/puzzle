using System;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Core.Puzzle
{
    public static class ShuffleService
    {
        /// <summary>Number of misplaced entries required for a given ratio (always ≥ 2 so a board is never solved).</summary>
        public static int RequiredMisplaced(int n, float ratio)
        {
            if (n < 2) return 0;
            int req = (int)Math.Ceiling(n * Math.Max(0f, Math.Min(1f, ratio)));
            return Math.Max(2, Math.Min(n, req));
        }

        /// <summary>
        /// Random permutation (cell → piece) with at least <see cref="RequiredMisplaced"/> misplaced pieces.
        /// Swapping a fixed point with any other entry never creates a new fixed point, so this always terminates.
        /// </summary>
        public static int[] Permutation(int n, SeededRandom rng, float minMisplacedRatio)
        {
            var p = new int[n];
            for (int i = 0; i < n; i++) p[i] = i;
            if (n < 2) return p;

            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            int required = RequiredMisplaced(n, minMisplacedRatio);
            while (CountMisplaced(p) < required)
            {
                int fixedIdx = RandomFixedPoint(p, rng);
                int other = rng.Next(n - 1);
                if (other >= fixedIdx) other++;
                (p[fixedIdx], p[other]) = (p[other], p[fixedIdx]);
            }
            return p;
        }

        public static int CountMisplaced(int[] p)
        {
            int c = 0;
            for (int i = 0; i < p.Length; i++) if (p[i] != i) c++;
            return c;
        }

        public static bool IsIdentity(int[] p) => CountMisplaced(p) == 0;

        /// <summary>Number of cycles of a permutation (fixed points count as cycles).</summary>
        public static int CycleCount(int[] p)
        {
            var seen = new bool[p.Length];
            int cycles = 0;
            for (int i = 0; i < p.Length; i++)
            {
                if (seen[i]) continue;
                cycles++;
                for (int j = i; !seen[j]; j = p[j]) seen[j] = true;
            }
            return cycles;
        }

        /// <summary>Minimum number of swaps to sort a permutation.</summary>
        public static int MinSwaps(int[] p) => p.Length - CycleCount(p);

        /// <summary>0 = even permutation, 1 = odd.</summary>
        public static int Parity(int[] p) => MinSwaps(p) & 1;

        static int RandomFixedPoint(int[] p, SeededRandom rng)
        {
            int count = p.Length - CountMisplaced(p);
            int pick = rng.Next(count);
            for (int i = 0; i < p.Length; i++)
                if (p[i] == i && pick-- == 0) return i;
            return 0;
        }
    }
}
