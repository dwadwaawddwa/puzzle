using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Widgets
{
    /// <summary>Label + color swatch + hex field. Clicking the swatch opens an HSV picker popup.</summary>
    public sealed class ColorField : VisualElement
    {
        readonly VisualElement _swatch;
        readonly TextField _hex;
        readonly Action<string> _onChange;
        readonly Func<VisualElement> _popupLayer;
        readonly Func<IEnumerable<string>> _palette;
        string _value;

        /// <param name="popupLayer">Absolute full-window layer where the picker popup is shown.</param>
        /// <param name="palette">Quick-pick colors (e.g. the other theme colors).</param>
        public ColorField(string label, string hex, Action<string> onChange, Func<VisualElement> popupLayer, Func<IEnumerable<string>> palette = null)
        {
            _onChange = onChange;
            _popupLayer = popupLayer;
            _palette = palette;
            AddToClassList("studio-color-field");
            AddToClassList("studio-row");

            var l = new Label(label);
            l.AddToClassList("studio-color-label");
            Add(l);

            _swatch = new VisualElement();
            _swatch.AddToClassList("studio-swatch");
            _swatch.RegisterCallback<ClickEvent>(_ => OpenPicker());
            Add(_swatch);

            _hex = new TextField { isDelayed = true };
            _hex.AddToClassList("studio-hex");
            _hex.RegisterValueChangedCallback(e =>
            {
                if (ColorUtil.TryParseHex(e.newValue, out var c)) Set(ColorUtil.ToHex(c, c.a < 0.999f), notify: true);
                else _hex.SetValueWithoutNotify(_value);
            });
            Add(_hex);

            Set(hex, notify: false);
        }

        public void Set(string hex, bool notify)
        {
            _value = hex;
            _hex.SetValueWithoutNotify(hex);
            _swatch.style.backgroundColor = ColorUtil.Parse(hex, Color.magenta);
            if (notify) _onChange?.Invoke(hex);
        }

        public void OpenPicker()
        {
            var layer = _popupLayer?.Invoke();
            if (layer == null) return;
            var picker = new ColorPickerPopup(ColorUtil.Parse(_value, Color.white), c => Set(ColorUtil.ToHex(c), notify: true), _palette?.Invoke());
            picker.ShowAt(layer, _swatch.worldBound);
        }
    }

    /// <summary>Saturation/value square + hue bar + hex + palette chips. Changes apply live.</summary>
    public sealed class ColorPickerPopup : VisualElement
    {
        readonly Action<Color> _onChange;
        float _h, _s, _v;
        readonly VisualElement _sv, _hue, _svHandle, _hueHandle, _preview;
        readonly Label _hexLabel;
        VisualElement _backdrop;

        public ColorPickerPopup(Color initial, Action<Color> onChange, IEnumerable<string> palette)
        {
            _onChange = onChange;
            Color.RGBToHSV(initial, out _h, out _s, out _v);
            AddToClassList("studio-colorpicker");

            _sv = new VisualElement { name = "sv" };
            _sv.AddToClassList("studio-sv");
            _sv.generateVisualContent += DrawSV;
            _svHandle = new VisualElement();
            _svHandle.AddToClassList("studio-handle");
            _sv.Add(_svHandle);
            RegisterDrag(_sv, (local, rect) =>
            {
                _s = Mathf.Clamp01(local.x / rect.width);
                _v = 1f - Mathf.Clamp01(local.y / rect.height);
                Changed();
            });

            _hue = new VisualElement { name = "hue" };
            _hue.AddToClassList("studio-hue");
            _hue.generateVisualContent += DrawHue;
            _hueHandle = new VisualElement();
            _hueHandle.AddToClassList("studio-hue-handle");
            _hue.Add(_hueHandle);
            RegisterDrag(_hue, (local, rect) =>
            {
                _h = Mathf.Clamp01(local.y / rect.height);
                _sv.MarkDirtyRepaint();
                Changed();
            });

            var top = new VisualElement();
            top.AddToClassList("studio-row");
            top.Add(_sv);
            top.Add(_hue);
            Add(top);

            _preview = new VisualElement();
            _preview.AddToClassList("studio-swatch");
            _hexLabel = new Label();
            _hexLabel.AddToClassList("studio-picker-hex");
            var info = new VisualElement();
            info.AddToClassList("studio-row");
            info.AddToClassList("studio-picker-info");
            info.Add(_preview);
            info.Add(_hexLabel);
            Add(info);

            if (palette != null)
            {
                var chips = new VisualElement();
                chips.AddToClassList("studio-palette");
                var seen = new HashSet<string>();
                foreach (var hex in palette)
                {
                    if (!ColorUtil.TryParseHex(hex, out var c) || !seen.Add(hex.ToUpperInvariant())) continue;
                    var chip = new VisualElement();
                    chip.AddToClassList("studio-palette-chip");
                    chip.style.backgroundColor = c;
                    chip.tooltip = hex;
                    chip.RegisterCallback<ClickEvent>(_ =>
                    {
                        Color.RGBToHSV(c, out _h, out _s, out _v);
                        _sv.MarkDirtyRepaint();
                        Changed();
                    });
                    chips.Add(chip);
                }
                Add(chips);
            }

            RegisterCallback<GeometryChangedEvent>(_ => UpdateHandles());
            UpdateInfo();
        }

        public void ShowAt(VisualElement layer, Rect anchorWorld)
        {
            _backdrop = new VisualElement();
            _backdrop.AddToClassList("studio-popup-layer");
            _backdrop.RegisterCallback<PointerDownEvent>(e => { if (e.target == _backdrop) Close(); });
            _backdrop.Add(this);
            layer.Add(_backdrop);

            var local = layer.WorldToLocal(new Vector2(anchorWorld.xMin, anchorWorld.yMax + 6));
            float width = 262f, height = 290f;
            float x = Mathf.Min(local.x, layer.layout.width - width - 8);
            float y = local.y + height > layer.layout.height - 8 ? local.y - height - anchorWorld.height - 12 : local.y;
            style.left = Mathf.Max(8, x);
            style.top = Mathf.Max(8, y);
        }

        public void Close() => _backdrop?.RemoveFromHierarchy();

        Color Current => Color.HSVToRGB(_h, _s, _v);

        void Changed()
        {
            UpdateHandles();
            UpdateInfo();
            _onChange?.Invoke(Current);
        }

        void UpdateInfo()
        {
            _preview.style.backgroundColor = Current;
            _hexLabel.text = ColorUtil.ToHex(Current);
        }

        void UpdateHandles()
        {
            var r = _sv.contentRect;
            _svHandle.style.left = _s * r.width;
            _svHandle.style.top = (1f - _v) * r.height;
            _svHandle.style.borderTopColor = _svHandle.style.borderBottomColor =
                _svHandle.style.borderLeftColor = _svHandle.style.borderRightColor = _v > 0.6f && _s < 0.4f ? Color.black : Color.white;
            _hueHandle.style.top = _h * _hue.contentRect.height;
        }

        static void RegisterDrag(VisualElement target, Action<Vector2, Rect> onPos)
        {
            target.RegisterCallback<PointerDownEvent>(e =>
            {
                target.CapturePointer(e.pointerId);
                onPos(e.localPosition, target.contentRect);
                e.StopPropagation();
            });
            target.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (target.HasPointerCapture(e.pointerId)) onPos(e.localPosition, target.contentRect);
            });
            target.RegisterCallback<PointerUpEvent>(e =>
            {
                if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);
            });
        }

        void DrawSV(MeshGenerationContext mgc)
        {
            var r = _sv.contentRect;
            if (r.width < 2) return;
            const int n = 16;
            var mesh = mgc.Allocate((n + 1) * (n + 1), n * n * 6);
            for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                float s = (float)x / n, v = 1f - (float)y / n;
                mesh.SetNextVertex(new Vertex
                {
                    position = new Vector3(r.xMin + s * r.width, r.yMin + (1f - v) * r.height, Vertex.nearZ),
                    tint = Color.HSVToRGB(_h, s, v),
                });
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                ushort i = (ushort)(y * (n + 1) + x);
                ushort right = (ushort)(i + 1), down = (ushort)(i + n + 1), diag = (ushort)(i + n + 2);
                mesh.SetNextIndex(i); mesh.SetNextIndex(right); mesh.SetNextIndex(diag);
                mesh.SetNextIndex(i); mesh.SetNextIndex(diag); mesh.SetNextIndex(down);
            }
        }

        static void DrawHue(MeshGenerationContext mgc)
        {
            var r = mgc.visualElement.contentRect;
            if (r.height < 2) return;
            const int n = 12;
            var mesh = mgc.Allocate((n + 1) * 2, n * 6);
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                var c = Color.HSVToRGB(Mathf.Min(t, 0.9999f), 1f, 1f);
                float y = r.yMin + t * r.height;
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, y, Vertex.nearZ), tint = c });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, y, Vertex.nearZ), tint = c });
            }
            for (int i = 0; i < n; i++)
            {
                ushort a = (ushort)(i * 2);
                mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 1)); mesh.SetNextIndex((ushort)(a + 3));
                mesh.SetNextIndex(a); mesh.SetNextIndex((ushort)(a + 3)); mesh.SetNextIndex((ushort)(a + 2));
            }
        }
    }
}
