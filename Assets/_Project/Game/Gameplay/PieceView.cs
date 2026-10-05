using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
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
        static readonly int BackId = Shader.PropertyToID("_Back");
        static readonly int BackColorId = Shader.PropertyToID("_BackColor");
        static readonly int BackColor2Id = Shader.PropertyToID("_BackColor2");
        static readonly int OutlineId = Shader.PropertyToID("_Outline");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int BadgeId = Shader.PropertyToID("_Badge");
        static readonly int BadgeRadiusId = Shader.PropertyToID("_BadgeRadius");

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

        // Memory card: face down (patterned back) or face up (picture + symbol), flipped around its vertical axis.
        bool _faceUp = true, _shownFaceUp = true;
        float _flipT = 1f;
        const float FlipDuration = 0.34f;
        string _label = "", _pendingLabel = "";
        Color _outline, _pendingOutline;
        Color _backColor, _backColor2;
        float _completeDelay = -1f;
        Color _completeFlash;
        TextMesh _text;
        MeshRenderer _textRenderer;
        static Font _labelFont;
        static float _glyphHeight;   // height of a digit at the label font size, in font pixels

        public bool IsMoving => _moveT < 1f;
        public bool IsCard { get; private set; }
        public bool ShowsFace => _shownFaceUp;
        public string Label => _shownFaceUp ? _label : "";
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
            IsCard = false;
            _faceUp = _shownFaceUp = true;
            _flipT = 1f;
            _label = _pendingLabel = "";
            _outline = _pendingOutline = Color.clear;
            _completeDelay = -1f;
            _dirty = true;
            enabled = true;
        }

        // ------------------------------------------------------------------ Memory cards

        /// <summary>Turns this piece into a card (Memory mode) with the given back colors.</summary>
        public void SetCard(Color back, Color back2)
        {
            IsCard = true;
            _backColor = back;
            _backColor2 = back2;
            _dirty = true;
            enabled = true;
        }

        /// <summary>
        /// Face up shows the picture plus a symbol: <paramref name="label"/> (number / letter, "" = none) and/or a
        /// colored <paramref name="outline"/> (alpha 0 = none). Changing side plays a flip; the new face appears half-way.
        /// </summary>
        public void SetCardFace(bool faceUp, string label, Color outline, bool animate)
        {
            label ??= "";
            bool flip = faceUp != _faceUp;
            _faceUp = faceUp;
            _pendingLabel = label;
            _pendingOutline = outline;
            _completeDelay = -1f;
            if (flip && animate && !UiAnim.ReduceMotion)
            {
                // A flip started during another one continues from the same edge-on point.
                _flipT = _flipT < 1f ? 1f - _flipT : 0f;
            }
            else if (!flip && animate && _flipT < 1f)
            {
                // Same side while a flip runs: the new face is applied half-way (or now, if already shown).
                if (_shownFaceUp == faceUp) { _label = label; _outline = outline; }
            }
            else
            {
                _flipT = 1f;
                _shownFaceUp = faceUp;
                _label = label;
                _outline = outline;
            }
            _dirty = true;
            enabled = true;
        }

        /// <summary>Pair found: after <paramref name="delay"/> the symbol disappears and the piece of the picture flashes.</summary>
        public void CompleteCard(float delay, Color flash)
        {
            _completeFlash = flash;
            _completeDelay = Mathf.Max(0f, delay);
            enabled = true;
        }

        void FinishCard()
        {
            _completeDelay = -1f;
            _label = _pendingLabel = "";
            _outline = _pendingOutline = Color.clear;
            Pop(_completeFlash);
        }

        void EnsureText()
        {
            if (_text != null) return;
            if (_labelFont == null)
            {
                _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _labelFont.RequestCharactersInTexture("8", 96, FontStyle.Bold);
                _glyphHeight = _labelFont.GetCharacterInfo('8', out var info, 96, FontStyle.Bold) ? Mathf.Max(1, info.glyphHeight) : 70f;
            }
            var go = new GameObject(name + " Label", typeof(MeshRenderer), typeof(TextMesh));
            go.transform.SetParent(transform.parent, false);
            _text = go.GetComponent<TextMesh>();
            _text.font = _labelFont;
            _text.fontSize = 96;
            _text.fontStyle = FontStyle.Bold;
            _text.characterSize = 1f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = Color.white;
            _textRenderer = go.GetComponent<MeshRenderer>();
            _textRenderer.sharedMaterial = _labelFont.material;
            _textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _textRenderer.receiveShadows = false;
        }

        void UpdateLabel(float scale, float flipX)
        {
            bool show = IsCard && _shownFaceUp && _label.Length > 0 && gameObject.activeSelf;
            if (!show)
            {
                if (_text != null) _text.gameObject.SetActive(false);
                return;
            }
            EnsureText();
            _text.gameObject.SetActive(true);
            if (_text.text != _label) _text.text = _label;
            float m = Mathf.Min(_size.x, _size.y) * scale;
            // TextMesh: 1 font pixel = 0.1 world unit at character size 1.
            float h = m * (_label.Length > 1 ? 0.27f : 0.32f);
            float s = h / (_glyphHeight * 0.1f);
            var t = _text.transform;
            t.localPosition = transform.localPosition + new Vector3(0f, -h * 0.04f, -0.02f);
            t.localScale = new Vector3(s * flipX, s, 1f);
            _textRenderer.sortingOrder = _renderer.sortingOrder + 1;
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
        void OnDisable()
        {
            if (_shadow != null) _shadow.gameObject.SetActive(false);
            if (_text != null && !gameObject.activeSelf) _text.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if (_shadow != null) Destroy(_shadow.gameObject);
            if (_text != null) Destroy(_text.gameObject);
        }

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
            _shadow.localScale = new Vector3(size.x * FlipScale, size.y, 1f);
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

            if (_flipT < 1f)
            {
                float before = _flipT;
                _flipT = Mathf.Min(1f, _flipT + dt / FlipDuration);
                if (before < 0.5f && _flipT >= 0.5f)
                {
                    // Edge-on: swap to the other side.
                    _shownFaceUp = _faceUp;
                    _label = _pendingLabel;
                    _outline = _pendingOutline;
                }
                busy = true;
            }

            if (_completeDelay >= 0f)
            {
                _completeDelay -= dt;
                if (_completeDelay < 0f && _flipT >= 1f) FinishCard();
                else if (_completeDelay < 0f) _completeDelay = 0f;
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

        /// <summary>Horizontal squash of a card being flipped (1 = flat on the board, 0 = edge-on).</summary>
        float FlipScale => _flipT >= 1f ? 1f : Mathf.Max(0.02f, Mathf.Abs(Mathf.Cos(_flipT * Mathf.PI)));

        void Apply(float pop, float flash)
        {
            float flipLift = _flipT < 1f ? Mathf.Sin(_flipT * Mathf.PI) * 0.06f : 0f;
            float s = _scale + pop + flipLift;
            _lastScale = s;
            float flipX = FlipScale;
            transform.localScale = new Vector3(_size.x * s * flipX, _size.y * s, 1f);
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

            bool back = IsCard && !_shownFaceUp;
            float m = Mathf.Min(_size.x, _size.y) * s;
            _mpb.SetFloat(BackId, back ? 1f : 0f);
            _mpb.SetColor(BackColorId, _backColor);
            _mpb.SetColor(BackColor2Id, _backColor2);
            bool outline = IsCard && !back && _outline.a > 0f;
            _mpb.SetColor(OutlineId, outline ? _outline : Color.clear);
            _mpb.SetFloat(OutlineWidthId, outline ? Mathf.Max(m * 0.085f, 0.004f) : 0f);
            bool badge = IsCard && !back && _label.Length > 0;
            _mpb.SetColor(BadgeId, badge ? new Color(0.06f, 0.06f, 0.09f, 0.62f) : Color.clear);
            _mpb.SetFloat(BadgeRadiusId, badge ? m * (_label.Length > 1 ? 0.27f : 0.25f) : 0f);
            _renderer.SetPropertyBlock(_mpb);
            UpdateShadow();
            UpdateLabel(s, flipX);
        }
    }
}
