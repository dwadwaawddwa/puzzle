using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;

namespace PuzzleStudio.Studio.Themes
{
    /// <summary>Built-in theme presets. To add one: add an entry to <see cref="All"/> (see HOW_TO_CUSTOMIZE.md).</summary>
    public static class ThemePresets
    {
        public static readonly string[] Names =
        {
            "CozyPastel", "DarkNeon", "MinimalWhite", "RetroWood", "OceanCalm", "Forest", "CandyPop", "NightSky"
        };

        public static string DisplayName(string id)
        {
            switch (id)
            {
                case "CozyPastel": return "Cozy Pastel";
                case "DarkNeon": return "Dark Neon";
                case "MinimalWhite": return "Minimal White";
                case "RetroWood": return "Retro Wood";
                case "OceanCalm": return "Ocean Calm";
                case "CandyPop": return "Candy Pop";
                case "NightSky": return "Night Sky";
                default: return id;
            }
        }

        /// <returns>A fresh ThemeConfig for the preset (callers may modify it).</returns>
        public static ThemeConfig Create(string id)
        {
            var t = new ThemeConfig { preset = id };
            switch (id)
            {
                case "DarkNeon":
                    t.colors = Colors("#12101F", "#1E1B33", "#FF4FD8", "#3DE0FF", "#F2EEFF", "#A39DC4", "#FFD23F", "#5CFFB0", "#3DE0FF", "#FFD23F");
                    Bg(t, BackgroundType.AnimatedGradient, BackgroundPattern.Grid, "#12101F", "#231A3D");
                    Pieces(t, radius: 10, gap: 6, border: 2, borderColor: "#2A2550");
                    Ui(t, ButtonShape.Rounded, PanelStyle.Glass, 14);
                    Fonts(t, "Default:Playful", "Default:Clean");
                    t.particles.victory = ParticleKind.Stars;
                    break;

                case "MinimalWhite":
                    t.colors = Colors("#F4F4F2", "#FFFFFF", "#1E1E1E", "#8C8C8C", "#1E1E1E", "#7A7A7A", "#FF6B4A", "#3FA36B", "#FF6B4A", "#2F6BFF");
                    Bg(t, BackgroundType.Solid, BackgroundPattern.None, "#F4F4F2", "#F4F4F2");
                    Pieces(t, radius: 2, gap: 2, border: 0, borderColor: "#FFFFFF");
                    t.pieces.shadow = false;
                    Ui(t, ButtonShape.Square, PanelStyle.Flat, 4);
                    Fonts(t, "Default:Clean", "Default:Clean");
                    t.particles.victory = ParticleKind.Confetti;
                    break;

                case "RetroWood":
                    t.colors = Colors("#E9D8B4", "#F7EBD3", "#9C4A1A", "#5B7F3A", "#3B2A1A", "#7D6A55", "#E2A33B", "#5B7F3A", "#F2C14E", "#2F6BFF");
                    Bg(t, BackgroundType.Pattern, BackgroundPattern.Stripes, "#E9D8B4", "#D9C29A");
                    Pieces(t, radius: 6, gap: 5, border: 3, borderColor: "#5A3A1E");
                    Ui(t, ButtonShape.Rounded, PanelStyle.Outlined, 10);
                    Fonts(t, "Default:Elegant", "Default:Clean");
                    break;

                case "OceanCalm":
                    t.colors = Colors("#E6F4F1", "#FFFFFF", "#1F7A8C", "#86BBD8", "#16324F", "#5F7A8A", "#F6AE2D", "#2BA84A", "#F6AE2D", "#E4572E");
                    Bg(t, BackgroundType.AnimatedGradient, BackgroundPattern.Waves, "#E6F4F1", "#BFE3EA");
                    Pieces(t, radius: 12, gap: 4, border: 2, borderColor: "#FFFFFF");
                    Ui(t, ButtonShape.Pill, PanelStyle.Soft, 18);
                    Fonts(t, "Default:Rounded", "Default:Clean");
                    t.particles.victory = ParticleKind.Bubbles;
                    break;

                case "Forest":
                    t.colors = Colors("#1F2A1E", "#2B3A29", "#8FBF5A", "#D9A441", "#EEF2E6", "#A9B8A0", "#D9A441", "#8FBF5A", "#E8D44D", "#4DA3FF");
                    Bg(t, BackgroundType.Gradient, BackgroundPattern.Dots, "#1F2A1E", "#2E3F2B");
                    Pieces(t, radius: 8, gap: 4, border: 2, borderColor: "#3E5139");
                    Ui(t, ButtonShape.Rounded, PanelStyle.Soft, 12);
                    Fonts(t, "Default:Rounded", "Default:Clean");
                    t.particles.victory = ParticleKind.Stars;
                    break;

                case "CandyPop":
                    t.colors = Colors("#FFF0F6", "#FFFFFF", "#FF5D8F", "#7B61FF", "#3A2152", "#8E7AA1", "#FFC93C", "#2EC4B6", "#FFC93C", "#2EC4B6");
                    Bg(t, BackgroundType.AnimatedGradient, BackgroundPattern.Dots, "#FFF0F6", "#F3E8FF");
                    Pieces(t, radius: 16, gap: 6, border: 3, borderColor: "#FFFFFF");
                    Ui(t, ButtonShape.Pill, PanelStyle.Soft, 22);
                    Fonts(t, "Default:Playful", "Default:Rounded");
                    t.particles.victory = ParticleKind.Confetti;
                    break;

                case "NightSky":
                    t.colors = Colors("#0E1430", "#1A2147", "#7AA2FF", "#C38BFF", "#EEF1FF", "#9AA3C7", "#FFD66B", "#6EE7B7", "#FFD66B", "#FF8FAB");
                    Bg(t, BackgroundType.AnimatedGradient, BackgroundPattern.Dots, "#0E1430", "#24184A");
                    Pieces(t, radius: 8, gap: 4, border: 1, borderColor: "#2C3566");
                    Ui(t, ButtonShape.Pill, PanelStyle.Glass, 16);
                    Fonts(t, "Default:Elegant", "Default:Clean");
                    t.particles.victory = ParticleKind.Stars;
                    break;

                default: // CozyPastel = the data defaults
                    t.preset = "CozyPastel";
                    break;
            }
            return t;
        }

        /// <summary>Replaces <paramref name="target"/>'s theme with a preset, keeping custom files (logo, background image).</summary>
        public static void Apply(GamePackData pack, string id)
        {
            string bgImage = pack.theme.background.image;
            string heading = pack.theme.font.heading, body = pack.theme.font.body;
            pack.theme = Create(id);
            pack.theme.background.image = bgImage;
            // Keep custom font files the user imported.
            if (!heading.StartsWith("Default:")) pack.theme.font.heading = heading;
            if (!body.StartsWith("Default:")) pack.theme.font.body = body;
        }

        static ThemeColors Colors(string bg, string surface, string primary, string secondary, string text, string muted,
            string accent, string success, string highlight, string highlightCb) => new ThemeColors
        {
            background = bg, surface = surface, primary = primary, secondary = secondary, text = text,
            textMuted = muted, accent = accent, success = success, highlight = highlight, highlightColorblind = highlightCb,
        };

        static void Bg(ThemeConfig t, BackgroundType type, BackgroundPattern pattern, string a, string b)
        {
            t.background.type = type;
            t.background.pattern = pattern;
            t.background.gradient = new List<string> { a, b };
        }

        static void Pieces(ThemeConfig t, float radius, float gap, float border, string borderColor)
        {
            t.pieces.cornerRadius = radius;
            t.pieces.gap = gap;
            t.pieces.borderWidth = border;
            t.pieces.borderColor = borderColor;
        }

        static void Ui(ThemeConfig t, ButtonShape shape, PanelStyle panel, float radius)
        {
            t.uiStyle.buttonShape = shape;
            t.uiStyle.panelStyle = panel;
            t.uiStyle.cornerRadius = radius;
        }

        static void Fonts(ThemeConfig t, string heading, string body)
        {
            t.font.heading = heading;
            t.font.body = body;
        }

        /// <summary>Deep copy (used before applying random/preset values).</summary>
        public static ThemeConfig Clone(ThemeConfig t) => PackJson.Deserialize<ThemeConfig>(PackJson.Serialize(t));
    }
}
