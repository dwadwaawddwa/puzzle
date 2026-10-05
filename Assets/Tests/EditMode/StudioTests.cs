using System.IO;
using System.Linq;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Export;
using PuzzleStudio.Studio.Themes;
using UnityEngine;

namespace PuzzleStudio.Tests
{
    public class StudioTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PuzzleStudioStudioTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        string WriteImage(string folder, string name, int w = 640, int h = 480)
        {
            Directory.CreateDirectory(folder);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var c = Color.HSVToRGB((name.GetHashCode() & 255) / 255f, 0.6f, 0.9f);
            for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(c, Color.black, (i % w) / (float)w);
            tex.SetPixels32(px);
            tex.Apply();
            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, name.EndsWith(".jpg") ? tex.EncodeToJPG() : tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        // ---------------------------------------------------------------- projects

        [Test]
        public void Project_CreateSaveOpenRoundTrip()
        {
            var p = StudioProject.Create(_dir, "My: Cool Game");
            StringAssert.EndsWith(".puzzleproj", p.Root);
            Assert.AreEqual("My: Cool Game", p.Pack.game.title);
            p.Pack.theme.colors.primary = "#123456";
            p.File.export.exeName = "CoolGame";
            p.Save();

            var again = StudioProject.Open(p.Root);
            Assert.AreEqual("#123456", again.Pack.theme.colors.primary);
            Assert.AreEqual("CoolGame", again.ExeName);
            Assert.AreEqual(p.Root, StudioProject.ResolveRoot(Path.Combine(p.Root, "project.json")));
            Assert.AreEqual(p.Root, StudioProject.ResolveRoot(Path.Combine(p.PackDir, "game.json")));
            Assert.IsNull(StudioProject.ResolveRoot(_dir));
        }

        [Test]
        public void Project_ExeNameDefaultsToTitleWithoutSpaces()
        {
            var p = StudioProject.Create(_dir, "Cozy Puzzles");
            Assert.AreEqual("CozyPuzzles", p.ExeName);
            p.Pack.game.title = "Bad/Name?";
            Assert.AreEqual("Bad_Name_", p.ExeName);
        }

        [Test]
        public void Project_CreateFromPackCopiesEverything()
        {
            string packDir = Path.Combine(_dir, "pack_src");
            WriteImage(Path.Combine(packDir, "levels"), "01.png");
            var pack = new GamePackData();
            pack.game.title = "Sample Pack";
            pack.levels.Add(new LevelConfig { id = "a", image = "levels/01.png", name = "A" });
            GamePackWriter.WriteJson(pack, packDir);

            var p = StudioProject.CreateFromPack(packDir, _dir);
            Assert.IsTrue(File.Exists(Path.Combine(p.PackDir, "levels", "01.png")));
            Assert.AreEqual("Sample Pack", p.Pack.game.title);
            Assert.AreEqual(1, p.Pack.levels.Count);
            var second = StudioProject.CreateFromPack(packDir, _dir);
            Assert.AreNotEqual(p.Root, second.Root, "never overwrites an existing project");
        }

        // ---------------------------------------------------------------- import

        [Test]
        public void Import_FolderIsNaturallySortedAndCopied()
        {
            string src = Path.Combine(_dir, "photos");
            WriteImage(src, "img10.png");
            WriteImage(src, "img2.png");
            WriteImage(src, "img1.jpg");
            File.WriteAllText(Path.Combine(src, "notes.txt"), "not an image");

            var p = StudioProject.Create(_dir, "Game");
            var result = LevelImporter.Import(p.Pack, new[] { src });

            Assert.AreEqual(3, result.Added.Count);
            CollectionAssert.AreEqual(new[] { "Img1", "Img2", "Img10" }, p.Pack.levels.Select(l => l.name).ToArray());
            foreach (var l in p.Pack.levels) Assert.IsTrue(File.Exists(p.Pack.Resolve(l.image)), l.image);
            Assert.AreEqual(3, p.Pack.levels.Select(l => l.id).Distinct().Count());
            Assert.IsFalse(PackValidator.Validate(p.Pack).HasErrors);
        }

        [Test]
        public void Import_SameFileNameTwiceGetsUniqueCopies()
        {
            string a = WriteImage(Path.Combine(_dir, "a"), "sunset.png");
            string b = WriteImage(Path.Combine(_dir, "b"), "sunset.png", 800, 600);
            var p = StudioProject.Create(_dir, "Game");
            LevelImporter.Import(p.Pack, new[] { a });
            LevelImporter.Import(p.Pack, new[] { b });
            Assert.AreEqual(2, p.Pack.levels.Count);
            Assert.AreNotEqual(p.Pack.levels[0].image, p.Pack.levels[1].image);
            Assert.AreNotEqual(p.Pack.levels[0].id, p.Pack.levels[1].id);
        }

        [Test]
        public void Remove_KeepsTheImageUntilTheUnusedFilesAreTidied()
        {
            var p = StudioProject.Create(_dir, "Game");
            LevelImporter.Import(p.Pack, new[] { WriteImage(Path.Combine(_dir, "src"), "x.png") });
            var original = p.Pack.levels[0];
            var copy = LevelImporter.Duplicate(p.Pack, original);
            string file = p.Pack.Resolve(original.image);

            LevelImporter.Remove(p.Pack, original);
            Assert.AreEqual(0, p.MoveUnusedFilesToTrash(), "still used by the duplicate");
            Assert.IsTrue(File.Exists(file));
            LevelImporter.Remove(p.Pack, copy);
            Assert.IsTrue(File.Exists(file), "kept during the session: undo can bring the level back");
            Assert.AreEqual(1, p.MoveUnusedFilesToTrash());
            Assert.IsFalse(File.Exists(file));
            Assert.IsTrue(File.Exists(Path.Combine(p.Root, StudioProject.TrashFolder, "levels", Path.GetFileName(file))), "moved to .trash, not deleted");
            Assert.AreEqual(0, p.Pack.levels.Count);
        }

        [Test]
        public void Names_ArePrettyAndNaturallyOrdered()
        {
            Assert.AreEqual("Sunset Hills 02", LevelImporter.PrettyName("sunset_hills-02"));
            Assert.Less(LevelImporter.NaturalCompare("img2", "img10"), 0);
            Assert.Greater(LevelImporter.NaturalCompare("b", "A"), 0);
        }

        // ---------------------------------------------------------------- themes

        [Test]
        public void Presets_AreAllReadable()
        {
            foreach (var id in ThemePresets.Names)
            {
                var pack = new GamePackData { theme = ThemePresets.Create(id) };
                var c = pack.theme.colors;
                float textOnSurface = ColorUtil.ContrastRatio(ColorUtil.Parse(c.text, Color.black), ColorUtil.Parse(c.surface, Color.white));
                float textOnBg = ColorUtil.ContrastRatio(ColorUtil.Parse(c.text, Color.black), ColorUtil.Parse(c.background, Color.white));
                Assert.GreaterOrEqual(textOnSurface, 4.5f, $"{id}: text on panels");
                Assert.GreaterOrEqual(textOnBg, 4.5f, $"{id}: text on background");
                var errors = PackValidator.Validate(pack, checkFiles: false).Issues.Where(i => i.code.StartsWith("theme.") && i.severity == IssueSeverity.Error);
                Assert.IsEmpty(errors, id);
            }
        }

        [Test]
        public void Randomizer_AlwaysProducesReadableThemes()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var c = ThemeRandomizer.Generate(seed).colors;
                var text = ColorUtil.Parse(c.text, Color.black);
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.surface, Color.white)), 4.5f, $"seed {seed}");
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.background, Color.white)), 4.5f, $"seed {seed}");
            }
        }

        [Test]
        public void ApplyPreset_KeepsCustomFiles()
        {
            var pack = new GamePackData();
            pack.theme.background.image = "theme/bg.png";
            pack.theme.font.heading = "theme/my.ttf";
            ThemePresets.Apply(pack, "DarkNeon");
            Assert.AreEqual("DarkNeon", pack.theme.preset);
            Assert.AreEqual("#12101F", pack.theme.colors.background);
            Assert.AreEqual("theme/bg.png", pack.theme.background.image);
            Assert.AreEqual("theme/my.ttf", pack.theme.font.heading);
        }

        // ---------------------------------------------------------------- icons

        [Test]
        public void Icon_BuildsAllSizesAndValidIco()
        {
            var src = new Texture2D(300, 200, TextureFormat.RGBA32, false);
            var images = IconBuilder.Build(src, roundCorners: true);
            Object.DestroyImmediate(src);
            CollectionAssert.AreEqual(IconBuilder.Sizes, images.Select(i => i.Size).ToArray());

            byte[] ico = IconBuilder.BuildIcoFile(images);
            Assert.AreEqual(0, ico[0] | ico[1] << 8);              // reserved
            Assert.AreEqual(1, ico[2] | ico[3] << 8);              // type = icon
            Assert.AreEqual(images.Count, ico[4] | ico[5] << 8);   // count
            var png = images.Last().Data;
            Assert.AreEqual(0x89, png[0], "256px entry is PNG");
            Assert.AreEqual(40, images[0].Data[0], "small entries are DIBs");
        }

        [Test]
        public void Icon_InjectionReplacesExeIcons()
        {
            string template = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Template", "Game.exe"));
            Assume.That(File.Exists(template), "Build the Player Template first");
            string exe = Path.Combine(_dir, "Copy.exe");
            File.Copy(template, exe);

            var src = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var images = IconBuilder.Build(src, roundCorners: false);
            Object.DestroyImmediate(src);
            IconInjector.Inject(exe, images);

            var (groups, icons) = IconInjector.ListIcons(exe);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(IconBuilder.Sizes.Length, icons.Count);
        }
    }
}
