using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Studio.App
{
    /// <summary>Small preview textures of level images, generated once per file (and per modification date).</summary>
    public static class ThumbnailCache
    {
        public const int Size = 160;
        static readonly Dictionary<string, (DateTime stamp, Texture2D tex)> Cache = new Dictionary<string, (DateTime, Texture2D)>();

        public static Texture2D Get(string path, int size = Size)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string key = path + "|" + size;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(key, out var entry) && entry.stamp == stamp && entry.tex != null) return entry.tex;

            var full = TextureLoader.Load(path, maxSize: 0, mipmaps: false);
            if (full == null) return null;
            var thumb = TextureLoader.Resize(full, size, mipmaps: false);
            UnityEngine.Object.Destroy(full);
            TextureLoader.Configure(thumb);
            thumb.name = Path.GetFileName(path);
            if (entry.tex != null) UnityEngine.Object.Destroy(entry.tex);
            Cache[key] = (stamp, thumb);
            return thumb;
        }
    }
}
