using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.EditorTools.BuildTools
{
    /// <summary>
    /// Writes ready-to-play sample Game Packs into &lt;project&gt;/SamplePacks with procedurally painted images
    /// (no copyrighted content). Each image has distinct features everywhere so tiles are recognisable.
    /// </summary>
    public static class SamplePackGenerator
    {
        delegate Color Painter(float u, float v, float px, Color[] pal, SeededRandom seedRng, PaintContext ctx);

        sealed class PaintContext
        {
            public Vector2[] Points;
            public Color[] PointColors;
        }

        sealed class LevelSpec
        {
            public string Mode;
            public MemorySymbols? Symbols;
            public string Name;
            public int W, H;
            public Painter Paint;
            public bool Jpg;
        }

        public static string Root => Path.Combine(BuildMenu.ProjectRoot, "SamplePacks");

        public static void GenerateAll()
        {
            GenerateCozyPastel();
            GenerateDarkNeon();
            GenerateMinimalWhite();
            Debug.Log($"[Samples] Sample packs written to {Root}");
        }

        // ------------------------------------------------------------------ packs

        static void GenerateCozyPastel()
        {
            var pal = Hex("#F6C28B", "#F28482", "#84A59D", "#F5CAC3", "#F7EDE2", "#3D405B", "#E07A5F", "#81B29A");
            var pack = new GamePackData();
            pack.game.title = "Cozy Puzzles";
            pack.game.subtitle = "Relaxing picture puzzles";
            pack.game.developer = "Puzzle Studio Samples";
            pack.gameplay.difficultyCurve = DifficultyCurve.Progressive;
            pack.gameplay.minGrid = 3;
            pack.gameplay.maxGrid = 6;
            pack.theme.preset = "CozyPastel";

            Write("CozyPastel", pack, pal, new[]
            {
                new LevelSpec { Name = "Sunset Hills", W = 1920, H = 1080, Paint = Hills },
                new LevelSpec { Name = "Pebble Mosaic", W = 1600, H = 1200, Paint = Mosaic },
                new LevelSpec { Name = "Ripples", W = 1400, H = 1400, Paint = Rings, Mode = "Rotate" },
                new LevelSpec { Name = "Candy Stripes", W = 1920, H = 1080, Paint = Stripes, Jpg = true, Mode = "Strips" },
                new LevelSpec { Name = "Quiet Forest", W = 1600, H = 1200, Paint = Forest, Mode = "Sliding" },
                new LevelSpec { Name = "Moonlit Peaks", W = 1080, H = 1440, Paint = NightSky },
                new LevelSpec { Name = "Hidden Valley", W = 1600, H = 1200, Paint = Hills, Mode = "Memory" },
            });
        }

        static void GenerateDarkNeon()
        {
            var pal = Hex("#12101F", "#FF4FD8", "#3DE0FF", "#7B5CFF", "#FFD23F", "#1E1B33", "#5CFFB0", "#2A2550");
            var pack = new GamePackData();
            pack.game.title = "Neon Nights";
            pack.game.subtitle = "Glowing puzzles after dark";
            pack.game.developer = "Puzzle Studio Samples";
            pack.gameplay.difficultyCurve = DifficultyCurve.Fixed;
            pack.gameplay.fixedGrid = 4;
            var t = pack.theme;
            t.preset = "DarkNeon";
            t.colors = new ThemeColors
            {
                background = "#12101F", surface = "#1E1B33", primary = "#FF4FD8", secondary = "#3DE0FF",
                text = "#F2EEFF", textMuted = "#A39DC4", accent = "#FFD23F", success = "#5CFFB0",
                highlight = "#3DE0FF", highlightColorblind = "#FFD23F",
            };
            t.background.gradient = new List<string> { "#12101F", "#231A3D" };
            t.background.pattern = BackgroundPattern.Grid;
            t.pieces.borderColor = "#2A2550";
            t.pieces.gap = 6;
            t.pieces.cornerRadius = 10;
            t.uiStyle.buttonShape = ButtonShape.Rounded;
            t.uiStyle.panelStyle = PanelStyle.Glass;
            t.font = new FontConfig { heading = "Default:Playful", body = "Default:Clean" };
            t.particles.victory = ParticleKind.Stars;

            Write("DarkNeon", pack, pal, new[]
            {
                new LevelSpec { Name = "Synth Horizon", W = 1920, H = 1080, Paint = Hills },
                new LevelSpec { Name = "Circuit Glass", W = 1600, H = 1200, Paint = Mosaic },
                new LevelSpec { Name = "Pulse", W = 1400, H = 1400, Paint = Rings, Mode = "Rotate" },
                new LevelSpec { Name = "City Lights", W = 1920, H = 1080, Paint = NightSky, Mode = "Strips" },
                new LevelSpec { Name = "Glow Pairs", W = 1600, H = 1200, Paint = Mosaic, Mode = "Memory", Symbols = MemorySymbols.Colors },
            });
        }

        static void GenerateMinimalWhite()
        {
            var pal = Hex("#F4F4F2", "#1E1E1E", "#8C8C8C", "#FF6B4A", "#D9D9D6", "#FFFFFF", "#4A4A4A", "#BDBDBA");
            var pack = new GamePackData();
            pack.game.title = "Quiet Shapes";
            pack.game.subtitle = "Minimal puzzles";
            pack.game.developer = "Puzzle Studio Samples";
            pack.gameplay.minGrid = 3;
            pack.gameplay.maxGrid = 5;
            var t = pack.theme;
            t.preset = "MinimalWhite";
            t.colors = new ThemeColors
            {
                background = "#F4F4F2", surface = "#FFFFFF", primary = "#1E1E1E", secondary = "#8C8C8C",
                text = "#1E1E1E", textMuted = "#8C8C8C", accent = "#FF6B4A", success = "#3FA36B",
                highlight = "#FF6B4A", highlightColorblind = "#2F6BFF",
            };
            t.background.type = BackgroundType.Solid;
            t.background.pattern = BackgroundPattern.None;
            t.pieces.gap = 2;
            t.pieces.cornerRadius = 2;
            t.pieces.borderWidth = 0;
            t.pieces.shadow = false;
            t.uiStyle.buttonShape = ButtonShape.Square;
            t.uiStyle.panelStyle = PanelStyle.Flat;
            t.font = new FontConfig { heading = "Default:Clean", body = "Default:Clean" };

            Write("MinimalWhite", pack, pal, new[]
            {
                new LevelSpec { Name = "Stones", W = 1600, H = 1200, Paint = Mosaic },
                new LevelSpec { Name = "Echo", W = 1400, H = 1400, Paint = Rings },
                new LevelSpec { Name = "Lines", W = 1920, H = 1080, Paint = Stripes },
                new LevelSpec { Name = "Ridge", W = 1920, H = 1080, Paint = Hills },
                new LevelSpec { Name = "Paper Forest", W = 1600, H = 1200, Paint = Forest, Mode = "Memory", Symbols = MemorySymbols.Letters },
            });
        }

        static void Write(string folder, GamePackData pack, Color[] pal, LevelSpec[] specs)
        {
            string dir = Path.Combine(Root, folder);
            string levelsDir = Path.Combine(dir, PackPaths.LevelsDir);
            if (Directory.Exists(levelsDir)) Directory.Delete(levelsDir, true);
            Directory.CreateDirectory(levelsDir);

            pack.levels.Clear();
            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                string file = $"{i + 1:00}.{(s.Jpg ? "jpg" : "png")}";
                byte[] bytes = Render(s, pal, StableHash.Fnv1a(folder + s.Name));
                File.WriteAllBytes(Path.Combine(levelsDir, file), bytes);
                pack.levels.Add(new LevelConfig
                {
                    id = $"lvl_{i + 1:00}",
                    image = $"{PackPaths.LevelsDir}/{file}",
                    name = s.Name,
                    mode = s.Mode,
                    symbols = s.Symbols,
                });
            }
            GamePackWriter.WriteJson(pack, dir);

            var report = PackValidator.Validate(GamePackLoader.LoadFromDirectory(dir));
            foreach (var issue in report.Issues) Debug.Log($"[Samples] {folder}: {issue}");
        }

        static byte[] Render(LevelSpec spec, Color[] pal, int seed)
        {
            var rng = new SeededRandom(seed);
            var ctx = new PaintContext();
            int count = 46;
            ctx.Points = new Vector2[count];
            ctx.PointColors = new Color[count];
            float aspect = (float)spec.W / spec.H;
            for (int i = 0; i < count; i++)
            {
                ctx.Points[i] = new Vector2(rng.NextFloat() * aspect, rng.NextFloat());
                var c = pal[rng.Next(pal.Length)];
                ctx.PointColors[i] = ColorUtil.Shade(c, (rng.NextFloat() - 0.5f) * 0.25f);
            }

            var pixels = new Color32[spec.W * spec.H];
            float px = 1f / spec.H;
            var local = new SeededRandom(seed ^ 0x5bd1e995);
            for (int y = 0; y < spec.H; y++)
            {
                float v = (float)y / spec.H;               // 0 = top
                int row = (spec.H - 1 - y) * spec.W;       // textures start at the bottom
                for (int x = 0; x < spec.W; x++)
                {
                    float u = (float)x / spec.H;           // u in [0, aspect]
                    pixels[row + x] = spec.Paint(u, v, px, pal, local, ctx);
                }
            }

            var tex = new Texture2D(spec.W, spec.H, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            byte[] bytes = spec.Jpg ? tex.EncodeToJPG(92) : tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return bytes;
        }

        // ------------------------------------------------------------------ painters (u: 0..aspect, v: 0..1 top→bottom)

        static Color Hills(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            Color sky = Color.Lerp(pal[0], pal[1], Mathf.SmoothStep(0f, 1f, v * 1.3f));
            sky = Color.Lerp(sky, pal[3], Mathf.Clamp01((0.25f - v) * 2f));

            // Sun with glow
            Vector2 sun = new Vector2(1.25f, 0.33f);
            float d = Vector2.Distance(new Vector2(u, v), sun);
            sky = Color.Lerp(sky, pal[4], Mathf.Clamp01(0.35f - d) * 1.6f);
            sky = Color.Lerp(sky, pal[4], AA(0.11f - d, px));

            // Birds-like dashes for detail
            Color col = sky;
            for (int layer = 0; layer < 4; layer++)
            {
                float baseY = 0.48f + layer * 0.13f;
                float h = baseY
                          - 0.07f * Mathf.Sin(u * (2.1f + layer * 0.9f) + layer * 1.7f)
                          - 0.035f * Mathf.Sin(u * (6.3f + layer * 1.3f) + layer)
                          - 0.012f * Mathf.Sin(u * 21f + layer * 3f);
                Color hill = Color.Lerp(Color.Lerp(pal[2], pal[3], 0.5f), pal[5], layer / 3.5f);
                hill = Color.Lerp(hill, pal[layer % 2 == 0 ? 6 : 7], 0.25f);
                // subtle vertical shading inside the hill
                hill = ColorUtil.Shade(hill, -(v - h) * 0.35f);
                col = Color.Lerp(col, hill, AA(v - h, px));
            }
            return col;
        }

        static Color Rings(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            Vector2 c1 = new Vector2(0.38f, 0.42f), c2 = new Vector2(0.72f, 0.7f);
            Vector2 p = new Vector2(u, v);
            float a = Mathf.Atan2(p.y - c1.y, p.x - c1.x);
            float d1 = Vector2.Distance(p, c1) + 0.012f * Mathf.Sin(a * 6f);
            float d2 = Vector2.Distance(p, c2);
            float bands = d1 * 14f;
            int idx = Mathf.FloorToInt(bands);
            Color col = pal[Mod(idx, pal.Length)];
            float edge = Mathf.Abs(bands - Mathf.Round(bands)) / 14f;
            col = Color.Lerp(ColorUtil.Shade(col, -0.18f), col, Mathf.Clamp01(edge / (px * 2.5f)));
            // second set of ripples blended
            float ring2 = Mathf.Sin(d2 * 55f) * 0.5f + 0.5f;
            col = Color.Lerp(col, pal[4], ring2 * Mathf.Clamp01(0.45f - d2) * 0.9f);
            col = ColorUtil.Shade(col, (0.5f - v) * 0.15f);
            return col;
        }

        static Color Mosaic(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            var p = new Vector2(u, v);
            var pts = ctx.Points;
            float best = float.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - p).sqrMagnitude;
                if (d < best) { best = d; bestIdx = i; }
            }
            // Exact distance to the closest Voronoi edge (perpendicular bisectors).
            Vector2 a = pts[bestIdx];
            float edge = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                if (i == bestIdx) continue;
                Vector2 b = pts[i];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-6f) continue;
                float dist = Vector2.Dot((a + b) * 0.5f - p, dir / len);
                if (dist < edge) edge = dist;
            }
            Color cell = ctx.PointColors[bestIdx];
            cell = ColorUtil.Shade(cell, 0.12f - Mathf.Sqrt(best) * 0.8f);
            Color grout = ColorUtil.Shade(pal[5], -0.1f);
            return Color.Lerp(grout, cell, AA(edge - 0.004f, px));
        }

        static Color Stripes(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            float w = u + v * 0.9f + 0.03f * Mathf.Sin(v * 18f + u * 3f);
            float bands = w * 9f;
            int idx = Mathf.FloorToInt(bands);
            Color col = pal[Mod(idx, 5)];
            float edge = Mathf.Abs(bands - Mathf.Round(bands)) / 9f;
            col = Color.Lerp(pal[4], col, AA(edge - 0.004f, px));

            // polka dots on alternate bands
            if (Mod(idx, 2) == 0)
            {
                float gx = u * 11f, gy = v * 11f;
                float dx = gx - Mathf.Floor(gx) - 0.5f, dy = gy - Mathf.Floor(gy) - 0.5f;
                float dd = Mathf.Sqrt(dx * dx + dy * dy) / 11f;
                col = Color.Lerp(col, pal[4], AA(0.012f - dd, px) * 0.85f);
            }
            col = ColorUtil.Shade(col, (0.5f - v) * 0.12f);
            return col;
        }

        static Color Forest(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            Color col = Color.Lerp(pal[4], pal[3], v);
            // big pale sun
            float ds = Vector2.Distance(new Vector2(u, v), new Vector2(0.35f, 0.28f));
            col = Color.Lerp(col, Color.white, AA(0.09f - ds, px) * 0.8f);

            for (int layer = 0; layer < 4; layer++)
            {
                float spacing = 0.11f - layer * 0.015f;
                float shift = layer * 0.037f;
                float cell = (u + shift) / spacing;
                float local = cell - Mathf.Floor(cell) - 0.5f;
                int treeIdx = Mathf.FloorToInt(cell);
                float hRand = Hash01(treeIdx * 31 + layer * 7);
                float baseY = 0.62f + layer * 0.1f;
                float height = 0.22f + hRand * 0.14f + layer * 0.02f;
                float top = baseY - height;
                float t = (v - top) / height;
                float halfWidth = t * spacing * 0.48f;
                float inside = t < 0f ? -1f : Mathf.Min(halfWidth - Mathf.Abs(local) * spacing, (baseY + 0.02f) - v);
                t = Mathf.Clamp01(t);
                float ground = v - (baseY + 0.015f);
                Color tree = Color.Lerp(Color.Lerp(pal[2], pal[7], 0.4f), pal[5], layer / 3f);
                tree = ColorUtil.Shade(tree, -t * 0.12f);
                col = Color.Lerp(col, tree, Mathf.Max(AA(inside, px), AA(ground, px)));
                // fog between layers
                col = Color.Lerp(col, pal[4], 0.06f);
            }
            return col;
        }

        static Color NightSky(float u, float v, float px, Color[] pal, SeededRandom r, PaintContext ctx)
        {
            Color top = ColorUtil.Shade(pal[5], -0.25f);
            Color col = Color.Lerp(top, Color.Lerp(pal[3], pal[1], 0.4f), Mathf.Pow(v, 1.6f));

            // stars on a jittered grid
            float gs = 34f;
            float gx = u * gs, gy = v * gs;
            int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
            float h = Hash01(ix * 73856093 ^ iy * 19349663);
            if (h > 0.55f)
            {
                float sx = ix + 0.2f + 0.6f * Hash01(ix * 17 + iy * 131);
                float sy = iy + 0.2f + 0.6f * Hash01(ix * 191 + iy * 29);
                float d = Mathf.Sqrt((gx - sx) * (gx - sx) + (gy - sy) * (gy - sy)) / gs;
                float size = 0.0015f + 0.003f * Hash01(ix * 7 + iy * 3);
                col = Color.Lerp(col, Color.white, AA(size - d, px) * (1f - v));
            }

            // crescent moon
            Vector2 p = new Vector2(u, v);
            float m1 = Vector2.Distance(p, new Vector2(0.55f, 0.22f));
            float m2 = Vector2.Distance(p, new Vector2(0.6f, 0.19f));
            float moon = Mathf.Min(AA(0.09f - m1, px), AA(m2 - 0.085f, px));
            col = Color.Lerp(col, pal[4], moon);
            col = Color.Lerp(col, pal[4], Mathf.Clamp01(0.2f - m1) * 0.5f);

            // mountains
            for (int layer = 0; layer < 3; layer++)
            {
                float baseY = 0.62f + layer * 0.12f;
                float x = u * (3f + layer) + layer * 2.3f;
                float ridge = baseY - 0.16f * Mathf.Abs(Mathf.Sin(x)) - 0.05f * Mathf.Abs(Mathf.Sin(x * 3.7f));
                Color m = Color.Lerp(Color.Lerp(pal[2], pal[5], 0.5f), ColorUtil.Shade(pal[5], -0.12f), layer / 2f);
                m = ColorUtil.Shade(m, -(v - ridge) * 0.25f);
                if (layer > 0)
                {
                    // little windows / lights so dark areas still have recognisable details
                    float ls = 46f + layer * 18f;
                    float lx = u * ls, ly = v * ls;
                    int cx = Mathf.FloorToInt(lx), cy = Mathf.FloorToInt(ly);
                    float hh = Hash01(cx * 92821 + cy * 68917 + layer * 1013);
                    if (hh > 0.72f && v > ridge + 0.03f)
                    {
                        float fx = lx - cx - 0.5f, fy = ly - cy - 0.5f;
                        float box = Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fy)) / ls;
                        Color light = hh > 0.9f ? pal[1] : pal[4];
                        m = Color.Lerp(m, light, AA(0.12f / ls - box, px) * 0.9f);
                    }
                }
                col = Color.Lerp(col, m, AA(v - ridge, px));
            }
            return col;
        }

        // ------------------------------------------------------------------ helpers

        static float AA(float signedDistance, float px) => Mathf.Clamp01(signedDistance / (px * 1.5f) + 0.5f);

        static int Mod(int a, int n) => ((a % n) + n) % n;

        static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16; x *= 0x7feb352d;
                x ^= x >> 15; x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }

        static Color[] Hex(params string[] values)
        {
            var c = new Color[values.Length];
            for (int i = 0; i < values.Length; i++) c[i] = ColorUtil.Parse(values[i], Color.magenta);
            return c;
        }
    }
}
