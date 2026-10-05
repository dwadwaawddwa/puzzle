using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Theme colors from a picture palette (per-level colors, "Generate palette from images"):
    /// <list type="bullet">
    /// <item>background ← the <b>dominant</b> color; gradient end ← the 2nd main color; animated glow ← the 3rd;</item>
    /// <item>buttons, secondary buttons, stars, selection ← the <b>less dominant, more colorful</b> colors;</item>
    /// <item>the theme stays light or dark, and every text stays readable (WCAG AA).</item>
    /// </list>
    /// Colors are placed in OKLab at fixed lightness bands, so a yellow and a blue picture give equally light backgrounds.
    /// </summary>
    public static class ThemeDerivation
    {
        /// <summary>Below this OKLab chroma a color is treated as gray (its hue means nothing).</summary>
        const float Neutral = 0.035f;
        /// <summary>Contrast targets are aimed slightly higher: saving a color as #RRGGBB rounds it.</summary>
        const float Margin = 0.06f;

        public static ThemeColors FromPalette(ThemeColors baseColors, IList<PaletteExtractor.Swatch> palette, AutoColors mode)
        {
            var c = Copy(baseColors);
            if (palette == null || palette.Count == 0 || mode == AutoColors.Off) return c;

            var baseBg = ColorUtil.Parse(baseColors.background, Color.white);
            bool dark = ColorUtil.RelativeLuminance(baseBg) < 0.35f;
            float baseL = Oklab.FromColor(baseBg).L;
            var text = ColorUtil.Parse(baseColors.text, dark ? Color.white : Color.black);
            var muted = ColorUtil.Parse(baseColors.textMuted, Color.gray);

            // ---- background: dominant color, then the next distinct main colors for the gradient and the glow
            var dominant = palette[0];
            float bgL = dark ? Mathf.Clamp(baseL, 0.17f, 0.27f) : Mathf.Clamp(baseL, 0.92f, 0.975f);
            float scale = dark ? 0.55f : 0.45f, maxC = dark ? 0.075f : 0.06f;
            var bg = Tint(dominant, bgL, scale, maxC);

            int second = NextDistinct(palette, new[] { 0 });
            int third = second >= 0 ? NextDistinct(palette, new[] { 0, second }) : -1;
            float l2 = dark ? bgL + 0.05f : bgL - 0.05f;
            float l3 = dark ? bgL + 0.08f : bgL - 0.015f;
            var bg2 = second >= 0 ? Tint(palette[second], l2, scale * 1.25f, maxC * 1.35f)
                                  : Oklab.FromLch(l2, Mathf.Min(dominant.Chroma * scale, maxC), dominant.Hue + 20f);
            var bg3 = third >= 0 ? Tint(palette[third], l3, scale * 1.5f, maxC * 1.7f)
                                 : Oklab.FromLch(l3, Mathf.Min(dominant.Chroma * scale * 1.4f, maxC * 1.4f), dominant.Hue - 28f);
            c.background = ColorUtil.ToHex(bg);
            c.background2 = ColorUtil.ToHex(bg2);
            c.background3 = ColorUtil.ToHex(bg3);

            // ---- panels, buttons, stars, selection: the colorful, less dominant colors
            Color surface = ColorUtil.Parse(baseColors.surface, Color.white);
            if (mode == AutoColors.Full)
            {
                surface = dark
                    ? Oklab.FromLch(bgL + 0.07f, Mathf.Min(dominant.Chroma * 0.5f, 0.05f), dominant.Hue)
                    : Oklab.FromLch(0.99f, Mathf.Min(dominant.Chroma * 0.15f, 0.012f), dominant.Hue);
                c.surface = ColorUtil.ToHex(surface);

                var accents = RankAccents(palette);
                if (accents.Count > 0)
                {
                    var p = palette[accents[0]];
                    // The picture's own lightness (clamped): oranges stay orange instead of turning brown.
                    float primaryL = dark ? Mathf.Clamp(p.Lightness, 0.68f, 0.82f) : Mathf.Clamp(p.Lightness, 0.56f, 0.72f);
                    var primary = Oklab.FromLch(primaryL, Mathf.Clamp(p.Chroma * 1.15f, 0.11f, 0.2f), p.Hue);
                    primary = ColorUtil.EnsureContrast(primary, surface, 3f + Margin, lighten: dark);
                    c.primary = ColorUtil.ToHex(primary);

                    int si = accents.FindIndex(i => Oklab.HueDistance(palette[i].Hue, p.Hue) >= 35f);
                    float secondHue = si >= 0 ? palette[accents[si]].Hue : p.Hue + 40f;
                    float secondC = si >= 0 ? palette[accents[si]].Chroma : p.Chroma;
                    float secondL = si >= 0 ? palette[accents[si]].Lightness : primaryL + 0.05f;
                    secondL = dark ? Mathf.Clamp(secondL, 0.7f, 0.86f) : Mathf.Clamp(secondL, 0.62f, 0.8f);
                    var secondary = Oklab.FromLch(secondL, Mathf.Clamp(secondC * 1.1f, 0.09f, 0.18f), secondHue);
                    secondary = ColorUtil.EnsureContrast(secondary, surface, 2.2f + Margin, lighten: dark);
                    c.secondary = ColorUtil.ToHex(secondary);

                    // Stars and selection: a light, lively color of the picture (the third accent, else the second).
                    int ai = accents.Count > 2 ? 2 : accents.Count - 1;
                    var a = palette[accents[ai]];
                    float accentHue = Oklab.HueDistance(a.Hue, p.Hue) < 20f ? secondHue : a.Hue;
                    var accent = Oklab.FromLch(dark ? 0.86f : 0.84f, Mathf.Clamp(a.Chroma * 1.2f, 0.12f, 0.18f), accentHue);
                    c.accent = ColorUtil.ToHex(accent);
                    c.highlight = ColorUtil.ToHex(Oklab.FromLch(dark ? 0.84f : 0.8f, 0.16f, accentHue));
                }
                // A gray picture keeps the base theme's buttons: inventing colors would not "come from the picture".
            }

            // ---- readable text on every background color and on the panels
            foreach (var surfaceColor in new[] { bg, bg2, bg3, surface })
                text = ColorUtil.EnsureContrast(text, surfaceColor, 4.5f + Margin, lighten: dark);
            muted = ColorUtil.EnsureContrast(muted, surface, 3f + Margin, lighten: dark);
            muted = ColorUtil.EnsureContrast(muted, bg, 3f + Margin, lighten: dark);
            c.text = ColorUtil.ToHex(text);
            c.textMuted = ColorUtil.ToHex(muted);
            return c;
        }

        /// <summary>The swatch's hue at another lightness, with a reduced chroma (soft backgrounds).</summary>
        static Color Tint(PaletteExtractor.Swatch s, float lightness, float chromaScale, float maxChroma) =>
            Oklab.FromLch(lightness, Mathf.Min(s.Chroma * chromaScale, maxChroma), s.Hue);

        /// <summary>Next swatch (by weight) whose color differs from all the <paramref name="used"/> ones, or -1.</summary>
        static int NextDistinct(IList<PaletteExtractor.Swatch> palette, int[] used)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                if (System.Array.IndexOf(used, i) >= 0 || palette[i].Weight < 0.02f) continue;
                bool distinct = true;
                foreach (int u in used)
                {
                    var a = palette[i];
                    var b = palette[u];
                    bool bothGray = a.Chroma < Neutral && b.Chroma < Neutral;
                    bool hueApart = a.Chroma >= Neutral && b.Chroma >= Neutral && Oklab.HueDistance(a.Hue, b.Hue) >= 22f;
                    bool vividVsGray = (a.Chroma >= Neutral) != (b.Chroma >= Neutral);
                    if (bothGray || !(hueApart || vividVsGray || Oklab.ChromaDistance(a.Lab, b.Lab) >= 0.06f)) { distinct = false; break; }
                }
                if (distinct) return i;
            }
            return -1;
        }

        /// <summary>
        /// Colorful swatches for the buttons, best first: vivid enough, present enough, and preferably not the dominant
        /// hue (that one is already the background).
        /// </summary>
        static List<int> RankAccents(IList<PaletteExtractor.Swatch> palette)
        {
            var dominant = palette[0];
            var scored = new List<(int index, float score)>();
            for (int i = 0; i < palette.Count; i++)
            {
                var s = palette[i];
                if (s.Chroma < 0.045f || s.Weight < 0.015f) continue;
                float score = s.Chroma * Mathf.Sqrt(0.25f + s.Weight);
                if (i == 0) score *= 0.35f;
                else if (dominant.Chroma >= Neutral && Oklab.HueDistance(s.Hue, dominant.Hue) < 30f) score *= 0.5f;
                scored.Add((i, score));
            }
            scored.Sort((x, y) => y.score.CompareTo(x.score));
            return scored.ConvertAll(x => x.index);
        }

        public static ThemeColors Copy(ThemeColors t) => new ThemeColors
        {
            background = t.background, background2 = t.background2, background3 = t.background3,
            surface = t.surface, primary = t.primary, secondary = t.secondary,
            text = t.text, textMuted = t.textMuted, accent = t.accent, success = t.success,
            highlight = t.highlight, highlightColorblind = t.highlightColorblind,
        };
    }
}
