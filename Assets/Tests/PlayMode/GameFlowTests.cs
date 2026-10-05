using System.Collections;
using System.IO;
using NUnit.Framework;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.Screens;
using UnityEngine;
using UnityEngine.TestTools;

namespace PuzzleStudio.Tests
{
    /// <summary>Plays the CozyPastel sample through the real runtime: screens, pause, victory, progress.</summary>
    public class GameFlowTests
    {
        GameObject _root;

        static string SamplePack => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SamplePacks", "CozyPastel"));

        GameRoot Launch(StartScreen screen, int level = 0)
        {
            Assume.That(Directory.Exists(SamplePack), "Run Build > Generate Sample Packs first");
            GameBootstrap.Initialize(SamplePack);
            Assert.IsTrue(ServiceHub.IsReady, ServiceHub.LoadError);
            ServiceHub.Save.ResetProgress();
            GameRoot.PendingHost = new GameRoot.HostConfig { StartScreen = screen, StartLevel = level, Muted = true };
            _root = new GameObject("GameRoot");
            return _root.AddComponent<GameRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceHub.Save?.ResetProgress();
            if (_root != null) Object.Destroy(_root);
            GameBootstrap.Shutdown();
        }

        static IEnumerator Solve(IPuzzleMode mode)
        {
            for (int cell = 0; cell < mode.Layout.CellCount; cell++)
            {
                if (mode.PieceAt(cell) == cell) continue;
                mode.HandleInput(PuzzleInput.Drop(mode.Pieces[cell].Cell, cell));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Level_CanBeSolvedAndIsSaved()
        {
            var root = Launch(StartScreen.Gameplay);
            yield return null;

            var gameplay = root.Gameplay;
            Assert.AreEqual(0, gameplay.LevelIndex);
            Assert.IsFalse(gameplay.Mode.IsSolved());
            yield return Solve(gameplay.Mode);

            Assert.IsTrue(gameplay.Session.IsSolved);
            Assert.AreEqual(gameplay.Session.Par, gameplay.Session.Moves);
            var levelId = ServiceHub.Pack.levels[0].id;
            Assert.IsTrue(ServiceHub.Save.Progress.IsCompleted(levelId));
            Assert.AreEqual(3, ServiceHub.Save.Progress.StarsOf(levelId));
            Assert.IsTrue(File.Exists(ServiceHub.Save.ProgressStore.Path));
        }

        [UnityTest]
        public IEnumerator Flow_MenuLevelsPausePlayNext()
        {
            var root = Launch(StartScreen.Menu);
            yield return null;
            var flow = root.Flow;
            Assert.IsInstanceOf<MainMenuScreen>(flow.CurrentPage);

            flow.ShowLevels();
            yield return null;
            Assert.IsInstanceOf<LevelSelectScreen>(flow.CurrentPage);

            flow.PlayLevel(0);
            yield return null;
            Assert.IsInstanceOf<GameplayScreen>(flow.CurrentPage);
            Assert.IsTrue(flow.Gameplay.IsPlaying);

            flow.Pause();
            yield return null;
            Assert.IsInstanceOf<PauseScreen>(flow.TopScreen);
            Assert.IsTrue(flow.Gameplay.Session.IsPaused);
            flow.Resume();
            yield return null;
            Assert.IsFalse(flow.Gameplay.Session.IsPaused);

            yield return Solve(flow.Gameplay.Mode);
            yield return new WaitForSecondsRealtime(1.8f);
            var screen = (GameplayScreen)flow.CurrentPage;
            Assert.IsTrue(screen.VictoryVisible, "victory panel shown");
            Assert.Greater(flow.Particles.AliveParticles, 0, "victory particles playing");

            flow.NextAfterVictory();
            yield return null;
            Assert.AreEqual(1, flow.Gameplay.LevelIndex, "sequential unlock opened level 2");

            flow.ShowMenu();
            yield return null;
            Assert.IsFalse(flow.Gameplay.IsPlaying, "board released when leaving gameplay");
        }
    }
}
