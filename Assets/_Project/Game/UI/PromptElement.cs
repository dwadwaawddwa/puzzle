using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>Gamepad buttons, labelled like an Xbox pad (also what the Steam Deck shows).</summary>
    public enum PadButton { A, B, X, Y, LB, RB, View, Menu, DPad }

    /// <summary>
    /// Small button glyph ("A", "LB", "Esc"…) shown next to an action. Pad glyphs only appear while a gamepad is used,
    /// key glyphs while the keyboard is used (classes pz-nav-pad / pz-nav-keys on the game's UI root).
    /// </summary>
    public sealed class PromptElement : Label
    {
        public const string PadClass = "pz-prompt--pad";
        public const string KeysClass = "pz-prompt--keys";

        PromptElement(string text, string classes) : base(text)
        {
            Pz.AddClasses(this, "pz-prompt " + classes);
            pickingMode = PickingMode.Ignore;
        }

        public static PromptElement Pad(PadButton button)
        {
            var e = new PromptElement(PadText(button), $"{PadClass} pz-prompt--{button.ToString().ToLowerInvariant()}");
            bool face = button <= PadButton.Y;
            e.AddToClassList(face ? "pz-prompt--face" : "pz-prompt--pill");
            if (face) e.style.backgroundColor = FaceColor(button);
            return e;
        }

        public static PromptElement Key(string key) => new PromptElement(key, $"{KeysClass} pz-prompt--key");

        /// <summary>Adds the pad and key glyphs in front of a button's content.</summary>
        public static void Attach(VisualElement button, PadButton pad, string key)
        {
            if (key != null) button.Insert(0, Key(key));
            button.Insert(0, Pad(pad));
        }

        public static string PadText(PadButton b)
        {
            // Plain letters: the game fonts (Latin subsets) have no button symbols.
            switch (b)
            {
                case PadButton.View: return "VIEW";
                case PadButton.Menu: return "MENU";
                case PadButton.DPad: return "D-PAD";
                default: return b.ToString();
            }
        }

        static Color FaceColor(PadButton b)
        {
            switch (b)
            {
                case PadButton.A: return new Color32(84, 170, 72, 255);
                case PadButton.B: return new Color32(214, 72, 64, 255);
                case PadButton.X: return new Color32(52, 128, 214, 255);
                default: return new Color32(222, 168, 40, 255);
            }
        }
    }
}
