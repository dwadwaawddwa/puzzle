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
