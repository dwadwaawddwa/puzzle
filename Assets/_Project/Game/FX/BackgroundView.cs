using UnityEngine;

namespace PuzzleStudio.Game.FX
{
    /// <summary>
    /// Full-screen background picture drawn behind the board (cover-fit, faded over the background color).
    /// Used for the "Image" and "level picture (blurred)" background types.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BackgroundView : MonoBehaviour
    {
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int BorderId = Shader.PropertyToID("_Border");
        static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        Camera _camera;
        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;
        Texture _texture;
        float _opacity = 1f, _shown;

        public static BackgroundView Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Background", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, 5f);
            var view = go.AddComponent<BackgroundView>();
            view.Init(camera);
            return view;
        }

        void Init(Camera camera)
        {
            _camera = camera;
            var mesh = new Mesh { name = "BackgroundQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = GetComponent<MeshRenderer>();
            var mat = Resources.Load<Material>("Materials/Piece");
            _renderer.sharedMaterial = mat != null ? mat : new Material(Shader.Find("PuzzleStudio/Piece"));
            _renderer.sortingOrder = -100;
            _renderer.enabled = false;
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Shows a picture (null hides it).</summary>
        public void Show(Texture texture, float opacity)
        {
            _texture = texture;
            _opacity = Mathf.Clamp01(opacity);
            if (texture == null) { _renderer.enabled = false; _shown = 0f; return; }
            _renderer.enabled = true;
            _shown = 0f;
        }

        void LateUpdate()
        {
            if (_texture == null || _camera == null) return;
            _shown = Mathf.MoveTowards(_shown, 1f, Time.unscaledDeltaTime * 3f);
            float viewH = _camera.orthographicSize * 2f, viewW = viewH * _camera.aspect;
            float texAspect = (float)_texture.width / Mathf.Max(1, _texture.height);
            float w = viewW, h = viewW / texAspect;
            if (h < viewH) { h = viewH; w = viewH * texAspect; }
            transform.localScale = new Vector3(w, h, 1f);

            _mpb.SetTexture(MainTexId, _texture);
            _mpb.SetVector(UVRectId, new Vector4(0, 0, 1, 1));
            _mpb.SetVector(SizeId, new Vector4(w, h, 0, 0));
            _mpb.SetFloat(RadiusId, 0f);
            _mpb.SetFloat(BorderId, 0f);
            _mpb.SetColor(HighlightId, new Color(0, 0, 0, 0));
            _mpb.SetColor(TintId, new Color(1f, 1f, 1f, _opacity * _shown));
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
