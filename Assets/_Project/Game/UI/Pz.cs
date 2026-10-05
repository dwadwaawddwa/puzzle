using System;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>USS class names + small factory helpers for themed UI Toolkit elements.</summary>
    public static class Pz
    {
        public const string Root = "pz-root";
        public const string Heading = "pz-heading";
        public const string Text = "pz-text";
        public const string TextMuted = "pz-text-muted";
        public const string Surface = "pz-surface";
        public const string Primary = "pz-primary";
        public const string Secondary = "pz-secondary";
        public const string Ghost = "pz-ghost";
        public const string ButtonClass = "pz-button";
        public const string Card = "pz-card";
        public const string Pill = "pz-pill";
        public const string Hidden = "pz-hidden";
        /// <summary>Element that blocks pointer input to the board (overlays, panels).</summary>
        public const string Blocking = "pz-blocking";
        public const string Danger = "pz-danger";

        public static VisualElement MakeBox(string classNames, params VisualElement[] children)
        {
            var e = new VisualElement();
            AddClasses(e, classNames);
            foreach (var child in children) e.Add(child);
            return e;
        }

        public static Label MakeLabel(string text, string classNames)
        {
            var l = new Label(text);
            AddClasses(l, classNames);
            return l;
        }

        /// <param name="style">Primary, Secondary or Ghost.</param>
        public static UnityEngine.UIElements.Button MakeButton(string text, string style, Action onClick)
        {
            var b = new UnityEngine.UIElements.Button(onClick) { text = text };
            b.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName); // drop Unity's default look
            AddClasses(b, $"{ButtonClass} {style} {Heading}");
            return b;
        }

        /// <summary>Button with a vector icon and an optional label (null/empty text = round icon button).</summary>
        public static UnityEngine.UIElements.Button MakeIconButton(Icon icon, string text, string style, Action onClick, bool iconAfter = false)
        {
            var b = new UnityEngine.UIElements.Button(onClick) { text = "" };
            b.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName);
            AddClasses(b, $"{ButtonClass} {style} {Heading} pz-button--with-icon");
            var ic = new IconElement(icon);
            Label l = string.IsNullOrEmpty(text) ? null : MakeLabel(text, $"pz-button-label {Heading}");
            if (l == null) b.AddToClassList("pz-button--icon");
            if (iconAfter) { if (l != null) b.Add(l); b.Add(ic); }
            else { b.Add(ic); if (l != null) b.Add(l); }
            return b;
        }

        public static void AddClasses(VisualElement e, string classNames)
        {
            if (string.IsNullOrEmpty(classNames)) return;
            foreach (var c in classNames.Split(' ', StringSplitOptions.RemoveEmptyEntries)) e.AddToClassList(c);
        }

        public static void SetVisible(VisualElement e, bool visible) => e.EnableInClassList(Hidden, !visible);

        /// <summary>Only buttons and blocking panels receive pointer events; everything else lets clicks reach the board.</summary>
        public static void ConfigurePicking(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.Query<VisualElement>().ForEach(e =>
            {
                bool interactive = e is UnityEngine.UIElements.Button || e.ClassListContains(Blocking);
                e.pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
            });
        }
    }
}
