using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>Short gamepad vibrations (snap, victory, blocked), only while the player uses a gamepad.</summary>
    public static class Rumble
    {
        public static bool Enabled = true;
        static float _stopAt = -1f;
        static Gamepad _pad;

        public static void Play(float low, float high, float seconds)
        {
            if (!Enabled || InputModeTracker.Current != InputMode.Gamepad) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            if (_pad != null && _pad != pad) _pad.ResetHaptics();
            _pad = pad;
            pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            _stopAt = Time.unscaledTime + seconds;
        }

        /// <summary>Call every frame: stops the motors when the vibration is over.</summary>
        public static void Tick()
        {
            if (_stopAt > 0f && Time.unscaledTime >= _stopAt) Stop();
        }

        public static void Stop()
        {
            _stopAt = -1f;
            if (_pad != null) _pad.ResetHaptics();
            _pad = null;
        }
    }
}
