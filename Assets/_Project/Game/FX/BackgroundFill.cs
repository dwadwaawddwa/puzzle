using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;

namespace PuzzleStudio.Game.FX
{
    /// <summary>
    /// Procedural full-screen background (solid, gradient, animated gradient, pattern, vignette) drawn behind
    /// everything, including the optional background picture.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BackgroundFill : MonoBehaviour
    {
        static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        static readonly int AngleId = Shader.PropertyToID("_Angle");
        static readonly int AnimateId = Shader.PropertyToID("_Animate");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");
        static readonly int PatternId = Shader.PropertyToID("_Pattern");
        static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");
        static readonly int PatternOpacityId = Shader.PropertyToID("_PatternOpacity");
        static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        Camera _camera;
        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;

        public static BackgroundFill Create(Transform parent, Camera camera)
        {
            var go = new GameObject("BackgroundFill", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, 8f);
            var fill = go.AddComponent<BackgroundFill>();
            fill._camera = camera;
            var mesh = new Mesh { name = "BackgroundFillQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            fill._renderer = go.GetComponent<MeshRenderer>();
            var mat = Resources.Load<Material>("Materials/Background");
            fill._renderer.sharedMaterial = mat != null ? mat : new Material(Shader.Find("PuzzleStudio/Background"));
            fill._renderer.sortingOrder = -200;
            fill._mpb = new MaterialPropertyBlock();
            return fill;
        }

        /// <param name="palette">Current palette (may come from per-level colors).</param>
        /// <param name="perLevelColors">True when the palette was derived from a level picture.</param>
        public void Apply(BackgroundConfig bg, ThemePalette palette, bool perLevelColors, bool reduceMotion)
        {
            Color a = palette.Background;
            bool dark = ColorUtil.RelativeLuminance(a) < 0.35f;
            Color b;
            if (!perLevelColors && bg.gradient != null && bg.gradient.Count > 1)
                b = ColorUtil.Parse(bg.gradient[1], ColorUtil.Shade(a, dark ? 0.06f : -0.06f));
            else b = ColorUtil.Shade(a, dark ? 0.07f : -0.07f);

            bool gradient = bg.type == BackgroundType.Gradient || bg.type == BackgroundType.AnimatedGradient ||
                            bg.type == BackgroundType.Image || bg.type == BackgroundType.BlurredLevel;
            if (!gradient) b = a;
            bool animate = bg.type == BackgroundType.AnimatedGradient && !reduceMotion;

            _mpb.SetColor(ColorAId, a);
            _mpb.SetColor(ColorBId, b);
            _mpb.SetFloat(AngleId, bg.gradientAngle);
            _mpb.SetFloat(AnimateId, animate ? 1f : 0f);
            _mpb.SetFloat(SpeedId, Mathf.Max(0.01f, bg.animationSpeed));
            _mpb.SetFloat(PatternId, (float)bg.pattern);
            _mpb.SetColor(PatternColorId, dark ? Color.white : ColorUtil.Shade(palette.Text, 0f));
            _mpb.SetFloat(PatternOpacityId, Mathf.Clamp01(bg.patternOpacity));
            _mpb.SetFloat(PatternScaleId, Mathf.Clamp(bg.patternScale, 0.25f, 4f));
            _mpb.SetFloat(VignetteId, Mathf.Clamp01(bg.vignette));
            _renderer.SetPropertyBlock(_mpb);
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            float h = _camera.orthographicSize * 2f, w = h * _camera.aspect;
            transform.localScale = new Vector3(w + 0.1f, h + 0.1f, 1f);
            _mpb.SetFloat(AspectId, _camera.aspect);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
