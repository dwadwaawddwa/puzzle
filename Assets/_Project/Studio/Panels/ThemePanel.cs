using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Themes;
using PuzzleStudio.Studio.Widgets;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    public sealed class ThemePanel : StudioPanel
    {
        static readonly List<string> FontIds = new List<string> { "Default:Rounded", "Default:Clean", "Default:Playful", "Default:Elegant" };
        static readonly List<string> FontNames = new List<string> { "Rounded (Nunito)", "Clean (Inter)", "Playful (Fredoka)", "Elegant (Playfair)" };

        public ThemePanel(StudioApp app) : base(app) { }
        public override string Id => "theme";
        public override string Title => "Theme";

        public override void Build(VisualElement content)
        {
            var t = Pack.theme;

            // ---- presets
            var presetNames = ThemePresets.Names.Select(ThemePresets.DisplayName).ToList();
            int presetIndex = Array.IndexOf(ThemePresets.Names, t.preset);
            var presetChoices = new List<string>(presetNames);
            if (presetIndex < 0) { presetChoices.Insert(0, t.preset == "Random" ? "Random" : "Custom"); presetIndex = 0; }
            var preset = Fields.Dropdown("Preset", presetChoices, presetIndex, i =>
            {
                string name = presetChoices[i];
                int id = presetNames.IndexOf(name);
                if (id < 0) return;
                ThemePresets.Apply(Pack, ThemePresets.Names[id]);
                Changed(rebuildInspector: true);
            });
            var randomize = Fields.Button("Randomize theme", () =>
            {
                ThemeRandomizer.Apply(Pack, Environment.TickCount);
                Changed(rebuildInspector: true);
            }, "studio-btn--accent");
            content.Add(Fields.Section("Style",
                preset,
                Fields.Row(randomize),
                Fields.Hint("A preset sets every color, the pieces, fonts and buttons at once. Then fine-tune anything below.")));

            // ---- colors
            var c = t.colors;
            Func<IEnumerable<string>> palette = () => new[]
            {
                c.background, c.surface, c.primary, c.secondary, c.text, c.textMuted, c.accent, c.success, c.highlight,
                "#FFFFFF", "#000000"
            };
            var colors = Fields.Section("Colors");
            var autoNames = new List<string> { "Off (fixed colors)", "Background only", "Whole theme" };
            colors.Add(Fields.Dropdown("From pictures", autoNames, (int)t.autoColors, i =>
            {
                if (i >= 0) { t.autoColors = (AutoColors)i; Changed(); }
            }));
            colors.Add(Fields.Hint("Follows each picture: during a level, the background (and with \"whole theme\" the buttons and highlights) " +
                                   "take the dominant colors of that level's picture. Text always stays readable."));
            colors.Add(Fields.Row(Fields.Button("Generate palette from images", GeneratePalette, "studio-btn--small")));
            void AddColor(string label, Func<string> get, Action<string> set) =>
                colors.Add(new ColorField(label, get(), v => { set(v); Changed(); App.RefreshContrast(); }, () => App.PopupLayer, palette));
            AddColor("Background", () => c.background, v =>
            {
                c.background = v;
                if (t.background.gradient.Count > 0) t.background.gradient[0] = v;
            });
            AddColor("Panels", () => c.surface, v => c.surface = v);
            AddColor("Primary", () => c.primary, v => c.primary = v);
            AddColor("Secondary", () => c.secondary, v => c.secondary = v);
            AddColor("Text", () => c.text, v => c.text = v);
            AddColor("Text (muted)", () => c.textMuted, v => c.textMuted = v);
            AddColor("Accent / stars", () => c.accent, v => c.accent = v);
            AddColor("Correct glow", () => c.success, v => c.success = v);
            AddColor("Selection", () => c.highlight, v => c.highlight = v);
            AddColor("Selection (colorblind)", () => c.highlightColorblind, v => c.highlightColorblind = v);
            colors.Add(App.ContrastHint);
            content.Add(colors);
            App.RefreshContrast();

            // ---- background & decorations
            var bg = t.background;
            var typeNames = new List<string> { "Color", "Gradient", "Animated gradient", "Picture", "Level picture (blurred)" };
            var typeValues = new[] { BackgroundType.Solid, BackgroundType.Gradient, BackgroundType.AnimatedGradient, BackgroundType.Image, BackgroundType.BlurredLevel };
            int typeIndex = Math.Max(0, Array.IndexOf(typeValues, bg.type == BackgroundType.Pattern ? BackgroundType.Solid : bg.type));
            var bgSection = Fields.Section("Background");
            bgSection.Add(Fields.Dropdown("Type", typeNames, typeIndex, i =>
            {
                if (i < 0) return;
                bg.type = typeValues[i];
                Changed(rebuildInspector: true);
            }));
            bool gradient = bg.type == BackgroundType.Gradient || bg.type == BackgroundType.AnimatedGradient;
            if (gradient)
            {
                while (bg.gradient.Count < 2) bg.gradient.Add(bg.gradient.Count == 0 ? c.background : c.surface);
                bgSection.Add(new ColorField("Second color", bg.gradient[1], v => { bg.gradient[1] = v; Changed(); }, () => App.PopupLayer, palette));
                bgSection.Add(Fields.FloatSlider("Angle", 0f, 360f, bg.gradientAngle, v => { bg.gradientAngle = v; Changed(); }));
                if (bg.type == BackgroundType.AnimatedGradient)
                    bgSection.Add(Fields.FloatSlider("Animation speed", 0.05f, 1.5f, bg.animationSpeed, v => { bg.animationSpeed = v; Changed(); }));
                bgSection.Add(Fields.Hint("The first color is \"Background\" in Colors above."));
            }
            if (bg.type == BackgroundType.Image)
                bgSection.Add(ImageRow("Picture", bg.image, "background", v => bg.image = v));
            if (bg.type == BackgroundType.Image || bg.type == BackgroundType.BlurredLevel)
            {
                bgSection.Add(Fields.FloatSlider("Picture opacity", 0.1f, 1f, bg.imageOpacity, v => { bg.imageOpacity = v; Changed(); }));
                bgSection.Add(Fields.Hint("Below 100 % the picture blends with the background color."));
            }
            bgSection.Add(Fields.Enum("Pattern", bg.pattern, v => { bg.pattern = v; Changed(rebuildInspector: true); }));
            if (bg.pattern != BackgroundPattern.None)
            {
                bgSection.Add(Fields.FloatSlider("Pattern opacity", 0.01f, 0.4f, bg.patternOpacity, v => { bg.patternOpacity = v; Changed(); }));
                bgSection.Add(Fields.FloatSlider("Pattern size", 0.4f, 3f, bg.patternScale, v => { bg.patternScale = v; Changed(); }));
            }
            bgSection.Add(Fields.FloatSlider("Vignette", 0f, 0.6f, bg.vignette, v => { bg.vignette = v; Changed(); }));
            content.Add(bgSection);

            content.Add(Fields.Section("Side decorations",
                ImageRow("Left picture", bg.decorLeft, "decor_left", v => bg.decorLeft = v),
                ImageRow("Right picture", bg.decorRight, "decor_right", v => bg.decorRight = v),
                Fields.Hint("Optional small pictures on each side (PNG with transparency looks best). " +
                            "Move and resize them in the Layout tab.")));

            // ---- pieces
            var p = t.pieces;
            content.Add(Fields.Section("Pieces",
                Fields.FloatSlider("Corner radius", 0, 40, p.cornerRadius, v => { p.cornerRadius = v; Changed(); }),
                Fields.FloatSlider("Gap", 0, 20, p.gap, v => { p.gap = v; Changed(); }),
                Fields.FloatSlider("Border width", 0, 10, p.borderWidth, v => { p.borderWidth = v; Changed(); }),
                new ColorField("Border color", p.borderColor, v => { p.borderColor = v; Changed(); }, () => App.PopupLayer, palette),
                Fields.Toggle("Glow when correct", p.correctGlow, v => { p.correctGlow = v; Changed(); }),
                Fields.Toggle("Shadow", p.shadow, v => { p.shadow = v; Changed(); }),
                Fields.FloatSlider("Shadow strength", 0f, 0.8f, p.shadowStrength, v => { p.shadowStrength = v; Changed(); }),
                Fields.FloatSlider("Lift when dragged", 1f, 1.25f, p.liftScale, v => { p.liftScale = v; Changed(); })));

            // ---- particles
            var fx = t.particles;
            var victoryKinds = new List<ParticleKind> { ParticleKind.Confetti, ParticleKind.Stars, ParticleKind.Bubbles, ParticleKind.None };
            var snapKinds = new List<ParticleKind> { ParticleKind.Sparkle, ParticleKind.Stars, ParticleKind.None };
            content.Add(Fields.Section("Effects",
                Fields.Dropdown("Victory", victoryKinds.Select(k => k.ToString()).ToList(), Math.Max(0, victoryKinds.IndexOf(fx.victory)), i =>
                {
                    if (i >= 0) { fx.victory = victoryKinds[i]; Changed(); }
                }),
                Fields.Dropdown("Piece in place", snapKinds.Select(k => k.ToString()).ToList(), Math.Max(0, snapKinds.IndexOf(fx.snap)), i =>
                {
                    if (i >= 0) { fx.snap = snapKinds[i]; Changed(); }
                }),
                Fields.FloatSlider("Amount", 0f, 2f, fx.intensity, v => { fx.intensity = v; Changed(); }),
                Fields.Hint("Choose \"Victory\" in the preview screen list to see the celebration.")));

            // ---- logos
            var g = Pack.game;
            content.Add(Fields.Section("Logos",
                ImageRow("Game logo", g.logo, "logo", v => g.logo = v),
                Fields.Hint("Replaces the title text in the menu, the splash and the credits. A PNG with a transparent background, about 1200 px wide."),
                ImageRow("Studio logo", g.devLogo, "devlogo", v => g.devLogo = v),
                Fields.Hint("Shown on the splash screen before the title (instead of \"<Developer> presents\").")));

            // ---- fonts
            content.Add(Fields.Section("Fonts",
                FontRow("Titles", t.font.heading, v => t.font.heading = v, "font_titles"),
                FontRow("Text", t.font.body, v => t.font.body = v, "font_text"),
                Fields.Hint("Built-in fonts are free (SIL Open Font License). Your own .ttf / .otf: check that its license allows embedding it in a game (OFL fonts do).")));

            // ---- interface
            var u = t.uiStyle;
            content.Add(Fields.Section("Interface",
                Fields.Enum("Buttons", u.buttonShape, v => { u.buttonShape = v; Changed(); }),
                Fields.Enum("Panels", u.panelStyle, v => { u.panelStyle = v; Changed(); }),
                Fields.FloatSlider("Panel corners", 0, 32, u.cornerRadius, v => { u.cornerRadius = v; Changed(); }),
                Fields.FloatSlider("Animation speed", 0.5f, 2f, u.animationSpeed, v => { u.animationSpeed = v; Changed(); })));
        }

        /// <summary>Built-in font families, or the user's own font file.</summary>
        VisualElement FontRow(string label, string current, Action<string> set, string baseName)
        {
            var ids = new List<string>(FontIds);
            var names = new List<string>(FontNames);
            if (!FontIds.Contains(current) && !string.IsNullOrEmpty(current))
            {
                ids.Add(current);
                names.Add("My font: " + System.IO.Path.GetFileName(current));
            }
            var dropdown = Fields.Dropdown(label, names, Math.Max(0, ids.IndexOf(current)), i =>
            {
                if (i >= 0) { set(ids[i]); Changed(rebuildInspector: true); }
            });
            var import = Fields.Button("Import…", () =>
            {
                string file = FileDialogs.OpenFile($"Choose the font for the {label.ToLowerInvariant()} (.ttf or .otf)", FileDialogs.FontFilter);
                if (string.IsNullOrEmpty(file)) return;
                string relative = PuzzleStudio.Studio.App.ProjectFiles.ImportThemeFile(Pack, file, baseName);
                if (PuzzleStudio.Game.UI.FontLibrary.LoadFile(Pack.Resolve(relative)) == null)
                {
                    App.Modal.Message("Font not usable", "This file could not be read as a font with Latin letters. Try another .ttf or .otf file.");
                    return;
                }
                set(relative);
                Changed(rebuildInspector: true);
            }, "studio-btn--small");
            var row = Fields.Row(dropdown, import);
            row.AddToClassList("studio-audio-row");
            return row;
        }

        VisualElement ImageRow(string label, string current, string baseName, Action<string> set)
        {
            var l = new Label(label);
            l.AddToClassList("studio-path-label");
            var thumb = new VisualElement();
            thumb.AddToClassList("studio-image-thumb");
            var tex = App.ThumbnailFor(current);
            if (tex != null) thumb.style.backgroundImage = Background.FromTexture2D(tex);
            var choose = Fields.Button(current == null ? "Choose…" : "Change…", () =>
            {
                string file = FileDialogs.OpenFile($"Choose the {label.ToLowerInvariant()}", FileDialogs.ImageFilter);
                if (string.IsNullOrEmpty(file)) return;
                set(PuzzleStudio.Studio.App.ProjectFiles.ImportThemeFile(Pack, file, baseName));
                Changed(rebuildInspector: true);
            }, "studio-btn--small");
            var row = Fields.Row(l, thumb, choose);
            if (current != null)
                row.Add(Fields.Button("Remove", () =>
                {
                    PuzzleStudio.Studio.App.ProjectFiles.RemoveThemeFile(Pack, current);
                    set(null);
                    Changed(rebuildInspector: true);
                }, "studio-btn--small studio-btn--danger"));
            row.AddToClassList("studio-image-row");
            return row;
        }

        void GeneratePalette()
        {
            var palettes = new List<List<PaletteExtractor.Swatch>>();
            foreach (var level in Pack.levels.Take(40))
            {
                var tex = TextureLoader.Load(Pack.Resolve(level.image), 256, false);
                if (tex == null) continue;
                palettes.Add(PaletteExtractor.FromTexture(tex));
                UnityEngine.Object.Destroy(tex);
            }
            if (palettes.Count == 0)
            {
                App.Modal.Message("No pictures", "Add some images in the Levels tab first.");
                return;
            }
            var merged = PaletteExtractor.Merge(palettes);
            var colors = ThemeDerivation.FromPalette(Pack.theme.colors, merged, AutoColors.Full);
            Pack.theme.colors = colors;
            Pack.theme.preset = "Custom";
            if (Pack.theme.background.gradient.Count > 0) Pack.theme.background.gradient[0] = colors.background;
            App.Toast("Palette generated from " + palettes.Count + " picture(s)");
            Changed(rebuildInspector: true);
        }

        /// <summary>Readable summary of the theme contrast checks.</summary>
        public static string ContrastSummary(GamePackData pack)
        {
            var report = PackValidator.Validate(pack, checkFiles: false);
            var lines = report.Issues.Where(i => i.code.StartsWith("theme.")).Select(i => "• " + i.message).ToList();
            return lines.Count == 0 ? "Colors are readable (WCAG AA contrast OK)." : string.Join("\n", lines);
        }
    }
}
