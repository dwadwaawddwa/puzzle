using System.IO;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Tests
{
    public class PackTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PuzzleStudioTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_dir, "levels"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        void WriteImage(string relative, int w, int h, Color color, bool jpg = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(color, Color.black, (i % w) / (float)w);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(_dir, relative), jpg ? tex.EncodeToJPG() : tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        GamePackData ValidPack()
        {
            WriteImage("levels/01.png", 600, 600, Color.red);
            WriteImage("levels/02.png", 800, 600, Color.blue);
            var pack = new GamePackData { RootPath = _dir };
            pack.game.title = "Test Game";
            pack.game.developer = "Me";
            pack.levels.Add(new LevelConfig { id = "a", image = "levels/01.png", name = "A" });
            pack.levels.Add(new LevelConfig { id = "b", image = "levels/02.png", name = "B" });
            return pack;
        }

        // ---------------------------------------------------------------- JSON

        [Test]
        public void Json_RoundTripKeepsValuesAndNulls()
        {
            var pack = new GamePackData();
            pack.game.title = "Round Trip";
            pack.gameplay.defaultMode = ModeIds.SwapTiles;
            pack.gameplay.difficultyCurve = DifficultyCurve.Fixed;
            pack.theme.background.gradient = new System.Collections.Generic.List<string> { "#111111", "#222222", "#333333" };
            pack.levels.Add(new LevelConfig { id = "x", image = "levels/x.png", grid = new GridOverride { cols = 5, rows = 4 }, seed = 7 });
            pack.levels.Add(new LevelConfig { id = "y", image = "levels/y.png" });

            string json = GamePackWriter.ToJson(pack);
            StringAssert.Contains("\"difficultyCurve\": \"Fixed\"", json, "enums are written as text");
            StringAssert.Contains("\"mode\": null", json, "null overrides are kept explicit");

            var back = GamePackLoader.Parse(json);
            Assert.AreEqual("Round Trip", back.game.title);
            Assert.AreEqual(DifficultyCurve.Fixed, back.gameplay.difficultyCurve);
            Assert.AreEqual(3, back.theme.background.gradient.Count, "lists are replaced, not appended to defaults");
            Assert.AreEqual(5, back.levels[0].grid.cols);
            Assert.AreEqual(7, back.levels[0].seed);
            Assert.IsNull(back.levels[1].grid);
            Assert.IsNull(back.levels[1].mode);
            Assert.IsNull(back.levels[1].seed);
        }

        [Test]
        public void Json_MissingFieldsGetDefaults()
        {
            var pack = GamePackLoader.Parse("{ \"packVersion\": 1, \"game\": { \"title\": \"Tiny\" }, \"levels\": [ { \"image\": \"a.png\" } ] }");
            Assert.AreEqual("Tiny", pack.game.title);
            Assert.AreEqual(ModeIds.SwapTiles, pack.gameplay.defaultMode);
            Assert.AreEqual("#F6EFE7", pack.theme.colors.background);
            Assert.AreEqual("lvl_01", pack.levels[0].id, "missing ids are generated");
            Assert.IsNotNull(pack.levels[0].crop);
            Assert.AreEqual(7, pack.audio.sfx.Count);
        }

        [Test]
        public void Json_ExplicitNullSectionsAreRepaired()
        {
            var pack = GamePackLoader.Parse("{ \"packVersion\": 1, \"theme\": null, \"audio\": { \"sfx\": null }, \"levels\": [ { \"crop\": null } ] }");
            Assert.IsNotNull(pack.theme.colors);
            Assert.IsNotNull(pack.audio.sfx);
            Assert.IsNotNull(pack.levels[0].crop);
        }

        [Test]
        public void Json_InvalidThrowsReadableError()
        {
            var ex = Assert.Throws<PackLoadException>(() => GamePackLoader.Parse("{ not json"));
            StringAssert.Contains("not valid JSON", ex.Message);
        }

        // ---------------------------------------------------------------- migration

        [Test]
        public void Migration_V0FlatFormatBecomesV1()
        {
            const string v0 = "{ \"title\": \"Old Game\", \"mode\": \"SwapTiles\", \"grid\": 5, \"images\": [\"levels/a.png\", \"levels/b.jpg\"] }";
            var pack = GamePackLoader.Parse(v0);
            Assert.AreEqual(GamePackData.CurrentVersion, pack.packVersion);
            Assert.AreEqual("Old Game", pack.game.title);
            Assert.AreEqual(DifficultyCurve.Fixed, pack.gameplay.difficultyCurve);
            Assert.AreEqual(5, pack.gameplay.fixedGrid);
            Assert.AreEqual(2, pack.levels.Count);
            Assert.AreEqual("lvl_02", pack.levels[1].id);
            Assert.AreEqual("levels/b.jpg", pack.levels[1].image);
            Assert.AreEqual("b", pack.levels[1].name);
        }

        [Test]
        public void Migration_FutureVersionIsRejected()
        {
            Assert.Throws<PackLoadException>(() => GamePackLoader.Parse("{ \"packVersion\": 999 }"));
        }

        // ---------------------------------------------------------------- loader / validator

        [Test]
        public void Loader_ReadsFolderAndResolvesPaths()
        {
            var pack = ValidPack();
            GamePackWriter.WriteJson(pack, _dir);
            var loaded = GamePackLoader.LoadFromDirectory(_dir);
            Assert.AreEqual(2, loaded.levels.Count);
            Assert.IsTrue(File.Exists(loaded.Resolve(loaded.levels[0].image)));
            Assert.IsNull(loaded.Resolve("default:calm_01"));
        }

        [Test]
        public void Validator_ValidPackHasNoErrors()
        {
            var report = PackValidator.Validate(ValidPack());
            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Issues));
        }

        [Test]
        public void Validator_DetectsProblems()
        {
            var pack = ValidPack();
            pack.game.title = " ";
            pack.levels.Add(new LevelConfig { id = "a", image = "levels/missing.png" });
            pack.levels.Add(new LevelConfig { id = "c", image = "levels/01.png", mode = "Nope" });
            pack.theme.colors.text = "#FFFFFF";
            pack.theme.colors.surface = "#FAFAFA";
            pack.theme.colors.accent = "not-a-color";

            var r = PackValidator.Validate(pack);
            Assert.IsTrue(r.HasErrors);
            Assert.IsTrue(r.Has("game.title.empty"));
            Assert.IsTrue(r.Has("level.id.duplicate"));
            Assert.IsTrue(r.Has("level.image.missing"));
            Assert.IsTrue(r.Has("level.mode.unknown"));
            Assert.IsTrue(r.Has("level.image.duplicate"));
            Assert.IsTrue(r.Has("theme.contrast.text-surface"));
            Assert.IsTrue(r.Has("theme.color.invalid"));
        }

        [Test]
        public void Validator_WarnsAboutSmallAndExtremeImages()
        {
            WriteImage("levels/small.png", 200, 150, Color.green);
            WriteImage("levels/wide.jpg", 2000, 500, Color.yellow, jpg: true);
            var pack = ValidPack();
            pack.levels.Add(new LevelConfig { id = "s", image = "levels/small.png" });
            pack.levels.Add(new LevelConfig { id = "w", image = "levels/wide.jpg" });
            var r = PackValidator.Validate(pack);
            Assert.IsTrue(r.Issues.Exists(i => i.code == "level.image.small" && i.levelId == "s"));
            Assert.IsTrue(r.Issues.Exists(i => i.code == "level.image.aspect" && i.levelId == "w"));
        }

        [Test]
        public void Validator_EmptyPackIsAnError()
        {
            var r = PackValidator.Validate(new GamePackData(), checkFiles: false);
            Assert.IsTrue(r.Has("levels.empty"));
        }

        // ---------------------------------------------------------------- images

        [Test]
        public void ImageHeader_ReadsPngAndJpgSizes()
        {
            WriteImage("levels/p.png", 321, 123, Color.red);
            WriteImage("levels/j.jpg", 640, 360, Color.red, jpg: true);
            Assert.IsTrue(ImageHeaderReader.TryReadSize(Path.Combine(_dir, "levels/p.png"), out int w, out int h));
            Assert.AreEqual((321, 123), (w, h));
            Assert.IsTrue(ImageHeaderReader.TryReadSize(Path.Combine(_dir, "levels/j.jpg"), out w, out h));
            Assert.AreEqual((640, 360), (w, h));
        }

        [Test]
        public void TextureLoader_DownscalesLargeImages()
        {
            WriteImage("levels/big.png", 1000, 500, Color.cyan);
            var tex = TextureLoader.Load(Path.Combine(_dir, "levels/big.png"), maxSize: 256);
            Assert.IsNotNull(tex);
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(128, tex.height);
            Object.DestroyImmediate(tex);
            Assert.IsNull(TextureLoader.Load(Path.Combine(_dir, "levels/none.png")));
        }

        [Test]
        public void ImageSlicer_CellUvsTileTheCrop()
        {
            var crop = new CropRect(0.1f, 0.2f, 0.8f, 0.6f);
            var topLeft = ImageSlicer.CellUV(crop, 4, 3, 0, 0);
            var bottomRight = ImageSlicer.CellUV(crop, 4, 3, 3, 2);
            Assert.AreEqual(0.1f, topLeft.x, 1e-5);
            Assert.AreEqual(0.8f, topLeft.yMax, 1e-5, "row 0 is at the top of the crop (UV y = 1 - crop.y)");
            Assert.AreEqual(0.9f, bottomRight.xMax, 1e-5);
            Assert.AreEqual(0.2f, bottomRight.y, 1e-5);
            Assert.AreEqual(0.2f, topLeft.width, 1e-5);
            Assert.AreEqual(0.2f, topLeft.height, 1e-5);
        }

        [Test]
        public void ImageSlicer_SquareCellCropMakesSquareCells()
        {
            var crop = ImageSlicer.SquareCellCrop(1920, 1080, new CropRect(), 4, 4);
            float cellW = 1920 * crop.w / 4, cellH = 1080 * crop.h / 4;
            Assert.AreEqual(cellW, cellH, 0.01f);
            Assert.AreEqual(0.5f, crop.x + crop.w / 2, 1e-4, "stays centred");
        }

        // ---------------------------------------------------------------- grid resolver

        [Test]
        public void GridResolver_ProgressiveCurveAndRatio()
        {
            var pack = new GamePackData();
            pack.gameplay.difficultyCurve = DifficultyCurve.Progressive;
            pack.gameplay.minGrid = 3;
            pack.gameplay.maxGrid = 7;
            for (int i = 0; i < 5; i++) pack.levels.Add(new LevelConfig { id = "l" + i });

            var first = GridResolver.Resolve(pack, 0, 1000, 1000);
            var last = GridResolver.Resolve(pack, 4, 1000, 1000);
            var wide = GridResolver.Resolve(pack, 4, 1920, 1080);
            Assert.AreEqual((3, 3), (first.Layout.Cols, first.Layout.Rows));
            Assert.AreEqual((7, 7), (last.Layout.Cols, last.Layout.Rows));
            Assert.Greater(wide.Layout.Cols, wide.Layout.Rows, "wide images get more columns");
            Assert.AreEqual(first.Seed, GridResolver.Resolve(pack, 0, 1000, 1000).Seed, "stable seed");
        }

        [Test]
        public void GridResolver_LevelOverrideWins()
        {
            var pack = new GamePackData();
            pack.levels.Add(new LevelConfig { id = "a", grid = new GridOverride { cols = 6, rows = 2 }, seed = 5 });
            var s = GridResolver.Resolve(pack, 0, 1000, 1000);
            Assert.AreEqual((6, 2), (s.Layout.Cols, s.Layout.Rows));
            Assert.AreEqual(5, s.Seed);
        }

        // ---------------------------------------------------------------- colors

        [Test]
        public void Color_ParseAndContrast()
        {
            Assert.IsTrue(ColorUtil.TryParseHex("#FFF", out var white));
            Assert.IsTrue(ColorUtil.TryParseHex("000000", out var black));
            Assert.IsTrue(ColorUtil.TryParseHex("#FF000080", out var halfRed));
            Assert.AreEqual(0.5f, halfRed.a, 0.01f);
            Assert.IsFalse(ColorUtil.TryParseHex("#12", out _));
            Assert.AreEqual(21f, ColorUtil.ContrastRatio(white, black), 0.01f);
            Assert.AreEqual(1f, ColorUtil.ContrastRatio(white, white), 0.001f);
            Assert.AreEqual("#E07A5F", ColorUtil.ToHex(ColorUtil.Parse("#E07A5F", Color.clear)));
        }
    }
}
