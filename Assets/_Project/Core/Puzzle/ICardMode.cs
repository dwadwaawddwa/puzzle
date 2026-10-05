using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Puzzle
{
    /// <summary>
    /// A mode whose cells are cards that can lie face down (Memory). The board draws the card backs, the symbols
    /// and the flips; pieces never move.
    /// </summary>
    public interface ICardMode
    {
        MemorySymbols Symbols { get; }
        /// <summary>Number of pairs on the board.</summary>
        int PairCount { get; }
        /// <summary>Pairs of cards turned over so far (= moves).</summary>
        int Attempts { get; }
        /// <summary>Two different cards are face up and wait to be turned back (<see cref="ResolveMismatch"/>).</summary>
        bool HasMismatch { get; }

        bool IsFaceUp(int cell);
        /// <summary>The card's pair was found: its piece of the picture stays visible.</summary>
        bool IsMatched(int cell);
        /// <summary>Pair index of the card on a cell, or -1 for the free card of an odd board (shown from the start).</summary>
        int SymbolOf(int cell);
        /// <summary>Turns two different cards face down again. Returns false if there were none.</summary>
        bool ResolveMismatch();
    }
}
