using System;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>A color in OKLab: L lightness (0..1), a/b green-red and blue-yellow axes.</summary>
    public struct Lab
    {
        public float L, a, b;

        public Lab(float l, float a, float b) { L = l; this.a = a; this.b = b; }

        /// <summary>Colorfulness (0 = gray, about 0.3 = the most vivid sRGB colors).</summary>
        public float Chroma => Mathf.Sqrt(a * a + b * b);

        /// <summary>Hue angle in degrees (0..360).</summary>
        public float Hue => Mathf.Repeat(Mathf.Atan2(b, a) * Mathf.Rad2Deg, 360f);
    }

    /// <summary>
    /// OKLab / OKLCh (Björn Ottosson): a perceptual color space. Equal distances look equally different and a given
    /// lightness looks equally light whatever the hue — which plain RGB / HSV don't do (yellow vs blue at the same "V").
    /// Used to cluster picture colors and to place theme colors at a chosen lightness while keeping their hue.
    /// </summary>
    public static class Oklab
    {
        public static Lab FromColor(Color c)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), bl = ToLinear(c.b);
            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * bl);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * bl);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * bl);
            return new Lab(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        /// <summary>Back to sRGB, clamped to the displayable range.</summary>
        public static Color ToColor(Lab lab)
        {
            Unclamped(lab, out float r, out float g, out float b);
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }

        /// <summary>
        /// The color with this lightness, chroma and hue; when it can't be displayed, the chroma is reduced
        /// (the hue and the lightness are kept).
        /// </summary>
        public static Color FromLch(float lightness, float chroma, float hueDegrees)
        {
            lightness = Mathf.Clamp01(lightness);
            float rad = hueDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float lo = 0f, hi = Mathf.Max(0f, chroma);
            if (InGamut(new Lab(lightness, hi * cos, hi * sin))) lo = hi;
            else
                for (int i = 0; i < 14; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (InGamut(new Lab(lightness, mid * cos, mid * sin))) lo = mid; else hi = mid;
                }
            return ToColor(new Lab(lightness, lo * cos, lo * sin));
        }

        public static float Distance(Lab x, Lab y)
        {
            float dl = x.L - y.L, da = x.a - y.a, db = x.b - y.b;
            return Mathf.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>How different two colors look, ignoring lightness (difference of hue and colorfulness).</summary>
        public static float ChromaDistance(Lab x, Lab y)
        {
            float da = x.a - y.a, db = x.b - y.b;
            return Mathf.Sqrt(da * da + db * db);
        }

        /// <summary>Smallest angle between two hues, 0..180.</summary>
        public static float HueDistance(float h1, float h2) => Mathf.Abs(Mathf.DeltaAngle(h1, h2));

        static bool InGamut(Lab lab)
        {
            Unclamped(lab, out float r, out float g, out float b);
            const float e = 0.0005f;
            return r >= -e && r <= 1f + e && g >= -e && g <= 1f + e && b >= -e && b <= 1f + e;
        }

        static void Unclamped(Lab lab, out float r, out float g, out float b)
        {
            float l = lab.L + 0.3963377774f * lab.a + 0.2158037573f * lab.b;
            float m = lab.L - 0.1055613458f * lab.a - 0.0638541728f * lab.b;
            float s = lab.L - 0.0894841775f * lab.a - 1.2914855480f * lab.b;
            l = l * l * l; m = m * m * m; s = s * s * s;
            r = ToGamma(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s);
            g = ToGamma(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s);
            b = ToGamma(-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
        }

        static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        static float ToGamma(float c)
        {
            if (c <= 0f) return c * 12.92f;   // keep the sign: used to test the gamut
            return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }

        static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    }
}
