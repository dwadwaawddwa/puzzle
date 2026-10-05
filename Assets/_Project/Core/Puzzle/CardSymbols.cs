using PuzzleStudio.Core.Data;
using UnityEngine;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>What is shown on Memory cards: numbers, letters or colored outlines, and how many pairs each style allows.</summary>
    public static class CardSymbols
    {
        /// <summary>Outline colors, as different from each other as possible (hue and lightness).</summary>
        public static readonly Color[] Colors =
        {
            Hex(0xE53935), // red
            Hex(0xFF8F00), // orange
            Hex(0xFDD835), // yellow
            Hex(0x43A047), // green
            Hex(0x00BCD4), // cyan
            Hex(0x1E63E9), // blue
            Hex(0x8E24AA), // purple
            Hex(0xF06292), // pink
            Hex(0xFFFFFF), // white
            Hex(0x1A1A1A), // black
            Hex(0x795548), // brown
            Hex(0xAEEA00), // lime
        };

        public static int MaxPairs(MemorySymbols style)
        {
            switch (style)
            {
                case MemorySymbols.Letters: return 26;
                case MemorySymbols.Colors: return Colors.Length;
                default: return 50;
            }
        }

        /// <summary>Text drawn on a card ("" for the Colors style).</summary>
        public static string Label(MemorySymbols style, int pair)
        {
            if (pair < 0) return "";
            switch (style)
            {
                case MemorySymbols.Letters: return ((char)('A' + pair % 26)).ToString();
                case MemorySymbols.Colors: return "";
                default: return (pair + 1).ToString();
            }
        }

        public static Color ColorOf(int pair) => pair < 0 ? Color.clear : Colors[pair % Colors.Length];

        /// <summary>
        /// Shrinks a grid until its cards fit the style (2 cards per pair, plus one free card when the count is odd),
        /// removing columns or rows so the cells stay as square as possible.
        /// </summary>
        public static (int cols, int rows) Fit(int cols, int rows, float aspect, int minGrid, MemorySymbols style)
        {
            int maxCells = 2 * MaxPairs(style) + 1;
            while (cols * rows > maxCells && (cols > minGrid || rows > minGrid))
            {
                // Cell width / height = aspect × rows / cols: remove a column when the cells are narrow.
                bool narrow = cols > rows * aspect;
                if ((narrow && cols > minGrid) || rows <= minGrid) cols--;
                else rows--;
            }
            return (cols, rows);
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
