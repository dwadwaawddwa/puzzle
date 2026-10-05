using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Main colors of a picture: deterministic k-means in OKLab (a perceptual space, so clusters match what the eye
    /// sees), then colors that look the same are merged. Sorted from the most to the least present.
    /// </summary>
    public static class PaletteExtractor
    {
        /// <summary>Two clusters closer than this (OKLab distance) are one color.</summary>
        public const float MergeDistance = 0.045f;

        public struct Swatch
        {
            public Color Color;
            /// <summary>Share of the picture (0..1).</summary>
            public float Weight;
            /// <summary>Saturation × value: how "colorful" the swatch is (HSV, kept for compatibility).</summary>
            public float Vividness;
            /// <summary>OKLab lightness (0..1).</summary>
            public float Lightness;
            /// <summary>OKLab chroma (0 = gray).</summary>
            public float Chroma;
            /// <summary>OKLab hue in degrees.</summary>
            public float Hue;

            public Lab Lab => new Lab(Lightness, Chroma * Mathf.Cos(Hue * Mathf.Deg2Rad), Chroma * Mathf.Sin(Hue * Mathf.Deg2Rad));

            public static Swatch Of(Color color, float weight)
            {
                var lab = Oklab.FromColor(color);
                Color.RGBToHSV(color, out _, out float s, out float v);
                return new Swatch { Color = color, Weight = weight, Vividness = s * v, Lightness = lab.L, Chroma = lab.Chroma, Hue = lab.Hue };
            }
        }

        /// <param name="pixels">Ideally ≤ 64 × 64 pixels.</param>
        /// <returns>Swatches sorted by weight (largest first).</returns>
        public static List<Swatch> Extract(Color32[] pixels, int k = 8, int iterations = 10)
        {
            var samples = new List<Lab>(pixels.Length);
            foreach (var p in pixels)
                if (p.a > 128) samples.Add(Oklab.FromColor(new Color(p.r / 255f, p.g / 255f, p.b / 255f)));
            if (samples.Count == 0) return new List<Swatch>();
            k = Math.Max(1, Math.Min(k, samples.Count));

            // Farthest-point initialisation: deterministic and spreads centers over distinct colors.
            var centers = new List<Lab> { Mean(samples) };
            while (centers.Count < k)
            {
                float best = -1f;
                Lab pick = samples[0];
                foreach (var s in samples)
                {
                    float d = float.MaxValue;
                    foreach (var c in centers) d = Mathf.Min(d, Oklab.Distance(s, c));
                    if (d > best) { best = d; pick = s; }
                }
                if (best <= 1e-5f) break;   // fewer distinct colors than k
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
                        float d = Oklab.Distance(samples[i], centers[c]);
                        if (d < best) { best = d; assign[i] = c; }
                    }
                }
                var sums = new Vector3[centers.Count];
                var counts = new int[centers.Count];
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    sums[assign[i]] += new Vector3(s.L, s.a, s.b);
                    counts[assign[i]]++;
                }
                for (int c = 0; c < centers.Count; c++)
                    if (counts[c] > 0) centers[c] = new Lab(sums[c].x / counts[c], sums[c].y / counts[c], sums[c].z / counts[c]);
            }

            var clusters = new List<(Lab lab, float weight)>();
            var totals = new int[centers.Count];
            foreach (var a in assign) totals[a]++;
            for (int c = 0; c < centers.Count; c++)
                if (totals[c] > 0) clusters.Add((centers[c], (float)totals[c] / samples.Count));
            MergeClose(clusters);

            var result = new List<Swatch>();
            foreach (var (lab, weight) in clusters) result.Add(Swatch.Of(Oklab.ToColor(lab), weight));
            result.Sort((x, y) => y.Weight.CompareTo(x.Weight));
            return result;
        }

        /// <summary>Joins clusters that look alike (weighted average), so "two shades of the same sky" count as one color.</summary>
        static void MergeClose(List<(Lab lab, float weight)> clusters)
        {
            bool merged = true;
            while (merged && clusters.Count > 1)
            {
                merged = false;
                for (int i = 0; i < clusters.Count && !merged; i++)
                    for (int j = i + 1; j < clusters.Count && !merged; j++)
                    {
                        if (Oklab.Distance(clusters[i].lab, clusters[j].lab) >= MergeDistance) continue;
                        var (a, wa) = clusters[i];
                        var (b, wb) = clusters[j];
                        float w = wa + wb;
                        clusters[i] = (new Lab((a.L * wa + b.L * wb) / w, (a.a * wa + b.a * wb) / w, (a.b * wa + b.b * wb) / w), w);
                        clusters.RemoveAt(j);
                        merged = true;
                    }
            }
        }

        /// <summary>Downscales a (readable or not) texture on the GPU and extracts its palette.</summary>
        public static List<Swatch> FromTexture(Texture2D texture, int sampleSize = 48, int k = 8)
        {
            var small = TextureLoader.Resize(texture, sampleSize, mipmaps: false);
            var px = small.GetPixels32();
            if (Application.isPlaying) UnityEngine.Object.Destroy(small);
            else UnityEngine.Object.DestroyImmediate(small);
            return Extract(px, k);
        }

        /// <summary>Merges the palettes of several pictures (each picture counts the same).</summary>
        public static List<Swatch> Merge(IEnumerable<List<Swatch>> palettes, int k = 8)
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

        static Lab Mean(List<Lab> v)
        {
            float l = 0f, a = 0f, b = 0f;
            foreach (var x in v) { l += x.L; a += x.a; b += x.b; }
            return new Lab(l / v.Count, a / v.Count, b / v.Count);
        }
    }
}
