using NUnit.Framework;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Util;

namespace PuzzleStudio.Tests
{
    public class AccessibilityTests
    {
        [Test]
        public void GridCursor_MovesOneCellAndStaysOnTheBoard()
        {
            var l = new BoardLayout(4, 3);
            Assert.AreEqual(l.Cell(1, 1), GridCursor.Center(l));
            int c = l.Cell(0, 0);
            Assert.AreEqual(l.Cell(1, 0), GridCursor.Move(c, 1, 0, l));
            Assert.AreEqual(l.Cell(0, 1), GridCursor.Move(c, 0, 1, l));
            Assert.AreEqual(c, GridCursor.Move(c, -1, 0, l), "left edge");
            Assert.AreEqual(c, GridCursor.Move(c, 0, -1, l), "top edge");
            int last = l.Cell(3, 2);
            Assert.AreEqual(last, GridCursor.Move(last, 1, 1, l), "bottom-right corner");
            Assert.AreEqual(l.Cell(2, 2), GridCursor.Move(last, -5, 0, l), "one cell per step whatever the magnitude");
            Assert.AreEqual(GridCursor.Center(l), GridCursor.Move(-1, 1, 0, l), "hidden cursor starts in the center");
        }

        [Test]
        public void GridCursor_WorksOnStrips()
        {
            var vertical = new BoardLayout(6, 1);
            Assert.AreEqual(3, GridCursor.Move(2, 1, 0, vertical));
            Assert.AreEqual(2, GridCursor.Move(2, 0, 1, vertical), "no rows to move to");
        }

        [Test]
        public void RepeatTimer_FiresOnPressThenRepeats()
        {
            var t = new RepeatTimer(0.3f, 0.1f);
            Assert.IsTrue(t.Tick(1, 0.016f), "press");
            Assert.IsFalse(t.Tick(1, 0.2f));
            Assert.IsTrue(t.Tick(1, 0.11f), "after the first delay");
            Assert.IsFalse(t.Tick(1, 0.05f));
            Assert.IsTrue(t.Tick(1, 0.06f), "then every interval");
            Assert.IsTrue(t.Tick(2, 0.01f), "another direction fires at once");
            Assert.IsFalse(t.Tick(0, 0.01f), "released");
            Assert.IsTrue(t.Tick(2, 0.01f), "pressed again");
            Assert.IsTrue(t.Tick(2, 5f), "a long frame fires once");
            Assert.IsFalse(t.Tick(2, 0.01f), "…without a burst afterwards");
        }

        [Test]
        public void DeckDefaults_ApplyOnceAndKeepPlayerChoices()
        {
            var s = new SettingsData();
            Assert.IsFalse(DeckDefaults.Apply(s, isSteamDeck: false));
            Assert.AreEqual(1f, s.uiScale);

            Assert.IsTrue(DeckDefaults.Apply(s, isSteamDeck: true));
            Assert.AreEqual(DeckDefaults.UiScale, s.uiScale);
            s.uiScale = 1f;   // the player prefers the normal size
            Assert.IsFalse(DeckDefaults.Apply(s, isSteamDeck: true), "only the first launch");
            Assert.AreEqual(1f, s.uiScale);

            var custom = new SettingsData { uiScale = 1.3f };
            DeckDefaults.Apply(custom, true);
            Assert.AreEqual(1.3f, custom.uiScale, "an explicit size is kept");
        }

        [Test]
        public void Settings_OldFilesGetTheNewDefaults()
        {
            var s = PackJson.Deserialize<SettingsData>("{\"version\":1,\"masterVolume\":0.5,\"reduceMotion\":true}");
            Assert.AreEqual(0.5f, s.masterVolume);
            Assert.IsTrue(s.reduceMotion);
            Assert.IsTrue(s.vibration);
            Assert.IsTrue(s.showTimer);
            Assert.IsFalse(s.readableFont);
            Assert.IsFalse(s.deckDefaultsApplied);
        }
    }
}
