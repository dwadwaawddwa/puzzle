using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Modes;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Tests
{
    public class MemoryModeTests
    {
        static MemoryMode Make(int cols, int rows, int seed = 3, MemorySymbols symbols = MemorySymbols.Numbers)
        {
            var m = new MemoryMode();
            m.Setup(new BoardLayout(cols, rows), new ModeSettings { ModeId = ModeIds.Memory, MemorySymbols = symbols });
            m.Shuffle(seed);
            return m;
        }

        /// <summary>Two cells with different symbols (neither matched).</summary>
        static (int a, int b) Mismatch(MemoryMode m)
        {
            int n = m.Layout.CellCount;
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    if (!m.IsMatched(a) && !m.IsMatched(b) && m.SymbolOf(a) != m.SymbolOf(b)) return (a, b);
            Assert.Fail("no mismatching cards");
            return (-1, -1);
        }

        [Test]
        public void Registry_KnowsMemory()
        {
            Assert.IsTrue(PuzzleModeRegistry.IsRegistered(ModeIds.Memory));
            Assert.IsInstanceOf<MemoryMode>(PuzzleModeRegistry.Create(ModeIds.Memory));
            Assert.IsInstanceOf<ICardMode>(PuzzleModeRegistry.Create(ModeIds.Memory));
            Assert.AreEqual(DragStyle.None, PuzzleModeRegistry.Create(ModeIds.Memory).DragStyle);
        }

        [Test]
        public void Shuffle_DealsEachSymbolOnExactlyTwoCards([Values(2, 3, 4, 5, 7)] int size)
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var m = Make(size, size + 1, seed);
                int n = size * (size + 1);
                Assert.AreEqual(n / 2, m.PairCount);
                var counts = Enumerable.Range(0, n).Select(m.SymbolOf).Where(s => s >= 0)
                    .GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
                Assert.AreEqual(m.PairCount, counts.Count);
                Assert.IsTrue(counts.Values.All(c => c == 2), "every symbol is on two cards");
                Assert.IsTrue(counts.Keys.All(k => k >= 0 && k < m.PairCount));
                for (int c = 0; c < n; c++)
                {
                    Assert.IsFalse(m.IsFaceUp(c), "all cards start face down");
                    Assert.AreEqual(c, m.PieceAt(c), "each card shows the piece of its own cell");
                }
                Assert.IsFalse(m.IsSolved());
            }
        }

        [Test]
        public void Shuffle_IsDeterministicAndDependsOnTheSeed()
        {
            var a = Enumerable.Range(0, 20).Select(Make(5, 4, 11).SymbolOf).ToArray();
            var b = Enumerable.Range(0, 20).Select(Make(5, 4, 11).SymbolOf).ToArray();
            var c = Enumerable.Range(0, 20).Select(Make(5, 4, 12).SymbolOf).ToArray();
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void OddBoard_HasAFreeCardInTheMiddle()
        {
            var m = Make(3, 3);
            Assert.AreEqual(4, m.FreeCell);
            Assert.AreEqual(-1, m.SymbolOf(4));
            Assert.IsTrue(m.IsMatched(4) && m.IsFaceUp(4), "the free card is shown from the start");
            Assert.AreEqual(4, m.PairCount);
            Assert.IsFalse(m.CanPick(4));
            Assert.AreEqual(-1, Make(4, 3).FreeCell);
        }

        [Test]
        public void MatchingPair_StaysFaceUp_AndCountsOneMove()
        {
            var m = Make(4, 4);
            var moves = new List<PuzzleMove>();
            var correct = new List<int>();
            m.OnMove += moves.Add;
            m.OnPieceCorrect += correct.Add;

            int a = 0, b = m.PartnerOf(0);
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Tap(a)));
            Assert.IsTrue(m.IsFaceUp(a));
            Assert.AreEqual(0, moves[0].Count, "the first card of a pair is not a move");
            Assert.AreEqual(MoveResult.Ignored, m.HandleInput(PuzzleInput.Tap(a)), "tapping the open card again does nothing");

            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Tap(b)));
            Assert.AreEqual(1, moves[1].Count);
            Assert.IsTrue(m.IsMatched(a) && m.IsMatched(b));
            Assert.IsFalse(m.HasMismatch);
            CollectionAssert.AreEquivalent(new[] { a, b }, correct, "both pieces of the picture are done");
            Assert.AreEqual(1, m.Attempts);
            Assert.AreEqual(2, m.CountCorrect());
            Assert.IsFalse(m.CanPick(a));
            Assert.AreEqual(MoveResult.Ignored, m.HandleInput(PuzzleInput.Tap(b)), "found cards stay");
        }

        [Test]
        public void DifferentCards_AreTurnedBack()
        {
            var m = Make(4, 4);
            var (a, b) = Mismatch(m);
            m.HandleInput(PuzzleInput.Tap(a));
            m.HandleInput(PuzzleInput.Tap(b));
            Assert.IsTrue(m.HasMismatch);
            Assert.IsTrue(m.IsFaceUp(a) && m.IsFaceUp(b), "both stay visible until turned back");
            Assert.IsFalse(m.IsMatched(a) || m.IsMatched(b));
            Assert.AreEqual(0, m.CountCorrect());

            Assert.IsTrue(m.ResolveMismatch());
            Assert.IsFalse(m.IsFaceUp(a) || m.IsFaceUp(b));
            Assert.IsFalse(m.ResolveMismatch(), "nothing left to turn back");
            Assert.AreEqual(1, m.Attempts);
        }

        [Test]
        public void ThirdTap_TurnsTheWrongPairBackAndFlipsTheNewCard()
        {
            var m = Make(4, 4);
            var (a, b) = Mismatch(m);
            m.HandleInput(PuzzleInput.Tap(a));
            m.HandleInput(PuzzleInput.Tap(b));
            int c = Enumerable.Range(0, 16).First(x => x != a && x != b);
            Assert.AreEqual(MoveResult.Moved, m.HandleInput(PuzzleInput.Tap(c)));
            Assert.IsFalse(m.HasMismatch);
            Assert.IsFalse(m.IsFaceUp(a) || m.IsFaceUp(b));
            Assert.IsTrue(m.IsFaceUp(c), "the new card is the first of the next pair");

            // Tapping one of the two wrong cards only turns them back.
            var m2 = Make(4, 4);
            m2.HandleInput(PuzzleInput.Tap(a));
            m2.HandleInput(PuzzleInput.Tap(b));
            m2.HandleInput(PuzzleInput.Tap(a));
            Assert.IsFalse(m2.IsFaceUp(a) || m2.IsFaceUp(b));
        }

        [Test]
        public void FindingEveryPair_SolvesOnce([Values(2, 3, 4, 6)] int size)
        {
            var m = Make(size, size, 7);
            int solved = 0;
            m.OnSolved += () => solved++;
            for (int c = 0; c < size * size; c++)
            {
                if (m.IsMatched(c)) continue;
                m.HandleInput(PuzzleInput.Tap(c));
                m.HandleInput(PuzzleInput.Tap(m.PartnerOf(c)));
            }
            Assert.IsTrue(m.IsSolved());
            Assert.AreEqual(1, solved);
            Assert.AreEqual(m.PairCount, m.Attempts, "a perfect game = one move per pair");
            Assert.LessOrEqual(m.Attempts, m.GetParMoves());
        }

        [Test]
        public void Hint_PointsToAPair()
        {
            var m = Make(4, 4);
            var h = m.GetHint();
            Assert.IsTrue(h.IsValid);
            Assert.AreEqual(m.SymbolOf(h.FromCell), m.SymbolOf(h.ToCell));
            Assert.AreNotEqual(h.FromCell, h.ToCell);

            var (a, _) = Mismatch(m);
            m.HandleInput(PuzzleInput.Tap(a));
            h = m.GetHint();
            Assert.AreEqual(a, h.FromCell, "with one card face up, the hint shows its partner");
            Assert.AreEqual(m.PartnerOf(a), h.ToCell);
        }

        [Test]
        public void ForceSolve_UncoversEverything()
        {
            var m = Make(5, 4);
            bool solved = false;
            m.OnSolved += () => solved = true;
            m.ForceSolve();
            Assert.IsTrue(solved && m.IsSolved());
            Assert.IsTrue(Enumerable.Range(0, 20).All(m.IsMatched));
        }

        [Test]
        public void ParIsAboutOneAndAHalfAttemptsPerPair()
        {
            Assert.AreEqual(13, Make(4, 4).GetParMoves());   // 8 pairs
            Assert.AreEqual(4, Make(2, 2).GetParMoves());    // 2 pairs
            Assert.IsFalse(Make(4, 4).CanUndo);
        }

        // ---------------------------------------------------------------- symbols and grid

        [Test]
        public void Symbols_LabelsAndColors()
        {
            Assert.AreEqual("1", CardSymbols.Label(MemorySymbols.Numbers, 0));
            Assert.AreEqual("50", CardSymbols.Label(MemorySymbols.Numbers, 49));
            Assert.AreEqual("A", CardSymbols.Label(MemorySymbols.Letters, 0));
            Assert.AreEqual("Z", CardSymbols.Label(MemorySymbols.Letters, 25));
            Assert.AreEqual("", CardSymbols.Label(MemorySymbols.Colors, 3));
            Assert.AreEqual("", CardSymbols.Label(MemorySymbols.Numbers, -1), "free card");

            // Outline colors must be told apart: perceptual distance between any two of them.
            var colors = CardSymbols.Colors;
            Assert.AreEqual(CardSymbols.MaxPairs(MemorySymbols.Colors), colors.Length);
            for (int i = 0; i < colors.Length; i++)
                for (int j = i + 1; j < colors.Length; j++)
                    Assert.Greater(Oklab.Distance(Oklab.FromColor(colors[i]), Oklab.FromColor(colors[j])), 0.1f, $"colors {i} and {j}");
        }

        [Test]
        public void Fit_ReducesTheGridToTheSymbolsAvailable()
        {
            Assert.AreEqual((10, 10), CardSymbols.Fit(10, 10, 1f, 2, MemorySymbols.Numbers));   // 50 pairs
            var letters = CardSymbols.Fit(10, 10, 1f, 2, MemorySymbols.Letters);
            Assert.LessOrEqual(letters.cols * letters.rows, 53);
            Assert.GreaterOrEqual(letters.cols * letters.rows, 40, "not reduced more than needed");
            var colors = CardSymbols.Fit(8, 6, 4f / 3f, 2, MemorySymbols.Colors);
            Assert.LessOrEqual(colors.cols * colors.rows, 25);
            Assert.GreaterOrEqual(colors.cols, colors.rows, "a wide picture keeps more columns");
        }

        [Test]
        public void GridResolver_MemoryUsesTheLevelSymbols()
        {
            var pack = new GamePackData();
            pack.gameplay.difficultyCurve = DifficultyCurve.Fixed;
            pack.gameplay.fixedGrid = 12;
            pack.gameplay.memorySymbols = MemorySymbols.Letters;
            pack.levels.Add(new LevelConfig { id = "mem", mode = ModeIds.Memory });
            pack.levels.Add(new LevelConfig { id = "colors", mode = ModeIds.Memory, symbols = MemorySymbols.Colors });

            var letters = GridResolver.Resolve(pack, 0, 1600, 1200);
            Assert.AreEqual(MemorySymbols.Letters, letters.Settings.MemorySymbols);
            Assert.LessOrEqual(letters.Layout.CellCount, 53);
            Assert.LessOrEqual(letters.Layout.Cols, 10, "memory grids stop at 10");

            var colors = GridResolver.Resolve(pack, 1, 1600, 1200);
            Assert.AreEqual(MemorySymbols.Colors, colors.Settings.MemorySymbols);
            Assert.LessOrEqual(colors.Layout.CellCount, 25);

            // Every resolved Memory board can be dealt.
            var m = PuzzleModeRegistry.Create(ModeIds.Memory);
            m.Setup(colors.Layout, colors.Settings);
            m.Shuffle(colors.Seed);
            Assert.LessOrEqual(((ICardMode)m).PairCount, 12);
        }

        [Test]
        public void Pack_RoundTripsTheSymbols()
        {
            var pack = new GamePackData();
            pack.gameplay.memorySymbols = MemorySymbols.Colors;
            pack.levels.Add(new LevelConfig { id = "a", mode = ModeIds.Memory, symbols = MemorySymbols.Letters });
            pack.levels.Add(new LevelConfig { id = "b" });
            var json = PuzzleStudio.Core.Pack.PackJson.Serialize(pack);
            StringAssert.Contains("\"memorySymbols\": \"Colors\"", json);
            var loaded = PuzzleStudio.Core.Pack.PackJson.Deserialize<GamePackData>(json);
            Assert.AreEqual(MemorySymbols.Colors, loaded.gameplay.memorySymbols);
            Assert.AreEqual(MemorySymbols.Letters, loaded.levels[0].symbols);
            Assert.IsNull(loaded.levels[1].symbols);
        }

        [Test]
        public void OldPacks_DefaultToNumbers()
        {
            var loaded = PuzzleStudio.Core.Pack.PackJson.Deserialize<GamePackData>("{ \"gameplay\": { \"defaultMode\": \"Memory\" } }");
            Assert.AreEqual(MemorySymbols.Numbers, loaded.gameplay.memorySymbols);
        }
    }
}
