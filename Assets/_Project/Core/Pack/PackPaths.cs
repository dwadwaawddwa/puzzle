namespace PuzzleStudio.Core.Pack
{
    public static class PackPaths
    {
        public const string GameJson = "game.json";
        public const string LevelsDir = "levels";
        public const string ThemeDir = "theme";
        public const string AudioDir = "audio";
        public const string LocaleDir = "locale";
        /// <summary>Folder name inside StreamingAssets of an exported game.</summary>
        public const string StreamingFolder = "GamePack";
        /// <summary>Command line argument to load a pack from disk: -pack "C:\path\to\GamePack".</summary>
        public const string PackArg = "-pack";
    }
}
