using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Studio.App;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PuzzleStudio.Studio.Export
{
    /// <summary>
    /// Store screenshots taken by the game itself (menu, level list, two puzzles, a victory) at 1920 × 1080:
    /// runs the Player Template with its capture options on a throw-away save, muted, without Steam.
    /// </summary>
    public static class ScreenshotCapture
    {
        public const int Count = 5;
        public const float TimeoutSeconds = 120f;

        public sealed class Result
        {
            public readonly List<string> Files = new List<string>();
            public string Error;
            public Vector2Int Size;
        }

        public static string FileName(int index) => $"screenshot_{index + 1}.png";

        public static IEnumerator Run(StudioProject project, string outDir, Action<string> status, Action<Result> done)
        {
            var result = new Result();
            if (!StudioPaths.TemplateAvailable) { result.Error = $"Player Template not found in {StudioPaths.TemplateDir}."; done(result); yield break; }
            int n = project.Pack.levels.Count;
            if (n == 0) { result.Error = "Add some levels first."; done(result); yield break; }

            Directory.CreateDirectory(outDir);
            var paths = new List<string>();
            for (int i = 0; i < Count; i++)
            {
                string path = Path.Combine(outDir, FileName(i));
                if (File.Exists(path)) File.Delete(path);
                paths.Add(path);
            }

            int second = Math.Min(2, n), third = Math.Min(3, n);
            string actions = $"menu;levels;play1;play{second}+partial;play{third}+solve";
            string args = $"-pack \"{project.PackDir}\" -screen menu -capture \"{string.Join(";", paths)}\" -debugAction {actions} " +
                          "-captureDelay 2.5 -captureQuit -tempSave -demoProgress -mute -noSteam " +
                          "-screen-fullscreen 0 -popupwindow -screen-width 1920 -screen-height 1080";

            Process process;
            try
            {
                process = Process.Start(new ProcessStartInfo(StudioPaths.TemplateExePath, args)
                {
                    UseShellExecute = false,
                    WorkingDirectory = StudioPaths.TemplateDir,
                });
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                done(result);
                yield break;
            }

            float t = 0f;
            while (process != null && !process.HasExited && t < TimeoutSeconds)
            {
                t += Time.unscaledDeltaTime;
                status?.Invoke($"Capturing screenshots… {Mathf.FloorToInt(t)} s (the game opens for about 25 s)");
                yield return null;
            }
            if (process != null && !process.HasExited)
            {
                try { process.Kill(); } catch (Exception) { }
                result.Error = "The game did not finish in time.";
            }
            // The last file can land a moment after the process exits.
            yield return new WaitForSecondsRealtime(0.3f);

            foreach (var path in paths)
                if (File.Exists(path)) result.Files.Add(path);
            if (result.Files.Count > 0 && ImageHeaderReader.TryReadSize(result.Files[0], out int w, out int h))
                result.Size = new Vector2Int(w, h);
            if (result.Files.Count < Count && result.Error == null)
                result.Error = $"Only {result.Files.Count} of {Count} screenshots were captured.";
            Debug.Log($"[Studio] Screenshots: {result.Files.Count} files, {result.Size.x}x{result.Size.y} {result.Error}");
            done(result);
        }
    }
}
