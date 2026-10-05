using System.Collections.Generic;
using NUnit.Framework;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.FX;
using PuzzleStudio.Game.UI;
using PuzzleStudio.Studio.Themes;
using UnityEngine;

namespace PuzzleStudio.Tests
{
    /// <summary>Colors from pictures: OKLab, palette extraction and who gets which color.</summary>
    public class ThemeColorTests
    {
        static readonly Color32 Sky = new Color32(110, 165, 230, 255);
        static readonly Color32 Grass = new Color32(70, 160, 80, 255);
        static readonly Color32 Poppy = new Color32(220, 50, 60, 255);
        static readonly Color32 Sun = new Color32(240, 205, 60, 255);

        /// <summary>A "picture" made of colored areas (shares in %).</summary>
        static Color32[] Picture(params (Color32 color, int percent)[] areas)
        {
            var px = new List<Color32>();
            foreach (var (color, percent) in areas)
                for (int i = 0; i < percent * 20; i++) px.Add(color);
            return px.ToArray();
        }

        static float HueOf(Color c) => Oklab.FromColor(c).Hue;
        static float HueOf(string hex) => HueOf(ColorUtil.Parse(hex, Color.black));

        // ---------------------------------------------------------------- OKLab

        [Test]
        public void Oklab_RoundTripsAndStaysInGamut()
        {
            var rng = new SeededRandom(5);
            for (int i = 0; i < 200; i++)
            {
                var c = new Color(rng.NextFloat(), rng.NextFloat(), rng.NextFloat());
                var back = Oklab.ToColor(Oklab.FromColor(c));
                Assert.AreEqual(c.r, back.r, 2e-3f);
                Assert.AreEqual(c.g, back.g, 2e-3f);
                Assert.AreEqual(c.b, back.b, 2e-3f);
            }
            // A very saturated request at a light level is brought back into the screen colors, same hue and lightness.
            var pastel = Oklab.FromLch(0.95f, 0.3f, 250f);
            var lab = Oklab.FromColor(pastel);
            Assert.AreEqual(0.95f, lab.L, 0.01f);
            Assert.Less(Oklab.HueDistance(lab.Hue, 250f), 4f);
            Assert.Less(lab.Chroma, 0.3f);
        }

        // ---------------------------------------------------------------- palette

        [Test]
        public void Palette_MergesShadesThatLookTheSame()
        {
            var px = Picture((new Color32(100, 150, 220, 255), 30), (new Color32(102, 152, 222, 255), 30), (Poppy, 40));
            var palette = PaletteExtractor.Extract(px);
            Assert.AreEqual(2, palette.Count, "two blues that look identical are one color");
            Assert.AreEqual(0.6f, palette[0].Weight, 0.02f);
        }

        [Test]
        public void Palette_SortsByPresence()
        {
            var palette = PaletteExtractor.Extract(Picture((Grass, 20), (Sky, 55), (Poppy, 15), (Sun, 10)));
            Assert.AreEqual(4, palette.Count);
            Assert.Less(Oklab.HueDistance(palette[0].Hue, HueOf(Sky)), 5f, "sky first");
            Assert.Less(Oklab.HueDistance(palette[1].Hue, HueOf(Grass)), 5f, "grass second");
            for (int i = 1; i < palette.Count; i++) Assert.GreaterOrEqual(palette[i - 1].Weight, palette[i].Weight);
        }

        // ---------------------------------------------------------------- derivation

        [Test]
        public void Derivation_DominantForTheBackground_OthersForTheButtons([Values("CozyPastel", "DarkNeon")] string preset)
        {
            var baseColors = ThemePresets.Create(preset).colors;
            var palette = PaletteExtractor.Extract(Picture((Sky, 55), (Grass, 20), (Poppy, 15), (Sun, 10)));
            var c = ThemeDerivation.FromPalette(baseColors, palette, AutoColors.Full);

            Assert.Less(Oklab.HueDistance(HueOf(c.background), HueOf(Sky)), 15f, "background = dominant (sky)");
            Assert.Less(Oklab.HueDistance(HueOf(c.background2), HueOf(Grass)), 20f, "gradient end = 2nd color (grass)");
            Assert.Less(Oklab.HueDistance(HueOf(c.background3), HueOf(Poppy)), 20f, "glow = 3rd color (poppies)");
            Assert.Greater(Oklab.HueDistance(HueOf(c.primary), HueOf(Sky)), 40f, "buttons are not the background color");
            Assert.Less(Oklab.HueDistance(HueOf(c.primary), HueOf(Poppy)), 20f, "buttons = the most colorful other color (poppies)");
            Assert.Greater(Oklab.HueDistance(HueOf(c.secondary), HueOf(c.primary)), 30f, "secondary differs from primary");

            float bgL = Oklab.FromColor(ColorUtil.Parse(c.background, Color.black)).L;
            if (preset == "DarkNeon") Assert.Less(bgL, 0.3f); else Assert.Greater(bgL, 0.9f);
        }

        [Test]
        public void Derivation_SameLightnessWhateverTheHue()
        {
            var light = new ThemeColors();
            float yellowL = Oklab.FromColor(ColorUtil.Parse(ThemeDerivation.FromPalette(light, PaletteExtractor.Extract(Picture((Sun, 100))), AutoColors.Background).background, Color.black)).L;
            float blueL = Oklab.FromColor(ColorUtil.Parse(ThemeDerivation.FromPalette(light, PaletteExtractor.Extract(Picture((new Color32(30, 40, 160, 255), 100))), AutoColors.Background).background, Color.black)).L;
            Assert.AreEqual(yellowL, blueL, 0.01f, "a yellow and a dark blue picture give equally light backgrounds");
        }

        [Test]
        public void Derivation_SingleHuePictureStaysInHarmony()
        {
            var palette = PaletteExtractor.Extract(Picture((new Color32(40, 80, 160, 255), 50), (new Color32(90, 140, 220, 255), 30), (new Color32(170, 200, 245, 255), 20)));
            var c = ThemeDerivation.FromPalette(new ThemeColors(), palette, AutoColors.Full);
            Assert.Less(Oklab.HueDistance(HueOf(c.primary), palette[0].Hue), 30f, "only blues: blue buttons");
            Assert.Less(Oklab.HueDistance(HueOf(c.background2), HueOf(c.background)), 40f, "gradient stays close");
        }

        [Test]
        public void Derivation_GrayPictureKeepsTheThemeButtons()
        {
            var baseColors = new ThemeColors();
            var palette = PaletteExtractor.Extract(Picture((new Color32(120, 120, 120, 255), 60), (new Color32(200, 200, 200, 255), 40)));
            var c = ThemeDerivation.FromPalette(baseColors, palette, AutoColors.Full);
            Assert.AreEqual(baseColors.primary, c.primary);
            Assert.AreEqual(baseColors.accent, c.accent);
            Assert.Less(Oklab.FromColor(ColorUtil.Parse(c.background, Color.black)).Chroma, 0.02f, "gray background");
        }

        [Test]
        public void Derivation_EveryPaletteStaysReadable()
        {
            var rng = new SeededRandom(11);
            foreach (var preset in new[] { "CozyPastel", "DarkNeon", "MinimalWhite", "Forest" })
            {
                var baseColors = ThemePresets.Create(preset).colors;
                for (int n = 0; n < 150; n++)
                {
                    var areas = new List<(Color32, int)>();
                    int count = 1 + rng.Next(6);
                    for (int i = 0; i < count; i++)
                        areas.Add((new Color32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), 255), 5 + rng.Next(40)));
                    var c = ThemeDerivation.FromPalette(baseColors, PaletteExtractor.Extract(Picture(areas.ToArray())), AutoColors.Full);
                    var p = ThemePalette.From(c);
                    foreach (var bg in new[] { p.Background, p.Background2, p.Background3, p.Surface })
                        Assert.GreaterOrEqual(ColorUtil.ContrastRatio(p.Text, bg), 4.5f - 1e-3f, $"{preset} #{n}");
                    // Buttons made from the picture stand out from the panels (a gray picture keeps the theme's own buttons).
                    if (c.primary != baseColors.primary)
                        Assert.GreaterOrEqual(ColorUtil.ContrastRatio(p.Primary, p.Surface), 3f - 1e-3f, $"{preset} #{n} button");
                    Assert.GreaterOrEqual(ColorUtil.ContrastRatio(p.OnPrimary, p.Primary), 2.5f, $"{preset} #{n} button label");
                }
            }
        }

        // ---------------------------------------------------------------- missing pictures

        [Test]
        public void Placeholder_TilesAllLookDifferent()
        {
            var tex = TextureLoader.Placeholder(240);
            try
            {
                Assert.IsTrue(TextureLoader.IsPlaceholder(tex));
                var px = tex.GetPixels32();
                var means = new List<Lab>();
                for (int ty = 0; ty < 3; ty++)
                    for (int tx = 0; tx < 3; tx++)
                    {
                        float r = 0, g = 0, b = 0;
                        for (int y = ty * 80; y < ty * 80 + 80; y++)
                            for (int x = tx * 80; x < tx * 80 + 80; x++) { var c = px[y * 240 + x]; r += c.r; g += c.g; b += c.b; }
                        means.Add(Oklab.FromColor(new Color(r / 6400f / 255f, g / 6400f / 255f, b / 6400f / 255f)));
                    }
                for (int i = 0; i < means.Count; i++)
                    for (int j = i + 1; j < means.Count; j++)
                        Assert.Greater(Oklab.Distance(means[i], means[j]), 0.02f, $"tiles {i} and {j} can be told apart");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ---------------------------------------------------------------- background colors

        [Test]
        public void Background_UsesPictureColorsDuringLevelsAndTheGradientOtherwise()
        {
            var bg = new BackgroundConfig { gradient = new List<string> { "#F6EFE7", "#EADBC8", "#FFFFFF" } };
            var theme = ThemePalette.From(new ThemeColors());
            var (a, b, c) = BackgroundFill.Colors(bg, theme, perLevelColors: false);
            Assert.AreEqual(ColorUtil.Parse("#EADBC8", Color.black), b);
            Assert.AreEqual(Color.white, c);

            var derived = ThemeDerivation.FromPalette(new ThemeColors(), PaletteExtractor.Extract(Picture((Sky, 60), (Grass, 40))), AutoColors.Background);
            var level = ThemePalette.From(derived);
            Assert.IsTrue(level.HasBackgroundColors);
            var (la, lb, lc) = BackgroundFill.Colors(bg, level, perLevelColors: true);
            Assert.AreEqual(level.Background, la);
            Assert.AreEqual(level.Background2, lb, "gradient end from the picture, not the theme");
            Assert.AreEqual(level.Background3, lc);
        }
    }
}
