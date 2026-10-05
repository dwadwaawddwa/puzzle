using System;

namespace PuzzleStudio.Core.Steam
{
    /// <summary>
    /// What the game asks of Steam. The real implementation (Steamworks.NET) lives in the Game assembly;
    /// <see cref="NullSteamService"/> is used without an App ID, outside Steam, in the Studio preview and in tests.
    /// </summary>
    public interface ISteamService
    {
        /// <summary>Steam is running and the API is initialized.</summary>
        bool IsAvailable { get; }
        /// <summary>Why Steam is not available (for the log), or null.</summary>
        string Status { get; }
        /// <summary>Steam's language for this game mapped to a game code ("fr"), or null.</summary>
        string LanguageCode { get; }
        /// <summary>The Steam overlay can show its own "achievement unlocked" popup.</summary>
        bool OverlayEnabled { get; }
        /// <summary>Raised when the Steam overlay opens (true) or closes (false).</summary>
        event Action<bool> OverlayToggled;

        void UnlockAchievement(string id);
        /// <param name="token">Rich Presence token ("#Playing"), defined in the uploaded localization file.</param>
        /// <param name="statusText">Plain text shown in "View game info".</param>
        void SetPresence(string token, int level, int total, string statusText);
        void ClearPresence();
        void Tick();
        void Shutdown();
    }

    public sealed class NullSteamService : ISteamService
    {
        public NullSteamService(string status = null) { Status = status; }

        public bool IsAvailable => false;
        public string Status { get; }
        public string LanguageCode => null;
        public bool OverlayEnabled => false;
        public event Action<bool> OverlayToggled { add { } remove { } }

        public void UnlockAchievement(string id) { }
        public void SetPresence(string token, int level, int total, string statusText) { }
        public void ClearPresence() { }
        public void Tick() { }
        public void Shutdown() { }
    }
}
