using System;
using System.Collections;
using System.Globalization;
using System.IO;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Game.Screens;
using UnityEngine;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>
    /// Developer tool for automated visual checks (external screen capture sees the GPU surface as black):
    ///   Game.exe -pack X -capture out.png [-captureDelay 2] [-debugAction select|hint|solve|partial] [-captureQuit]
    /// Several captures: -capture a.png;b.png with -debugAction none;solve.
    /// Navigation actions: menu, levels, settings, credits, end, pause, play1 (level 1)…
    /// "-screen menu|levels|settings|credits|end|splash" chooses the first screen.
    /// </summary>
    public sealed class DebugCapture : MonoBehaviour
    {
        public static void InstallIfRequested(GameRoot root)
        {
            if (Arg("-capture") != null) root.gameObject.AddComponent<DebugCapture>();
        }

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
                Perform(root, action);
                yield return new WaitForSecondsRealtime(action == "solve" ? 4.2f : 1.0f);
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
