using PuzzleStudio.Core.Data;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>
    /// Maps grid cells to UV rectangles of a single texture (no texture is ever cut into files).
    /// Grid rows go top → bottom; UVs go bottom → top, so rows are flipped here.
    /// </summary>
    public static class ImageSlicer
    {
        /// <summary>UV rect (x, y, w, h) of cell (col, row) inside the cropped area.</summary>
        public static Rect CellUV(CropRect crop, int cols, int rows, int col, int row)
        {
            float cw = crop.w / cols;
            float ch = crop.h / rows;
            float u = crop.x + col * cw;
            float vTop = 1f - crop.y - row * ch;   // top edge in UV space
            return new Rect(u, vTop - ch, cw, ch);
        }

        /// <summary>UV rect of the whole cropped area.</summary>
        public static Rect CropUV(CropRect crop) => new Rect(crop.x, 1f - crop.y - crop.h, crop.w, crop.h);

        /// <summary>Width / height of the cropped area in pixels.</summary>
        public static float CroppedAspect(int texWidth, int texHeight, CropRect crop)
        {
            if (texHeight <= 0 || crop.h <= 0) return 1f;
            return (texWidth * crop.w) / (texHeight * crop.h);
        }

        /// <summary>
        /// Returns a crop that makes every cell of a cols×rows grid exactly square (needed by Rotate mode),
        /// keeping as much of <paramref name="crop"/> as possible, centred.
        /// </summary>
        public static CropRect SquareCellCrop(int texWidth, int texHeight, CropRect crop, int cols, int rows)
        {
            float cropPxW = texWidth * crop.w, cropPxH = texHeight * crop.h;
            float target = (float)cols / rows;
            float aspect = cropPxW / cropPxH;
            var r = new CropRect(crop.x, crop.y, crop.w, crop.h);
            if (aspect > target)
            {
                float newW = crop.w * target / aspect;
                r.x = crop.x + (crop.w - newW) * 0.5f;
                r.w = newW;
            }
            else if (aspect < target)
            {
                float newH = crop.h * aspect / target;
                r.y = crop.y + (crop.h - newH) * 0.5f;
                r.h = newH;
            }
            return r;
        }
    }
}
