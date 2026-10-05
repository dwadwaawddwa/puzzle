using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>
    /// Draws a UI Toolkit tree into an image through an off-screen panel (PanelSettings.targetTexture):
    /// store capsules, achievement icons… Same text rendering and fonts as the game.
    /// </summary>
    public sealed class UiImageRenderer : IDisposable
    {
        readonly GameObject _go;
        readonly UIDocument _document;
        readonly PanelSettings _settings;
        RenderTexture _target;

        public UiImageRenderer(Transform parent)
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/RuntimeTheme");
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _settings.scale = 1f;
            _settings.clearColor = true;
            _settings.colorClearValue = Color.clear;

            _go = new GameObject("Image Renderer");
            _go.transform.SetParent(parent, false);
            _go.SetActive(false);
            _document = _go.AddComponent<UIDocument>();
            _document.panelSettings = _settings;
        }

        /// <summary>
        /// Renders <paramref name="build"/> at <paramref name="width"/> × <paramref name="height"/>.
        /// <paramref name="afterLayout"/> runs once the layout is known (e.g. to shrink a title that is too wide).
        /// The texture given to <paramref name="done"/> belongs to the caller.
        /// </summary>
        public IEnumerator Render(int width, int height, Action<VisualElement> build, Action<Texture2D> done, Action<VisualElement> afterLayout = null)
        {
            _go.SetActive(false);
            if (_target == null || _target.width != width || _target.height != height)
            {
                ReleaseTarget();
                _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "UiImage" };
                _target.Create();
            }
            _settings.targetTexture = _target;
            _go.SetActive(true);

            var root = _document.rootVisualElement;
            root.Clear();
            root.style.width = width;
            root.style.height = height;
            root.style.overflow = Overflow.Hidden;
            build(root);

            yield return null;
            if (afterLayout != null)
            {
                afterLayout(root);
                yield return null;
            }
            yield return null;
            yield return new WaitForEndOfFrame();

            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = previous;
            _go.SetActive(false);
            done(tex);
        }

        void ReleaseTarget()
        {
            if (_target == null) return;
            _settings.targetTexture = null;
            _target.Release();
            Object.Destroy(_target);
            _target = null;
        }

        public void Dispose()
        {
            ReleaseTarget();
            if (_go != null) Object.Destroy(_go);
            if (_settings != null) Object.Destroy(_settings);
        }
    }

    /// <summary>Vertical two-color gradient (UI Toolkit has no gradient background).</summary>
    public sealed class GradientElement : VisualElement
    {
        public Color Top, Bottom;

        public GradientElement(Color top, Color bottom)
        {
            Top = top;
            Bottom = bottom;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 1 || r.height < 1) return;
            var mesh = mgc.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = Bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = Top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = Top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = Bottom });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }
    }
}
