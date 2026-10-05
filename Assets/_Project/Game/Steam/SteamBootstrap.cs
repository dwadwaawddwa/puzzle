using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Steam;
using UnityEngine;

namespace PuzzleStudio.Game.Steam
{
    /// <summary>Picks the Steam implementation for this run: Steamworks when an App ID is set, otherwise nothing.</summary>
    public static class SteamBootstrap
    {
        /// <summary>True when the game was started from the Studio / a script with "-pack" (test run, never relaunched through Steam).</summary>
        public static bool IsDevLaunch()
        {
            if (Application.isEditor) return true;
            foreach (var a in Environment.GetCommandLineArgs())
                if (string.Equals(a, "-pack", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// May quit the application: when the game was started outside Steam, Steam relaunches it
        /// (<see cref="SteamConfig.restartThroughSteam"/>).
        /// </summary>
        public static ISteamService Create(GamePackData pack)
        {
            long appId = pack.game.steamAppId;
            if (appId <= 0 || appId > uint.MaxValue) return new NullSteamService("no Steam App ID");
#if PUZZLE_STEAMWORKS && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
            return SteamworksService.TryCreate(pack, (uint)appId, IsDevLaunch());
#else
            return new NullSteamService("Steamworks.NET is not available on this platform");
#endif
        }
    }
}
