using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Game.UI
{
    /// <summary>
    /// Level thumbnails for menus: a sharp version (completed levels) and a blurred one (not finished yet).
    /// Loaded one per frame so opening the level select never freezes the game.
    /// </summary>
    public sealed class ThumbnailService
    {
        public const int SharpSize = 480;
        public const int BlurSize = 28;

        readonly GamePackData _pack;
        readonly Dictionary<int, Texture2D> _sharp = new Dictionary<int, Texture2D>();
        readonly Dictionary<int, Texture2D> _blur = new Dictionary<int, Texture2D>();
        readonly Queue<(int index, bool blurred, Action<Texture2D> cb)> _queue = new Queue<(int, bool, Action<Texture2D>)>();

        public ThumbnailService(GamePackData pack) { _pack = pack; }

        public void Request(int index, bool blurred, Action<Texture2D> onReady)
        {
            var cache = blurred ? _blur : _sharp;
            if (cache.TryGetValue(index, out var tex)) { onReady(tex); return; }
            _queue.Enqueue((index, blurred, onReady));
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (_queue.Count == 0) return;
            var (index, blurred, cb) = _queue.Dequeue();
            cb(blurred ? GetBlur(index) : GetSharp(index));
        }

        public Texture2D GetSharp(int index)
        {
            if (_sharp.TryGetValue(index, out var t)) return t;
            if (index < 0 || index >= _pack.levels.Count) return null;
            var level = _pack.levels[index];
            var full = TextureLoader.Load(_pack.Resolve(level.image), maxSize: 0, mipmaps: false);
            if (full == null) { _sharp[index] = null; return null; }
            var cropped = Crop(full, level.crop);
            if (cropped != full) UnityEngine.Object.Destroy(full);
            var thumb = TextureLoader.Resize(cropped, SharpSize, mipmaps: false);
            UnityEngine.Object.Destroy(cropped);
            TextureLoader.Configure(thumb);
            _sharp[index] = thumb;
            return thumb;
        }

        public Texture2D GetBlur(int index)
        {
            if (_blur.TryGetValue(index, out var t)) return t;
            var sharp = GetSharp(index);
            if (sharp == null) { _blur[index] = null; return null; }
            var tiny = TextureLoader.Resize(sharp, BlurSize, mipmaps: false);
            var soft = SmoothUpscale(tiny, BlurSize * 12);
            UnityEngine.Object.Destroy(tiny);
            _blur[index] = soft;
            return soft;
        }

        /// <summary>CPU box blur + smooth (smoothstep) bilinear upscale: a soft, dreamy version of the picture.</summary>
        static Texture2D SmoothUpscale(Texture2D src, int maxSize)
        {
            int w = src.width, h = src.height;
            var px = src.GetPixels();
            for (int pass = 0; pass < 2; pass++) px = BoxBlur(px, w, h);

            float scale = (float)maxSize / Mathf.Max(w, h);
            int ow = Mathf.Max(1, Mathf.RoundToInt(w * scale)), oh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            var outPx = new Color[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                float fy = Mathf.Clamp((y + 0.5f) / oh * h - 0.5f, 0, h - 1);
                int y0 = (int)fy, y1 = Mathf.Min(h - 1, y0 + 1);
                float ty = Smooth(fy - y0);
                for (int x = 0; x < ow; x++)
                {
                    float fx = Mathf.Clamp((x + 0.5f) / ow * w - 0.5f, 0, w - 1);
                    int x0 = (int)fx, x1 = Mathf.Min(w - 1, x0 + 1);
                    float tx = Smooth(fx - x0);
                    var top = Color.Lerp(px[y0 * w + x0], px[y0 * w + x1], tx);
                    var bottom = Color.Lerp(px[y1 * w + x0], px[y1 * w + x1], tx);
                    outPx[y * ow + x] = Color.Lerp(top, bottom, ty);
                }
            }
            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            tex.SetPixels(outPx);
            tex.Apply(false);
            TextureLoader.Configure(tex);
            return tex;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static Color[] BoxBlur(Color[] src, int w, int h)
        {
            var dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = Mathf.Clamp(x + dx, 0, w - 1), sy = Mathf.Clamp(y + dy, 0, h - 1);
                    sum += src[sy * w + sx];
                    n++;
                }
                dst[y * w + x] = sum / n;
            }
            return dst;
        }

        static Texture2D Crop(Texture2D src, CropRect crop)
        {
            if (crop == null || (crop.x <= 0.0001f && crop.y <= 0.0001f && crop.w >= 0.9999f && crop.h >= 0.9999f)) return src;
            int x = Mathf.RoundToInt(crop.x * src.width), w = Mathf.Max(1, Mathf.RoundToInt(crop.w * src.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(crop.h * src.height));
            int y = src.height - Mathf.RoundToInt(crop.y * src.height) - h; // crop.y is from the top
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels(src.GetPixels(Mathf.Clamp(x, 0, src.width - 1), Mathf.Clamp(y, 0, src.height - 1),
                Mathf.Min(w, src.width - x), Mathf.Min(h, src.height - Mathf.Max(0, y))));
            dst.Apply(false);
            return dst;
        }

        public void Dispose()
        {
            foreach (var t in _sharp.Values) if (t != null) UnityEngine.Object.Destroy(t);
            foreach (var t in _blur.Values) if (t != null) UnityEngine.Object.Destroy(t);
            _sharp.Clear();
            _blur.Clear();
            _queue.Clear();
        }
    }
}
