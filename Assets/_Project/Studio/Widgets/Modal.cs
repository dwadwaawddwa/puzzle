using System;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Widgets
{
    /// <summary>Simple modal dialogs drawn in a full-window layer of the Studio.</summary>
    public sealed class Modal
    {
        readonly VisualElement _layer;
        VisualElement _current;

        public bool IsOpen => _current != null;

        public Modal(VisualElement layer)
        {
            _layer = layer;
            _layer.AddToClassList("studio-modal-layer");
            _layer.style.display = DisplayStyle.None;
        }

        public struct ButtonSpec
        {
            public string Text;
            public string Classes;
            public Action OnClick;
            /// <summary>Return false to keep the dialog open (e.g. validation failed).</summary>
            public Func<bool> Validate;

            public ButtonSpec(string text, Action onClick, string classes = null, Func<bool> validate = null)
            {
                Text = text;
                OnClick = onClick;
                Classes = classes;
                Validate = validate;
            }
        }

        public void Show(string title, VisualElement body, params ButtonSpec[] buttons)
        {
            Close();
            var box = new VisualElement();
            box.AddToClassList("studio-modal");
            var t = new Label(title);
            t.AddToClassList("studio-modal-title");
            box.Add(t);
            if (body != null)
            {
                body.AddToClassList("studio-modal-body");
                box.Add(body);
            }
            var row = new VisualElement();
            row.AddToClassList("studio-modal-buttons");
            foreach (var spec in buttons)
            {
                var s = spec;
                row.Add(Fields.Button(s.Text, () =>
                {
                    if (s.Validate != null && !s.Validate()) return;
                    Close();
                    s.OnClick?.Invoke();
                }, s.Classes));
            }
            box.Add(row);
            _current = box;
            _layer.Add(box);
            _layer.style.display = DisplayStyle.Flex;
            StudioFont.Apply(box);
        }

        public void Message(string title, string message) =>
            Show(title, Fields.Hint(message, "studio-modal-text"), new ButtonSpec("OK", null, "studio-btn--primary"));

        public void Confirm(string title, string message, string okText, Action onOk, string okClasses = "studio-btn--primary") =>
            Show(title, Fields.Hint(message, "studio-modal-text"),
                new ButtonSpec("Cancel", null),
                new ButtonSpec(okText, onOk, okClasses));

        public void Close()
        {
            _current?.RemoveFromHierarchy();
            _current = null;
            _layer.style.display = DisplayStyle.None;
        }
    }

    /// <summary>Studio UI font (Inter) applied inline so it wins over the default runtime theme.</summary>
    public static class StudioFont
    {
        static StyleFontDefinition? _regular, _bold;

        public static void Apply(VisualElement root)
        {
            if (_regular == null)
            {
                var r = UnityEngine.Resources.Load<UnityEngine.Font>("Fonts/Inter-Regular");
                var b = UnityEngine.Resources.Load<UnityEngine.Font>("Fonts/Inter-Bold");
                _regular = r != null ? new StyleFontDefinition(FontDefinition.FromFont(r)) : new StyleFontDefinition(StyleKeyword.Null);
                _bold = b != null ? new StyleFontDefinition(FontDefinition.FromFont(b)) : _regular;
            }
            root.Query<TextElement>().ForEach(t =>
            {
                if (t.ClassListContains("pz-root") || t.GetFirstAncestorOfType<GameUiMarker>() != null) return;
                bool bold = t.ClassListContains("studio-bold") || t.ClassListContains("studio-section-title") ||
                            t.ClassListContains("studio-modal-title") || t.ClassListContains("studio-logo") ||
                            t.ClassListContains("studio-inspector-header") || t.ClassListContains("studio-welcome-title");
                t.style.unityFontDefinition = bold ? _bold.Value : _regular.Value;
            });
        }
    }

    /// <summary>Marks the container of the hosted game UI so Studio styling helpers skip it.</summary>
    public sealed class GameUiMarker : VisualElement { }
}
