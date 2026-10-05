using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleStudio.Game.UI
{
    public enum InputMode { Pointer, Keyboard, Gamepad }

    /// <summary>
    /// Which kind of device the player is using right now. Drives the board cursor, the on-screen button prompts
    /// (A / X / LB… or keys) and the focus ring in menus: the game follows whatever was touched last.
    /// </summary>
    public static class InputModeTracker
    {
        const float StickThreshold = 0.45f;
        const float MouseMoveThreshold = 4f;

        static InputMode _current = InputMode.Pointer;
        static int _lastFrame = -1;

        /// <summary>Studio preview: show the prompts of a device without owning one. Null = follow the devices.</summary>
        public static InputMode? Forced;

        public static InputMode Current => Forced ?? _current;
        public static bool UsesNavigation => Current != InputMode.Pointer;

        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        public static event Action<InputMode> Changed;

        /// <summary>Call every frame (several callers per frame are fine).</summary>
        /// <param name="keyboardEnabled">False while the Studio is typing in a text field.</param>
        public static void Update(bool keyboardEnabled = true)
        {
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;

            InputMode? detected = null;
            if (GamepadUsed()) detected = InputMode.Gamepad;
            else if (keyboardEnabled && KeyboardNavigationUsed()) detected = InputMode.Keyboard;
            else if (PointerUsed()) detected = InputMode.Pointer;
            if (detected.HasValue) Set(detected.Value);
        }

        public static void Set(InputMode mode)
        {
            if (mode == _current) return;
            var before = Current;
            _current = mode;
            if (Current != before) Changed?.Invoke(Current);
        }

        /// <summary>Re-sends the current mode (after <see cref="Forced"/> changes).</summary>
        public static void Notify() => Changed?.Invoke(Current);

        static bool GamepadUsed()
        {
            var pad = Gamepad.current;
            if (pad == null) return false;
            if (pad.leftStick.ReadValue().magnitude > StickThreshold || pad.rightStick.ReadValue().magnitude > StickThreshold) return true;
            if (pad.leftTrigger.ReadValue() > 0.5f || pad.rightTrigger.ReadValue() > 0.5f) return true;
            return pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
                || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
                || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
                || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame
                || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;
        }

        static bool KeyboardNavigationUsed()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
                || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
                || kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame
                || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame
                || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.tabKey.wasPressedThisFrame;
        }

        static bool PointerUsed()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.delta.ReadValue().magnitude > MouseMoveThreshold) return true;
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) return true;
            }
            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
        }
    }
}
