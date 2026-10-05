using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>Dominant colors of a picture (deterministic k-means on a small copy of the image).</summary>
    public static class PaletteExtractor
    {
        public struct Swatch
        {
            public Color Color;
            /// <summary>Share of the picture (0..1).</summary>
            public float Weight;
            /// <summary>Saturation × value: how "colorful" the swatch is.</summary>
            public float Vividness;
        }

        /// <param name="pixels">Ideally ≤ 64 × 64 pixels.</param>
        /// <returns>Swatches sorted by weight (largest first).</returns>
        public static List<Swatch> Extract(Color32[] pixels, int k = 6, int iterations = 10)
        {
            var samples = new List<Vector3>(pixels.Length);
            foreach (var p in pixels)
                if (p.a > 128) samples.Add(new Vector3(p.r / 255f, p.g / 255f, p.b / 255f));
            if (samples.Count == 0) return new List<Swatch>();
            k = Math.Max(1, Math.Min(k, samples.Count));

            // Farthest-point initialisation: deterministic and spreads centers over distinct colors.
            var centers = new List<Vector3> { Mean(samples) };
            while (centers.Count < k)
            {
                float best = -1f;
                Vector3 pick = samples[0];
                foreach (var s in samples)
                {
                    float d = float.MaxValue;
                    foreach (var c in centers) d = Mathf.Min(d, (s - c).sqrMagnitude);
                    if (d > best) { best = d; pick = s; }
                }
                centers.Add(pick);
            }

            var assign = new int[samples.Count];
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    float best = float.MaxValue;
                    for (int c = 0; c < centers.Count; c++)
                    {
                        float d = (samples[i] - centers[c]).sqrMagnitude;
                        if (d < best) { best = d; assign[i] = c; }
                    }
                }
                var sums = new Vector3[centers.Count];
                var counts = new int[centers.Count];
                for (int i = 0; i < samples.Count; i++) { sums[assign[i]] += samples[i]; counts[assign[i]]++; }
                for (int c = 0; c < centers.Count; c++) if (counts[c] > 0) centers[c] = sums[c] / counts[c];
            }

            var totals = new int[centers.Count];
            foreach (var a in assign) totals[a]++;
            var result = new List<Swatch>();
            for (int c = 0; c < centers.Count; c++)
            {
                if (totals[c] == 0) continue;
                var color = new Color(centers[c].x, centers[c].y, centers[c].z, 1f);
                Color.RGBToHSV(color, out _, out float s, out float v);
                result.Add(new Swatch { Color = color, Weight = (float)totals[c] / samples.Count, Vividness = s * v });
            }
            result.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            return result;
        }

        /// <summary>Downscales a (readable or not) texture on the GPU and extracts its palette.</summary>
        public static List<Swatch> FromTexture(Texture2D texture, int sampleSize = 48, int k = 6)
        {
            var small = TextureLoader.Resize(texture, sampleSize, mipmaps: false);
            var px = small.GetPixels32();
            if (Application.isPlaying) UnityEngine.Object.Destroy(small);
            else UnityEngine.Object.DestroyImmediate(small);
            return Extract(px, k);
        }

        /// <summary>Merges the palettes of several pictures (each picture counts the same).</summary>
        public static List<Swatch> Merge(IEnumerable<List<Swatch>> palettes, int k = 6)
        {
            var pseudo = new List<Color32>();
            foreach (var palette in palettes)
                foreach (var s in palette)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(s.Weight * 100));
                    for (int i = 0; i < n; i++) pseudo.Add(s.Color);
                }
            return Extract(pseudo.ToArray(), k);
        }

        static Vector3 Mean(List<Vector3> v)
        {
            var sum = Vector3.zero;
            foreach (var x in v) sum += x;
            return sum / v.Count;
        }
    }
}
