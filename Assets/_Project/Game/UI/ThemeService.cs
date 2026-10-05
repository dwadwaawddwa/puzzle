using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>Parsed theme colors (computed once, reused every frame).</summary>
    public struct ThemePalette
    {
        public Color Background, Surface, Primary, Secondary, Text, TextMuted, Accent, Success, Highlight, HighlightColorblind;
        public Color OnPrimary, OnSecondary;

        public static ThemePalette From(ThemeColors c)
        {
            var d = new ThemeColors();
            var p = new ThemePalette
            {
                Background = ColorUtil.Parse(c.background, ColorUtil.Parse(d.background, Color.white)),
                Surface = ColorUtil.Parse(c.surface, Color.white),
                Primary = ColorUtil.Parse(c.primary, ColorUtil.Parse(d.primary, Color.gray)),
                Secondary = ColorUtil.Parse(c.secondary, ColorUtil.Parse(d.secondary, Color.gray)),
                Text = ColorUtil.Parse(c.text, Color.black),
                TextMuted = ColorUtil.Parse(c.textMuted, Color.gray),
                Accent = ColorUtil.Parse(c.accent, Color.yellow),
                Success = ColorUtil.Parse(c.success, ColorUtil.Parse(d.success, Color.green)),
                Highlight = ColorUtil.Parse(c.highlight, ColorUtil.Parse(d.highlight, Color.yellow)),
                HighlightColorblind = ColorUtil.Parse(c.highlightColorblind, ColorUtil.Parse(d.highlightColorblind, Color.cyan)),
            };
            p.OnPrimary = ReadableOn(p.Primary, p.Text);
            p.OnSecondary = ReadableOn(p.Secondary, p.Text);
            return p;
        }

        /// <summary>White or the theme text color, whichever reads better on <paramref name="bg"/>.</summary>
        public static Color ReadableOn(Color bg, Color themeText)
        {
            float white = ColorUtil.ContrastRatio(Color.white, bg);
            float text = ColorUtil.ContrastRatio(themeText, bg);
            return white >= text ? Color.white : themeText;
        }
    }

    /// <summary>
    /// Applies the pack theme to UI Toolkit trees. Unity can't change USS variables at runtime, so layout lives in
    /// USS and theme values are pushed as inline styles onto elements tagged with pz-* classes.
    /// </summary>
    public sealed class ThemeService
    {
        public ThemeConfig Config { get; private set; }
        public ThemePalette Palette { get; private set; }
        /// <summary>Built-in font or the pack's own font file (see <see cref="FontLibrary.Definition"/>).</summary>
        public FontDefinition HeadingFont { get; private set; }
        public FontDefinition BodyFont { get; private set; }

        public bool ColorblindMode;
        public Color HighlightColor => ColorblindMode ? Palette.HighlightColorblind : Palette.Highlight;
        public Color GlowColor => ColorUtil.Parse(Config.pieces.glowColor, Palette.Success);

        readonly GamePackData _pack;

        public ThemeService(ThemeConfig config, GamePackData pack = null)
        {
            _pack = pack;
            SetTheme(config);
        }

        /// <summary>Colors currently replacing the theme colors (per-level colors), or null.</summary>
        public ThemeColors ColorOverride { get; private set; }

        /// <summary>Per-level colors (null = back to the theme colors). Call Apply again on visible screens.</summary>
        public void SetColorOverride(ThemeColors colors)
        {
            ColorOverride = colors;
            Palette = ThemePalette.From(colors ?? Config.colors);
        }

        public void SetTheme(ThemeConfig config)
        {
            Config = config ?? new ThemeConfig();
            ColorOverride = null;
            Palette = ThemePalette.From(Config.colors);
            UpdateFonts();
        }

        bool _readableFont;

        /// <summary>Accessibility: the plain built-in font (Inter) instead of the theme fonts.</summary>
        public bool ReadableFont
        {
            get => _readableFont;
            set { _readableFont = value; UpdateFonts(); }
        }

        void UpdateFonts()
        {
            HeadingFont = FontLibrary.Definition(_readableFont ? "Default:Clean" : Config.font.heading, FontRole.Heading, _pack);
            BodyFont = FontLibrary.Definition(_readableFont ? "Default:Clean" : Config.font.body, FontRole.Body, _pack);
        }

        public float ButtonRadius(float height)
        {
            switch (Config.uiStyle.buttonShape)
            {
                case ButtonShape.Pill: return height * 0.5f;
                case ButtonShape.Square: return 4f;
                default: return Mathf.Min(Config.uiStyle.cornerRadius, height * 0.5f);
            }
        }

        /// <summary>Applies theme values to every themed element under <paramref name="root"/>.</summary>
        public void Apply(VisualElement root)
        {
            var p = Palette;
            bool hasBody = FontLibrary.IsSet(BodyFont), hasHeading = FontLibrary.IsSet(HeadingFont);
            var body = hasBody ? new StyleFontDefinition(BodyFont) : default;
            var heading = hasHeading ? new StyleFontDefinition(HeadingFont) : default;

            root.Query<TextElement>().ForEach(t =>
            {
                if (hasBody) t.style.unityFontDefinition = body;
                if (t.ClassListContains(Pz.Heading) && hasHeading) t.style.unityFontDefinition = heading;
            });
            root.Query(className: Pz.Heading).ForEach(e => { if (hasHeading) e.style.unityFontDefinition = heading; });

            root.Query(className: Pz.Text).ForEach(e => e.style.color = p.Text);
            root.Query(className: Pz.TextMuted).ForEach(e => e.style.color = p.TextMuted);
            root.Query(className: Pz.Surface).ForEach(e => ApplyPanel(e, p));
            root.Query(className: Pz.Primary).ForEach(e =>
            {
                e.style.backgroundColor = p.Primary;
                e.style.color = p.OnPrimary;
            });
            root.Query(className: Pz.Secondary).ForEach(e =>
            {
                e.style.backgroundColor = p.Secondary;
                e.style.color = p.OnSecondary;
            });
            root.Query(className: Pz.Ghost).ForEach(e =>
            {
                e.style.backgroundColor = p.Surface;
                e.style.color = p.Text;
            });
            root.Query(className: Pz.Danger).ForEach(e =>
            {
                e.style.backgroundColor = new Color(0.86f, 0.27f, 0.3f);
                e.style.color = Color.white;
            });
            root.Query(className: Pz.ButtonClass).ForEach(e =>
            {
                SetRadius(e, ButtonRadius(e.ClassListContains("pz-button--small") ? 48f : 64f));
                Color c = e.ClassListContains(Pz.Primary) ? p.OnPrimary
                        : e.ClassListContains(Pz.Secondary) ? p.OnSecondary
                        : e.ClassListContains(Pz.Danger) ? Color.white : p.Text;
                e.Query<IconElement>().ForEach(i => i.Color = c);
                e.Query<Label>().ForEach(l => { if (!(l is PromptElement)) l.style.color = c; });
            });

            // Settings controls
            root.Query(className: "pz-slider").ForEach(slider =>
            {
                var tracker = slider.Q(className: "unity-base-slider__tracker");
                if (tracker != null)
                {
                    tracker.style.backgroundColor = p.TextMuted.WithAlpha(0.25f);
                    SetBorder(tracker, 0f, Color.clear);
                }
                var dragger = slider.Q(className: "unity-base-slider__dragger");
                if (dragger != null)
                {
                    dragger.style.backgroundColor = p.Primary;
                    SetBorder(dragger, 0f, Color.clear);
                }
            });
            root.Query<PzToggle>().ForEach(t => t.SetColors(p.Primary, p.TextMuted.WithAlpha(0.35f), Color.white));
            root.Query(className: "pz-selector").ForEach(e => ApplyPanelColors(e, p, 0.6f));
            root.Query(className: "pz-selector__arrow").ForEach(e => { e.style.color = p.Primary; e.style.backgroundColor = Color.clear; });
            root.Query(className: "pz-selector__value").ForEach(e => e.style.color = p.Text);
        }

        static void ApplyPanelColors(VisualElement e, ThemePalette p, float alpha)
        {
            e.style.backgroundColor = p.TextMuted.WithAlpha(0.12f * alpha / 0.6f);
            SetRadius(e, 12f);
        }

        void ApplyPanel(VisualElement e, ThemePalette p)
        {
            float r = Config.uiStyle.cornerRadius * (e.ClassListContains(Pz.Card) ? 1.6f : 1f);
            if (e.ClassListContains(Pz.Pill)) r = ButtonRadius(56f);
            SetRadius(e, r);
            e.style.color = p.Text;
            switch (Config.uiStyle.panelStyle)
            {
                case PanelStyle.Outlined:
                    e.style.backgroundColor = p.Surface.WithAlpha(0.15f);
                    SetBorder(e, 2f, p.TextMuted.WithAlpha(0.6f));
                    break;
                case PanelStyle.Glass:
                    e.style.backgroundColor = p.Surface.WithAlpha(0.72f);
                    SetBorder(e, 1f, Color.white.WithAlpha(0.35f));
                    break;
                case PanelStyle.Flat:
                    e.style.backgroundColor = p.Surface;
                    SetBorder(e, 0f, Color.clear);
                    break;
                default: // Soft
                    e.style.backgroundColor = p.Surface.WithAlpha(0.94f);
                    SetBorder(e, 0f, Color.clear);
                    break;
            }
        }

        public static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        public static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
        }
    }
}
