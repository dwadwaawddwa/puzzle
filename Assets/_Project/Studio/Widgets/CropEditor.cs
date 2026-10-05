using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Widgets
{
    /// <summary>
    /// Crop tool shown in a dialog: the picture with a frame to move (drag inside) and resize (drag a corner),
    /// aspect buttons (Free, Original, 1:1, 4:3, 16:9, 3:4) and Reset.
    /// </summary>
    public sealed class CropEditor : VisualElement
    {
        const float MaxWidth = 480f, MaxHeight = 330f;

        static readonly (string label, float aspect)[] Aspects =
        {
            ("Free", 0f), ("Original", -1f), ("1:1", 1f), ("4:3", 4f / 3f), ("16:9", 16f / 9f), ("3:4", 3f / 4f),
        };

        readonly VisualElement _picture, _frame;
        readonly VisualElement[] _masks = new VisualElement[4];
        readonly List<Button> _aspectButtons = new List<Button>();
        readonly Label _info;
        readonly float _imageAspect;
        readonly float _width, _height;
        float _aspect;
        int _dragMode = -2;          // -2 none, -1 move, 0..3 corner
        Vector2 _dragStart;
        CropRect _startRect;

        public CropRect Value { get; private set; }

        public CropEditor(Texture2D picture, int imageWidth, int imageHeight, CropRect initial)
        {
            AddToClassList("studio-crop");
            _imageAspect = imageHeight > 0 ? (float)imageWidth / imageHeight : 1f;
            _width = MaxWidth;
            _height = _width / _imageAspect;
            if (_height > MaxHeight) { _height = MaxHeight; _width = _height * _imageAspect; }
            Value = initial != null && initial.IsValid ? new CropRect(initial.x, initial.y, initial.w, initial.h) : CropMath.Full;

            _picture = new VisualElement();
            _picture.AddToClassList("studio-crop-picture");
            _picture.style.width = _width;
            _picture.style.height = _height;
            if (picture != null) _picture.style.backgroundImage = Background.FromTexture2D(picture);
            for (int i = 0; i < 4; i++)
            {
                _masks[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                _masks[i].AddToClassList("studio-crop-mask");
                _picture.Add(_masks[i]);
            }
            _frame = new VisualElement();
            _frame.AddToClassList("studio-crop-frame");
            for (int corner = 0; corner < 4; corner++)
            {
                var handle = new VisualElement();
                handle.AddToClassList("studio-crop-handle");
                handle.AddToClassList("studio-crop-handle--" + corner);
                int c = corner;
                handle.RegisterCallback<PointerDownEvent>(e => BeginDrag(e, c), TrickleDown.TrickleDown);
                _frame.Add(handle);
            }
            _frame.RegisterCallback<PointerDownEvent>(e => BeginDrag(e, -1));
            _picture.Add(_frame);
            _picture.RegisterCallback<PointerMoveEvent>(OnMove);
            _picture.RegisterCallback<PointerUpEvent>(OnUp);
            _picture.RegisterCallback<PointerCaptureOutEvent>(_ => _dragMode = -2);

            var holder = new VisualElement();
            holder.AddToClassList("studio-crop-holder");
            holder.Add(_picture);
            Add(holder);

            var row = new VisualElement();
            row.AddToClassList("studio-row");
            row.AddToClassList("studio-crop-aspects");
            foreach (var (label, aspect) in Aspects)
            {
                float a = aspect;
                var b = Fields.Button(label, () => SetAspect(a), "studio-btn--small");
                b.userData = a;
                _aspectButtons.Add(b);
                row.Add(b);
            }
            row.Add(Fields.Spacer());
            row.Add(Fields.Button("Reset", () => { _aspect = 0f; Value = CropMath.Full; Refresh(); }, "studio-btn--small"));
            Add(row);
            _info = Fields.Hint("");
            Add(_info);
            Refresh();
        }

        void SetAspect(float aspect)
        {
            _aspect = aspect < 0f ? _imageAspect : aspect;
            if (_aspect > 0f)
                Value = CropMath.IsFull(Value) ? CropMath.Fit(_aspect, _imageAspect) : CropMath.WithAspect(Value, _aspect, _imageAspect);
            Refresh();
        }

        void BeginDrag(PointerDownEvent e, int mode)
        {
            if (_dragMode != -2) return;
            _dragMode = mode;
            _dragStart = _picture.WorldToLocal(e.position);
            _startRect = new CropRect(Value.x, Value.y, Value.w, Value.h);
            _picture.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (_dragMode == -2 || !_picture.HasPointerCapture(e.pointerId)) return;
            Vector2 p = _picture.WorldToLocal(e.position);
            if (_dragMode == -1)
                Value = CropMath.Move(_startRect, (p.x - _dragStart.x) / _width, (p.y - _dragStart.y) / _height);
            else
                Value = CropMath.ResizeCorner(_startRect, _dragMode, p.x / _width, p.y / _height, _aspect, _imageAspect);
            Refresh();
        }

        void OnUp(PointerUpEvent e)
        {
            if (_picture.HasPointerCapture(e.pointerId)) _picture.ReleasePointer(e.pointerId);
            _dragMode = -2;
        }

        void Refresh()
        {
            var c = Value;
            float x = c.x * _width, y = c.y * _height, w = c.w * _width, h = c.h * _height;
            Place(_frame, x, y, w, h);
            Place(_masks[0], 0, 0, _width, y);                         // above
            Place(_masks[1], 0, y + h, _width, _height - y - h);       // below
            Place(_masks[2], 0, y, x, h);                              // left
            Place(_masks[3], x + w, y, _width - x - w, h);             // right
            foreach (var b in _aspectButtons)
            {
                float a = (float)b.userData;
                bool active = a < 0f ? Mathf.Approximately(_aspect, _imageAspect) && _aspect > 0f : Mathf.Approximately(a, _aspect);
                b.EnableInClassList("studio-btn--primary", active);
            }
            _info.text = CropMath.IsFull(c)
                ? "Whole picture. Drag a corner to crop, drag inside the frame to move it."
                : $"Keeps {Mathf.RoundToInt(c.w * 100)} % × {Mathf.RoundToInt(c.h * 100)} % of the picture  ·  ratio {CropMath.AspectOf(c, _imageAspect):0.00}";
        }

        static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.left = x;
            e.style.top = y;
            e.style.width = Mathf.Max(0f, w);
            e.style.height = Mathf.Max(0f, h);
        }
    }

    /// <summary>Six dots: the "drag me" grip of a list row.</summary>
    public sealed class GripElement : VisualElement
    {
        public GripElement()
        {
            AddToClassList("studio-grip");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            var p = mgc.painter2D;
            p.fillColor = new Color(0.55f, 0.58f, 0.65f, 1f);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 2; col++)
                {
                    var c = new Vector2(r.center.x + (col - 0.5f) * 5f, r.center.y + (row - 1) * 5f);
                    p.BeginPath();
                    p.Arc(c, 1.4f, 0f, 360f);
                    p.Fill();
                }
        }
    }
}
