using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleStudio.Game.UI
{
    public enum FontRole { Heading, Body }

    /// <summary>Built-in OFL fonts ("Default:Rounded"...) stored in Resources/Fonts.</summary>
    public static class FontLibrary
    {
        public static readonly string[] Families = { "Rounded", "Clean", "Playful", "Elegant" };

        static readonly Dictionary<string, (string heading, string body)> Files =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Rounded", ("Nunito-ExtraBold", "Nunito-Medium") },
                { "Clean", ("Inter-Bold", "Inter-Regular") },
                { "Playful", ("Fredoka-SemiBold", "Fredoka-Regular") },
                { "Elegant", ("PlayfairDisplay-Bold", "PlayfairDisplay-Regular") },
            };

        static readonly Dictionary<string, Font> Cache = new Dictionary<string, Font>();

        /// <param name="reference">"Default:Rounded" or a pack-relative file (custom fonts: Studio v2).</param>
        public static Font Get(string reference, FontRole role)
        {
            string family = "Rounded";
            if (!string.IsNullOrEmpty(reference) && reference.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
                family = reference.Substring("Default:".Length);
            if (!Files.TryGetValue(family, out var files)) files = Files["Rounded"];

            string file = role == FontRole.Heading ? files.heading : files.body;
            if (Cache.TryGetValue(file, out var font) && font != null) return font;
            font = Resources.Load<Font>("Fonts/" + file);
            Cache[file] = font;
            return font;
        }
    }
}
