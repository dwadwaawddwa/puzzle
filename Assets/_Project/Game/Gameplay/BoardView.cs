using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>
    /// Renders a puzzle mode: lays out the board in world space, keeps PieceViews in sync with the logic,
    /// and shows selection / hover / drag / hint / preview / victory feedback. No game rules live here.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float ReferenceHeight = 1080f;
        public const float MoveDuration = 0.28f;
        /// <summary>Memory: how long a found pair keeps its symbols before they make way for the picture.</summary>
        public const float CardHold = 0.7f;

        readonly List<PieceView> _pool = new List<PieceView>();
        PieceView[] _views = System.Array.Empty<PieceView>();   // by piece id
        PieceView _preview;
        Mesh _quad;
        Material _material;

        IPuzzleMode _mode;
        ICardMode _cards;                       // Memory mode, else null
        bool[] _completed = System.Array.Empty<bool>();
        Texture2D _texture;
        ThemeService _theme;
        PieceStyle _style;
        Camera _camera;

        Rect _boardRect;          // world space
        Vector2 _cell;            // cell size in world units
        float _px;                // world units per reference pixel

        int _hoverCell = -1, _dropCell = -1, _dragCell = -1;
        Vector3 _dragOffset;
        Hint _hint;
        float _hintTime;
        const float HintDuration = 2.4f;

        float _previewAlpha, _previewTarget;

        // Gamepad / keyboard cursor (an outline drawn above the pieces).
        GameObject _cursor;
        MaterialPropertyBlock _cursorMpb;
        int _cursorCell = -1;
        bool _cursorVisible;
        Vector3 _cursorPos;
        float _seamT = 0f, _seamTarget = 0f;   // 0 = normal pieces, 1 = seamless full image (victory)
        float _zoomT = 1f;                      // victory zoom of the finished picture

        public Rect BoardRect => _boardRect;
        /// <summary>Player UI size multiplier: the HUD grows, so the board area shrinks.</summary>
        public System.Func<float> UiScale = () => 1f;
        /// <summary>Normalized screen area (origin top-left) the board is fitted into. Null = default margins.</summary>
        public System.Func<Rect> Area;
        public bool IsDragging => _dragCell >= 0;
        public int DragCell => _dragCell;
        public int CursorCell => _cursorCell;
        public bool CursorVisible => _cursorVisible;
        public int HoverCell => _hoverCell;
        /// <summary>Time for the last move to finish on screen before the victory starts.</summary>
        public float SettleTime => _cards != null ? CardHold + 0.3f : MoveDuration + 0.1f;
        /// <summary>The view of a piece (tests, debug).</summary>
        public PieceView ViewOf(int pieceId) => pieceId >= 0 && pieceId < _views.Length ? _views[pieceId] : null;

        public void Init(Camera cam, ThemeService theme)
        {
            _camera = cam;
            _theme = theme;
            _quad = CreateQuad();
            var mat = Resources.Load<Material>("Materials/Piece");
            _material = mat != null ? mat : new Material(Shader.Find("PuzzleStudio/Piece"));
        }

        public void Build(IPuzzleMode mode, Texture2D texture, PieceStyle style)
        {
            _mode = mode;
            _cards = mode as ICardMode;
            _completed = new bool[mode.Layout.CellCount];
            _texture = texture;
            _style = style ?? new PieceStyle();
            _hint = Hint.None;
            _hoverCell = _dropCell = _dragCell = -1;
            _cursorCell = -1;
            HideCursor();
            _seamT = _seamTarget = 0f;
            _previewAlpha = _previewTarget = 0f;
            _zoomT = 1f;
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;

            var layout = mode.Layout;
            int n = layout.CellCount;
            while (_pool.Count < n) _pool.Add(CreatePieceView($"Piece_{_pool.Count}"));
            _views = new PieceView[n];
            for (int i = 0; i < _pool.Count; i++)
            {
                bool used = i < n;
                _pool[i].gameObject.SetActive(used);
                if (!used) continue;
                var v = _pool[i];
                v.Init(i, _quad, _material, texture,
                    ImageSlicer.CellUV(layout.Crop, layout.Cols, layout.Rows, layout.Col(i), layout.Row(i)));
                v.SetSorting(0);
                _views[i] = v;
            }
            ApplyCardColors();

            if (_preview == null) _preview = CreatePieceView("Preview");
            _preview.Init(-1, _quad, _material, texture, ImageSlicer.CropUV(layout.Crop));
            _preview.SetSorting(500);
            _preview.SetTint(new Color(1, 1, 1, 0));
            _preview.gameObject.SetActive(false);

            Relayout();
            Sync(animate: false);
        }

        public void Clear()
        {
            foreach (var v in _pool) v.gameObject.SetActive(false);
            if (_preview != null) _preview.gameObject.SetActive(false);
            HideCursor();
            _views = System.Array.Empty<PieceView>();
            _mode = null;
            _cards = null;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Fits the board (keeping the picture ratio) in its layout area, centered.</summary>
        public void Relayout()
        {
            if (_mode == null || _camera == null) return;
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            _px = 2f * halfH / ReferenceHeight;
            var a = Area != null ? Area() : PuzzleStudio.Game.UI.LayoutService.DefaultBoardArea(_camera.aspect, Mathf.Max(0.5f, UiScale()), false);

            float areaW = a.width * 2f * halfW, areaH = a.height * 2f * halfH;
            float areaCx = -halfW + (a.x + a.width * 0.5f) * 2f * halfW;
            float areaCy = halfH - (a.y + a.height * 0.5f) * 2f * halfH;

            float aspect = _mode.Layout.ImageAspect;
            float w = areaW, h = areaW / aspect;
            if (h > areaH) { h = areaH; w = areaH * aspect; }

            _boardRect = new Rect(areaCx - w * 0.5f, areaCy - h * 0.5f, w, h);
            _cell = new Vector2(w / _mode.Layout.Cols, h / _mode.Layout.Rows);

            ApplyPieceStyle();
            ApplyCardColors();
            _preview.SetStyle(_boardRect.size, _style.cornerRadius * _px, 0f, Color.clear);
            _preview.SnapTo(new Vector3(_boardRect.center.x, _boardRect.center.y, 0f));
            Sync(animate: false);
        }

        void ApplyPieceStyle()
        {
            float seam = 1f - Easing.Evaluate(Ease.InOutCubic, _seamT);
            float gap = Mathf.Min(_style.gap * _px, Mathf.Min(_cell.x, _cell.y) * 0.2f) * seam;
            float radius = _style.cornerRadius * _px * seam;
            float border = _style.borderWidth * _px * seam;
            Color borderColor = ColorUtil.Parse(_style.borderColor, Color.white);
            var size = new Vector2(_cell.x - gap, _cell.y - gap);
            float ring = Mathf.Max(5f * _px, border * 1.5f);
            float shadow = _style.shadow ? Mathf.Clamp01(_style.shadowStrength) * seam : 0f;
            foreach (var v in _views)
            {
                v.SetStyle(size, radius, border, borderColor, ring);
                v.SetShadow(shadow, Mathf.Max(1f, _style.shadowOffset) * _px, 6f * _px, _style.liftScale);
            }
        }

        /// <summary>Memory card backs use the theme's main color (or the picture's, with "colors from pictures").</summary>
        void ApplyCardColors()
        {
            if (_cards == null) return;
            Color back = _theme.Palette.Primary;
            back.a = 1f;
            Color pattern = ColorUtil.RelativeLuminance(back) > 0.4f ? ColorUtil.Shade(back, -0.3f) : ColorUtil.Shade(back, 0.32f);
            foreach (var v in _views) v.SetCard(back, pattern);
        }

        public Vector3 CellCenter(int cell)
        {
            var l = _mode.Layout;
            float x = _boardRect.xMin + (l.Col(cell) + 0.5f) * _cell.x;
            float y = _boardRect.yMax - (l.Row(cell) + 0.5f) * _cell.y;
            return new Vector3(x, y, 0f);
        }

        /// <returns>Cell under a world position, or -1.</returns>
        public int CellAt(Vector3 world)
        {
            if (_mode == null || !_boardRect.Contains(new Vector2(world.x, world.y))) return -1;
            var l = _mode.Layout;
            int col = Mathf.Clamp((int)((world.x - _boardRect.xMin) / _cell.x), 0, l.Cols - 1);
            int row = Mathf.Clamp((int)((_boardRect.yMax - world.y) / _cell.y), 0, l.Rows - 1);
            return l.Cell(col, row);
        }

        // ------------------------------------------------------------------ sync with logic

        /// <summary>Moves every view to the cell its piece now occupies.</summary>
        public void Sync(bool animate)
        {
            if (_mode == null) return;
            bool solved = _mode.IsSolved();
            foreach (var p in _mode.Pieces)
            {
                var v = _views[p.Id];
                v.gameObject.SetActive(!p.IsEmpty || solved);
                if (p.Id == _dragPiece) continue;
                var target = CellCenter(p.Cell);
                if (animate) v.MoveTo(target, MoveDuration * AnimSpeed);
                else v.SnapTo(target);
                v.SetRotation(p.Rotation, animate);
            }
            if (_cards != null) SyncCards(animate);
            RefreshHighlights();
        }

        /// <summary>Memory: re-applies the symbols (after the colorblind option changed) without animation.</summary>
        public void RefreshCards()
        {
            if (_cards != null) SyncCards(animate: false);
        }

        /// <summary>Memory: turns the cards to match the logic (face down / face up with symbol / found pair).</summary>
        void SyncCards(bool animate)
        {
            var style = _cards.Symbols;
            // Colorblind players also get numbers on colored cards.
            bool numbersToo = style == MemorySymbols.Colors && _theme.ColorblindMode;
            foreach (var p in _mode.Pieces)
            {
                int cell = p.Cell;
                var v = _views[p.Id];
                int symbol = _cards.SymbolOf(cell);
                string label = style == MemorySymbols.Colors
                    ? (numbersToo ? CardSymbols.Label(MemorySymbols.Numbers, symbol) : "")
                    : CardSymbols.Label(style, symbol);
                Color outline = style == MemorySymbols.Colors ? CardSymbols.ColorOf(symbol) : Color.clear;

                if (_cards.IsMatched(cell))
                {
                    if (_completed[cell]) continue;
                    _completed[cell] = true;
                    if (symbol < 0 || !animate)
                    {
                        v.SetCardFace(true, "", Color.clear, false);   // free card, or a board shown already solved
                        continue;
                    }
                    // Both symbols stay visible a moment, then the two pieces of the picture are done.
                    v.SetCardFace(true, label, outline, true);
                    v.CompleteCard(CardHold, _style.correctGlow ? _theme.GlowColor : Color.clear);
                }
                else
                {
                    _completed[cell] = false;
                    v.SetCardFace(_cards.IsFaceUp(cell), label, outline, animate);
                }
            }
        }

        public void PlayCorrect(int pieceId)
        {
            if (pieceId < 0 || pieceId >= _views.Length) return;
            if (_cards != null) return;   // cards pop when their symbols disappear (SyncCards)
            if (_style.correctGlow) _views[pieceId].Pop(_theme.GlowColor);
        }

        /// <summary>Victory: gaps, borders and corners fade out so the full picture appears, then a small zoom.</summary>
        public void PlayVictory()
        {
            HideCursor();
            _seamTarget = 1f;
            _zoomT = 0f;
        }

        /// <summary>World position of a piece (particles).</summary>
        public Vector3 PieceWorldPosition(int pieceId) =>
            pieceId >= 0 && pieceId < _views.Length ? _views[pieceId].transform.position : transform.position;

        /// <summary>Approximate size of one cell in world units.</summary>
        public float CellSize => Mathf.Min(_cell.x, _cell.y);

        /// <summary>Board rectangle in world space.</summary>
        public Rect WorldBoardRect => new Rect(transform.TransformPoint(_boardRect.position), _boardRect.size * transform.localScale.x);

        float AnimSpeed => _theme?.Config.uiStyle.animationSpeed > 0 ? 1f / _theme.Config.uiStyle.animationSpeed : 1f;

        // ------------------------------------------------------------------ pointer feedback

        int _dragPiece = -1;

        public void SetHover(int cell)
        {
            if (cell == _hoverCell) return;
            _hoverCell = cell;
            RefreshHighlights();
        }

        // ------------------------------------------------------------------ gamepad / keyboard cursor

        /// <summary>Shows the cursor on <paramref name="cell"/> (-1 = keep its cell, or the center of the board).</summary>
        public void ShowCursor(int cell = -1)
        {
            if (_mode == null) return;
            var layout = _mode.Layout;
            if (cell >= 0 && cell < layout.CellCount) _cursorCell = cell;
            else if (_cursorCell < 0 || _cursorCell >= layout.CellCount) _cursorCell = GridCursor.Center(layout);
            if (_cursor == null) CreateCursor();
            if (!_cursorVisible) _cursorPos = CellCenter(_cursorCell);
            _cursorVisible = true;
            _cursor.SetActive(true);
            SetHover(_cursorCell);
        }

        public void MoveCursor(int dx, int dy)
        {
            if (_mode == null) return;
            if (!_cursorVisible) { ShowCursor(); return; }
            ShowCursor(GridCursor.Move(_cursorCell, dx, dy, _mode.Layout));
        }

        public void HideCursor()
        {
            _cursorVisible = false;
            if (_cursor != null) _cursor.SetActive(false);
        }

        void CreateCursor()
        {
            _cursor = new GameObject("Cursor", typeof(MeshFilter), typeof(MeshRenderer));
            _cursor.transform.SetParent(transform, false);
            _cursor.GetComponent<MeshFilter>().sharedMesh = _quad;
            var r = _cursor.GetComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.sortingOrder = 600;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _cursorMpb = new MaterialPropertyBlock();
        }

        void UpdateCursor(float dt)
        {
            if (!_cursorVisible || _cursor == null || _mode == null) return;
            var target = CellCenter(_cursorCell);
            _cursorPos = Vector3.Lerp(_cursorPos, target, 1f - Mathf.Exp(-dt * 22f));
            _cursor.transform.localPosition = new Vector3(_cursorPos.x, _cursorPos.y, -0.05f);
            float margin = 6f * _px;
            var size = new Vector2(_cell.x + margin, _cell.y + margin);
            _cursor.transform.localScale = new Vector3(size.x, size.y, 1f);
            float pulse = UiAnim.ReduceMotion ? 1f : 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 6f);
            var color = _theme.Palette.Text;
            color.a = pulse;
            _cursorMpb.SetTexture("_MainTex", Texture2D.whiteTexture);
            _cursorMpb.SetVector("_UVRect", new Vector4(0, 0, 1, 1));
            _cursorMpb.SetVector("_Size", new Vector4(size.x, size.y, 0, 0));
            _cursorMpb.SetFloat("_Radius", _style.cornerRadius * _px + margin * 0.5f);
            _cursorMpb.SetFloat("_Border", Mathf.Max(4f * _px, 0.035f * Mathf.Min(_cell.x, _cell.y)));
            _cursorMpb.SetColor("_BorderColor", color);
            _cursorMpb.SetColor("_Tint", Color.white);
            _cursorMpb.SetFloat("_Hollow", 1f);
            _cursorMpb.SetFloat("_Softness", 0f);
            _cursor.GetComponent<MeshRenderer>().SetPropertyBlock(_cursorMpb);
        }

        public void BeginDrag(int cell, Vector3 pointerWorld)
        {
            _dragCell = cell;
            _dragPiece = _mode.PieceAt(cell);
            var v = _views[_dragPiece];
            _dragOffset = v.transform.localPosition - pointerWorld;
            v.SetSorting(100);
            v.SetScaleTarget(_style.liftScale);
            RefreshHighlights();
        }

        public void DragTo(Vector3 pointerWorld)
        {
            if (_dragPiece < 0) return;
            _views[_dragPiece].DragTo(pointerWorld + _dragOffset);
            int target = CellAt(pointerWorld);
            bool insert = _mode.DragStyle == DragStyle.Insert;
            int drop = target != _dragCell && target >= 0 && (insert || _mode.CanPick(target)) ? target : -1;
            if (drop == _dropCell) return;
            _dropCell = drop;
            if (insert)
            {
                // List reordering: the other strips slide aside to show where the dragged one will land.
                foreach (var p in _mode.Pieces)
                {
                    if (p.Id == _dragPiece) continue;
                    int cell = drop >= 0 ? PuzzleStudio.Core.Modes.StripsMode.PreviewCell(p.Cell, _dragCell, drop) : p.Cell;
                    _views[p.Id].MoveTo(CellCenter(cell), 0.16f * AnimSpeed);
                }
            }
            RefreshHighlights();
        }

        /// <summary>Ends a drag; the caller then applies the move (or not) and calls Sync.</summary>
        public void EndDrag()
        {
            if (_dragPiece >= 0)
            {
                var v = _views[_dragPiece];
                v.SetScaleTarget(1f);
                v.SetSorting(0);
            }
            _dragPiece = -1;
            _dragCell = -1;
            _dropCell = -1;
        }

        public void ShowHint(Hint hint)
        {
            _hint = hint;
            _hintTime = hint.IsValid ? HintDuration : 0f;
        }

        public void SetPreview(bool visible)
        {
            _previewTarget = visible ? 0.92f : 0f;
            if (visible) _preview.gameObject.SetActive(true);
        }

        public void RefreshHighlights()
        {
            if (_mode == null) return;
            Color hl = _theme.HighlightColor;
            float pulse = _hintTime > 0f ? 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
            int sel = _mode.SelectedCell;

            foreach (var p in _mode.Pieces)
            {
                var v = _views[p.Id];
                int cell = p.Cell;
                float a = 0f;
                if (cell == _hoverCell && _dragCell < 0 && _mode.CanPick(cell)) a = 0.16f;
                if (cell == _dropCell && _mode.DragStyle == DragStyle.Swap) a = 0.45f;
                if (cell == sel) a = 0.6f;
                if (_hintTime > 0f && (cell == _hint.FromCell || cell == _hint.ToCell)) a = Mathf.Max(a, pulse);
                v.SetHighlight(a, hl);

                // Memory: the cards turned over in this attempt are lifted.
                bool open = _cards != null && _cards.IsFaceUp(cell) && !_cards.IsMatched(cell);
                if (p.Id != _dragPiece)
                    v.SetScaleTarget(cell == sel || open ? _style.liftScale : cell == _hoverCell && _mode.CanPick(cell) && _dragCell < 0 ? _style.hoverScale : 1f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateCursor(dt);

            if (_hintTime > 0f)
            {
                _hintTime -= dt;
                RefreshHighlights();
            }

            if (!Mathf.Approximately(_previewAlpha, _previewTarget))
            {
                _previewAlpha = Mathf.MoveTowards(_previewAlpha, _previewTarget, dt * 6f);
                _preview.SetTint(new Color(1f, 1f, 1f, _previewAlpha));
                if (_previewAlpha <= 0f) _preview.gameObject.SetActive(false);
            }

            if (!Mathf.Approximately(_seamT, _seamTarget) && _views.Length > 0)
            {
                _seamT = Mathf.MoveTowards(_seamT, _seamTarget, dt / 0.7f);
                ApplyPieceStyle();
                // Pieces leave hairline anti-aliasing seams; finish on the whole picture.
                if (_seamT >= 1f) { _previewTarget = 1f; _preview.gameObject.SetActive(true); }
            }

            if (_seamT >= 1f && _zoomT < 1f)
            {
                _zoomT = Mathf.Min(1f, _zoomT + dt / 0.7f);
                float s = 1f + 0.035f * Easing.Evaluate(Ease.OutBack, _zoomT);
                var c = _boardRect.center;
                transform.localScale = new Vector3(s, s, 1f);
                transform.localPosition = new Vector3(c.x * (1f - s), c.y * (1f - s), 0f);
            }
        }

        // ------------------------------------------------------------------ helpers

        PieceView CreatePieceView(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            return go.AddComponent<PieceView>();
        }

        static Mesh CreateQuad()
        {
            var m = new Mesh { name = "PieceQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }
}
