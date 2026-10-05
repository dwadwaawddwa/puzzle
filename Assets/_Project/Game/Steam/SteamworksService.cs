#if PUZZLE_STEAMWORKS && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Steam;
using Steamworks;
using UnityEngine;

namespace PuzzleStudio.Game.Steam
{
    /// <summary>Steamworks.NET implementation: achievements, Rich Presence, overlay events, Steam language.</summary>
    public sealed class SteamworksService : ISteamService
    {
        Callback<GameOverlayActivated_t> _overlayCallback;
        bool _initialized;
        bool _storePending;

        public bool IsAvailable => _initialized;
        public string Status { get; private set; }
        public string LanguageCode { get; private set; }
        public bool OverlayEnabled => _initialized && SteamUtils.IsOverlayEnabled();
        public event Action<bool> OverlayToggled;

        /// <summary>Returns a working service, or a <see cref="NullSteamService"/> explaining why Steam is off.</summary>
        public static ISteamService TryCreate(GamePackData pack, uint appId, bool devLaunch)
        {
            try
            {
                if (!Packsize.Test())
                    return Off("Steamworks.NET was built for another platform (Packsize test failed).");

                if (!devLaunch && pack.steam.restartThroughSteam && SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                {
                    // Started outside Steam: Steam starts the game again (with the overlay, the right account…).
                    Debug.Log("[Steam] Relaunching through Steam.");
                    Application.Quit();
                    return new NullSteamService("relaunching through Steam");
                }

                // Test runs have no steam_appid.txt: tell the Steam API which game this is.
                if (devLaunch)
                {
                    Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
                    Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
                }

                var result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                    return Off($"Steam is not available ({result}{(string.IsNullOrEmpty(error) ? "" : ": " + error)}).");

                var service = new SteamworksService();
                service.Start();
                return service;
            }
            catch (DllNotFoundException e)
            {
                return Off("steam_api64.dll is missing: " + e.Message);
            }
            catch (Exception e)
            {
                return Off("Steam initialization failed: " + e.Message);
            }
        }

        static ISteamService Off(string reason)
        {
            Debug.LogWarning("[Steam] " + reason + " Achievements are kept locally.");
            return new NullSteamService(reason);
        }

        void Start()
        {
            _initialized = true;
            Status = "running";
            LanguageCode = SteamworksFiles.CodeFromSteamLanguage(SteamApps.GetCurrentGameLanguage());
            _overlayCallback = Callback<GameOverlayActivated_t>.Create(e => OverlayToggled?.Invoke(e.m_bActive != 0));
            Debug.Log($"[Steam] Initialized for {SteamFriends.GetPersonaName()} (language: {SteamApps.GetCurrentGameLanguage()}).");
        }

        public void UnlockAchievement(string id)
        {
            if (!_initialized || string.IsNullOrEmpty(id)) return;
            if (SteamUserStats.GetAchievement(id, out bool achieved) && achieved) return;
            if (SteamUserStats.SetAchievement(id)) _storePending = true;
            else Debug.LogWarning($"[Steam] Achievement \"{id}\" is unknown to Steam: create it in Steamworks with this API name and publish.");
        }

        public void SetPresence(string token, int level, int total, string statusText)
        {
            if (!_initialized) return;
            SteamFriends.SetRichPresence("level", level.ToString());
            SteamFriends.SetRichPresence("total", total.ToString());
            SteamFriends.SetRichPresence("steam_display", token);
            SteamFriends.SetRichPresence("status", statusText ?? "");
        }

        public void ClearPresence()
        {
            if (_initialized) SteamFriends.ClearRichPresence();
        }

        public void Tick()
        {
            if (!_initialized) return;
            SteamAPI.RunCallbacks();
            if (_storePending)
            {
                _storePending = false;
                SteamUserStats.StoreStats();
            }
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            if (_storePending) SteamUserStats.StoreStats();
            _overlayCallback?.Dispose();
            _overlayCallback = null;
            SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
#endif
