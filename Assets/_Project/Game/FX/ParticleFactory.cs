using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Game.UI;
using UnityEngine;

namespace PuzzleStudio.Game.FX
{
    /// <summary>
    /// Builds and plays the particle effects in code (no prefab): sparkles/stars when a piece snaps into place,
    /// confetti / stars / bubbles for the victory. Textures are generated at runtime.
    /// </summary>
    public sealed class ParticleFactory : MonoBehaviour
    {
        const int SortingOrder = 300;

        ThemeService _theme;
        ParticleConfig _config;
        Camera _camera;
        Material _baseMaterial;
        readonly Dictionary<string, ParticleSystem> _systems = new Dictionary<string, ParticleSystem>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public bool ReduceMotion;

        public void Init(Camera camera, ThemeService theme, ParticleConfig config)
        {
            _camera = camera;
            _theme = theme;
            _config = config ?? new ParticleConfig();
            var mat = Resources.Load<Material>("Materials/Particle");
            _baseMaterial = mat != null ? mat : new Material(Shader.Find("PuzzleStudio/Particle"));
        }

        public int AliveParticles
        {
            get
            {
                int n = 0;
                foreach (var s in _systems.Values) if (s != null) n += s.particleCount;
                return n;
            }
        }

        float Intensity => Mathf.Clamp(_config.intensity, 0f, 2f) * (ReduceMotion ? 0.35f : 1f);

        Color[] PaletteColors()
        {
            var p = _theme.Palette;
            return new[] { p.Primary, p.Secondary, p.Accent, p.Highlight, p.Success, Color.white };
        }

        // ------------------------------------------------------------------ snap

        /// <summary>Small burst where a piece just snapped into its correct place.</summary>
        public void PlaySnap(Vector3 position, float pieceSize)
        {
            if (_config.snap == ParticleKind.None || ReduceMotion) return;
            bool stars = _config.snap == ParticleKind.Stars;
            var ps = Get(stars ? "snap-stars" : "snap-sparkle", stars ? "star" : "glow", s =>
            {
                var main = s.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.gravityModifier = stars ? 0.4f : 0f;
                var size = s.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
                var col = s.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
            });
            int count = Mathf.RoundToInt((stars ? 7 : 12) * Intensity);
            var glow = _theme.GlowColor;
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + dir * pieceSize * 0.25f + Vector3.back * 0.5f,
                    velocity = dir * Random.Range(1.2f, 3f) * Mathf.Max(0.5f, pieceSize),
                    startSize = Random.Range(0.08f, 0.22f) * Mathf.Max(0.6f, pieceSize) * (stars ? 1.5f : 1f),
                    startColor = Random.value < 0.6f ? glow : Color.white,
                    startLifetime = Random.Range(0.35f, 0.7f),
                    rotation = Random.Range(0f, 360f),
                };
                ps.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ victory

        /// <summary>Victory celebration over the whole screen (<paramref name="board"/> = board rect in world space).</summary>
        public void PlayVictory(Rect board)
        {
            if (_config.victory == ParticleKind.None) return;
            float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
            var colors = PaletteColors();
            switch (_config.victory)
            {
                case ParticleKind.Stars:
                    VictoryStars(board, colors);
                    StartCoroutine(SecondBurst(board, colors));
                    break;
                case ParticleKind.Bubbles: VictoryBubbles(halfW, halfH, colors); break;
                default: VictoryConfetti(halfW, halfH, colors); break;
            }
        }

        void VictoryConfetti(float halfW, float halfH, Color[] colors)
        {
            var rain = Get("confetti-rain", "rect", s =>
            {
                var main = s.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                main.gravityModifier = 0.12f;
                main.startSize3D = true;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
                Flutter(s);
                var noise = s.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.4f;
            });
            int count = Mathf.RoundToInt(160 * Intensity);
            for (int i = 0; i < count; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(Random.Range(-halfW, halfW), halfH + Random.Range(0.2f, 3.5f), -1f),
                    velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-2.2f, -0.8f), 0f),
                    startSize3D = new Vector3(Random.Range(0.12f, 0.2f), Random.Range(0.2f, 0.34f), 1f),
                    startColor = colors[Random.Range(0, colors.Length)],
                    startLifetime = Random.Range(4f, 6.5f),
                    rotation = Random.Range(0f, 360f),
                };
                rain.Emit(ep, 1);
            }

            var cannon = Get("confetti-cannon", "rect", s =>
            {
                var main = s.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
                main.gravityModifier = 0.9f;
                main.startSize3D = true;
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                Flutter(s);
                var drag = s.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 1.2f;
                drag.limit = 100f;
            });
            int perSide = Mathf.RoundToInt(70 * Intensity);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < perSide; i++)
                {
                    float angle = Mathf.Deg2Rad * Random.Range(55f, 80f);
                    var dir = new Vector3(-side * Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(side * (halfW - 0.3f), -halfH - 0.2f, -1f),
                        velocity = dir * Random.Range(9f, 15f),
                        startSize3D = new Vector3(Random.Range(0.12f, 0.2f), Random.Range(0.2f, 0.32f), 1f),
                        startColor = colors[Random.Range(0, colors.Length)],
                        startLifetime = Random.Range(2.5f, 3.5f),
                        rotation = Random.Range(0f, 360f),
                    };
                    cannon.Emit(ep, 1);
                }
        }

        void VictoryStars(Rect board, Color[] colors)
        {
            var ps = Get("victory-stars", "star", s =>
            {
                var main = s.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
                main.gravityModifier = 0.3f;
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                var col = s.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
                var drag = s.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 1.5f;
                drag.limit = 100f;
            });
            int count = Mathf.RoundToInt(90 * Intensity);
            var center = new Vector3(board.center.x, board.center.y, -1f);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = center + new Vector3(dir.x * board.width * 0.25f, dir.y * board.height * 0.25f, 0f),
                    velocity = dir * Random.Range(4f, 11f) + Vector3.up * 2f,
                    startSize = Random.Range(0.18f, 0.45f),
                    startColor = Random.value < 0.5f ? _theme.Palette.Accent : colors[Random.Range(0, colors.Length)],
                    startLifetime = Random.Range(2.2f, 3.4f),
                    rotation = Random.Range(0f, 360f),
                };
                ps.Emit(ep, 1);
            }
        }

        System.Collections.IEnumerator SecondBurst(Rect board, Color[] colors)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            VictoryStars(board, colors);
        }

        void VictoryBubbles(float halfW, float halfH, Color[] colors)
        {
            var ps = Get("victory-bubbles", "bubble", s =>
            {
                var main = s.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
                main.gravityModifier = -0.05f;
                var noise = s.noise;
                noise.enabled = true;
                noise.strength = 0.5f;
                noise.frequency = 0.3f;
                var col = s.colorOverLifetime;
                col.enabled = true;
                col.color = Fade();
            });
            int count = Mathf.RoundToInt(110 * Intensity);
            for (int i = 0; i < count; i++)
            {
                var c = colors[Random.Range(0, colors.Length)];
                c.a = Random.Range(0.45f, 0.85f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(Random.Range(-halfW, halfW), -halfH - Random.Range(0.3f, 4f), -1f),
                    velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 3f), 0f),
                    startSize = Random.Range(0.18f, 0.7f),
                    startColor = c,
                    startLifetime = Random.Range(3.5f, 6f),
                };
                ps.Emit(ep, 1);
            }
        }

        public void Clear()
        {
            foreach (var s in _systems.Values) if (s != null) s.Clear(true);
        }

        // ------------------------------------------------------------------ helpers

        ParticleSystem Get(string key, string texture, System.Action<ParticleSystem> configure)
        {
            if (_systems.TryGetValue(key, out var existing) && existing != null) return existing;

            var go = new GameObject("FX " + key);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true; // keeps simulating particles emitted by script (emission module is off)
            main.duration = 1f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            configure(ps);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = SortingOrder;
            var material = new Material(_baseMaterial) { mainTexture = Texture(texture) };
            renderer.sharedMaterial = material;
            ps.Play();
            _systems[key] = ps;
            return ps;
        }

        /// <summary>Paper-like flipping: the width oscillates over the particle life.</summary>
        static void Flutter(ParticleSystem s)
        {
            var size = s.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            var flip = new AnimationCurve();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                flip.AddKey(t, Mathf.Abs(Mathf.Cos(t * Mathf.PI * 5f)) * 0.85f + 0.15f);
            }
            size.x = new ParticleSystem.MinMaxCurve(1f, flip);
            size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        }

        static ParticleSystem.MinMaxGradient Fade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        // ------------------------------------------------------------------ generated textures

        static Texture2D Texture(string kind)
        {
            if (Textures.TryGetValue(kind, out var t) && t != null) return t;
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                float a;
                switch (kind)
                {
                    case "rect":
                    {
                        var q = new Vector2(Mathf.Abs(p.x) - 0.85f, Mathf.Abs(p.y) - 0.85f);
                        float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.1f;
                        a = Mathf.Clamp01(0.5f - d * n * 0.5f);
                        break;
                    }
                    case "star":
                        a = Mathf.Clamp01(0.5f - StarDistance(p) * n * 0.5f);
                        break;
                    case "bubble":
                    {
                        float r = p.magnitude;
                        float fill = Mathf.Clamp01((0.92f - r) * n * 0.5f);
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 9f);
                        float shine = Mathf.Clamp01(1f - (p - new Vector2(-0.35f, 0.35f)).magnitude * 4f);
                        a = Mathf.Clamp01(fill * 0.25f + rim * 0.8f + shine * 0.9f) * fill;
                        break;
                    }
                    default: // glow
                    {
                        float r = p.magnitude;
                        float core = Mathf.Clamp01(1f - r * 1.6f);
                        float cross = Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.y)) * 9f) * Mathf.Clamp01(1f - r);
                        a = Mathf.Clamp01(core * core + cross * 0.8f);
                        break;
                    }
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "fx-" + kind, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(true);
            Textures[kind] = t;
            return t;
        }

        static float StarDistance(Vector2 p)
        {
            // Signed distance to a 5-point star (approx.), radius ~0.9.
            float a = Mathf.Atan2(p.x, p.y);
            float seg = Mathf.PI * 2f / 5f;
            float k = Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f;
            float r = p.magnitude;
            float outer = 0.95f, inner = 0.42f;
            float edge = Mathf.Lerp(outer, inner, Mathf.Abs(k) / (seg * 0.5f));
            return r - edge;
        }
    }
}
