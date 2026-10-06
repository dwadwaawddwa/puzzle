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
    ///   Game.exe -pack X -capture out.png [-captureDelay 2] [-debugAction select|hint|solve|partial|flip|mismatch] [-captureQuit]
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
                    else if (part == "perf") yield return MeasurePerformance();
                    else Perform(root, part);
                }
                // Memory cards turned over are captured before they are turned back (GameplayController.MismatchDelay).
                float wait = action.EndsWith("solve") ? 4.2f : action.EndsWith("mismatch") || action.EndsWith("flip") ? 0.6f : 1.0f;
                yield return new WaitForSecondsRealtime(wait);
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
                case "colorblind": flow.Theme.ColorblindMode = true; flow.Gameplay?.RefreshBoard(); return;
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
                    if (mode is ICardMode) MatchPairs(mode, mode.Layout.CellCount / 4);
                    else SolveCells(mode, mode.Layout.CellCount / 2);
                    break;
                case "flip":       // Memory: turns over the first card still face down
                    for (int c = 0; c < mode.Layout.CellCount; c++)
                        if (mode.CanPick(c)) { mode.HandleInput(PuzzleInput.Tap(c)); break; }
                    break;
                case "mismatch":   // Memory: turns over two different cards
                    if (mode is ICardMode cards)
                        for (int a = 0; a < mode.Layout.CellCount; a++)
                            for (int b = a + 1; b < mode.Layout.CellCount; b++)
                                if (mode.CanPick(a) && mode.CanPick(b) && cards.SymbolOf(a) != cards.SymbolOf(b))
                                {
                                    mode.HandleInput(PuzzleInput.Tap(a));
                                    mode.HandleInput(PuzzleInput.Tap(b));
                                    return;
                                }
                    break;
                case "solve":
                    mode.ForceSolve();
                    break;
            }
        }

        /// <summary>
        /// "perf": 400 frames without the frame cap (V-Sync off) → average / 95th percentile / worst frame time,
        /// garbage collections and memory, written to Player.log as "[Perf] …".
        /// </summary>
        static IEnumerator MeasurePerformance()
        {
            int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            for (int i = 0; i < 30; i++) yield return null;    // settle

            const int frames = 400;
            var times = new float[frames];
            int gc0 = GC.CollectionCount(0);
            long mem0 = GC.GetTotalMemory(false);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                times[i] = Time.unscaledDeltaTime * 1000f;
            }
            int gcs = GC.CollectionCount(0) - gc0;
            long mem1 = GC.GetTotalMemory(false);
            Array.Sort(times);
            float sum = 0f;
            foreach (var t in times) sum += t;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Perf] {0}x{1}: avg {2:0.00} ms ({3:0} fps), p95 {4:0.00} ms, worst {5:0.00} ms, GC {6} in {7} frames, managed heap {8:0.0} -> {9:0.0} MB",
                Screen.width, Screen.height, sum / frames, 1000f / (sum / frames), times[(int)(frames * 0.95f)], times[frames - 1],
                gcs, frames, mem0 / 1048576f, mem1 / 1048576f));
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = target;
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

        /// <summary>Memory: finds <paramref name="count"/> pairs.</summary>
        static void MatchPairs(IPuzzleMode mode, int count)
        {
            var cards = (ICardMode)mode;
            cards.ResolveMismatch();
            for (int a = 0; a < mode.Layout.CellCount && count > 0; a++)
                if (cards.IsFaceUp(a) && !cards.IsMatched(a))
                {
                    // A card already turned over is completed first.
                    for (int b = 0; b < mode.Layout.CellCount; b++)
                        if (b != a && cards.SymbolOf(b) == cards.SymbolOf(a)) { mode.HandleInput(PuzzleInput.Tap(b)); count--; break; }
                    break;
                }
            for (int a = 0; a < mode.Layout.CellCount && count > 0; a++)
            {
                if (!mode.CanPick(a)) continue;
                for (int b = a + 1; b < mode.Layout.CellCount; b++)
                    if (mode.CanPick(b) && cards.SymbolOf(b) == cards.SymbolOf(a))
                    {
                        mode.HandleInput(PuzzleInput.Tap(a));
                        mode.HandleInput(PuzzleInput.Tap(b));
                        count--;
                        break;
                    }
            }
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
