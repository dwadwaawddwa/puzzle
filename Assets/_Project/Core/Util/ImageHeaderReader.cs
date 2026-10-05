using System.IO;

namespace PuzzleStudio.Core.Util
{
    /// <summary>Reads PNG/JPEG dimensions from the file header without decoding the image.</summary>
    public static class ImageHeaderReader
    {
        public static bool TryReadSize(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                byte[] sig = br.ReadBytes(8);
                if (sig.Length < 8) return false;

                // PNG: 89 50 4E 47 0D 0A 1A 0A, then IHDR chunk (len, "IHDR", width, height) big-endian.
                if (sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47)
                {
                    br.ReadBytes(8); // chunk length + "IHDR"
                    width = ReadBE32(br);
                    height = ReadBE32(br);
                    return width > 0 && height > 0;
                }

                // JPEG: FF D8, then scan markers until a SOFn frame header.
                if (sig[0] == 0xFF && sig[1] == 0xD8)
                {
                    fs.Position = 2;
                    while (fs.Position < fs.Length)
                    {
                        int b = fs.ReadByte();
                        if (b != 0xFF) continue;
                        int marker = fs.ReadByte();
                        while (marker == 0xFF) marker = fs.ReadByte();
                        if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;
                        int len = (fs.ReadByte() << 8) | fs.ReadByte();
                        bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                        if (isSof)
                        {
                            fs.ReadByte(); // precision
                            height = (fs.ReadByte() << 8) | fs.ReadByte();
                            width = (fs.ReadByte() << 8) | fs.ReadByte();
                            return width > 0 && height > 0;
                        }
                        fs.Position += len - 2;
                    }
                }
            }
            catch (IOException) { }
            return false;
        }

        public static bool IsSupportedExtension(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        static int ReadBE32(BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }
    }
}
