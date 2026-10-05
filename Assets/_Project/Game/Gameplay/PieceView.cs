using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>
    /// One rendered piece (unit quad + shared material + property block). Animates itself and disables its
    /// Update when idle, so a still board costs nothing.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PieceView : MonoBehaviour
    {
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int BorderId = Shader.PropertyToID("_Border");
        static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        static readonly int HighlightWidthId = Shader.PropertyToID("_HighlightWidth");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        public int PieceId { get; private set; }

        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;
        Vector2 _size;
        float _radius, _border, _highlightWidth;
        Color _borderColor;

        // Movement
        Vector3 _from, _to;
        float _moveT = 1f, _moveDuration;
        Ease _moveEase;

        // Scale (hover / lift / pop)
        float _scale = 1f, _scaleTarget = 1f;
        float _popT = 1f;
        const float PopDuration = 0.35f;

        // Highlight
        Color _highlightColor;
        float _highlight, _highlightTarget;
        float _flashT = 1f;
        Color _flashColor;
        const float FlashDuration = 0.6f;

        // Rotation (quarter turns, animated)
        float _angle, _angleFrom, _angleTo, _rotT = 1f;
        int _quarter;
        const float RotateDuration = 0.22f;

        int _baseSorting;
        bool _dirty;

        // Drop shadow (separate quad under the piece; grows when the piece is lifted)
        Transform _shadow;
        MeshRenderer _shadowRenderer;
        MaterialPropertyBlock _shadowMpb;
        float _shadowStrength, _shadowOffset, _shadowSoftness, _liftScale = 1.08f, _lastScale = 1f;

        public bool IsMoving => _moveT < 1f;
        public Vector3 TargetPosition => _to;

        public void Init(int pieceId, Mesh quad, Material material, Texture texture, Rect uv)
        {
            PieceId = pieceId;
            GetComponent<MeshFilter>().sharedMesh = quad;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.Clear();
            _mpb.SetTexture(MainTexId, texture);
            _mpb.SetVector(UVRectId, new Vector4(uv.x, uv.y, uv.width, uv.height));
            _mpb.SetColor(TintId, Color.white);
            _highlight = _highlightTarget = 0f;
            _flashT = 1f;
            _popT = 1f;
            _moveT = 1f;
            _rotT = 1f;
            _quarter = 0;
            _angle = _angleFrom = _angleTo = 0f;
            _scale = _scaleTarget = 1f;
            _dirty = true;
            enabled = true;
        }

        public void SetStyle(Vector2 size, float radius, float border, Color borderColor, float highlightWidth = 0.05f)
        {
            _highlightWidth = highlightWidth;
            _size = size;
            _radius = radius;
            _border = border;
            _borderColor = borderColor;
            _dirty = true;
            enabled = true;
        }

        /// <param name="strength">0 = no shadow.</param>
        public void SetShadow(float strength, float offset, float softness, float liftScale)
        {
            _shadowStrength = strength;
            _shadowOffset = offset;
            _shadowSoftness = softness;
            _liftScale = Mathf.Max(1.01f, liftScale);
            if (strength > 0f && _shadow == null)
            {
                var go = new GameObject(name + " Shadow", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform.parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                _shadowRenderer = go.GetComponent<MeshRenderer>();
                _shadowRenderer.sharedMaterial = _renderer.sharedMaterial;
                _shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shadowRenderer.sortingOrder = _baseSorting - 1;
                _shadowMpb = new MaterialPropertyBlock();
                _shadow = go.transform;
            }
            if (_shadow != null) _shadow.gameObject.SetActive(strength > 0f && gameObject.activeSelf);
            _dirty = true;
            enabled = true;
        }

        void OnEnable() { if (_shadow != null) _shadow.gameObject.SetActive(_shadowStrength > 0f); }
        void OnDisable() { if (_shadow != null) _shadow.gameObject.SetActive(false); }
        void OnDestroy() { if (_shadow != null) Destroy(_shadow.gameObject); }

        void UpdateShadow()
        {
            if (_shadow == null || _shadowStrength <= 0f) return;
            float s = _lastScale;
            float lift = Mathf.Clamp01((s - 1f) / (_liftScale - 1f));
            float offset = _shadowOffset * (1f + 2.2f * lift);
            float soft = _shadowSoftness * (1f + lift);
            var pos = transform.localPosition;
            _shadow.localPosition = new Vector3(pos.x + offset * 0.35f, pos.y - offset, pos.z + 0.01f);
            _shadow.localRotation = transform.localRotation;
            var size = new Vector2(_size.x * s + soft * 2f, _size.y * s + soft * 2f);
            _shadow.localScale = new Vector3(size.x, size.y, 1f);
            _shadowMpb.SetTexture(MainTexId, Texture2D.whiteTexture);
            _shadowMpb.SetVector(UVRectId, new Vector4(0, 0, 1, 1));
            _shadowMpb.SetVector(SizeId, new Vector4(size.x, size.y, 0, 0));
            _shadowMpb.SetFloat(RadiusId, _radius * s + soft);
            _shadowMpb.SetFloat(BorderId, 0f);
            _shadowMpb.SetFloat(SoftnessId, soft);
            _shadowMpb.SetColor(HighlightId, new Color(0, 0, 0, 0));
            _shadowMpb.SetColor(TintId, new Color(0f, 0f, 0f, _shadowStrength * (0.45f + 0.55f * lift)));
            _shadowRenderer.SetPropertyBlock(_shadowMpb);
        }

        public void SetTint(Color tint)
        {
            _mpb.SetColor(TintId, tint);
            _renderer.SetPropertyBlock(_mpb);
        }

        public void SetSorting(int order)
        {
            _baseSorting = order;
            _renderer.sortingOrder = order;
            if (_shadowRenderer != null) _shadowRenderer.sortingOrder = order - 1;
        }

        public void SnapTo(Vector3 position)
        {
            _from = _to = position;
            _moveT = 1f;
            transform.localPosition = position;
            UpdateShadow();
        }

        public void MoveTo(Vector3 position, float duration, Ease ease = Ease.OutCubic)
        {
            if ((position - _to).sqrMagnitude < 1e-8f && _moveT >= 1f) return;
            _from = transform.localPosition;
            _to = position;
            _moveDuration = Mathf.Max(0.0001f, duration);
            _moveEase = ease;
            _moveT = duration <= 0f ? 1f : 0f;
            if (duration <= 0f) transform.localPosition = position;
            enabled = true;
        }

        /// <summary>Follow the pointer while dragged (no easing).</summary>
        public void DragTo(Vector3 position)
        {
            _from = _to = position;
            _moveT = 1f;
            transform.localPosition = position;
            UpdateShadow();
        }

        public void SetScaleTarget(float scale)
        {
            if (Mathf.Approximately(scale, _scaleTarget)) return;
            _scaleTarget = scale;
            enabled = true;
        }

        public void SetHighlight(float amount, Color color)
        {
            if (Mathf.Approximately(amount, _highlightTarget) && color == _highlightColor) return;
            _highlightTarget = amount;
            _highlightColor = color;
            enabled = true;
        }

        /// <summary>Quick scale pop + colored flash (piece snapped into its correct place).</summary>
        public void Pop(Color flashColor)
        {
            _popT = 0f;
            _flashT = 0f;
            _flashColor = flashColor;
            enabled = true;
        }

        public void SetRotation(int quarterTurns, bool animate)
        {
            quarterTurns = ((quarterTurns % 4) + 4) % 4;
            if (!animate)
            {
                _quarter = quarterTurns;
                _angle = _angleFrom = _angleTo = -90f * quarterTurns;
                _rotT = 1f;
                _dirty = true;
                enabled = true;
                return;
            }
            int delta = ((quarterTurns - _quarter) % 4 + 4) % 4;
            if (delta == 0) return;
            _quarter = quarterTurns;
            // Turn in the direction of the move: +1 quarter = clockwise (-90°), -1 (= 3) = counter-clockwise.
            float step = delta == 1 ? -90f : delta == 3 ? 90f : -180f;
            _angleFrom = _angle;
            _angleTo += step; // cumulative, so a turn started during another one continues smoothly
            _rotT = 0f;
            enabled = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool busy = _dirty;
            _dirty = false;

            if (_moveT < 1f)
            {
                _moveT = Mathf.Min(1f, _moveT + dt / _moveDuration);
                transform.localPosition = Vector3.LerpUnclamped(_from, _to, Easing.Evaluate(_moveEase, _moveT));
                _renderer.sortingOrder = _baseSorting + 10;
                if (_moveT >= 1f) _renderer.sortingOrder = _baseSorting;
                if (_shadowRenderer != null) _shadowRenderer.sortingOrder = _renderer.sortingOrder - 1;
                busy = true;
            }

            if (_rotT < 1f)
            {
                _rotT = Mathf.Min(1f, _rotT + dt / RotateDuration);
                _angle = Mathf.LerpUnclamped(_angleFrom, _angleTo, Easing.Evaluate(Ease.OutBack, _rotT));
                busy = true;
            }

            if (!Mathf.Approximately(_scale, _scaleTarget))
            {
                _scale = Mathf.MoveTowards(_scale, _scaleTarget, dt * 1.6f);
                busy = true;
            }

            if (!Mathf.Approximately(_highlight, _highlightTarget))
            {
                _highlight = Mathf.MoveTowards(_highlight, _highlightTarget, dt * 5f);
                busy = true;
            }

            float pop = 0f;
            if (_popT < 1f)
            {
                _popT = Mathf.Min(1f, _popT + dt / PopDuration);
                pop = Mathf.Sin(_popT * Mathf.PI) * 0.08f;
                busy = true;
            }

            float flash = 0f;
            if (_flashT < 1f)
            {
                _flashT = Mathf.Min(1f, _flashT + dt / FlashDuration);
                flash = (1f - _flashT) * 0.65f;
                busy = true;
            }

            if (busy) Apply(pop, flash);
            else enabled = false;
        }

        void Apply(float pop, float flash)
        {
            float s = _scale + pop;
            _lastScale = s;
            transform.localScale = new Vector3(_size.x * s, _size.y * s, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, _angle);

            _mpb.SetVector(SizeId, new Vector4(_size.x * s, _size.y * s, 0f, 0f));
            _mpb.SetFloat(RadiusId, _radius * s);
            _mpb.SetFloat(BorderId, _border);
            _mpb.SetFloat(HighlightWidthId, _highlightWidth);
            _mpb.SetColor(BorderColorId, _borderColor);

            Color h = _highlightColor;
            float amount = _highlight;
            if (flash > amount) { h = _flashColor; amount = flash; }
            h.a = amount;
            _mpb.SetColor(HighlightId, h);
            _mpb.SetFloat(SoftnessId, 0f);
            _renderer.SetPropertyBlock(_mpb);
            UpdateShadow();
        }
    }
}
