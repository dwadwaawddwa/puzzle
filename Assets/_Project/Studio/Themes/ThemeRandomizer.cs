using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Studio.Themes
{
    /// <summary>Generates coherent random themes: harmonious hues, light or dark, readable text (WCAG AA).</summary>
    public static class ThemeRandomizer
    {
        public static ThemeConfig Generate(int seed)
        {
            var rng = new SeededRandom(seed);
            float baseHue = rng.NextFloat();
            bool dark = rng.NextFloat() < 0.4f;
            int scheme = rng.Next(3); // 0 analogous, 1 complementary, 2 triadic
            float h2 = scheme == 0 ? baseHue + 0.08f : scheme == 1 ? baseHue + 0.5f : baseHue + 0.333f;
            float h3 = scheme == 0 ? baseHue - 0.08f : scheme == 1 ? baseHue + 0.42f : baseHue + 0.666f;

            Color background, surface, text, muted;
            if (dark)
            {
                background = Hsv(baseHue, 0.35f, 0.12f);
                surface = Hsv(baseHue, 0.3f, 0.2f);
                text = Hsv(baseHue, 0.06f, 0.96f);
                muted = Hsv(baseHue, 0.12f, 0.7f);
            }
            else
            {
                background = Hsv(baseHue, 0.08f + rng.NextFloat() * 0.06f, 0.96f);
                surface = Hsv(baseHue, 0.02f, 1f);
                text = Hsv(baseHue, 0.45f, 0.24f);
                muted = Hsv(baseHue, 0.18f, 0.55f);
            }
            Color primary = Hsv(baseHue, 0.55f + rng.NextFloat() * 0.25f, dark ? 0.95f : 0.82f);
            Color secondary = Hsv(h2, 0.4f + rng.NextFloat() * 0.25f, dark ? 0.85f : 0.72f);
            Color accent = Hsv(h3, 0.6f, 0.95f);

            text = EnsureContrast(text, background, 4.5f, dark);
            text = EnsureContrast(text, surface, 4.5f, dark);
            muted = EnsureContrast(muted, surface, 3f, dark);

            var shapes = new[] { ButtonShape.Pill, ButtonShape.Rounded, ButtonShape.Square };
            var panels = dark ? new[] { PanelStyle.Soft, PanelStyle.Glass, PanelStyle.Outlined } : new[] { PanelStyle.Soft, PanelStyle.Flat, PanelStyle.Outlined };
            var fonts = new[] { "Default:Rounded", "Default:Clean", "Default:Playful", "Default:Elegant" };

            var t = new ThemeConfig { preset = "Random" };
            t.colors = new ThemeColors
            {
                background = ColorUtil.ToHex(background),
                surface = ColorUtil.ToHex(surface),
                primary = ColorUtil.ToHex(primary),
                secondary = ColorUtil.ToHex(secondary),
                text = ColorUtil.ToHex(text),
                textMuted = ColorUtil.ToHex(muted),
                accent = ColorUtil.ToHex(accent),
                success = ColorUtil.ToHex(Hsv(0.36f, 0.55f, dark ? 0.85f : 0.65f)),
                highlight = ColorUtil.ToHex(accent),
                highlightColorblind = ColorUtil.ToHex(Hsv(0.6f, 0.75f, 1f)),
            };
            t.background.type = BackgroundType.AnimatedGradient;
            t.background.gradient = new List<string> { t.colors.background, ColorUtil.ToHex(ColorUtil.Shade(background, dark ? 0.06f : -0.05f)) };
            t.background.pattern = (BackgroundPattern)rng.Next(5);
            t.pieces.cornerRadius = new[] { 2f, 6f, 10f, 16f }[rng.Next(4)];
            t.pieces.gap = 2 + rng.Next(6);
            t.pieces.borderWidth = rng.Next(4);
            t.pieces.borderColor = dark ? ColorUtil.ToHex(ColorUtil.Shade(surface, 0.1f)) : "#FFFFFF";
            t.uiStyle.buttonShape = shapes[rng.Next(shapes.Length)];
            t.uiStyle.panelStyle = panels[rng.Next(panels.Length)];
            t.uiStyle.cornerRadius = 8 + rng.Next(16);
            t.font.heading = fonts[rng.Next(fonts.Length)];
            t.font.body = rng.NextFloat() < 0.6f ? "Default:Clean" : "Default:Rounded";
            t.particles.victory = (ParticleKind)(1 + rng.Next(3));
            return t;
        }

        static Color Hsv(float h, float s, float v) => Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));

        /// <summary>Darkens (light theme) or lightens (dark theme) <paramref name="fg"/> until it reaches the ratio.</summary>
        public static Color EnsureContrast(Color fg, Color bg, float ratio, bool lighten) =>
            ColorUtil.EnsureContrast(fg, bg, ratio, lighten);

        public static void Apply(GamePackData pack, int seed)
        {
            string bgImage = pack.theme.background.image;
            pack.theme = Generate(seed);
            pack.theme.background.image = bgImage;
        }
    }
}
