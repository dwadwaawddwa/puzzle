using System.Collections;
using System.IO;
using NUnit.Framework;
using PuzzleStudio.Core.Modes;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.Gameplay;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PuzzleStudio.Tests
{
    /// <summary>Plays with a virtual gamepad through the real input path (no mouse at all).</summary>
    public class GamepadTests
    {
        GameObject _root;
        Gamepad _pad;

        static string SamplePack => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SamplePacks", "CozyPastel"));

        GameRoot Launch(StartScreen screen, int level)
        {
            Assume.That(Directory.Exists(SamplePack), "Run Build > Generate Sample Packs first");
            GameBootstrap.Initialize(SamplePack);
            Assert.IsTrue(ServiceHub.IsReady, ServiceHub.LoadError);
            ServiceHub.Save.ResetProgress();
            GameRoot.PendingHost = new GameRoot.HostConfig { StartScreen = screen, StartLevel = level, Muted = true };
            _root = new GameObject("GameRoot");
            _pad = InputSystem.AddDevice<Gamepad>("TestGamepad");
            return _root.AddComponent<GameRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_pad != null) InputSystem.RemoveDevice(_pad);
            _pad = null;
            InputModeTracker.Set(InputMode.Pointer);
            ServiceHub.Save?.ResetProgress();
            if (_root != null) Object.Destroy(_root);
            GameBootstrap.Shutdown();
        }

        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
        }

        IEnumerator MoveCursorTo(GameplayController g, int cell)
        {
            var layout = g.Mode.Layout;
            for (int i = 0; i < 60 && g.CursorCell != cell; i++)
            {
                int c = g.CursorCell;
                int dx = layout.Col(cell) - layout.Col(c), dy = layout.Row(cell) - layout.Row(c);
                var button = dx > 0 ? GamepadButton.DpadRight : dx < 0 ? GamepadButton.DpadLeft
                    : dy > 0 ? GamepadButton.DpadDown : GamepadButton.DpadUp;
                yield return Press(button);
            }
            Assert.AreEqual(cell, g.CursorCell, "cursor reached the cell");
        }

        [UnityTest]
        public IEnumerator Gamepad_SolvesASwapLevel()
        {
            var root = Launch(StartScreen.Gameplay, 0);
            yield return null;
            var g = root.Gameplay;
            Assume.That(g.Mode.DragStyle, Is.EqualTo(DragStyle.Swap), "level 1 of CozyPastel is a swap puzzle");

            yield return Press(GamepadButton.DpadRight);
            Assert.AreEqual(InputMode.Gamepad, InputModeTracker.Current);
            Assert.GreaterOrEqual(g.CursorCell, 0, "the first press shows the cursor");

            var mode = g.Mode;
            for (int guard = 0; guard < 100 && !mode.IsSolved(); guard++)
            {
                int target = 0;
                while (mode.PieceAt(target) == target) target++;
                int from = mode.Pieces[target].Cell;
                yield return MoveCursorTo(g, from);
                yield return Press(GamepadButton.South);
                Assert.AreEqual(from, mode.SelectedCell, "A picks the piece under the cursor");
                yield return MoveCursorTo(g, target);
                yield return Press(GamepadButton.South);
            }
            Assert.IsTrue(mode.IsSolved());
            Assert.IsTrue(g.Session.IsSolved);
            Assert.AreEqual(-1, g.CursorCell, "cursor hidden for the victory");
        }

        [UnityTest]
        public IEnumerator Gamepad_PlaysMemory()
        {
            var root = Launch(StartScreen.Gameplay, 6);   // level 7: memory (numbers)
            yield return null;
            var g = root.Gameplay;
            var memory = g.Mode as MemoryMode;
            Assume.That(memory, Is.Not.Null, "level 7 of CozyPastel is a memory game");
            int n = memory.Layout.CellCount;
            Assert.IsTrue(g.Board.ViewOf(0).IsCard);
            Assert.IsFalse(g.Board.ViewOf(0).ShowsFace, "cards start face down");

            yield return Press(GamepadButton.DpadRight);
            Assert.GreaterOrEqual(g.CursorCell, 0);

            // Two different cards: both shown with their numbers, then turned back by themselves.
            int a = 0, b = 1;
            while (memory.SymbolOf(b) == memory.SymbolOf(a) || memory.IsMatched(b)) b++;
            yield return MoveCursorTo(g, a);
            yield return Press(GamepadButton.South);
            yield return MoveCursorTo(g, b);
            yield return Press(GamepadButton.South);
            Assert.IsTrue(memory.HasMismatch);
            Assert.AreEqual(1, g.Session.Moves, "one move per pair of cards");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(g.Board.ViewOf(a).ShowsFace);
            Assert.AreEqual((memory.SymbolOf(a) + 1).ToString(), g.Board.ViewOf(a).Label);
            yield return new WaitForSecondsRealtime(GameplayController.MismatchDelay);
            Assert.IsFalse(memory.IsFaceUp(a) || memory.IsFaceUp(b), "turned back after a moment");

            // Then every pair, straight away.
            for (int c = 0; c < n; c++)
            {
                if (memory.IsMatched(c)) continue;
                yield return MoveCursorTo(g, c);
                yield return Press(GamepadButton.South);
                Assert.IsTrue(memory.IsFaceUp(c), "A turns the card over");
                yield return MoveCursorTo(g, memory.PartnerOf(c));
                yield return Press(GamepadButton.South);
                Assert.IsTrue(memory.IsMatched(c));
            }
            Assert.IsTrue(memory.IsSolved());
            Assert.IsTrue(g.Session.IsSolved);
            Assert.AreEqual(1 + memory.PairCount, g.Session.Moves);
            yield return new WaitForSecondsRealtime(BoardView.CardHold + 0.4f);
            Assert.AreEqual("", g.Board.ViewOf(a).Label, "found pairs leave only the picture");
        }

        [UnityTest]
        public IEnumerator Gamepad_SlidesTilesAndUsesShortcuts()
        {
            var root = Launch(StartScreen.Gameplay, 4);   // level 5: sliding puzzle
            yield return null;
            var g = root.Gameplay;
            Assume.That(g.Mode.DragStyle, Is.EqualTo(DragStyle.Slide));

            int moves = g.Session.Moves;
            foreach (var b in new[] { GamepadButton.DpadRight, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadUp })
            {
                yield return Press(b);
                if (g.Session.Moves > moves) break;
            }
            Assert.Greater(g.Session.Moves, moves, "the D-pad pushes a tile into the gap");
            Assert.AreEqual(-1, g.CursorCell, "no cursor in the sliding mode");

            Assert.IsTrue(g.Mode.CanUndo);
            yield return Press(GamepadButton.LeftShoulder);
            Assert.IsFalse(g.Mode.CanUndo, "LB undoes");

            int hints = g.Session.HintsUsed;
            yield return Press(GamepadButton.West);
            Assert.AreEqual(hints + 1, g.Session.HintsUsed, "X asks for a hint");

            yield return Press(GamepadButton.Start);
            Assert.IsTrue(g.Session.IsPaused, "Menu pauses");
        }
    }
}
