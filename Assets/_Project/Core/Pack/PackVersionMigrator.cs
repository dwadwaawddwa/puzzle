using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PuzzleStudio.Core.Data;

namespace PuzzleStudio.Core.Pack
{
    /// <summary>
    /// Upgrades raw game.json (as JObject) step by step to <see cref="GamePackData.CurrentVersion"/>
    /// before deserialization. To add a version: bump CurrentVersion and add a step "from N to N+1".
    /// </summary>
    public static class PackVersionMigrator
    {
        static readonly Dictionary<int, Action<JObject>> Steps = new Dictionary<int, Action<JObject>>
        {
            { 0, MigrateV0ToV1 },
        };

        /// <returns>The version the document had before migration.</returns>
        public static int Migrate(JObject root)
        {
            int from = root.Value<int?>("packVersion") ?? 0;
            if (from > GamePackData.CurrentVersion)
                throw new PackLoadException($"Pack version {from} is newer than this game supports ({GamePackData.CurrentVersion}).");

            for (int v = from; v < GamePackData.CurrentVersion; v++)
            {
                if (!Steps.TryGetValue(v, out var step))
                    throw new PackLoadException($"No migration step from pack version {v}.");
                step(root);
                root["packVersion"] = v + 1;
            }
            return from;
        }

        /// <summary>
        /// v0 = early flat format: { "title", "mode", "grid", "images": ["a.png", ...] }.
        /// </summary>
        static void MigrateV0ToV1(JObject root)
        {
            var game = root["game"] as JObject ?? new JObject();
            MoveIfPresent(root, "title", game, "title");
            MoveIfPresent(root, "subtitle", game, "subtitle");
            MoveIfPresent(root, "developer", game, "developer");
            root["game"] = game;

            var gameplay = root["gameplay"] as JObject ?? new JObject();
            MoveIfPresent(root, "mode", gameplay, "defaultMode");
            if (root["grid"] != null)
            {
                gameplay["difficultyCurve"] = "Fixed";
                gameplay["fixedGrid"] = root["grid"];
                root.Remove("grid");
            }
            root["gameplay"] = gameplay;

            if (root["images"] is JArray images && root["levels"] == null)
            {
                var levels = new JArray();
                int i = 1;
                foreach (var img in images)
                {
                    string path = img.Value<string>();
                    levels.Add(new JObject
                    {
                        ["id"] = $"lvl_{i:00}",
                        ["image"] = path,
                        ["name"] = System.IO.Path.GetFileNameWithoutExtension(path),
                    });
                    i++;
                }
                root["levels"] = levels;
                root.Remove("images");
            }
        }

        static void MoveIfPresent(JObject from, string key, JObject to, string newKey)
        {
            var token = from[key];
            if (token == null) return;
            if (to[newKey] == null) to[newKey] = token;
            from.Remove(key);
        }
    }

    public sealed class PackLoadException : Exception
    {
        public PackLoadException(string message, Exception inner = null) : base(message, inner) { }
    }
}
