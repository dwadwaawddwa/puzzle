namespace PuzzleStudio.Core.Data
{
    /// <summary>Built-in puzzle mode ids. Modes are registered by string id so new ones need no enum change.</summary>
    public static class ModeIds
    {
        public const string SwapTiles = "SwapTiles";
        public const string Strips = "Strips";
        public const string Sliding = "Sliding";
        public const string Rotate = "Rotate";
    }

    public enum DifficultyCurve { Fixed, Progressive, Custom }
    public enum UnlockRule { Sequential, AllUnlocked, ByStars }
    public enum StripsOrientation { Vertical, Horizontal }

    public enum BackgroundType { Solid, Gradient, AnimatedGradient, Pattern, Image, BlurredLevel }
    public enum BackgroundPattern { None, Dots, Stripes, Grid, Waves }
    public enum ButtonShape { Pill, Rounded, Square }
    public enum PanelStyle { Soft, Flat, Outlined, Glass }
    public enum TitleAnimation { None, Float, Pulse }
    public enum ParticleKind { None, Confetti, Stars, Bubbles, Sparkle }

    public enum AchievementRule
    {
        LevelsCompleted,   // value = number of levels
        PercentCompleted,  // value = percent 0..100
        AllThreeStars,     // every level at 3 stars
        NoPreviewLevel,    // finish a level without using preview
        NoHintLevel,       // finish a level without hints
        FastLevel,         // value = seconds
        PerfectLevel       // finish a level in par moves or less
    }
}
