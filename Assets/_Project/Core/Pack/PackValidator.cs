using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using UnityEngine;

namespace PuzzleStudio.Core.Pack
{
    public enum IssueSeverity { Info, Warning, Error }

    [Serializable]
    public sealed class ValidationIssue
    {
        public IssueSeverity severity;
        public string code;
        public string message;
        /// <summary>Level id the issue relates to, or null for global issues.</summary>
        public string levelId;

        public override string ToString() => $"[{severity}] {code}{(levelId != null ? $" ({levelId})" : "")}: {message}";
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();
        public bool HasErrors => Issues.Exists(i => i.severity == IssueSeverity.Error);
        public int Count(IssueSeverity s) => Issues.FindAll(i => i.severity == s).Count;
        public bool Has(string code) => Issues.Exists(i => i.code == code);

        public void Add(IssueSeverity severity, string code, string message, string levelId = null) =>
            Issues.Add(new ValidationIssue { severity = severity, code = code, message = message, levelId = levelId });
    }

    /// <summary>Checks a pack and returns readable errors/warnings (English: shown in the Studio).</summary>
    public static class PackValidator
    {
        public const int MinImageSize = 512;
        public const float MaxAspect = 3f;
        public const float MinTextContrast = 4.5f;   // WCAG AA, normal text
        public const float MinUiContrast = 3f;       // WCAG AA, large text / UI components

        public static ValidationReport Validate(GamePackData pack, bool checkFiles = true)
        {
            var r = new ValidationReport();
            if (pack == null) { r.Add(IssueSeverity.Error, "pack.null", "No game pack loaded."); return r; }

            ValidateGame(pack, r);
            ValidateGameplay(pack, r);
            ValidateTheme(pack, r);
            ValidateLevels(pack, r, checkFiles);
            if (checkFiles) ValidateAssets(pack, r);
            return r;
        }

        static void ValidateGame(GamePackData p, ValidationReport r)
        {
            if (string.IsNullOrWhiteSpace(p.game.title))
                r.Add(IssueSeverity.Error, "game.title.empty", "The game title is empty.");
            else if (p.game.title.Length > 60)
                r.Add(IssueSeverity.Warning, "game.title.long", "The game title is very long (> 60 characters).");
            if (p.game.steamAppId < 0)
                r.Add(IssueSeverity.Error, "steam.appid.invalid", "Steam App ID must be 0 (no Steam) or a positive number.");
            if (string.IsNullOrWhiteSpace(p.game.developer))
                r.Add(IssueSeverity.Info, "game.developer.empty", "No developer name set (shown on the splash and credits).");
        }

        static void ValidateGameplay(GamePackData p, ValidationReport r)
        {
            var g = p.gameplay;
            if (!PuzzleModeRegistry.IsRegistered(g.defaultMode))
                r.Add(IssueSeverity.Error, "gameplay.mode.unknown", $"Unknown default puzzle mode \"{g.defaultMode}\".");
            if (g.minGrid < 2 || g.maxGrid < g.minGrid || g.maxGrid > GridResolver.MaxGrid)
                r.Add(IssueSeverity.Error, "gameplay.grid.range", $"Grid range must satisfy 2 ≤ min ≤ max ≤ {GridResolver.MaxGrid}.");
            if (g.fixedGrid < 2 || g.fixedGrid > GridResolver.MaxGrid)
                r.Add(IssueSeverity.Error, "gameplay.grid.fixed", $"Fixed grid size must be between 2 and {GridResolver.MaxGrid}.");
            if (g.starRules.threeStarsMoveFactor < 1f || g.starRules.twoStarsMoveFactor < g.starRules.threeStarsMoveFactor)
                r.Add(IssueSeverity.Warning, "gameplay.stars.rules", "Star rules look odd: expected 1 ≤ three-star factor ≤ two-star factor.");
            if (g.minMisplacedRatio < 0f || g.minMisplacedRatio > 1f)
                r.Add(IssueSeverity.Error, "gameplay.shuffle.ratio", "Minimum misplaced ratio must be between 0 and 1.");
        }

        static void ValidateTheme(GamePackData p, ValidationReport r)
        {
            var c = p.theme.colors;
            CheckColor(r, "background", c.background);
            CheckColor(r, "surface", c.surface);
            CheckColor(r, "primary", c.primary);
            CheckColor(r, "secondary", c.secondary);
            CheckColor(r, "text", c.text);
            CheckColor(r, "textMuted", c.textMuted);
            CheckColor(r, "accent", c.accent);

            if (ColorUtil.TryParseHex(c.text, out var text))
            {
                if (ColorUtil.TryParseHex(c.surface, out var surface) && ColorUtil.ContrastRatio(text, surface) < MinTextContrast)
                    r.Add(IssueSeverity.Warning, "theme.contrast.text-surface",
                        $"Text on panels is hard to read (contrast {F(ColorUtil.ContrastRatio(text, surface))}:1, WCAG AA needs {F(MinTextContrast)}:1).");
                if (ColorUtil.TryParseHex(c.background, out var bg) && ColorUtil.ContrastRatio(text, bg) < MinTextContrast)
                    r.Add(IssueSeverity.Warning, "theme.contrast.text-background",
                        $"Text on the background is hard to read (contrast {F(ColorUtil.ContrastRatio(text, bg))}:1, WCAG AA needs {F(MinTextContrast)}:1).");
            }
            if (ColorUtil.TryParseHex(c.primary, out var primary) && ColorUtil.TryParseHex(c.surface, out var surf) &&
                ColorUtil.ContrastRatio(primary, surf) < MinUiContrast)
                r.Add(IssueSeverity.Info, "theme.contrast.primary-surface",
                    $"Primary color stands out little from panels (contrast {F(ColorUtil.ContrastRatio(primary, surf))}:1).");

            if (p.theme.pieces.gap < 0 || p.theme.pieces.cornerRadius < 0 || p.theme.pieces.borderWidth < 0)
                r.Add(IssueSeverity.Error, "theme.pieces.negative", "Piece gap, corner radius and border width cannot be negative.");
        }

        static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        static void CheckColor(ValidationReport r, string name, string value)
        {
            if (!ColorUtil.TryParseHex(value, out _))
                r.Add(IssueSeverity.Error, "theme.color.invalid", $"Color \"{name}\" is not a valid hex color: \"{value}\".");
        }

        static void ValidateLevels(GamePackData p, ValidationReport r, bool checkFiles)
        {
            if (p.levels.Count == 0)
            {
                r.Add(IssueSeverity.Error, "levels.empty", "The game has no levels. Import some images.");
                return;
            }

            var ids = new HashSet<string>();
            var hashes = new Dictionary<string, string>();
            using var md5 = checkFiles ? MD5.Create() : null;

            foreach (var l in p.levels)
            {
                if (!ids.Add(l.id))
                    r.Add(IssueSeverity.Error, "level.id.duplicate", $"Level id \"{l.id}\" is used more than once.", l.id);
                if (string.IsNullOrWhiteSpace(l.name))
                    r.Add(IssueSeverity.Info, "level.name.empty", "Level has no name.", l.id);
                if (l.mode != null && !PuzzleModeRegistry.IsRegistered(l.mode))
                    r.Add(IssueSeverity.Error, "level.mode.unknown", $"Unknown puzzle mode \"{l.mode}\".", l.id);
                if (!l.crop.IsValid)
                    r.Add(IssueSeverity.Error, "level.crop.invalid", "Crop rectangle is outside the image.", l.id);
                if (l.grid != null)
                {
                    if ((l.grid.cols ?? 2) < 2 || (l.grid.rows ?? 2) < 1 || (l.grid.cols ?? 0) > GridResolver.MaxGrid || (l.grid.rows ?? 0) > GridResolver.MaxGrid)
                        r.Add(IssueSeverity.Error, "level.grid.invalid", $"Grid must be between 2 and {GridResolver.MaxGrid} cells per side.", l.id);
                    if (l.grid.strips.HasValue && (l.grid.strips < 2 || l.grid.strips > 24))
                        r.Add(IssueSeverity.Error, "level.strips.invalid", "Strip count must be between 2 and 24.", l.id);
                }

                if (string.IsNullOrWhiteSpace(l.image))
                {
                    r.Add(IssueSeverity.Error, "level.image.none", "Level has no image.", l.id);
                    continue;
                }
                if (!checkFiles) continue;

                string path = p.Resolve(l.image);
                if (path == null || !File.Exists(path))
                {
                    r.Add(IssueSeverity.Error, "level.image.missing", $"Image file not found: {l.image}", l.id);
                    continue;
                }
                if (!ImageHeaderReader.IsSupportedExtension(path))
                {
                    r.Add(IssueSeverity.Error, "level.image.format", "Only PNG and JPG images are supported.", l.id);
                    continue;
                }
                if (!ImageHeaderReader.TryReadSize(path, out int w, out int h))
                {
                    r.Add(IssueSeverity.Error, "level.image.unreadable", $"Image could not be read: {l.image}", l.id);
                    continue;
                }

                float cw = w * l.crop.w, ch = h * l.crop.h;
                if (Mathf.Min(cw, ch) < MinImageSize)
                    r.Add(IssueSeverity.Warning, "level.image.small",
                        $"Image is small ({Mathf.RoundToInt(cw)}×{Mathf.RoundToInt(ch)} after crop). It will look blurry; {MinImageSize}px minimum is recommended.", l.id);
                float aspect = Mathf.Max(cw, ch) / Mathf.Max(1f, Mathf.Min(cw, ch));
                if (aspect > MaxAspect)
                    r.Add(IssueSeverity.Warning, "level.image.aspect", $"Extreme image ratio ({F(aspect)}:1). Consider cropping it.", l.id);

                string hash = Convert.ToBase64String(md5.ComputeHash(File.ReadAllBytes(path)));
                if (hashes.TryGetValue(hash, out string other))
                    r.Add(IssueSeverity.Warning, "level.image.duplicate", $"Same image as level \"{other}\".", l.id);
                else hashes[hash] = l.id;
            }
        }

        static void ValidateAssets(GamePackData p, ValidationReport r)
        {
            CheckOptionalFile(p, r, p.game.logo, "theme.logo.missing", "Logo");
            CheckOptionalFile(p, r, p.game.devLogo, "theme.devlogo.missing", "Developer logo");
            CheckOptionalFile(p, r, p.theme.background.image, "theme.background.missing", "Background image");
            CheckOptionalFile(p, r, p.theme.background.decorLeft, "theme.decor.missing", "Left decoration");
            CheckOptionalFile(p, r, p.theme.background.decorRight, "theme.decor.missing", "Right decoration");
            if (p.theme.background.type == BackgroundType.Image && string.IsNullOrEmpty(p.theme.background.image))
                r.Add(IssueSeverity.Warning, "theme.background.noimage", "Background type is Image but no picture is chosen.");
            CheckOptionalFile(p, r, p.audio.musicMenu, "audio.music.missing", "Menu music");
            CheckOptionalFile(p, r, p.audio.musicGame, "audio.music.missing", "Game music");
            foreach (var kv in p.audio.sfx) CheckOptionalFile(p, r, kv.Value, "audio.sfx.missing", $"Sound \"{kv.Key}\"");
            if (!p.theme.font.heading.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
                CheckOptionalFile(p, r, p.theme.font.heading, "theme.font.missing", "Heading font");
            if (!p.theme.font.body.StartsWith("Default:", StringComparison.OrdinalIgnoreCase))
                CheckOptionalFile(p, r, p.theme.font.body, "theme.font.missing", "Body font");
        }

        static void CheckOptionalFile(GamePackData p, ValidationReport r, string rel, string code, string label)
        {
            if (string.IsNullOrEmpty(rel) || PackPathsUtil.IsDefaultRef(rel)) return;
            string path = p.Resolve(rel);
            if (path == null || !File.Exists(path))
                r.Add(IssueSeverity.Error, code, $"{label} file not found: {rel}");
        }
    }
}
