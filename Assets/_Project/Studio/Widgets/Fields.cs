using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Widgets
{
    /// <summary>Factory for the Studio's inspector controls (dark theme classes, value callbacks).</summary>
    public static class Fields
    {
        public static VisualElement Section(string title, params VisualElement[] children)
        {
            var s = new VisualElement();
            s.AddToClassList("studio-section");
            if (!string.IsNullOrEmpty(title))
            {
                var t = new Label(title.ToUpperInvariant());
                t.AddToClassList("studio-section-title");
                s.Add(t);
            }
            foreach (var c in children) if (c != null) s.Add(c);
            return s;
        }

        public static Label Hint(string text, string extraClass = null)
        {
            var l = new Label(text);
            l.AddToClassList("studio-hint");
            if (extraClass != null) l.AddToClassList(extraClass);
            return l;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var r = new VisualElement();
            r.AddToClassList("studio-row");
            foreach (var c in children) if (c != null) r.Add(c);
            return r;
        }

        public static VisualElement Spacer()
        {
            var s = new VisualElement();
            s.AddToClassList("studio-spacer");
            return s;
        }

        public static Button Button(string text, Action onClick, string classes = null)
        {
            var b = new Button(onClick) { text = text };
            b.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName);
            b.AddToClassList("studio-btn");
            if (!string.IsNullOrEmpty(classes))
                foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) b.AddToClassList(c);
            return b;
        }

        public static TextField Text(string label, string value, Action<string> onChange, bool multiline = false)
        {
            var f = new TextField(label) { value = value ?? "", multiline = multiline };
            f.AddToClassList("studio-field");
            f.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            return f;
        }

        public static Slider FloatSlider(string label, float min, float max, float value, Action<float> onChange)
        {
            var s = new Slider(label, min, max) { value = value, showInputField = true };
            s.AddToClassList("studio-field");
            s.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            return s;
        }

        public static SliderInt IntSlider(string label, int min, int max, int value, Action<int> onChange)
        {
            var s = new SliderInt(label, min, max) { value = value, showInputField = true };
            s.AddToClassList("studio-field");
            s.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            return s;
        }

        public static Toggle Toggle(string label, bool value, Action<bool> onChange)
        {
            var t = new Toggle(label) { value = value };
            t.AddToClassList("studio-field");
            t.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            return t;
        }

        public static DropdownField Dropdown(string label, List<string> choices, int index, Action<int> onChange)
        {
            var d = new DropdownField(label, choices, Mathf.Clamp(index, 0, Math.Max(0, choices.Count - 1)));
            d.AddToClassList("studio-field");
            d.RegisterValueChangedCallback(e => onChange(choices.IndexOf(e.newValue)));
            return d;
        }

        /// <summary>Dropdown over an enum, showing "PascalCase" names as "Pascal Case".</summary>
        public static DropdownField Enum<T>(string label, T value, Action<T> onChange) where T : struct, System.Enum
        {
            var values = (T[])System.Enum.GetValues(typeof(T));
            var names = new List<string>();
            foreach (var v in values) names.Add(Nicify(v.ToString()));
            return Dropdown(label, names, Array.IndexOf(values, value), i => { if (i >= 0) onChange(values[i]); });
        }

        public static string Nicify(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i]) && !char.IsUpper(pascal[i - 1])) sb.Append(' ');
                sb.Append(pascal[i]);
            }
            return sb.ToString();
        }

        /// <summary>Read-only path display with an action button.</summary>
        public static VisualElement PathRow(string label, string path, string buttonText, Action onClick)
        {
            var l = new Label(label);
            l.AddToClassList("studio-path-label");
            var p = new Label(string.IsNullOrEmpty(path) ? "—" : path);
            p.AddToClassList("studio-path");
            var row = Row(l, p, Button(buttonText, onClick, "studio-btn--small"));
            row.AddToClassList("studio-path-row");
            return row;
        }
    }
}
