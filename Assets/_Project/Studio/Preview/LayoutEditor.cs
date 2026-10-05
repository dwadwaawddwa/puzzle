using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Preview
{
    /// <summary>
    /// Transparent layer over the live preview that lets you drag UI blocks, resize them with the corner handle,
    /// snap to a grid or to other blocks (guide lines) and move/resize the puzzle area.
    /// Edits go straight into the project's <see cref="LayoutConfig"/> and are re-applied live to the preview.
    /// </summary>
    public sealed class LayoutEditor : VisualElement
    {
        public const float SnapPixels = 10f;
        const float HandleSize = 14f;

        enum DragMode { None, Move, Scale, BoardMove, BoardResize }

        readonly Func<LayoutConfig> _config;
        readonly Func<GamePackData> _pack;
        readonly Func<GameFlow> _flow;

        public string Screen = LayoutConfig.GameplayScreen;
        public string Selected { get; private set; }
        public bool SnapToGrid = true;
        public bool ShowGrid = true;
        public bool SnapToElements = true;
        public int GridSize = 20;

        public event Action SelectionChanged;
        /// <summary>live = true while dragging (cheap refresh), false when the gesture ends.</summary>
        public event Action<bool> Edited;

        DragMode _drag;
        Vector2 _startPointer;
        Rect _startRect;
        LayoutItem _startItem;
        LayoutRect _startBoard;
        readonly List<(Vector2 a, Vector2 b)> _guides = new List<(Vector2, Vector2)>();

        public LayoutEditor(Func<LayoutConfig> config, Func<GamePackData> pack, Func<GameFlow> flow)
        {
            _config = config;
            _pack = pack;
            _flow = flow;
            AddToClassList("studio-layout-editor");
            pickingMode = PickingMode.Position;
            focusable = true;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            schedule.Execute(MarkDirtyRepaint).Every(120);
        }

        LayoutConfig Config => _config();
        float Aspect => layout.height > 1 ? layout.width / layout.height : 16f / 9f;
        float RefWidth => 1080f * Aspect;

        public IEnumerable<string> Ids
        {
            get
            {
                if (Screen == LayoutConfig.GameplayScreen) yield return LayoutService.BoardId;
                foreach (var s in LayoutService.Slots[Screen]) yield return s.Id;
            }
        }

        public void Select(string id)
        {
            Selected = id;
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>Current rect of a block in normalized screen coordinates (origin top-left).</summary>
        public bool TryGetRect(string id, out Rect rect)
        {
            rect = default;
            if (id == LayoutService.BoardId)
            {
                rect = LayoutService.BoardArea(_pack(), Aspect, 1f);
                return true;
            }
            var root = _flow()?.CurrentPage?.Root;
            var e = root?.Q(id, LayoutService.LayoutClass);
            if (e == null || e.resolvedStyle.display == DisplayStyle.None) return false;
            var local = this.WorldToLocal(e.worldBound);
            if (layout.width < 1 || layout.height < 1) return false;
            rect = new Rect(local.x / layout.width, local.y / layout.height, local.width / layout.width, local.height / layout.height);
            return rect.width > 0 && rect.height > 0;
        }

        LayoutItem EnsureItem(string id)
        {
            var dict = Config.For(Screen);
            if (!dict.TryGetValue(id, out var item))
            {
                var slot = LayoutService.FindSlot(Screen, id);
                item = new LayoutItem { x = slot.DefaultPosition.x, y = slot.DefaultPosition.y, scale = slot.DefaultScale, visible = true };
                dict[id] = item;
            }
            return item;
        }

        LayoutRect EnsureBoard()
        {
            if (Config.boardArea == null)
            {
                var r = LayoutService.BoardArea(_pack(), Aspect, 1f);
                Config.boardArea = new LayoutRect(r.x, r.y, r.width, r.height);
            }
            return Config.boardArea;
        }

        /// <summary>Moves a block so its rect starts at <paramref name="rect"/>.position (keeps the anchor convention).</summary>
        public void PlaceRect(string id, Rect rect)
        {
            if (id == LayoutService.BoardId)
            {
                var b = EnsureBoard();
                b.x = rect.x; b.y = rect.y; b.w = rect.width; b.h = rect.height;
                return;
            }
            var slot = LayoutService.FindSlot(Screen, id);
            var item = EnsureItem(id);
            item.x = rect.x + slot.Anchor.x * rect.width;
            item.y = rect.y + slot.Anchor.y * rect.height;
        }

        /// <summary>Aligns the selected block to the screen: h = -1 left, 0 center, 1 right; v likewise.</summary>
        public void Align(int? h, int? v, float margin = 0.025f)
        {
            if (Selected == null || !TryGetRect(Selected, out var r)) return;
            float mx = margin * 1080f / RefWidth;
            if (h.HasValue) r.x = h.Value < 0 ? mx : h.Value > 0 ? 1f - mx - r.width : 0.5f - r.width * 0.5f;
            if (v.HasValue) r.y = v.Value < 0 ? margin : v.Value > 0 ? 1f - margin - r.height : 0.5f - r.height * 0.5f;
            PlaceRect(Selected, r);
            Apply(false);
        }

        void Apply(bool live)
        {
            _flow()?.SetLayout(Config);
            MarkDirtyRepaint();
            Edited?.Invoke(live);
        }

        // ------------------------------------------------------------------ pointer

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            Focus();
            Vector2 p = e.localPosition;
            _drag = DragMode.None;

            if (Selected != null && TryGetRect(Selected, out var sel) && HandleRect(sel).Contains(p))
            {
                _drag = Selected == LayoutService.BoardId ? DragMode.BoardResize : DragMode.Scale;
                Begin(Selected, sel, p);
            }
            else
            {
                string hit = HitTest(p, out var rect);
                if (hit != Selected) Select(hit);
                if (hit != null)
                {
                    _drag = hit == LayoutService.BoardId ? DragMode.BoardMove : DragMode.Move;
                    Begin(hit, rect, p);
                }
            }
            if (_drag != DragMode.None) this.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void Begin(string id, Rect rect, Vector2 pointer)
        {
            _startPointer = pointer;
            _startRect = rect;
            if (id == LayoutService.BoardId)
            {
                var b = EnsureBoard();
                _startBoard = new LayoutRect(b.x, b.y, b.w, b.h);
            }
            else _startItem = EnsureItem(id).Clone();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (_drag == DragMode.None || !this.HasPointerCapture(e.pointerId)) return;
            Vector2 p = e.localPosition;
            var delta = new Vector2((p.x - _startPointer.x) / layout.width, (p.y - _startPointer.y) / layout.height);
            _guides.Clear();

            switch (_drag)
            {
                case DragMode.Move:
                case DragMode.BoardMove:
                {
                    var r = _startRect;
                    r.position += delta;
                    r = SnapRect(r, Selected, moveWhole: true);
                    PlaceRect(Selected, r);
                    break;
                }
                case DragMode.BoardResize:
                {
                    var r = new Rect(_startBoard.x, _startBoard.y, Mathf.Max(0.15f, _startBoard.w + delta.x), Mathf.Max(0.15f, _startBoard.h + delta.y));
                    r = SnapRect(r, Selected, moveWhole: false);
                    PlaceRect(Selected, r);
                    break;
                }
                case DragMode.Scale:
                {
                    var slot = LayoutService.FindSlot(Screen, Selected);
                    var anchorPx = new Vector2((_startRect.x + slot.Anchor.x * _startRect.width) * layout.width,
                                               (_startRect.y + slot.Anchor.y * _startRect.height) * layout.height);
                    float d0 = Mathf.Max(4f, (_startPointer - anchorPx).magnitude);
                    float d1 = (p - anchorPx).magnitude;
                    float scale = Mathf.Clamp(_startItem.scale * d1 / d0, 0.3f, 3f);
                    if (SnapToGrid) scale = Mathf.Round(scale * 20f) / 20f;
                    EnsureItem(Selected).scale = scale;
                    break;
                }
            }
            Apply(true);
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (!this.HasPointerCapture(e.pointerId)) return;
            this.ReleasePointer(e.pointerId);
            bool changed = _drag != DragMode.None;
            _drag = DragMode.None;
            _guides.Clear();
            MarkDirtyRepaint();
            if (changed) Edited?.Invoke(false);
            e.StopPropagation();
        }

        string HitTest(Vector2 p, out Rect hitRect)
        {
            hitRect = default;
            string best = null;
            float bestArea = float.MaxValue;
            foreach (var id in Ids)
            {
                if (!TryGetRect(id, out var r)) continue;
                var px = ToPixels(r);
                if (!px.Contains(p)) continue;
                float area = px.width * px.height;
                if (area < bestArea) { bestArea = area; best = id; hitRect = r; }
            }
            return best;
        }

        Rect ToPixels(Rect n) => new Rect(n.x * layout.width, n.y * layout.height, n.width * layout.width, n.height * layout.height);

        Rect HandleRect(Rect normalized)
        {
            var px = ToPixels(normalized);
            return new Rect(px.xMax - HandleSize * 0.5f, px.yMax - HandleSize * 0.5f, HandleSize, HandleSize);
        }

        // ------------------------------------------------------------------ snapping

        Rect SnapRect(Rect r, string selfId, bool moveWhole)
        {
            float thrX = SnapPixels / RefWidth, thrY = SnapPixels / 1080f;
            var xs = new List<float> { 0f, 0.5f, 1f };
            var ys = new List<float> { 0f, 0.5f, 1f };
            if (SnapToElements)
                foreach (var id in Ids)
                {
                    if (id == selfId || !TryGetRect(id, out var o)) continue;
                    xs.Add(o.xMin); xs.Add(o.center.x); xs.Add(o.xMax);
                    ys.Add(o.yMin); ys.Add(o.center.y); ys.Add(o.yMax);
                }

            float[] ownX = moveWhole ? new[] { r.xMin, r.center.x, r.xMax } : new[] { r.xMax };
            float[] ownY = moveWhole ? new[] { r.yMin, r.center.y, r.yMax } : new[] { r.yMax };
            bool snappedX = TryBest(ownX, xs, thrX, out float dx, out float gx);
            bool snappedY = TryBest(ownY, ys, thrY, out float dy, out float gy);

            if (!snappedX && SnapToGrid)
            {
                float step = GridSize / RefWidth;
                float edge = moveWhole ? r.xMin : r.xMax;
                dx = Mathf.Round(edge / step) * step - edge;
            }
            if (!snappedY && SnapToGrid)
            {
                float step = GridSize / 1080f;
                float edge = moveWhole ? r.yMin : r.yMax;
                dy = Mathf.Round(edge / step) * step - edge;
            }

            if (moveWhole) r.position += new Vector2(dx, dy);
            else { r.width += dx; r.height += dy; }

            if (snappedX) _guides.Add((new Vector2(gx, 0f), new Vector2(gx, 1f)));
            if (snappedY) _guides.Add((new Vector2(0f, gy), new Vector2(1f, gy)));
            return r;
        }

        static bool TryBest(float[] own, List<float> targets, float threshold, out float delta, out float guide)
        {
            delta = 0f;
            guide = 0f;
            float best = threshold;
            bool found = false;
            foreach (var o in own)
                foreach (var t in targets)
                {
                    float d = t - o;
                    if (Mathf.Abs(d) < best) { best = Mathf.Abs(d); delta = d; guide = t; found = true; }
                }
            return found;
        }

        // ------------------------------------------------------------------ drawing

        void Draw(MeshGenerationContext mgc)
        {
            var size = layout.size;
            if (size.x < 2) return;
            var p = mgc.painter2D;

            if (ShowGrid)
            {
                float step = GridSize * size.y / 1080f;
                if (step >= 4f)
                {
                    p.strokeColor = new Color(1f, 1f, 1f, 0.07f);
                    p.lineWidth = 1f;
                    p.BeginPath();
                    for (float x = step; x < size.x; x += step) { p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, size.y)); }
                    for (float y = step; y < size.y; y += step) { p.MoveTo(new Vector2(0, y)); p.LineTo(new Vector2(size.x, y)); }
                    p.Stroke();
                }
            }

            foreach (var id in Ids)
            {
                if (!TryGetRect(id, out var r)) continue;
                var px = ToPixels(r);
                bool selected = id == Selected;
                bool board = id == LayoutService.BoardId;
                p.lineWidth = selected ? 2.5f : 1.2f;
                p.strokeColor = selected ? new Color(0.36f, 0.49f, 0.98f, 1f)
                              : board ? new Color(1f, 0.71f, 0.33f, 0.8f)
                              : new Color(1f, 1f, 1f, 0.55f);
                p.BeginPath();
                p.MoveTo(px.min);
                p.LineTo(new Vector2(px.xMax, px.yMin));
                p.LineTo(px.max);
                p.LineTo(new Vector2(px.xMin, px.yMax));
                p.ClosePath();
                p.Stroke();
                if (selected)
                {
                    p.fillColor = new Color(0.36f, 0.49f, 0.98f, 0.12f);
                    p.Fill();
                    var h = HandleRect(r);
                    p.fillColor = new Color(0.36f, 0.49f, 0.98f, 1f);
                    p.BeginPath();
                    p.MoveTo(h.min); p.LineTo(new Vector2(h.xMax, h.yMin)); p.LineTo(h.max); p.LineTo(new Vector2(h.xMin, h.yMax));
                    p.ClosePath();
                    p.Fill();
                }
            }

            if (_guides.Count > 0)
            {
                p.strokeColor = new Color(1f, 0.3f, 0.75f, 0.95f);
                p.lineWidth = 1.5f;
                p.BeginPath();
                foreach (var (a, b) in _guides)
                {
                    p.MoveTo(new Vector2(a.x * size.x, a.y * size.y));
                    p.LineTo(new Vector2(b.x * size.x, b.y * size.y));
                }
                p.Stroke();
            }
        }
    }
}
