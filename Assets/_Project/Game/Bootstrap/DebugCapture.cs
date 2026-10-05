using System;
using System.Collections;
using System.Globalization;
using System.IO;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Game.Screens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>
    /// Developer tool for automated visual checks (external screen capture sees the GPU surface as black):
    ///   Game.exe -pack X -capture out.png [-captureDelay 2] [-debugAction select|hint|solve|partial] [-captureQuit]
    /// Several captures: -capture a.png;b.png with -debugAction none;solve.
    /// Navigation actions: menu, levels, settings, credits, end, pause, achievements, play1 (level 1)…, combined with "+".
    /// "-tempSave" uses a fresh throw-away save, "-demoProgress" fills it, "-mute" silences, "-noSteam" skips Steam.
    /// "-screen menu|levels|settings|credits|end|splash" chooses the first screen.
    /// </summary>
    public sealed class DebugCapture : MonoBehaviour
    {
        public static void InstallIfRequested(GameRoot root)
        {
            if (Arg("-capture") != null) root.gameObject.AddComponent<DebugCapture>();
        }

        /// <summary>Command-line switch without value ("-mute", "-tempSave"…).</summary>
        public static bool HasFlag(string name) =>
            Array.Exists(Environment.GetCommandLineArgs(), a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>"-level N" (1-based) starts directly on a level; -1 if absent.</summary>
        public static int StartLevelArg() => int.TryParse(Arg("-level"), out int n) ? n - 1 : -1;

        public static StartScreen StartScreenArg()
        {
            string s = Arg("-screen");
            return s != null && Enum.TryParse(s, true, out StartScreen v) ? v : StartScreen.Auto;
        }

        IEnumerator Start()
        {
            string[] paths = Arg("-capture").Split(';');
            string[] actions = (Arg("-debugAction") ?? "none").Split(';');
            float delay = float.TryParse(Arg("-captureDelay"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 2f;
            var root = GetComponent<GameRoot>();

            for (int i = 0; i < paths.Length; i++)
            {
                string action = i < actions.Length ? actions[i] : "none";
                yield return new WaitForSecondsRealtime(delay);
                // "play2+partial": several actions before one capture; "pad:right" presses a button of a virtual gamepad.
                foreach (var part in action.Split('+'))
                {
                    if (part.StartsWith("pad:")) yield return PressPad(part.Substring(4));
                    else Perform(root, part);
                }
                yield return new WaitForSecondsRealtime(action.EndsWith("solve") ? 4.2f : 1.0f);
                string path = Path.GetFullPath(paths[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield return null;
                Debug.Log($"[Capture] {action} → {path}");
            }

            if (Environment.GetCommandLineArgs().Length > 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-captureQuit") >= 0)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
            }
        }

        static void Perform(GameRoot root, string action)
        {
            var flow = root != null ? root.Flow : null;
            if (flow == null) return;
            switch (action)
            {
                case "menu": flow.ShowMenu(); return;
                case "levels": flow.ShowLevels(); return;
                case "settings": flow.ShowSettings(false); return;
                case "credits": flow.ShowCredits(); return;
                case "end": flow.ShowEnd(); return;
                case "pause": flow.Pause(); return;
                case "pausesettings": flow.Pause(); flow.ShowSettings(true); return;
                case "achievements": flow.ShowAchievements(); return;
                case "controls": flow.ShowControls(); return;
            }
            if (action.StartsWith("play") && int.TryParse(action.Substring(4), out int lvl)) { flow.PlayLevel(lvl - 1); return; }

            var g = flow.Gameplay;
            var mode = g?.Mode;
            if (mode == null) return;
            switch (action)
            {
                case "select":
                    for (int c = 0; c < mode.Layout.CellCount; c++)
                        if (mode.CanPick(c)) { mode.HandleInput(PuzzleInput.Tap(c)); break; }
                    g.RefreshHighlights();
                    break;
                case "hint":
                    g.RequestHint();
                    break;
                case "partial":
                    SolveCells(mode, mode.Layout.CellCount / 2);
                    break;
                case "solve":
                    mode.ForceSolve();
                    break;
            }
        }

        static Gamepad _virtualPad;

        /// <summary>Presses and releases one button of a virtual gamepad (tests the real gamepad path end to end).</summary>
        static IEnumerator PressPad(string name)
        {
            if (_virtualPad == null)
            {
                _virtualPad = InputSystem.AddDevice<Gamepad>("DebugGamepad");
                // Captures must not flip back to "mouse" when the real mouse moves meanwhile.
                PuzzleStudio.Game.UI.InputModeTracker.Forced = PuzzleStudio.Game.UI.InputMode.Gamepad;
                PuzzleStudio.Game.UI.InputModeTracker.Notify();
            }
            GamepadButton button;
            switch (name)
            {
                case "a": button = GamepadButton.South; break;
                case "b": button = GamepadButton.East; break;
                case "x": button = GamepadButton.West; break;
                case "y": button = GamepadButton.North; break;
                case "lb": button = GamepadButton.LeftShoulder; break;
                case "rb": button = GamepadButton.RightShoulder; break;
                case "start": button = GamepadButton.Start; break;
                case "select": button = GamepadButton.Select; break;
                case "up": button = GamepadButton.DpadUp; break;
                case "down": button = GamepadButton.DpadDown; break;
                case "left": button = GamepadButton.DpadLeft; break;
                default: button = GamepadButton.DpadRight; break;
            }
            InputSystem.QueueStateEvent(_virtualPad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_virtualPad, new GamepadState());
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
        }

        static void SolveCells(IPuzzleMode mode, int count)
        {
            for (int cell = 0; cell < count; cell++)
                if (mode.PieceAt(cell) != cell)
                    mode.HandleInput(PuzzleInput.Drop(mode.Pieces[cell].Cell, cell));
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
