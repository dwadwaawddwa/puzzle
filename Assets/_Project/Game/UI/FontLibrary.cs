using System;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

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

        static readonly Dictionary<string, FontAsset> FileAssets = new Dictionary<string, FontAsset>();

        /// <summary>
        /// The font of a role as UI Toolkit uses it: a built-in font, or a font asset made at runtime from the pack's
        /// .ttf / .otf ("theme/font_titles_1a2b3c4d.ttf"). Unreadable files fall back to the built-in font.
        /// </summary>
        public static FontDefinition Definition(string reference, FontRole role, GamePackData pack = null)
        {
            if (pack != null && IsCustom(reference))
            {
                var asset = LoadFile(pack.Resolve(reference));
                if (asset != null) return FontDefinition.FromSDFFont(asset);
            }
            var font = Get(reference, role);
            return font != null ? FontDefinition.FromFont(font) : default;
        }

        public static bool IsSet(FontDefinition d) => d.font != null || d.fontAsset != null;

        /// <param name="reference">"Default:Rounded"… (anything else gives the default family).</param>
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

        public static bool IsCustom(string reference) =>
            !string.IsNullOrEmpty(reference) && !reference.StartsWith("Default:", StringComparison.OrdinalIgnoreCase);

        /// <summary>A .ttf / .otf file turned into a dynamic SDF font asset (glyphs are added as text needs them). Null if unusable.</summary>
        public static FontAsset LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (FileAssets.TryGetValue(path, out var cached)) return cached;
            FontAsset asset = null;
            try
            {
                asset = FontAsset.CreateFontAsset(path, 0, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                if (asset != null)
                {
                    asset.name = Path.GetFileNameWithoutExtension(path);
                    if (!asset.HasCharacter('A', false, true) && !asset.HasCharacter('a', false, true))
                    {
                        Debug.LogWarning($"[Fonts] {path} has no Latin letters; using a built-in font.");
                        asset = null;
                    }
                }
                else Debug.LogWarning($"[Fonts] {path} could not be read as a font; using a built-in font.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Fonts] Could not load {path}: {e.Message}");
                asset = null;
            }
            FileAssets[path] = asset;
            return asset;
        }
    }
}
