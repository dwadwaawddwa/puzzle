using System.IO;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Pack
{
    public static class GamePackWriter
    {
        public static string ToJson(GamePackData pack)
        {
            pack.packVersion = GamePackData.CurrentVersion;
            return PackJson.Serialize(pack);
        }

        /// <summary>Writes game.json atomically into <paramref name="packDir"/> (assets are copied by the caller).</summary>
        public static void WriteJson(GamePackData pack, string packDir)
        {
            Directory.CreateDirectory(packDir);
            string path = Path.Combine(packDir, PackPaths.GameJson);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(pack));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Deep copy through JSON (used by the Studio for undo snapshots and previews).</summary>
        public static GamePackData Clone(GamePackData pack)
        {
            var copy = GamePackLoader.Parse(ToJson(pack));
            copy.RootPath = pack.RootPath;
            return copy;
        }
    }
}
