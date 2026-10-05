using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Save;
using UnityEngine;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>Turns the saved player settings into engine settings (display, frame rate, volumes).</summary>
    public static class SettingsApplier
    {
        public static readonly int[] FpsChoices = { 30, 60, 120, 144, 0 };
        public static readonly float[] UiScales = { 0.9f, 1f, 1.15f, 1.3f };

        public static void ApplyFrameRate(SettingsData s)
        {
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.vSync ? -1 : (s.fpsLimit > 0 ? s.fpsLimit : -1);
        }

        public static void ApplyDisplay(SettingsData s)
        {
            var mode = ToMode(s.displayMode);
            int w = s.resolutionWidth, h = s.resolutionHeight;
            if (mode == FullScreenMode.FullScreenWindow || w <= 0 || h <= 0)
            {
                var native = Screen.currentResolution;
                if (mode == FullScreenMode.Windowed && (w <= 0 || h <= 0))
                {
                    w = Mathf.RoundToInt(native.width * 0.8f);
                    h = Mathf.RoundToInt(native.height * 0.8f);
                }
                else if (w <= 0 || h <= 0 || mode == FullScreenMode.FullScreenWindow)
                {
                    w = native.width;
                    h = native.height;
                }
            }
            Screen.SetResolution(w, h, mode);
        }

        public static FullScreenMode ToMode(DisplayMode m)
        {
            switch (m)
            {
                case DisplayMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                case DisplayMode.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        public static DisplayMode CurrentMode()
        {
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.ExclusiveFullScreen: return DisplayMode.Fullscreen;
                case FullScreenMode.Windowed: return DisplayMode.Windowed;
                default: return DisplayMode.Borderless;
            }
        }

        /// <summary>Distinct supported resolutions (largest first), at least 1024 × 600.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && v.y >= 600 && !list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            list.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return list;
        }

        public static float Music(SettingsData s, GamePackData p) => s.musicVolume >= 0 ? s.musicVolume : p.audio.musicVolume;
        public static float Sfx(SettingsData s, GamePackData p) => s.sfxVolume >= 0 ? s.sfxVolume : p.audio.sfxVolume;
    }
}
