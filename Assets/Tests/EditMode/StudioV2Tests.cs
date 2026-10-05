using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.UI;
using PuzzleStudio.Studio.App;
using UnityEngine;

namespace PuzzleStudio.Tests
{
    /// <summary>Studio v2: undo history, autosave, file handling, crop geometry, texts and fonts.</summary>
    public class StudioV2Tests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PuzzleStudioV2Tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            // A font read by the font engine stays open until Unity quits: leave that temp folder behind.
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch (IOException) { }
        }

        string WriteFile(string name, string content)
        {
            string path = Path.Combine(_dir, "src", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        // ---------------------------------------------------------------- undo history

        [Test]
        public void Undo_StepsBackAndForth()
        {
            var h = new UndoHistory();
            h.Reset("A");
            Assert.IsFalse(h.CanUndo);
            Assert.IsFalse(h.Commit("A"), "no change, no step");
            Assert.IsTrue(h.Commit("B"));
            Assert.IsTrue(h.Commit("C"));
            Assert.AreEqual("B", h.Undo());
            Assert.AreEqual("A", h.Undo());
            Assert.IsNull(h.Undo());
            Assert.AreEqual("B", h.Redo());
            Assert.IsTrue(h.Commit("D"), "a new edit after undo");
            Assert.IsFalse(h.CanRedo, "…forgets the redo branch");
            Assert.AreEqual("B", h.Undo());
            Assert.AreEqual("A", h.Undo());
        }

        [Test]
        public void Undo_KeepsTheLastStepsOnly()
        {
            var h = new UndoHistory(capacity: 3);
            h.Reset("0");
            for (int i = 1; i <= 6; i++) h.Commit(i.ToString());
            Assert.AreEqual(3, h.UndoCount);
            Assert.AreEqual("5", h.Undo());
            Assert.AreEqual("4", h.Undo());
            Assert.AreEqual("3", h.Undo());
            Assert.IsNull(h.Undo());
        }

        [Test]
        public void Snapshot_RestoresPackAndProjectSettings()
        {
            var p = StudioProject.Create(_dir, "Snap");
            p.Pack.game.title = "Before";
            p.File.export.exeName = "One";
            string before = p.Snapshot();
            p.Pack.game.title = "After";
            p.Pack.levels.Add(new LevelConfig { id = "x", image = "levels/x.png" });
            p.File.export.exeName = "Two";

            p.Restore(before);
            Assert.AreEqual("Before", p.Pack.game.title);
            Assert.AreEqual(0, p.Pack.levels.Count);
            Assert.AreEqual("One", p.File.export.exeName);
            Assert.AreEqual(p.PackDir, p.Pack.RootPath, "files still resolve");
        }

        // ---------------------------------------------------------------- autosave

        [Test]
        public void Autosave_IsOfferedOnlyWhenNewerThanTheSavedProject()
        {
            var p = StudioProject.Create(_dir, "Auto");
            Assert.IsNull(p.PendingAutosave());
            p.Pack.game.title = "Unsaved title";
            System.Threading.Thread.Sleep(20);
            p.WriteAutosave();
            Assert.IsNotNull(p.PendingAutosave());

            var reopened = StudioProject.Open(p.Root);
            Assert.AreEqual("Auto", reopened.Pack.game.title, "the saved project is untouched");
            reopened.RecoverAutosave();
            Assert.AreEqual("Unsaved title", reopened.Pack.game.title);

            reopened.Save();
            Assert.IsNull(reopened.PendingAutosave(), "saving clears the autosave");
            Assert.IsFalse(Directory.Exists(Path.Combine(p.Root, StudioProject.AutosaveFolder)));
        }

        // ---------------------------------------------------------------- files

        [Test]
        public void ImportedFiles_GetContentNamesAndNeverOverwrite()
        {
            var p = StudioProject.Create(_dir, "Files");
            string a = ProjectFiles.ImportAudioFile(p.Pack, WriteFile("song.ogg", "first"), "music_menu");
            string b = ProjectFiles.ImportAudioFile(p.Pack, WriteFile("song2.ogg", "second"), "music_menu");
            string a2 = ProjectFiles.ImportAudioFile(p.Pack, WriteFile("copy.ogg", "first"), "music_menu");
            StringAssert.StartsWith("audio/music_menu_", a);
            StringAssert.EndsWith(".ogg", a);
            Assert.AreNotEqual(a, b, "a new file never replaces the old one (undo)");
            Assert.AreEqual(a, a2, "same content, same file");
            Assert.AreEqual("first", File.ReadAllText(p.Pack.Resolve(a)));
            StringAssert.StartsWith("theme/logo_", ProjectFiles.ImportThemeFile(p.Pack, WriteFile("l.png", "png"), "logo"));
        }

        [Test]
        public void Tidy_MovesOnlyUnreferencedFiles()
        {
            var p = StudioProject.Create(_dir, "Tidy");
            p.Pack.audio.musicMenu = ProjectFiles.ImportAudioFile(p.Pack, WriteFile("m.ogg", "menu"), "music_menu");
            string old = ProjectFiles.ImportAudioFile(p.Pack, WriteFile("o.ogg", "old"), "music_game");
            p.Pack.theme.background.image = ProjectFiles.ImportThemeFile(p.Pack, WriteFile("b.png", "bg"), "background");

            Assert.AreEqual(1, p.MoveUnusedFilesToTrash());
            Assert.IsTrue(File.Exists(p.Pack.Resolve(p.Pack.audio.musicMenu)));
            Assert.IsTrue(File.Exists(p.Pack.Resolve(p.Pack.theme.background.image)));
            Assert.IsFalse(File.Exists(p.Pack.Resolve(old)));
            Assert.IsTrue(File.Exists(Path.Combine(p.Root, StudioProject.TrashFolder, "audio", Path.GetFileName(old))));
        }

        [Test]
        public void Levels_MoveToAnyPosition()
        {
            var pack = new GamePackData();
            foreach (var id in new[] { "a", "b", "c", "d" }) pack.levels.Add(new LevelConfig { id = id });
            Assert.IsTrue(LevelImporter.Move(pack, 0, 2));
            Assert.AreEqual("b,c,a,d", string.Join(",", pack.levels.ConvertAll(l => l.id)));
            Assert.IsTrue(LevelImporter.Move(pack, 3, 0));
            Assert.AreEqual("d,b,c,a", string.Join(",", pack.levels.ConvertAll(l => l.id)));
            Assert.IsFalse(LevelImporter.Move(pack, 1, 1));
            Assert.IsTrue(LevelImporter.Move(pack, 0, 99), "clamped to the end");
            Assert.AreEqual("b,c,a,d", string.Join(",", pack.levels.ConvertAll(l => l.id)));
        }

        // ---------------------------------------------------------------- crop

        [Test]
        public void Crop_MoveStaysInsideThePicture()
        {
            var c = CropMath.Move(new CropRect(0.5f, 0.5f, 0.4f, 0.4f), 0.5f, -0.9f);
            Assert.AreEqual(0.6f, c.x, 1e-4f);
            Assert.AreEqual(0f, c.y, 1e-4f);
            Assert.AreEqual(0.4f, c.w, 1e-4f);
            Assert.IsTrue(c.IsValid);
        }

        [Test]
        public void Crop_CornerResizeKeepsTheOppositeCornerAndAspect()
        {
            var full = CropMath.Full;
            var free = CropMath.ResizeCorner(full, 0, 0.25f, 0.1f, 0f, 1.5f);
            Assert.AreEqual(1f, free.x + free.w, 1e-4f, "bottom-right corner fixed");
            Assert.AreEqual(1f, free.y + free.h, 1e-4f);
            Assert.AreEqual(0.25f, free.x, 1e-4f);

            // Square crop of a 3:2 picture.
            var square = CropMath.ResizeCorner(full, 2, 0.9f, 0.9f, 1f, 1.5f);
            Assert.AreEqual(1f, CropMath.AspectOf(square, 1.5f), 1e-3f);
            Assert.IsTrue(square.IsValid);
            Assert.AreEqual(0f, square.x, 1e-4f, "top-left corner fixed");

            var tiny = CropMath.ResizeCorner(full, 2, 0.001f, 0.001f, 0f, 1f);
            Assert.GreaterOrEqual(tiny.w, CropMath.MinSize - 1e-4f, "never smaller than the minimum");
        }

        [Test]
        public void Crop_FitAndWithAspect()
        {
            var wide = CropMath.Fit(16f / 9f, 4f / 3f);      // 16:9 out of a 4:3 picture: full width, less height
            Assert.AreEqual(1f, wide.w, 1e-4f);
            Assert.AreEqual(16f / 9f, CropMath.AspectOf(wide, 4f / 3f), 1e-3f);
            Assert.AreEqual(0.5f, wide.y + wide.h * 0.5f, 1e-4f, "centered");

            var c = CropMath.WithAspect(new CropRect(0.1f, 0.1f, 0.6f, 0.3f), 1f, 1f);
            Assert.AreEqual(1f, CropMath.AspectOf(c, 1f), 1e-3f);
            Assert.IsTrue(c.IsValid);
            Assert.IsTrue(CropMath.IsFull(CropMath.Fit(0f, 2f)));
        }

        // ---------------------------------------------------------------- texts

        [Test]
        public void Texts_OverrideTheRightLanguageOnly()
        {
            var pack = new GamePackData();
            pack.texts["en"] = new Dictionary<string, string> { ["menu.play"] = "Start puzzling" };
            var en = GameBootstrap.LoadLocalization(pack, "en");
            Assert.AreEqual("Start puzzling", en.T("menu.play"));
            var fr = GameBootstrap.LoadLocalization(pack, "fr");
            Assert.AreNotEqual("Start puzzling", fr.T("menu.play"), "an English change never replaces a French built-in text");

            pack.texts["fr"] = new Dictionary<string, string> { ["menu.play"] = "C'est parti" };
            Assert.AreEqual("C'est parti", GameBootstrap.LoadLocalization(pack, "fr").T("menu.play"));
        }

        [Test]
        public void Texts_NewLanguageStartsFromEnglish()
        {
            var pack = new GamePackData();
            Assert.IsFalse(GameBootstrap.HasLanguage(pack, "de"));
            pack.texts["de"] = new Dictionary<string, string> { ["menu.play"] = "Spielen" };
            Assert.IsTrue(GameBootstrap.HasLanguage(pack, "de"));
            var de = GameBootstrap.LoadLocalization(pack, "de");
            Assert.AreEqual("Spielen", de.T("menu.play"));
            Assert.AreEqual(GameBootstrap.LoadLocalization(pack, "en").T("menu.levels"), de.T("menu.levels"), "untranslated: English");

            var back = GamePackLoader.Parse(GamePackWriter.ToJson(pack));
            Assert.AreEqual("Spielen", back.texts["de"]["menu.play"], "saved in game.json");
        }

        // ---------------------------------------------------------------- fonts

        [Test]
        public void Fonts_ReadAFontFileAndFallBackWhenBroken()
        {
            string ttf = Path.Combine(Application.dataPath, "_Project", "Resources", "Fonts", "Inter-Regular.ttf");
            Assume.That(File.Exists(ttf));
            var pack = new GamePackData { RootPath = _dir };
            Directory.CreateDirectory(Path.Combine(_dir, "theme"));
            File.Copy(ttf, Path.Combine(_dir, "theme", "font_titles_test.ttf"));
            File.WriteAllText(Path.Combine(_dir, "theme", "broken.ttf"), "not a font");

            var custom = FontLibrary.Definition("theme/font_titles_test.ttf", FontRole.Heading, pack);
            Assert.IsNotNull(custom.fontAsset, "the file became a font asset");
            Assert.AreEqual("font_titles_test", custom.fontAsset.name);

            var fallback = FontLibrary.Definition("theme/broken.ttf", FontRole.Heading, pack);
            Assert.IsNull(fallback.fontAsset);
            Assert.IsNotNull(fallback.font, "unreadable file → built-in font");
            Assert.IsTrue(FontLibrary.IsSet(FontLibrary.Definition("Default:Elegant", FontRole.Body)));
        }
    }
}
