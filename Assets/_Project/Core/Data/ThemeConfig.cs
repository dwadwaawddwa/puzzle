using System;
using System.Collections.Generic;

namespace PuzzleStudio.Core.Data
{
    [Serializable]
    public sealed class ThemeConfig
    {
        /// <summary>Informative only: the values below are the source of truth.</summary>
        public string preset = "CozyPastel";
        /// <summary>Off = fixed colors; Background / Full = derived from each level picture (gameplay screen).</summary>
        public AutoColors autoColors = AutoColors.Off;
        public ThemeColors colors = new ThemeColors();
        public BackgroundConfig background = new BackgroundConfig();
        public PieceStyle pieces = new PieceStyle();
        public FontConfig font = new FontConfig();
        public UIStyle uiStyle = new UIStyle();
        public ParticleConfig particles = new ParticleConfig();
    }

    [Serializable]
    public sealed class ThemeColors
    {
        public string background = "#F6EFE7";
        public string surface = "#FFFFFF";
        public string primary = "#E07A5F";
        public string secondary = "#81B29A";
        public string text = "#3D405B";
        public string textMuted = "#8D8FA6";
        public string accent = "#F2CC8F";
        public string success = "#6BBF59";
        public string highlight = "#FFD166";
        public string highlightColorblind = "#3A86FF";
    }

    [Serializable]
    public sealed class BackgroundConfig
    {
        public BackgroundType type = BackgroundType.AnimatedGradient;
        /// <summary>Pack-relative background picture (type = Image).</summary>
        public string image = null;
        /// <summary>Opacity of the background picture over the background color (Image / BlurredLevel).</summary>
        public float imageOpacity = 1f;
        /// <summary>Optional decorative pictures shown left and right of the board / menu (position: layout).</summary>
        public string decorLeft = null;
        public string decorRight = null;
        public List<string> gradient = new List<string> { "#F6EFE7", "#EADBC8" };
        public float gradientAngle = 135f;
        public float animationSpeed = 0.2f;
        public BackgroundPattern pattern = BackgroundPattern.Dots;
        public float patternOpacity = 0.06f;
        public float patternScale = 1f;
        public bool blurLevelImage = false;
        public float vignette = 0.15f;
    }

    [Serializable]
    public sealed class PieceStyle
    {
        /// <summary>Sizes are in reference pixels (1080p).</summary>
        public float cornerRadius = 8f;
        public float gap = 4f;
        public float borderWidth = 2f;
        public string borderColor = "#FFFFFF";
        public bool shadow = true;
        public float shadowStrength = 0.25f;
        public float shadowOffset = 4f;
        public bool correctGlow = true;
        /// <summary>null → colors.success.</summary>
        public string glowColor = null;
        public float hoverScale = 1.04f;
        public float liftScale = 1.08f;
    }

    [Serializable]
    public sealed class FontConfig
    {
        /// <summary>"Default:Rounded" | "Default:Clean" | "Default:Playful" | "Default:Elegant" | "theme/custom.ttf".</summary>
        public string heading = "Default:Rounded";
        public string body = "Default:Clean";
    }

    [Serializable]
    public sealed class UIStyle
    {
        public ButtonShape buttonShape = ButtonShape.Pill;
        public PanelStyle panelStyle = PanelStyle.Soft;
        public float cornerRadius = 16f;
        public float animationSpeed = 1f;
        public TitleAnimation titleAnimation = TitleAnimation.Float;
    }

    [Serializable]
    public sealed class ParticleConfig
    {
        public ParticleKind victory = ParticleKind.Confetti;
        public ParticleKind snap = ParticleKind.Sparkle;
        public float intensity = 1f;
    }
}
