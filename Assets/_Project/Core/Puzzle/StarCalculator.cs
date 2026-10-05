using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Puzzle
{
    public static class StarCalculator
    {
        /// <returns>1 to 3 stars.</returns>
        public static int Compute(int moves, int par, int hintsUsed, StarRules rules)
        {
            rules ??= new StarRules();
            if (par < 1) par = 1;
            int stars;
            if (moves <= par * rules.threeStarsMoveFactor) stars = 3;
            else if (moves <= par * rules.twoStarsMoveFactor) stars = 2;
            else stars = 1;
            if (rules.hintCostsStar && hintsUsed > 0 && stars > 2) stars = 2;
            return stars;
        }
    }
}
