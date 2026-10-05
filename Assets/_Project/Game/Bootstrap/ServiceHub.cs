using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Game.UI;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>Tiny service registry (no DI framework). Filled by <see cref="GameBootstrap"/>.</summary>
    public static class ServiceHub
    {
        public static GamePackData Pack { get; internal set; }
        public static LocalizationService Loc { get; internal set; }
        public static SaveSystem Save { get; internal set; }
        public static ThemeService Theme { get; internal set; }

        /// <summary>Set when the pack could not be loaded (the game shows an error screen).</summary>
        public static string LoadError { get; internal set; }

        public static bool IsReady => Pack != null && LoadError == null;

        public static void Clear()
        {
            Pack = null;
            Loc = null;
            Save = null;
            Theme = null;
            LoadError = null;
        }
    }
}
