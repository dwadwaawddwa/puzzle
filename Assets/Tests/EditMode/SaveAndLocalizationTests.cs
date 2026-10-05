using System.IO;
using NUnit.Framework;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Save;

namespace PuzzleStudio.Tests
{
    public class SaveAndLocalizationTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "PuzzleSaveTests_" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Save_RoundTrip()
        {
            var save = new SaveSystem(_dir, "My: Game?");
            save.LoadAll();
            save.Progress.Submit("lvl_01", 3, 42.5f, 12, usedPreview: false, usedHint: false);
            save.SaveProgress();

            var again = new SaveSystem(_dir, "My: Game?");
            again.LoadAll();
            var r = again.Progress.Get("lvl_01");
            Assert.IsTrue(r.completed);
            Assert.AreEqual(3, r.stars);
            Assert.AreEqual(12, r.bestMoves);
            Assert.IsTrue(r.completedWithoutPreview);
            StringAssert.DoesNotContain("?", Path.GetFileName(again.Directory), "folder name is sanitized");
        }

        [Test]
        public void Save_CorruptFileRecoversFromBackup()
        {
            var store = new JsonFileStore<PlayerProgress>(Path.Combine(_dir, "progress.json"));
            var p = new PlayerProgress();
            p.Submit("a", 2, 10f, 5, false, false);
            store.Save(p);
            p.Submit("b", 3, 10f, 5, false, false);
            store.Save(p); // previous good version → .bak

            File.WriteAllText(store.Path, "{ this is garbage");
            var loaded = store.Load();
            Assert.IsTrue(store.RecoveredFromBackup);
            Assert.IsTrue(loaded.IsCompleted("a"));
            Assert.IsTrue(File.Exists(store.Path + ".corrupt"));
        }

        [Test]
        public void Save_NothingReadableStartsFresh()
        {
            var store = new JsonFileStore<PlayerProgress>(Path.Combine(_dir, "progress.json"));
            Directory.CreateDirectory(_dir);
            File.WriteAllText(store.Path, "garbage");
            var loaded = store.Load();
            Assert.IsTrue(store.StartedFresh);
            Assert.AreEqual(0, loaded.levels.Count);
        }

        [Test]
        public void Progress_KeepsBestValues()
        {
            var p = new PlayerProgress();
            var first = p.Submit("x", 1, 100f, 50, true, true);
            Assert.IsTrue(first.FirstCompletion);
            var second = p.Submit("x", 3, 60f, 70, false, false);
            Assert.IsFalse(second.FirstCompletion);
            Assert.IsTrue(second.NewBestTime);
            Assert.IsFalse(second.NewBestMoves);
            Assert.IsTrue(second.MoreStars);
            var r = p.Get("x");
            Assert.AreEqual(3, r.stars);
            Assert.AreEqual(60f, r.bestTime);
            Assert.AreEqual(50, r.bestMoves);
            Assert.AreEqual(2, r.timesCompleted);
        }

        [Test]
        public void Localization_LayersOverrideAndFallback()
        {
            var loc = new LocalizationService();
            loc.Load("fr",
                "{ \"menu.play\": \"Play\", \"menu.quit\": \"Quit\", \"hud.level\": \"Level {0} / {1}\" }",
                "{ \"menu.play\": \"Jouer\" }",
                null);
            Assert.AreEqual("Jouer", loc.T("menu.play"));
            Assert.AreEqual("Quit", loc.T("menu.quit"), "falls back to English");
            Assert.AreEqual("missing.key", loc.T("missing.key"));
            Assert.AreEqual("Level 3 / 10", loc.T("hud.level", 3, 10));
        }

        [Test]
        public void Localization_FrenchHasEveryEnglishKey()
        {
            var en = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(
                UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/en").text);
            var fr = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(
                UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/fr").text);
            foreach (var key in en.Keys) Assert.IsTrue(fr.ContainsKey(key), "fr.json is missing " + key);
        }

        [Test]
        public void Localization_BuiltInEnglishHasCoreKeys()
        {
            var en = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Localization/en");
            Assert.IsNotNull(en);
            var loc = new LocalizationService();
            loc.Load("en", en.text);
            foreach (var key in new[] { "menu.play", "hud.moves", "button.undo", "victory.title" })
                Assert.IsTrue(loc.Has(key), key);
        }
    }
}
