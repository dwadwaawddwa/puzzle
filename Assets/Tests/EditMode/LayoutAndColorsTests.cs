using System.Collections.Generic;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Tests
{
    public class LayoutAndColorsTests
    {
        // ---------------------------------------------------------------- palette

        [Test]
        public void Palette_FindsTheTwoMainColors()
        {
            var px = new Color32[40 * 40];
            for (int i = 0; i < px.Length; i++)
                px[i] = i % 40 < 30 ? new Color32(220, 40, 40, 255) : new Color32(30, 60, 200, 255);
            var palette = PaletteExtractor.Extract(px, k: 4);
            Assert.GreaterOrEqual(palette.Count, 2);
            Assert.Greater(palette[0].Color.r, 0.7f, "red dominates");
            Assert.AreEqual(0.75f, palette[0].Weight, 0.02f);
            Assert.IsTrue(palette.Exists(s => s.Color.b > 0.6f && s.Color.r < 0.3f), "blue found");
        }

        [Test]
        public void Palette_IsDeterministic()
        {
            var rng = new SeededRandom(3);
            var px = new Color32[32 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), 255);
            var a = PaletteExtractor.Extract(px);
            var b = PaletteExtractor.Extract(px);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Color, b[i].Color);
        }

        static List<PaletteExtractor.Swatch> RandomPalette(int seed)
        {
            var rng = new SeededRandom(seed);
            var list = new List<PaletteExtractor.Swatch>();
            for (int i = 0; i < 5; i++)
            {
                var c = new Color(rng.NextFloat(), rng.NextFloat(), rng.NextFloat());
                list.Add(PaletteExtractor.Swatch.Of(c, 0.2f));
            }
            return list;
        }

        [Test]
        public void Derivation_KeepsTextReadable([Values(AutoColors.Background, AutoColors.Full)] AutoColors mode,
                                                 [Values("CozyPastel", "DarkNeon", "MinimalWhite", "Forest")] string preset)
        {
            var baseColors = PuzzleStudio.Studio.Themes.ThemePresets.Create(preset).colors;
            for (int seed = 0; seed < 60; seed++)
            {
                var c = ThemeDerivation.FromPalette(baseColors, RandomPalette(seed), mode);
                var text = ColorUtil.Parse(c.text, Color.black);
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.background, Color.white)), 4.5f, $"{preset} seed {seed} bg");
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.surface, Color.white)), 4.5f, $"{preset} seed {seed} surface");
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.background2, Color.white)), 4.5f, $"{preset} seed {seed} gradient end");
                Assert.GreaterOrEqual(ColorUtil.ContrastRatio(text, ColorUtil.Parse(c.background3, Color.white)), 4.5f, $"{preset} seed {seed} glow");
                if (mode == AutoColors.Full)
                    Assert.GreaterOrEqual(ColorUtil.ContrastRatio(ColorUtil.Parse(c.primary, Color.white), ColorUtil.Parse(c.surface, Color.white)), 3f - 1e-3f, $"{preset} seed {seed} buttons stand out");
            }
        }

        [Test]
        public void Derivation_KeepsLightOrDarkCharacter()
        {
            var light = new ThemeColors();
            var dark = PuzzleStudio.Studio.Themes.ThemePresets.Create("DarkNeon").colors;
            var p = RandomPalette(9);
            Assert.Greater(ColorUtil.RelativeLuminance(ColorUtil.Parse(ThemeDerivation.FromPalette(light, p, AutoColors.Full).background, Color.black)), 0.6f);
            Assert.Less(ColorUtil.RelativeLuminance(ColorUtil.Parse(ThemeDerivation.FromPalette(dark, p, AutoColors.Full).background, Color.white)), 0.1f);
            Assert.AreEqual(light.background, ThemeDerivation.FromPalette(light, p, AutoColors.Off).background, "Off keeps colors");
        }

        // ---------------------------------------------------------------- layout data

        [Test]
        public void Layout_JsonRoundTripAndRepair()
        {
            var pack = new GamePackData();
            pack.layout.gameplay["title"] = new LayoutItem { x = 0.2f, y = 0.3f, scale = 1.5f, visible = false };
            pack.layout.boardArea = new LayoutRect(0.1f, 0.2f, 0.6f, 0.5f);
            var back = GamePackLoader.Parse(GamePackWriter.ToJson(pack));
            var t = back.layout.Get(LayoutConfig.GameplayScreen, "title");
            Assert.AreEqual(0.2f, t.x, 1e-5f);
            Assert.AreEqual(1.5f, t.scale, 1e-5f);
            Assert.IsFalse(t.visible);
            Assert.AreEqual(0.6f, back.layout.boardArea.w, 1e-5f);

            var repaired = GamePackLoader.Parse("{ \"packVersion\": 1, \"layout\": { \"menu\": null } }");
            Assert.IsNotNull(repaired.layout.menu);
            Assert.IsNull(repaired.layout.boardArea);
        }

        [Test]
        public void LayoutService_DefaultAndOverride()
        {
            var root = new VisualElement();
            var title = LayoutService.Tag(new VisualElement(), "title");
            root.Add(title);
            var config = new LayoutConfig();

            LayoutService.Apply(root, LayoutConfig.GameplayScreen, config);
            var slot = LayoutService.FindSlot(LayoutConfig.GameplayScreen, "title");
            Assert.AreEqual(slot.DefaultPosition.x * 100f, title.style.left.value.value, 1e-3f);
            Assert.AreEqual(LengthUnit.Percent, title.style.left.value.unit);

            config.gameplay["title"] = new LayoutItem { x = 0.5f, y = 0.25f, scale = 2f, visible = false };
            LayoutService.Apply(root, LayoutConfig.GameplayScreen, config);
            Assert.AreEqual(50f, title.style.left.value.value, 1e-3f);
            Assert.AreEqual(25f, title.style.top.value.value, 1e-3f);
            Assert.AreEqual(2f, title.style.scale.value.value.x, 1e-3f);
            Assert.AreEqual(DisplayStyle.None, title.style.display.value);
        }

        [Test]
        public void BoardArea_DefaultLeavesRoomForDecorations()
        {
            var pack = new GamePackData();
            var plain = LayoutService.BoardArea(pack, 16f / 9f, 1f);
            pack.theme.background.decorLeft = "theme/left.png";
            var withDecor = LayoutService.BoardArea(pack, 16f / 9f, 1f);
            Assert.Greater(withDecor.x, plain.x);
            Assert.AreEqual(1f, withDecor.x + withDecor.width + withDecor.x, 1e-4f, "centered");

            pack.layout.boardArea = new LayoutRect(0.3f, 0.1f, 0.4f, 0.8f);
            var custom = LayoutService.BoardArea(pack, 16f / 9f, 1f);
            Assert.AreEqual(new Rect(0.3f, 0.1f, 0.4f, 0.8f), custom);
        }
    }
}
