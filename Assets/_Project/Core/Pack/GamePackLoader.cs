using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Pack
{
    public static class GamePackLoader
    {
        /// <summary>Loads and migrates game.json from a pack folder. Throws <see cref="PackLoadException"/>.</summary>
        public static GamePackData LoadFromDirectory(string packDir)
        {
            if (string.IsNullOrEmpty(packDir) || !Directory.Exists(packDir))
                throw new PackLoadException($"Game pack folder not found: {packDir}");

            string jsonPath = Path.Combine(packDir, PackPaths.GameJson);
            if (!File.Exists(jsonPath))
                throw new PackLoadException($"Missing {PackPaths.GameJson} in {packDir}");

            var pack = Parse(File.ReadAllText(jsonPath));
            pack.RootPath = Path.GetFullPath(packDir);
            return pack;
        }

        /// <summary>Parses (and migrates) a game.json string.</summary>
        public static GamePackData Parse(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException e) { throw new PackLoadException($"game.json is not valid JSON: {e.Message}", e); }

            PackVersionMigrator.Migrate(root);

            try
            {
                var pack = root.ToObject<GamePackData>(PackJson.Serializer()) ?? new GamePackData();
                Normalize(pack);
                return pack;
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException)
            {
                throw new PackLoadException($"game.json has an invalid value: {e.Message}", e);
            }
        }

        /// <summary>Replaces nulls left by explicit "null" values with defaults and makes ids unique-ish.</summary>
        public static void Normalize(GamePackData pack)
        {
            pack.game ??= new GameInfo();
            pack.gameplay ??= new GameplayConfig();
            pack.theme ??= new ThemeConfig();
            pack.theme.colors ??= new ThemeColors();
            pack.theme.background ??= new BackgroundConfig();
            pack.theme.pieces ??= new PieceStyle();
            pack.theme.font ??= new FontConfig();
            pack.theme.uiStyle ??= new UIStyle();
            pack.theme.particles ??= new ParticleConfig();
            pack.audio ??= new AudioConfig();
            pack.audio.sfx ??= AudioConfig.DefaultSfx();
            pack.steam ??= new SteamConfig();
            pack.layout ??= new LayoutConfig();
            pack.layout.gameplay ??= new System.Collections.Generic.Dictionary<string, LayoutItem>();
            pack.layout.menu ??= new System.Collections.Generic.Dictionary<string, LayoutItem>();
            pack.levels ??= new System.Collections.Generic.List<LevelConfig>();
            pack.texts ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            pack.gameplay.starRules ??= new StarRules();
            pack.gameplay.stripsCount ??= new IntRange { min = 4, max = 12 };

            for (int i = 0; i < pack.levels.Count; i++)
            {
                var l = pack.levels[i] ??= new LevelConfig();
                if (string.IsNullOrWhiteSpace(l.id)) l.id = $"lvl_{i + 1:00}";
                l.crop ??= new CropRect();
                l.name ??= "";
            }
        }
    }
}
