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

        readonly List<PieceView> _pool = new List<PieceView>();
        PieceView[] _views = System.Array.Empty<PieceView>();   // by piece id
        PieceView _preview;
        Mesh _quad;
        Material _material;

        IPuzzleMode _mode;
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
        float _seamT = 0f, _seamTarget = 0f;   // 0 = normal pieces, 1 = seamless full image (victory)
        float _zoomT = 1f;                      // victory zoom of the finished picture

        public Rect BoardRect => _boardRect;
        /// <summary>Player UI size multiplier: the HUD grows, so the board area shrinks.</summary>
        public System.Func<float> UiScale = () => 1f;
        /// <summary>Normalized screen area (origin top-left) the board is fitted into. Null = default margins.</summary>
        public System.Func<Rect> Area;
        public bool IsDragging => _dragCell >= 0;
        public int DragCell => _dragCell;

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
            _texture = texture;
            _style = style ?? new PieceStyle();
            _hint = Hint.None;
            _hoverCell = _dropCell = _dragCell = -1;
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
            _views = System.Array.Empty<PieceView>();
            _mode = null;
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
            RefreshHighlights();
        }

        public void PlayCorrect(int pieceId)
        {
            if (pieceId < 0 || pieceId >= _views.Length) return;
            if (_style.correctGlow) _views[pieceId].Pop(_theme.GlowColor);
        }

        /// <summary>Victory: gaps, borders and corners fade out so the full picture appears, then a small zoom.</summary>
        public void PlayVictory()
        {
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

                if (p.Id != _dragPiece)
                    v.SetScaleTarget(cell == sel ? _style.liftScale : cell == _hoverCell && _mode.CanPick(cell) && _dragCell < 0 ? _style.hoverScale : 1f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

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
