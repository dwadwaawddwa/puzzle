using System;

namespace PuzzleStudio.Core.Data
{
    [Serializable]
    public sealed class LevelConfig
    {
        public string id = "";
        /// <summary>Pack-relative image path, e.g. "levels/01.png".</summary>
        public string image = "";
        public string name = "";
        /// <summary>null = gameplay.defaultMode.</summary>
        public string mode = null;
        /// <summary>null = difficulty curve.</summary>
        public GridOverride grid = null;
        /// <summary>Normalized crop rectangle (0..1, origin top-left).</summary>
        public CropRect crop = new CropRect();
        /// <summary>null = derived from the level id.</summary>
        public int? seed = null;
    }

    [Serializable]
    public sealed class GridOverride
    {
        public int? cols;
        public int? rows;
        /// <summary>Number of strips (Strips mode).</summary>
        public int? strips;
    }

    [Serializable]
    public sealed class CropRect
    {
        public float x = 0f;
        public float y = 0f;
        public float w = 1f;
        public float h = 1f;

        public CropRect() { }
        public CropRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }

        public bool IsValid => w > 0.001f && h > 0.001f && x >= 0f && y >= 0f && x + w <= 1.0001f && y + h <= 1.0001f;
    }
}
