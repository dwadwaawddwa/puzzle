using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>Turns any image into a multi-size Windows icon (square crop, optional rounded corners).</summary>
    public static class IconBuilder
    {
        public static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

        public sealed class IconImage
        {
            public int Size;
            /// <summary>Encoded entry: 32-bit DIB for small sizes, PNG for 256.</summary>
            public byte[] Data;
        }

        public static List<IconImage> Build(Texture2D source, bool roundCorners)
        {
            var list = new List<IconImage>();
            foreach (int size in Sizes)
            {
                var tex = RenderSquare(source, size);
                if (roundCorners) RoundCorners(tex, size * 0.2f);
                list.Add(new IconImage { Size = size, Data = size >= 256 ? tex.EncodeToPNG() : EncodeDib(tex) });
                Object.DestroyImmediate(tex);
            }
            return list;
        }

        /// <summary>Center square crop, GPU-resized.</summary>
        public static Texture2D RenderSquare(Texture2D source, int size)
        {
            float aspect = (float)source.width / source.height;
            Vector2 scale = aspect > 1f ? new Vector2(1f / aspect, 1f) : new Vector2(1f, aspect);
            Vector2 offset = new Vector2((1f - scale.x) * 0.5f, (1f - scale.y) * 0.5f);

            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            source.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt, scale, offset);
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        public static void RoundCorners(Texture2D tex, float radius)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                int i = y * w + x;
                px[i].a = (byte)(px[i].a * a);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>BITMAPINFOHEADER + bottom-up BGRA pixels + empty AND mask (alpha does the work).</summary>
        public static byte[] EncodeDib(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32(); // bottom-up, like a DIB
            int maskStride = ((w + 31) / 32) * 4;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(40); bw.Write(w); bw.Write(h * 2);
            bw.Write((short)1); bw.Write((short)32);
            bw.Write(0); bw.Write(w * h * 4 + maskStride * h);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            foreach (var c in px) { bw.Write(c.b); bw.Write(c.g); bw.Write(c.r); bw.Write(c.a); }
            bw.Write(new byte[maskStride * h]);
            return ms.ToArray();
        }

        /// <summary>A standalone .ico file (for Steamworks' client icon, shortcuts...).</summary>
        public static byte[] BuildIcoFile(List<IconImage> images)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)images.Count);
            int offset = 6 + 16 * images.Count;
            foreach (var img in images)
            {
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(img.Data.Length);
                bw.Write(offset);
                offset += img.Data.Length;
            }
            foreach (var img in images) bw.Write(img.Data);
            return ms.ToArray();
        }

        /// <summary>GRPICONDIR resource referencing RT_ICON ids firstId..firstId+n-1.</summary>
        public static byte[] BuildGroupResource(List<IconImage> images, ushort firstId)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(img.Data.Length);
                bw.Write((ushort)(firstId + i));
            }
            return ms.ToArray();
        }
    }
}
