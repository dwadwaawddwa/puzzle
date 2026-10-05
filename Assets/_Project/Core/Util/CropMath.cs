using System;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Geometry of the Studio crop tool. Crops are normalized (0..1, origin top-left) and never leave the picture.
    /// Aspect ratios are in pixels (width / height of the cropped picture), 0 = free.
    /// </summary>
    public static class CropMath
    {
        public const float MinSize = 0.08f;

        public static CropRect Full => new CropRect(0f, 0f, 1f, 1f);

        public static bool IsFull(CropRect c) =>
            c == null || (c.x <= 0.0005f && c.y <= 0.0005f && c.w >= 0.9995f && c.h >= 0.9995f);

        public static CropRect Move(CropRect c, float dx, float dy) =>
            new CropRect(Clamp(c.x + dx, 0f, 1f - c.w), Clamp(c.y + dy, 0f, 1f - c.h), c.w, c.h);

        /// <summary>Drags a corner (0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left) to (px, py); the opposite corner stays.</summary>
        public static CropRect ResizeCorner(CropRect c, int corner, float px, float py, float aspect, float imageAspect)
        {
            bool left = corner == 0 || corner == 3, top = corner == 0 || corner == 1;
            float fx = left ? c.x + c.w : c.x;          // fixed corner
            float fy = top ? c.y + c.h : c.y;
            float maxW = left ? fx : 1f - fx;
            float maxH = top ? fy : 1f - fy;

            float w = Clamp(Math.Abs(Clamp(px, 0f, 1f) - fx), MinSize, maxW);
            float h = Clamp(Math.Abs(Clamp(py, 0f, 1f) - fy), MinSize, maxH);
            if (aspect > 0f && imageAspect > 0f)
            {
                float r = aspect / imageAspect;          // normalized width / height
                if (w / h > r) w = h * r; else h = w / r;
                if (w > maxW) { w = maxW; h = w / r; }
                if (h > maxH) { h = maxH; w = h * r; }
            }
            return new CropRect(left ? fx - w : fx, top ? fy - h : fy, w, h);
        }

        /// <summary>The largest centered crop with this aspect (0 = the whole picture).</summary>
        public static CropRect Fit(float aspect, float imageAspect)
        {
            if (aspect <= 0f || imageAspect <= 0f) return Full;
            float r = aspect / imageAspect;
            float w = r >= 1f ? 1f : r, h = r >= 1f ? 1f / r : 1f;
            return new CropRect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
        }

        /// <summary>Same center, new aspect, as large as possible inside the current crop's area and the picture.</summary>
        public static CropRect WithAspect(CropRect c, float aspect, float imageAspect)
        {
            if (aspect <= 0f || imageAspect <= 0f) return c;
            float r = aspect / imageAspect;
            float cx = c.x + c.w * 0.5f, cy = c.y + c.h * 0.5f;
            float w = c.w, h = c.h;
            if (w / h > r) w = h * r; else h = w / r;
            if (w < MinSize) { w = MinSize; h = w / r; }
            if (h < MinSize) { h = MinSize; w = h * r; }
            float scale = Math.Min(1f, Math.Min(1f / w, 1f / h));
            w *= scale; h *= scale;
            return new CropRect(Clamp(cx - w * 0.5f, 0f, 1f - w), Clamp(cy - h * 0.5f, 0f, 1f - h), w, h);
        }

        /// <summary>Pixel aspect of the cropped picture.</summary>
        public static float AspectOf(CropRect c, float imageAspect) => c.h <= 0f ? 1f : imageAspect * c.w / c.h;

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
