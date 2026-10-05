using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>
    /// Where the game is drawn and where its pointer comes from. The default is the whole screen;
    /// the Studio live preview overrides it to run the very same game inside a frame of its window.
    /// Surface coordinates are pixels with origin bottom-left (like Unity screen coordinates).
    /// </summary>
    public class GameViewport
    {
        public const float ReferenceHeight = 1080f;

        /// <summary>Player UI size multiplier (settings "text size").</summary>
        public float UiScale = 1f;

        /// <summary>Pixel size of the render surface.</summary>
        public virtual Vector2Int Size => new Vector2Int(Screen.width, Screen.height);

        /// <summary>False while the game should ignore keyboard shortcuts (e.g. the Studio is typing in a field).</summary>
        public virtual bool KeyboardEnabled => true;

        /// <summary>Whether this host is the Studio preview (no window title, no real saves...).</summary>
        public virtual bool IsPreview => false;

        public virtual bool TryGetPointer(out Vector2 surfacePosition)
        {
            var p = Pointer.current;
            if (p == null) { surfacePosition = default; return false; }
            surfacePosition = p.position.ReadValue();
            return true;
        }

        /// <summary>True if a game UI element (button, panel) is under the pointer.</summary>
        public virtual bool IsOverUI(Vector2 surfacePosition, VisualElement uiRoot)
        {
            var panel = uiRoot?.panel;
            if (panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(surfacePosition.x, Size.y - surfacePosition.y));
            return panel.Pick(panelPos) != null;
        }
    }
}
