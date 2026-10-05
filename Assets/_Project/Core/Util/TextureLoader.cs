using System.IO;
using UnityEngine;

namespace PuzzleStudio.Core.Util
{
    /// <summary>Loads PNG/JPG files at runtime into GPU textures, downscaling very large images.</summary>
    public static class TextureLoader
    {
        public const int DefaultMaxSize = 2048;

        /// <returns>The texture, or null if the file is missing/unreadable. Caller owns it (Destroy when done).</returns>
        public static Texture2D Load(string path, int maxSize = DefaultMaxSize, bool mipmaps = true)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException) { return null; }
            return LoadFromBytes(bytes, Path.GetFileNameWithoutExtension(path), maxSize, mipmaps);
        }

        public static Texture2D LoadFromBytes(byte[] bytes, string name, int maxSize = DefaultMaxSize, bool mipmaps = true)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps) { name = name };
            if (!tex.LoadImage(bytes, false))
            {
                Destroy(tex);
                return null;
            }

            if (maxSize > 0 && (tex.width > maxSize || tex.height > maxSize))
            {
                var scaled = Resize(tex, maxSize, mipmaps);
                Destroy(tex);
                tex = scaled;
                tex.name = name;
            }

            Configure(tex);
            return tex;
        }

        /// <summary>GPU downscale keeping aspect ratio so the longest side = maxSize.</summary>
        public static Texture2D Resize(Texture2D source, int maxSize, bool mipmaps)
        {
            float scale = (float)maxSize / Mathf.Max(source.width, source.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, mipmaps);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply(mipmaps);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public const string PlaceholderName = "placeholder";

        /// <summary>
        /// Picture used when a level's file is missing or unreadable: colorful bands and rings so every piece looks
        /// different and the level stays playable (the error is logged; the Studio refuses to export such a pack).
        /// </summary>
        public static Texture2D Placeholder(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = PlaceholderName };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    // Hue changes left → right, lightness and saturation top → bottom: every tile has its own color.
                    var c = Color.HSVToRGB(u * 0.85f, 0.3f + v * 0.35f, 0.98f - v * 0.28f);
                    float ring = Mathf.Repeat(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 9f, 1f);
                    if (ring < 0.12f) c *= 0.78f;
                    if (((x + y) / (size / 16)) % 2 == 0) c = Color.Lerp(c, Color.white, 0.14f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            Configure(tex);
            return tex;
        }

        public static bool IsPlaceholder(Texture tex) => tex != null && tex.name == PlaceholderName;

        public static void Configure(Texture2D tex)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
        }

        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
