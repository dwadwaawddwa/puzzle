namespace PuzzleStudio.Core.Puzzle
{
    public static class SolvabilityChecker
    {
        /// <summary>
        /// Sliding puzzle solvability for any grid ≥ 2×2 and any hole position.
        /// Each slide is one transposition (flips permutation parity) and moves the hole by one cell
        /// (flips the parity of its distance to home). So a state is solvable iff both parities match.
        /// </summary>
        /// <param name="cellToPiece">piece id on each cell (piece id = home cell).</param>
        /// <param name="emptyPiece">id of the hole piece.</param>
        public static bool IsSlidingSolvable(int[] cellToPiece, BoardLayout layout, int emptyPiece)
        {
            int holeCell = -1;
            for (int i = 0; i < cellToPiece.Length; i++)
                if (cellToPiece[i] == emptyPiece) { holeCell = i; break; }
            if (holeCell < 0) return false;

            int permParity = ShuffleService.Parity(cellToPiece);
            int distParity = layout.Manhattan(holeCell, emptyPiece) & 1;
            return permParity == distParity;
        }
    }
}
