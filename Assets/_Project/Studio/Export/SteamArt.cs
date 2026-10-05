using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PuzzleStudio.Studio.Export
{
    public enum ArtKind { Capsule, Small, Tall, Hero, Logo, PageBackground, Square }

    public sealed class StoreImageSpec
    {
        public readonly string Name, File;
        public readonly int Width, Height;
        public readonly ArtKind Kind;

        public StoreImageSpec(string name, string file, int width, int height, ArtKind kind)
        {
            Name = name; File = file; Width = width; Height = height; Kind = kind;
        }
    }

    /// <summary>
    /// Steam graphics generated from the game itself: store/library capsules (cover picture cut into puzzle tiles,
    /// title in the theme font) and 256 × 256 achievement icons. Starting points: replace any file with your own art.
    /// </summary>
    public static class SteamArt
    {
        public const int AchievementIconSize = 256;

        /// <summary>Sizes required by Steamworks (Store Page Admin → Graphical Assets, Library Assets, Community).</summary>
        public static readonly StoreImageSpec[] StoreImages =
        {
            new StoreImageSpec("Header capsule", "header_capsule.png", 920, 430, ArtKind.Capsule),
            new StoreImageSpec("Small capsule", "small_capsule.png", 462, 174, ArtKind.Small),
            new StoreImageSpec("Main capsule", "main_capsule.png", 1232, 706, ArtKind.Capsule),
            new StoreImageSpec("Vertical capsule", "vertical_capsule.png", 748, 896, ArtKind.Tall),
            new StoreImageSpec("Page background", "page_background.png", 1438, 810, ArtKind.PageBackground),
            new StoreImageSpec("Library capsule", "library_capsule.png", 600, 900, ArtKind.Tall),
            new StoreImageSpec("Library header", "library_header.png", 920, 430, ArtKind.Capsule),
            new StoreImageSpec("Library hero", "library_hero.png", 3840, 1240, ArtKind.Hero),
            new StoreImageSpec("Library logo", "library_logo.png", 1280, 720, ArtKind.Logo),
            new StoreImageSpec("Community icon", "community_icon.jpg", 184, 184, ArtKind.Square),
        };

        sealed class Context
        {
            public ThemePalette Palette;
            public Font Heading, Body;
            public string Title, Subtitle;
            public Texture2D Cover;
        }

        // ------------------------------------------------------------------ store images

        /// <summary>Writes every store image into <paramref name="dir"/>. <paramref name="coverLevel"/> = level whose picture is used.</summary>
        public static IEnumerator RenderStoreImages(GamePackData pack, int coverLevel, string dir, UiImageRenderer renderer, Action<string> status)
        {
            Directory.CreateDirectory(dir);
            var ctx = MakeContext(pack, coverLevel, 2048);
            try
            {
                for (int i = 0; i < StoreImages.Length; i++)
                {
                    var spec = StoreImages[i];
                    status?.Invoke($"Store images… {i + 1}/{StoreImages.Length} ({spec.Name})");
                    Texture2D result = null;
                    yield return renderer.Render(spec.Width, spec.Height, root => Build(spec, root, ctx), t => result = t, FitTitles);
                    string path = Path.Combine(dir, spec.File);
                    File.WriteAllBytes(path, spec.File.EndsWith(".jpg") ? result.EncodeToJPG(92) : result.EncodeToPNG());
                    Object.Destroy(result);
                }
            }
            finally
            {
                if (ctx.Cover != null) Object.Destroy(ctx.Cover);
            }
        }

        static Context MakeContext(GamePackData pack, int coverLevel, int maxSize)
        {
            var theme = new ThemeService(pack.theme, pack);
            var ctx = new Context
            {
                Palette = theme.Palette,
                Heading = theme.HeadingFont,
                Body = theme.BodyFont,
                Title = string.IsNullOrWhiteSpace(pack.game.title) ? "My Puzzle Game" : pack.game.title,
                Subtitle = pack.game.subtitle,
            };
            if (pack.levels.Count > 0)
            {
                int index = Mathf.Clamp(coverLevel, 0, pack.levels.Count - 1);
                ctx.Cover = TextureLoader.Load(pack.Resolve(pack.levels[index].image), maxSize, false);
            }
            return ctx;
        }

        static void Build(StoreImageSpec spec, VisualElement root, Context c)
        {
            float w = spec.Width, h = spec.Height;
            var p = c.Palette;
            root.style.backgroundColor = spec.Kind == ArtKind.Logo ? Color.clear : p.Background;

            switch (spec.Kind)
            {
                case ArtKind.Logo:
                    root.Add(TitleBlock(c, h * 0.3f, w * 0.9f, Color.white, new Color(0.08f, 0.08f, 0.1f, 0.9f), true, Align.Center, Justify.Center));
                    return;

                case ArtKind.Square:
                    root.Add(Picture(c.Cover));
                    return;

                case ArtKind.PageBackground:
                    root.Add(Picture(c.Cover, 0.22f));
                    root.Add(Fill(new GradientElement(p.Background.WithAlpha(0.15f), p.Background.WithAlpha(0.85f))));
                    return;
            }

            // Capsules and hero: the cover cut into puzzle tiles, one tile lifted out of its slot.
            float cell = spec.Kind == ArtKind.Tall ? w / 3.2f : spec.Kind == ArtKind.Small ? h / 1.6f : h / 3.1f;
            root.Add(Picture(c.Cover));
            root.Add(Fill(new GridLines(cell, p.Background.WithAlpha(0.55f), Mathf.Max(2f, cell * 0.022f))));
            if (c.Cover != null && spec.Kind != ArtKind.Small)
            {
                int cols = Mathf.FloorToInt(w / cell);
                var slot = new Rect((cols - 2) * cell, spec.Kind == ArtKind.Tall ? cell : 0f, cell, cell);
                AddLiftedTile(root, c, w, h, slot, 7f);
                if (spec.Kind == ArtKind.Hero)
                    AddLiftedTile(root, c, w, h, new Rect(Mathf.Floor(cols * 0.4f) * cell, cell, cell, cell), -5f);
            }
            if (spec.Kind == ArtKind.Hero) return; // Steam draws the library logo over the hero: no text.

            switch (spec.Kind)
            {
                case ArtKind.Small:
                    root.Add(Fill(new GradientElement(p.Background.WithAlpha(0.45f), p.Background.WithAlpha(0.75f))));
                    root.Add(TitleBlock(c, h * 0.42f, w * 0.9f, p.Text, p.Background, false, Align.Center, Justify.Center));
                    break;
                case ArtKind.Tall:
                {
                    var band = Fill(new GradientElement(p.Background.WithAlpha(0f), p.Background.WithAlpha(0.94f)));
                    band.style.top = h * 0.42f;
                    root.Add(band);
                    var title = TitleBlock(c, w * 0.15f, w * 0.86f, p.Text, p.Background, true, Align.Center, Justify.FlexEnd);
                    title.style.paddingBottom = h * 0.07f;
                    root.Add(title);
                    break;
                }
                default:
                {
                    var band = Fill(new GradientElement(p.Background.WithAlpha(0f), p.Background.WithAlpha(0.93f)));
                    band.style.top = h * 0.38f;
                    root.Add(band);
                    var title = TitleBlock(c, h * 0.2f, w * 0.88f, p.Text, p.Background, h >= 400, Align.Center, Justify.FlexEnd);
                    title.style.paddingBottom = h * 0.08f;
                    root.Add(title);
                    break;
                }
            }
        }

        /// <summary>A copy of one tile of the cover, rotated and shadowed, above an empty slot.</summary>
        static void AddLiftedTile(VisualElement root, Context c, float w, float h, Rect slot, float angle)
        {
            var p = c.Palette;
            var hole = Absolute(slot);
            hole.style.backgroundColor = p.Background.WithAlpha(0.9f);
            SetRadius(hole, slot.width * 0.06f);
            root.Add(hole);

            float scale = Mathf.Max(w / c.Cover.width, h / c.Cover.height);
            float dw = c.Cover.width * scale, dh = c.Cover.height * scale;
            float ox = (w - dw) * 0.5f, oy = (h - dh) * 0.5f;

            var holder = Absolute(slot);
            holder.style.rotate = new Rotate(angle);
            holder.style.translate = new Translate(-slot.width * 0.22f, slot.height * 0.3f);

            var shadow = Fill(new VisualElement());
            shadow.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            shadow.style.translate = new Translate(slot.width * 0.05f, slot.height * 0.07f);
            SetRadius(shadow, slot.width * 0.06f);
            holder.Add(shadow);

            var tile = Fill(new VisualElement());
            tile.style.backgroundImage = Background.FromTexture2D(c.Cover);
            tile.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(new Length(dw), new Length(dh)));
            tile.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(-(slot.x - ox))));
            tile.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Top, new Length(-(slot.y - oy))));
            float border = Mathf.Max(2f, slot.width * 0.03f);
            tile.style.borderLeftWidth = tile.style.borderRightWidth = tile.style.borderTopWidth = tile.style.borderBottomWidth = border;
            tile.style.borderLeftColor = tile.style.borderRightColor = tile.style.borderTopColor = tile.style.borderBottomColor = p.Surface;
            SetRadius(tile, slot.width * 0.06f);
            holder.Add(tile);
            root.Add(holder);
        }

        static VisualElement TitleBlock(Context c, float fontSize, float maxWidth, Color color, Color outline, bool withSubtitle, Align align, Justify justify)
        {
            var block = Fill(new VisualElement());
            block.style.alignItems = align;
            block.style.justifyContent = justify;

            var title = new Label(c.Title);
            title.AddToClassList("art-fit");
            title.userData = maxWidth;
            if (c.Heading != null) title.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(c.Heading));
            title.style.fontSize = fontSize;
            title.style.color = color;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.unityTextOutlineWidth = Mathf.Clamp(fontSize * 0.05f, 1f, 8f);
            title.style.unityTextOutlineColor = outline.WithAlpha(0.85f);
            title.style.textShadow = new TextShadow { offset = new Vector2(0, fontSize * 0.04f), blurRadius = fontSize * 0.08f, color = new Color(0, 0, 0, 0.35f) };
            title.style.marginBottom = 0;
            block.Add(title);

            if (withSubtitle && !string.IsNullOrWhiteSpace(c.Subtitle))
            {
                var sub = new Label(c.Subtitle);
                sub.AddToClassList("art-fit");
                sub.userData = maxWidth;
                if (c.Body != null) sub.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(c.Body));
                sub.style.fontSize = fontSize * 0.32f;
                sub.style.color = color;
                sub.style.opacity = 0.85f;
                sub.style.whiteSpace = WhiteSpace.NoWrap;
                sub.style.unityTextAlign = TextAnchor.MiddleCenter;
                sub.style.marginTop = fontSize * 0.08f;
                sub.style.unityTextOutlineWidth = Mathf.Clamp(fontSize * 0.02f, 0.5f, 3f);
                sub.style.unityTextOutlineColor = outline.WithAlpha(0.6f);
                block.Add(sub);
            }
            return block;
        }

        /// <summary>Shrinks titles wider than their allowed width (long game names).</summary>
        static void FitTitles(VisualElement root)
        {
            root.Query<Label>(className: "art-fit").ForEach(l =>
            {
                if (!(l.userData is float max)) return;
                float size = l.resolvedStyle.fontSize;
                float width = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                if (width > max && width > 0) l.style.fontSize = Mathf.Max(8f, size * max / width * 0.97f);
            });
        }

        // ------------------------------------------------------------------ achievement icons

        /// <summary>
        /// &lt;id&gt;.jpg (achieved) and &lt;id&gt;_locked.jpg (gray) for every achievement, 256 × 256 as Steamworks asks.
        /// Each one uses a different level picture with a badge showing what it rewards.
        /// </summary>
        public static IEnumerator RenderAchievementIcons(GamePackData pack, IList<AchievementDef> achievements, string dir, UiImageRenderer renderer, Action<string> status)
        {
            Directory.CreateDirectory(dir);
            var palette = new ThemeService(pack.theme, pack).Palette;
            var pictures = new Dictionary<int, Texture2D>();
            int size = AchievementIconSize;
            try
            {
                for (int i = 0; i < achievements.Count; i++)
                {
                    var def = achievements[i];
                    status?.Invoke($"Achievement icons… {i + 1}/{achievements.Count}");
                    Texture2D picture = null;
                    if (pack.levels.Count > 0)
                    {
                        int level = i % pack.levels.Count;
                        if (!pictures.TryGetValue(level, out picture))
                            pictures[level] = picture = TextureLoader.Load(pack.Resolve(pack.levels[level].image), 512, false);
                    }

                    Texture2D icon = null;
                    yield return renderer.Render(size, size, root => BuildAchievement(root, size, picture, palette, IconFor(def.rule)), t => icon = t);
                    File.WriteAllBytes(Path.Combine(dir, def.id + ".jpg"), icon.EncodeToJPG(92));
                    MakeLocked(icon);
                    File.WriteAllBytes(Path.Combine(dir, def.id + "_locked.jpg"), icon.EncodeToJPG(92));
                    Object.Destroy(icon);
                }
            }
            finally
            {
                foreach (var t in pictures.Values) if (t != null) Object.Destroy(t);
            }
        }

        public static Icon IconFor(AchievementRule rule)
        {
            switch (rule)
            {
                case AchievementRule.AllThreeStars: return Icon.Star;
                case AchievementRule.NoPreviewLevel: return Icon.Eye;
                case AchievementRule.NoHintLevel: return Icon.Hint;
                case AchievementRule.FastLevel: return Icon.Clock;
                case AchievementRule.PerfectLevel: return Icon.Check;
                default: return Icon.Trophy;
            }
        }

        static void BuildAchievement(VisualElement root, float size, Texture2D picture, ThemePalette p, Icon icon)
        {
            root.style.backgroundColor = p.Background;
            root.Add(Picture(picture));
            root.Add(Fill(new GradientElement(new Color(0, 0, 0, 0.05f), new Color(0, 0, 0, 0.5f))));

            float badgeSize = size * 0.54f;
            var badge = Absolute(new Rect((size - badgeSize) * 0.5f, (size - badgeSize) * 0.5f, badgeSize, badgeSize));
            badge.style.backgroundColor = p.Primary;
            SetRadius(badge, badgeSize * 0.5f);
            float ring = size * 0.035f;
            badge.style.borderLeftWidth = badge.style.borderRightWidth = badge.style.borderTopWidth = badge.style.borderBottomWidth = ring;
            badge.style.borderLeftColor = badge.style.borderRightColor = badge.style.borderTopColor = badge.style.borderBottomColor = p.Surface;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            var glyph = new IconElement(icon) { Color = p.OnPrimary };
            glyph.style.width = glyph.style.height = badgeSize * 0.56f;
            badge.Add(glyph);
            root.Add(badge);
        }

        /// <summary>Gray, darker copy (Steam shows it while the achievement is locked).</summary>
        static void MakeLocked(Texture2D tex)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float lum = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt((lum * 0.55f + 0.06f) * 255f), 0, 255);
                px[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        // ------------------------------------------------------------------ helpers

        static VisualElement Picture(Texture2D tex, float opacity = 1f)
        {
            var e = Fill(new VisualElement());
            if (tex == null) return e;
            e.style.backgroundImage = Background.FromTexture2D(tex);
            e.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            e.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            e.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            e.style.opacity = opacity;
            return e;
        }

        static T Fill<T>(T e) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.top = 0;
            e.style.right = 0;
            e.style.bottom = 0;
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        static VisualElement Absolute(Rect r)
        {
            var e = new VisualElement();
            e.style.position = Position.Absolute;
            e.style.left = r.x;
            e.style.top = r.y;
            e.style.width = r.width;
            e.style.height = r.height;
            return e;
        }

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        /// <summary>Thin lines every <c>cell</c> pixels: the picture looks cut into puzzle tiles.</summary>
        sealed class GridLines : VisualElement
        {
            readonly float _cell, _width;
            readonly Color _color;

            public GridLines(float cell, Color color, float width)
            {
                _cell = Mathf.Max(8f, cell);
                _color = color;
                _width = width;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext mgc)
            {
                var r = contentRect;
                var p = mgc.painter2D;
                p.strokeColor = _color;
                p.lineWidth = _width;
                p.BeginPath();
                for (float x = _cell; x < r.width; x += _cell) { p.MoveTo(new Vector2(x, 0)); p.LineTo(new Vector2(x, r.height)); }
                for (float y = _cell; y < r.height; y += _cell) { p.MoveTo(new Vector2(0, y)); p.LineTo(new Vector2(r.width, y)); }
                p.Stroke();
            }
        }
    }
}
