using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Data
{
    /// <summary>
    /// Element positions edited in the Studio Layout tab. Missing entries = default layout.
    /// Positions are normalized screen coordinates (0..1, origin top-left) of each element's anchor point,
    /// so a layout keeps its proportions on 16:9, 16:10, 21:9 and 4:3 screens.
    /// </summary>
    [Serializable]
    public sealed class LayoutConfig
    {
        public const string GameplayScreen = "gameplay";
        public const string MenuScreen = "menu";

        public Dictionary<string, LayoutItem> gameplay = new Dictionary<string, LayoutItem>();
        public Dictionary<string, LayoutItem> menu = new Dictionary<string, LayoutItem>();
        /// <summary>Area the puzzle board is fitted into (normalized). Null = automatic.</summary>
        public LayoutRect boardArea = null;

        public Dictionary<string, LayoutItem> For(string screen) => screen == MenuScreen ? menu : gameplay;

        public LayoutItem Get(string screen, string id)
        {
            var d = For(screen);
            return d != null && d.TryGetValue(id, out var item) ? item : null;
        }
    }

    [Serializable]
    public sealed class LayoutItem
    {
        public float x;
        public float y;
        public float scale = 1f;
        public bool visible = true;

        public LayoutItem Clone() => new LayoutItem { x = x, y = y, scale = scale, visible = visible };
    }

    [Serializable]
    public sealed class LayoutRect
    {
        public float x, y, w, h;

        public LayoutRect() { }
        public LayoutRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>How the theme reacts to each level's picture.</summary>
    public enum AutoColors
    {
        /// <summary>The theme colors never change.</summary>
        Off,
        /// <summary>The background color follows the dominant color of each level picture.</summary>
        Background,
        /// <summary>Background, panels, buttons and highlights all follow each level picture.</summary>
        Full
    }
}
