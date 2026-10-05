using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Steam;
using PuzzleStudio.Game.Steam;
using PuzzleStudio.Studio.Export;

namespace PuzzleStudio.Tests
{
    public class SteamTests
    {
        static GamePackData Pack(int levels)
        {
            var pack = new GamePackData();
            for (int i = 0; i < levels; i++) pack.levels.Add(new LevelConfig { id = $"lvl_{i + 1:00}", image = $"levels/{i + 1:00}.png" });
            return pack;
        }

        static void Complete(PlayerProgress p, GamePackData pack, int count, int stars = 3, float seconds = 90f, bool usedHint = true, bool usedPreview = true, bool perfect = false)
        {
            for (int i = 0; i < count; i++) p.Submit(pack.levels[i].id, stars, seconds, 30, usedPreview, usedHint, perfect);
        }

        // ---------------------------------------------------------------- generator

        [Test]
        public void Auto_ScalesWithTheLevelCount()
        {
            var one = AchievementGenerator.Auto(Pack(1)).Select(a => a.id).ToList();
            CollectionAssert.Contains(one, "ACH_FIRST_PUZZLE");
            CollectionAssert.DoesNotContain(one, "ACH_ALL_PUZZLES");
            CollectionAssert.DoesNotContain(one, "ACH_HALF");

            var ten = AchievementGenerator.Auto(Pack(10));
            var ids = ten.Select(a => a.id).ToList();
            CollectionAssert.IsSubsetOf(new[] { "ACH_QUARTER", "ACH_HALF", "ACH_THREE_QUARTERS", "ACH_ALL_PUZZLES", "ACH_ALL_STARS" }, ids);
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (var a in ten)
            {
                Assert.IsTrue(AchievementGenerator.IsValidId(a.id), a.id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(a.name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(a.description));
            }
            Assert.IsEmpty(AchievementGenerator.Auto(Pack(0)));
        }

        [Test]
        public void Auto_SkipsHintAndPreviewAchievementsWhenTheGameHasNone()
        {
            var pack = Pack(5);
            pack.gameplay.allowHints = false;
            pack.gameplay.allowPreview = false;
            var ids = AchievementGenerator.Auto(pack).Select(a => a.id).ToList();
            CollectionAssert.DoesNotContain(ids, "ACH_NO_HINT");
            CollectionAssert.DoesNotContain(ids, "ACH_NO_PREVIEW");
        }

        [Test]
        public void Effective_CustomListOrOff()
        {
            var pack = Pack(4);
            Assert.AreEqual(AchievementGenerator.Auto(pack).Count, AchievementGenerator.Effective(pack).Count);
            pack.steam.achievements.Add(new AchievementDef { id = "ACH_MINE", name = "Mine", rule = AchievementRule.LevelsCompleted, value = 2 });
            Assert.AreEqual(1, AchievementGenerator.Effective(pack).Count);
            Assert.IsTrue(AchievementGenerator.IsCustom(pack));
            pack.steam.achievementsEnabled = false;
            Assert.IsEmpty(AchievementGenerator.Effective(pack));
        }

        [TestCase("ACH_FIRST_PUZZLE", true)]
        [TestCase("ach_lower_1", true)]
        [TestCase("ACH FIRST", false)]
        [TestCase("ACH-FIRST", false)]
        [TestCase("", false)]
        [TestCase("ÉTOILE", false)]
        public void ApiNames(string id, bool valid) => Assert.AreEqual(valid, AchievementGenerator.IsValidId(id));

        // ---------------------------------------------------------------- tracker

        [Test]
        public void Tracker_UnlocksProgressAchievementsOnceAtTheRightMoment()
        {
            var pack = Pack(8);
            var p = new PlayerProgress();
            Assert.IsEmpty(AchievementTracker.Unlock(pack, p));

            Complete(p, pack, 1);
            var first = AchievementTracker.Unlock(pack, p).Select(a => a.id).ToList();
            CollectionAssert.Contains(first, "ACH_FIRST_PUZZLE");
            CollectionAssert.DoesNotContain(first, "ACH_QUARTER");
            Assert.IsEmpty(AchievementTracker.Unlock(pack, p), "never twice");

            Complete(p, pack, 2);
            CollectionAssert.Contains(AchievementTracker.Unlock(pack, p).Select(a => a.id).ToList(), "ACH_QUARTER");

            var half = pack.steam.achievements.Count == 0 ? AchievementGenerator.Auto(pack).First(a => a.id == "ACH_HALF") : null;
            Assert.AreEqual((2, 4), AchievementTracker.ProgressOf(half, pack, p));

            Complete(p, pack, 8);
            var last = AchievementTracker.Unlock(pack, p).Select(a => a.id).ToList();
            CollectionAssert.IsSubsetOf(new[] { "ACH_HALF", "ACH_THREE_QUARTERS", "ACH_ALL_PUZZLES", "ACH_ALL_STARS" }, last);
        }

        [Test]
        public void Tracker_LevelRules()
        {
            var pack = Pack(3);
            var p = new PlayerProgress();
            Complete(p, pack, 1, stars: 2, seconds: 90f, usedHint: true, usedPreview: true, perfect: false);
            var ids = AchievementTracker.Unlock(pack, p).Select(a => a.id).ToList();
            CollectionAssert.DoesNotContain(ids, "ACH_PERFECT");
            CollectionAssert.DoesNotContain(ids, "ACH_NO_HINT");
            CollectionAssert.DoesNotContain(ids, "ACH_NO_PREVIEW");
            CollectionAssert.DoesNotContain(ids, "ACH_FAST");

            p.Submit(pack.levels[1].id, 3, 42f, 12, usedPreview: false, usedHint: false, perfect: true);
            ids = AchievementTracker.Unlock(pack, p).Select(a => a.id).ToList();
            CollectionAssert.IsSubsetOf(new[] { "ACH_PERFECT", "ACH_NO_HINT", "ACH_NO_PREVIEW", "ACH_FAST" }, ids);
            CollectionAssert.DoesNotContain(ids, "ACH_ALL_STARS", "level 1 has 2 stars, level 3 none");
        }

        [Test]
        public void Tracker_IgnoresRecordsOfLevelsNoLongerInThePack()
        {
            var pack = Pack(2);
            var p = new PlayerProgress();
            p.Submit("deleted_level", 3, 10f, 5, false, false, true);
            Assert.IsEmpty(AchievementTracker.Unlock(pack, p));
        }

        [Test]
        public void Progress_AchievementsAndPerfectAreSaved()
        {
            string dir = Path.Combine(Path.GetTempPath(), "PuzzleSteamTest_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonFileStore<PlayerProgress>(Path.Combine(dir, "progress.json"));
                var p = new PlayerProgress();
                p.Submit("a", 3, 20f, 9, false, false, perfect: true);
                p.achievements.Add("ACH_FIRST_PUZZLE");
                store.Save(p);
                var back = store.Load();
                Assert.IsTrue(back.Get("a").perfect);
                Assert.IsTrue(back.HasAchievement("ACH_FIRST_PUZZLE"));
                Assert.AreEqual(1, back.achievements.Count, "no duplicates when loading over the default list");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---------------------------------------------------------------- service (game side, without Steam)

        sealed class FakeSteam : ISteamService
        {
            public readonly List<string> Unlocked = new List<string>();
            public bool IsAvailable => true;
            public string Status => "fake";
            public string LanguageCode => "fr";
            public bool OverlayEnabled => false;
            public event System.Action<bool> OverlayToggled { add { } remove { } }
            public void UnlockAchievement(string id) => Unlocked.Add(id);
            public void SetPresence(string token, int level, int total, string statusText) { }
            public void ClearPresence() { }
            public void Tick() { }
            public void Shutdown() { }
        }

        [Test]
        public void Service_ForwardsToSteamAndResyncsAtStart()
        {
            var pack = Pack(4);
            var save = new SaveSystem(Path.GetTempPath(), "unused");
            save.LoadAll();
            save.Progress.levels.Clear();
            save.Progress.achievements.Clear();
            var steam = new FakeSteam();
            var service = new AchievementService(pack, save, steam, persist: false);
            var raised = new List<string>();
            service.Unlocked += a => raised.Add(a.id);

            Complete(save.Progress, pack, 1);
            service.Check();
            CollectionAssert.Contains(raised, "ACH_FIRST_PUZZLE");
            CollectionAssert.Contains(steam.Unlocked, "ACH_FIRST_PUZZLE");

            // Next start: everything already unlocked is sent again (earned while Steam was off).
            var steam2 = new FakeSteam();
            new AchievementService(pack, save, steam2, persist: false).SyncOnStart();
            CollectionAssert.AreEquivalent(save.Progress.achievements, steam2.Unlocked);
        }

        [Test]
        public void Service_TranslatesOnlyUntouchedGeneratedTexts()
        {
            var pack = Pack(2);
            var save = new SaveSystem(Path.GetTempPath(), "unused");
            save.LoadAll();
            var loc = new LocalizationService();
            loc.Set("ach.ACH_FIRST_PUZZLE.name", "Premier puzzle");
            var service = new AchievementService(pack, save, new NullSteamService(), persist: false);
            var first = service.All.First(a => a.id == "ACH_FIRST_PUZZLE");
            Assert.AreEqual("Premier puzzle", service.NameOf(first, loc));

            pack.steam.achievements.Add(new AchievementDef { id = "ACH_FIRST_PUZZLE", name = "My own name", description = "x" });
            var custom = new AchievementService(pack, save, new NullSteamService(), persist: false);
            Assert.AreEqual("My own name", custom.NameOf(custom.All[0], loc));
        }

        // ---------------------------------------------------------------- Steamworks files

        [Test]
        public void Languages_MapBothWays()
        {
            Assert.AreEqual("fr", SteamworksFiles.CodeFromSteamLanguage("french"));
            Assert.AreEqual("zh", SteamworksFiles.CodeFromSteamLanguage("schinese"));
            Assert.AreEqual("pt", SteamworksFiles.CodeFromSteamLanguage("brazilian"));
            Assert.IsNull(SteamworksFiles.CodeFromSteamLanguage("klingon"));
            Assert.AreEqual("english", SteamworksFiles.SteamLanguage("en"));
            Assert.IsNull(SteamworksFiles.SteamLanguage("xx"));
        }

        [Test]
        public void RichPresenceFile_UsesSteamPlaceholders()
        {
            var texts = new Dictionary<string, string>
            {
                ["steam.rp.menu"] = "In the menus",
                ["steam.rp.playing"] = "Solving puzzle {0} of {1}",
                ["steam.rp.finished"] = "All \"done\"",
            };
            string vdf = SteamworksFiles.RichPresenceVdf("english", k => texts[k]);
            StringAssert.Contains("\"Language\"\t\"english\"", vdf);
            StringAssert.Contains("\"#Playing\"\t\"Solving puzzle %level% of %total%\"", vdf);
            StringAssert.Contains("\"#Finished\"\t\"All 'done'\"", vdf);
            Assert.AreEqual(vdf.Count(c => c == '{'), vdf.Count(c => c == '}'));
        }

        [Test]
        public void AppBuild_PointsAtTheGameFolderAndSkipsSteamAppId()
        {
            string vdf = SteamworksFiles.AppBuildVdf(1234560, 1234561, "My Game 1.0", "..\\..\\MyGame\\");
            StringAssert.Contains("\"AppID\"\t\"1234560\"", vdf);
            StringAssert.Contains("\"1234561\"", vdf);
            StringAssert.Contains("\"ContentRoot\"\t\"..\\..\\MyGame\\\"", vdf);
            StringAssert.Contains("\"FileExclusion\"\t\"steam_appid.txt\"", vdf);
            Assert.AreEqual(vdf.Count(c => c == '{'), vdf.Count(c => c == '}'));
            StringAssert.Contains("+run_app_build", SteamworksFiles.UploadBat("app_build_1234560.vdf"));
        }

        [Test]
        public void Guide_ListsAchievementsAndCloudFolder()
        {
            var pack = Pack(3);
            pack.game.title = "Cozy: Puzzles";
            pack.game.steamAppId = 480;
            pack.steam.cloudEnabled = true;
            var achievements = AchievementGenerator.Effective(pack);
            string guide = SteamworksFiles.Guide(pack, achievements, "CozyPuzzles", new[] { "header_capsule.png" });
            StringAssert.Contains("ACH_FIRST_PUZZLE", guide);
            StringAssert.Contains("Depot ID: 481", guide);
            StringAssert.Contains("PuzzleStudio/PuzzleGame/Cozy_ Puzzles", guide);
            StringAssert.Contains("store\\header_capsule.png", guide);
            Assert.AreEqual(achievements.Count + 1, SteamworksFiles.AchievementsTable(achievements).Split('\n').Count(l => l.Trim().Length > 0));
        }

        // ---------------------------------------------------------------- pack / validator

        [Test]
        public void Pack_SteamSettingsRoundTripAndOldPacksGetDefaults()
        {
            var pack = Pack(2);
            pack.steam.depotId = 99;
            pack.steam.restartThroughSteam = false;
            pack.steam.achievements.Add(new AchievementDef { id = "ACH_X", name = "X", rule = AchievementRule.FastLevel, value = 30, hidden = true });
            var back = GamePackLoader.Parse(GamePackWriter.ToJson(pack));
            Assert.AreEqual(99, back.steam.depotId);
            Assert.IsFalse(back.steam.restartThroughSteam);
            Assert.AreEqual(AchievementRule.FastLevel, back.steam.achievements[0].rule);
            Assert.IsTrue(back.steam.achievements[0].hidden);

            var old = GamePackLoader.Parse("{\"packVersion\":1,\"game\":{\"title\":\"Old\"},\"steam\":{\"richPresence\":false},\"levels\":[]}");
            Assert.IsTrue(old.steam.restartThroughSteam);
            Assert.IsTrue(old.steam.achievementsEnabled);
            Assert.IsTrue(old.steam.showAchievementsInGame);
            Assert.IsFalse(old.steam.richPresence);
        }

        [Test]
        public void Validator_ChecksSteamSettings()
        {
            var pack = Pack(2);
            Assert.IsTrue(PackValidator.Validate(pack, checkFiles: false).Has("steam.appid.none"));

            pack.game.steamAppId = 1000;
            pack.steam.depotId = 1000;
            pack.steam.achievements.Add(new AchievementDef { id = "BAD ID", name = "A" });
            pack.steam.achievements.Add(new AchievementDef { id = "ACH_A", name = "B" });
            pack.steam.achievements.Add(new AchievementDef { id = "ACH_A", name = "C" });
            pack.steam.achievements.Add(new AchievementDef { id = "ACH_B", name = "D", rule = AchievementRule.LevelsCompleted, value = 5 });
            var r = PackValidator.Validate(pack, checkFiles: false);
            Assert.IsTrue(r.Has("steam.depot.same"));
            Assert.IsTrue(r.Has("steam.ach.id"));
            Assert.IsTrue(r.Has("steam.ach.duplicate"));
            Assert.IsTrue(r.Has("steam.ach.value"), "5 levels in a 2-level game");
            Assert.IsFalse(r.Has("steam.appid.none"));
        }

        // ---------------------------------------------------------------- export helpers

        static IEnumerator Inner() { yield return 2; yield return 3; }
        static IEnumerator Outer() { yield return 1; yield return Inner(); yield return 4; }

        [Test]
        public void Flatten_RunsNestedCoroutinesInOrder()
        {
            var e = CoroutineUtil.Flatten(Outer());
            var seen = new List<object>();
            while (e.MoveNext()) seen.Add(e.Current);
            CollectionAssert.AreEqual(new object[] { 1, 2, 3, 4 }, seen);
        }

        [Test]
        public void StoreImages_HaveSteamworksSizes()
        {
            var sizes = SteamArt.StoreImages.ToDictionary(s => s.File, s => (s.Width, s.Height));
            Assert.AreEqual((920, 430), sizes["header_capsule.png"]);
            Assert.AreEqual((462, 174), sizes["small_capsule.png"]);
            Assert.AreEqual((1232, 706), sizes["main_capsule.png"]);
            Assert.AreEqual((748, 896), sizes["vertical_capsule.png"]);
            Assert.AreEqual((600, 900), sizes["library_capsule.png"]);
            Assert.AreEqual((3840, 1240), sizes["library_hero.png"]);
            Assert.AreEqual((1280, 720), sizes["library_logo.png"]);
            CollectionAssert.AllItemsAreUnique(SteamArt.StoreImages.Select(s => s.File));
        }
    }
}
