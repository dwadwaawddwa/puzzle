using System;
using System.Globalization;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    public static class ColorUtil
    {
        /// <summary>Parses "#RGB", "#RRGGBB" or "#RRGGBBAA" (the # is optional).</summary>
        public static bool TryParseHex(string hex, out Color color)
        {
            color = Color.magenta;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string s = hex.Trim().TrimStart('#');
            if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
            if (s.Length != 6 && s.Length != 8) return false;
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
            if (s.Length == 6) v = (v << 8) | 0xFF;
            color = new Color(((v >> 24) & 0xFF) / 255f, ((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
            return true;
        }

        public static Color Parse(string hex, Color fallback) => TryParseHex(hex, out var c) ? c : fallback;

        public static string ToHex(Color c, bool includeAlpha = false)
        {
            Color32 c32 = c;
            return includeAlpha
                ? $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}{c32.a:X2}"
                : $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }

        /// <summary>WCAG 2.x relative luminance of an sRGB color.</summary>
        public static float RelativeLuminance(Color c)
        {
            return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);

            static float Channel(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>WCAG contrast ratio, from 1 (none) to 21 (black on white).</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            float hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Darkens (or lightens) <paramref name="fg"/> until it reaches <paramref name="ratio"/> against <paramref name="bg"/>.</summary>
        public static Color EnsureContrast(Color fg, Color bg, float ratio, bool lighten)
        {
            for (int i = 0; i < 40 && ContrastRatio(fg, bg) < ratio; i++)
                fg = Shade(fg, lighten ? 0.04f : -0.04f);
            if (ContrastRatio(fg, bg) < ratio) fg = lighten ? Color.white : Color.black;
            return fg;
        }

        /// <summary>Lighten (amount &gt; 0) or darken (amount &lt; 0) in HSV value space.</summary>
        public static Color Shade(Color c, float amount)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + amount);
            if (amount > 0) s = Mathf.Clamp01(s - amount * 0.3f);
            var r = Color.HSVToRGB(h, s, v);
            r.a = c.a;
            return r;
        }
    }
}
