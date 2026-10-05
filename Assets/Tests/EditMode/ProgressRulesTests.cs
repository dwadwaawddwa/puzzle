using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Save;

namespace PuzzleStudio.Tests
{
    public class ProgressRulesTests
    {
        static GamePackData Pack(int levels, UnlockRule rule, int starsPerLevel = 2)
        {
            var p = new GamePackData();
            p.gameplay.unlockRule = rule;
            p.gameplay.starsToUnlockPerLevel = starsPerLevel;
            for (int i = 0; i < levels; i++) p.levels.Add(new LevelConfig { id = "l" + i });
            return p;
        }

        [Test]
        public void Sequential_UnlocksOneByOne()
        {
            var pack = Pack(4, UnlockRule.Sequential);
            var prog = new PlayerProgress();
            Assert.IsTrue(ProgressRules.IsUnlocked(pack, prog, 0));
            Assert.IsFalse(ProgressRules.IsUnlocked(pack, prog, 1));
            prog.Submit("l0", 1, 10, 10, false, false);
            Assert.IsTrue(ProgressRules.IsUnlocked(pack, prog, 1));
            Assert.IsFalse(ProgressRules.IsUnlocked(pack, prog, 2));
            Assert.AreEqual(1, ProgressRules.NextPlayable(pack, prog, 0));
            Assert.AreEqual(-1, ProgressRules.NextPlayable(pack, prog, 1));
        }

        [Test]
        public void AllUnlocked_OpensEverything()
        {
            var pack = Pack(5, UnlockRule.AllUnlocked);
            for (int i = 0; i < 5; i++) Assert.IsTrue(ProgressRules.IsUnlocked(pack, new PlayerProgress(), i));
        }

        [Test]
        public void ByStars_NeedsEnoughStars()
        {
            var pack = Pack(4, UnlockRule.ByStars, starsPerLevel: 2);
            var prog = new PlayerProgress();
            prog.Submit("l0", 1, 10, 10, false, false);
            Assert.IsFalse(ProgressRules.IsUnlocked(pack, prog, 1), "1 star < 2 needed");
            prog.Submit("l0", 3, 10, 10, false, false);
            Assert.IsTrue(ProgressRules.IsUnlocked(pack, prog, 1));
            Assert.IsFalse(ProgressRules.IsUnlocked(pack, prog, 2), "3 stars < 4 needed");
            Assert.AreEqual(4, ProgressRules.StarsRequired(pack, 2));
        }

        [Test]
        public void Continue_ResumesLastUnfinishedThenFirstOpen()
        {
            var pack = Pack(4, UnlockRule.AllUnlocked);
            var prog = new PlayerProgress();
            Assert.AreEqual(0, ProgressRules.ContinueIndex(pack, prog));
            prog.lastPlayedLevelId = "l2";
            Assert.AreEqual(2, ProgressRules.ContinueIndex(pack, prog));
            prog.Submit("l2", 3, 1, 1, false, false);
            Assert.AreEqual(0, ProgressRules.ContinueIndex(pack, prog));
        }

        [Test]
        public void Totals()
        {
            var pack = Pack(3, UnlockRule.Sequential);
            var prog = new PlayerProgress();
            Assert.IsFalse(ProgressRules.HasStarted(pack, prog));
            prog.Submit("l0", 3, 10, 5, false, false);
            prog.Submit("l1", 2, 20, 5, false, false);
            Assert.AreEqual(2, ProgressRules.CompletedCount(pack, prog));
            Assert.AreEqual(5, ProgressRules.TotalStars(pack, prog));
            Assert.AreEqual(30f, ProgressRules.TotalBestTime(pack, prog), 0.001f);
            Assert.IsFalse(ProgressRules.AllCompleted(pack, prog));
            prog.Submit("l2", 1, 1, 1, false, false);
            Assert.IsTrue(ProgressRules.AllCompleted(pack, prog));
        }
    }
}
