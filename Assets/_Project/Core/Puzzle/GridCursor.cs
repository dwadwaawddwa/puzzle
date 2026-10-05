using System;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>The board cursor used with a gamepad or the keyboard: one cell, moved by directions, never leaves the grid.</summary>
    public static class GridCursor
    {
        public static int Center(BoardLayout layout) => layout.Cell((layout.Cols - 1) / 2, (layout.Rows - 1) / 2);

        /// <param name="dx">-1 left, +1 right.</param>
        /// <param name="dy">-1 up, +1 down (screen direction).</param>
        public static int Move(int cell, int dx, int dy, BoardLayout layout)
        {
            if (cell < 0 || cell >= layout.CellCount) return Center(layout);
            int col = Math.Max(0, Math.Min(layout.Cols - 1, layout.Col(cell) + Math.Sign(dx)));
            int row = Math.Max(0, Math.Min(layout.Rows - 1, layout.Row(cell) + Math.Sign(dy)));
            return layout.Cell(col, row);
        }
    }
}
