using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Builds theme colors from a picture palette while keeping the base theme's character (light or dark)
    /// and readable text (WCAG AA): used for per-level colors and "Generate palette from images".
    /// </summary>
    public static class ThemeDerivation
    {
        public static ThemeColors FromPalette(ThemeColors baseColors, IList<PaletteExtractor.Swatch> palette, AutoColors mode)
        {
            var c = Copy(baseColors);
            if (palette == null || palette.Count == 0 || mode == AutoColors.Off) return c;

            var baseBg = ColorUtil.Parse(baseColors.background, Color.white);
            bool dark = ColorUtil.RelativeLuminance(baseBg) < 0.35f;
            var text = ColorUtil.Parse(baseColors.text, dark ? Color.white : Color.black);
            var muted = ColorUtil.Parse(baseColors.textMuted, Color.gray);

            Color.RGBToHSV(palette[0].Color, out float h, out float s, out _);
            var bg = dark
                ? Color.HSVToRGB(h, Mathf.Clamp(s * 0.6f, 0.18f, 0.55f), 0.14f)
                : Color.HSVToRGB(h, Mathf.Clamp(s * 0.45f, 0.05f, 0.26f), 0.95f);
            c.background = ColorUtil.ToHex(bg);

            Color surface = ColorUtil.Parse(baseColors.surface, Color.white);
            if (mode == AutoColors.Full)
            {
                surface = dark
                    ? Color.HSVToRGB(h, Mathf.Clamp(s * 0.5f, 0.15f, 0.45f), 0.22f)
                    : Color.HSVToRGB(h, Mathf.Min(s * 0.12f, 0.05f), 1f);
                c.surface = ColorUtil.ToHex(surface);

                var vivid = MostVivid(palette, -1f);
                Color.RGBToHSV(vivid, out float ph, out float ps, out float pv);
                var primary = Color.HSVToRGB(ph, Mathf.Clamp(ps, 0.45f, 0.85f), Mathf.Clamp(pv, 0.55f, 0.9f));
                primary = ColorUtil.EnsureContrast(primary, surface, 3f, lighten: dark);
                c.primary = ColorUtil.ToHex(primary);

                var second = MostVivid(palette, ph);
                Color.RGBToHSV(second, out float sh, out float ss, out _);
                if (Mathf.Abs(Mathf.DeltaAngle(sh * 360f, ph * 360f)) < 25f) sh = Mathf.Repeat(ph + 0.12f, 1f);
                var secondary = Color.HSVToRGB(sh, Mathf.Clamp(ss, 0.35f, 0.7f), dark ? 0.8f : 0.7f);
                c.secondary = ColorUtil.ToHex(secondary);

                var accent = Color.HSVToRGB(sh, 0.6f, 0.97f);
                c.accent = ColorUtil.ToHex(accent);
                c.highlight = ColorUtil.ToHex(accent);
            }

            // Readable text on the new background and panels.
            text = ColorUtil.EnsureContrast(text, bg, 4.5f, lighten: dark);
            text = ColorUtil.EnsureContrast(text, surface, 4.5f, lighten: dark);
            muted = ColorUtil.EnsureContrast(muted, surface, 3f, lighten: dark);
            c.text = ColorUtil.ToHex(text);
            c.textMuted = ColorUtil.ToHex(muted);
            return c;
        }

        /// <summary>Most colorful swatch (weight ≥ 4 %), optionally with a hue far from <paramref name="avoidHue"/>.</summary>
        static Color MostVivid(IList<PaletteExtractor.Swatch> palette, float avoidHue)
        {
            Color best = palette[0].Color;
            float bestScore = -1f;
            foreach (var sw in palette)
            {
                if (sw.Weight < 0.04f) continue;
                Color.RGBToHSV(sw.Color, out float h, out _, out _);
                if (avoidHue >= 0f && Mathf.Abs(Mathf.DeltaAngle(h * 360f, avoidHue * 360f)) < 25f) continue;
                float score = sw.Vividness * (0.6f + sw.Weight);
                if (score > bestScore) { bestScore = score; best = sw.Color; }
            }
            return best;
        }

        public static ThemeColors Copy(ThemeColors t) => new ThemeColors
        {
            background = t.background, surface = t.surface, primary = t.primary, secondary = t.secondary,
            text = t.text, textMuted = t.textMuted, accent = t.accent, success = t.success,
            highlight = t.highlight, highlightColorblind = t.highlightColorblind,
        };
    }
}
