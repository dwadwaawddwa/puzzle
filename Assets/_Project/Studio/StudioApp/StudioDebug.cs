using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PuzzleStudio.Studio.App
{
    /// <summary>
    /// Developer tool for automated visual checks of the Studio:
    ///   PuzzleStudio.exe -openProject "C:\...\X.puzzleproj" -capture a.png;b.png -captureSteps levels;theme
    ///                    [-captureDelay 2] [-captureQuit]
    /// Steps: a panel id (project, levels, gameplay, theme, steam, export), "welcome", "victory", "select2" (level 2),
    /// "storeart" / "screenshots" (Steam tab generators)...
    /// </summary>
    public sealed class StudioDebug : MonoBehaviour
    {
        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static bool HasFlag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static float Fl(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        IEnumerator Start()
        {
            var app = GetComponent<StudioApp>();
            string[] paths = Arg("-capture").Split(';');
            string[] steps = (Arg("-captureSteps") ?? "").Split(';');
            float delay = float.TryParse(Arg("-captureDelay"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 2f;

            yield return new WaitForSecondsRealtime(delay);
            for (int i = 0; i < paths.Length; i++)
            {
                string step = i < steps.Length ? steps[i] : "";
                if (step == "victory") app.Preview.ShowVictory();
                else if (step.StartsWith("select") && int.TryParse(step.Substring(6), out int lvl)) app.SelectLevel(lvl - 1);
                else if (step == "picker") app.DebugOpenFirstColorPicker();
                else if (step.StartsWith("dropdown:"))
                {
                    app.ShowPanel(step.Substring(9));
                    yield return new WaitForSecondsRealtime(0.5f);
                    app.DebugOpenFirstDropdown();
                }
                else if (step.StartsWith("layout:"))
                {
                    var parts = step.Substring(7).Split(':');
                    app.DebugLayout(parts[0], parts.Length > 1 ? parts[1] : null);
                    yield return new WaitForSecondsRealtime(1.5f);
                }
                else if (step.StartsWith("screen:") && System.Enum.TryParse(step.Substring(7), true, out PuzzleStudio.Game.Screens.StartScreen sc))
                    app.Preview.SetScreen(sc);
                else if (step == "storeart" || step == "screenshots")
                {
                    app.ShowPanel("steam");
                    var steam = app.Panel<PuzzleStudio.Studio.Panels.SteamPanel>();
                    yield return step == "storeart" ? steam.GenerateStoreImages() : steam.CaptureScreenshots();
                    Debug.Log($"[StudioCapture] {step} done");
                }
                else if (step.StartsWith("scroll:") && float.TryParse(step.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    app.DebugScrollInspector(y);
                else if (step == "pad") app.DebugGamepadView(true);
                else if (step == "close") app.Modal.Close();
                else if (step.StartsWith("settext:"))
                {
                    // settext:fr:menu.play:Allons-y — what typing in the Texts tab does.
                    var v = step.Split(new[] { ':' }, 4);
                    var pack = app.Project.Pack;
                    if (!pack.texts.TryGetValue(v[1], out var t)) pack.texts[v[1]] = t = new System.Collections.Generic.Dictionary<string, string>();
                    t[v[2]] = v[3];
                    app.ShowPanel("texts");
                    app.MarkDirty(rebuildInspector: true);
                }
                else if (step.StartsWith("importfont:"))
                {
                    var pack = app.Project.Pack;
                    pack.theme.font.heading = ProjectFiles.ImportThemeFile(pack, step.Substring(11), "font_titles");
                    app.ShowPanel("theme");
                    app.MarkDirty(rebuildInspector: true);
                }
                else if (step == "undo") app.Undo();
                else if (step == "redo") app.Redo();
                else if (step == "crop")
                {
                    app.ShowPanel("levels");
                    var level = app.Project.Pack.levels[app.SelectedLevel];
                    Core.Util.ImageHeaderReader.TryReadSize(app.Project.Pack.Resolve(level.image), out int w, out int h);
                    app.Panel<PuzzleStudio.Studio.Panels.LevelsPanel>().OpenCrop(level, w, h);
                }
                else if (step.StartsWith("cropset:"))
                {
                    // cropset:x,y,w,h — what Apply in the crop dialog does.
                    var v = step.Substring(8).Split(',');
                    var level = app.Project.Pack.levels[app.SelectedLevel];
                    level.crop = new Core.Data.CropRect(Fl(v[0]), Fl(v[1]), Fl(v[2]), Fl(v[3]));
                    app.ShowPanel("levels");
                    app.MarkDirty(rebuildInspector: true);
                }
                else if (step.StartsWith("move:"))
                {
                    var v = step.Substring(5).Split(':');
                    app.ShowPanel("levels");
                    app.Panel<PuzzleStudio.Studio.Panels.LevelsPanel>().DebugMove(int.Parse(v[0]) - 1, int.Parse(v[1]) - 1);
                }
                else if (step.StartsWith("rename:"))
                {
                    app.Project.Pack.levels[app.SelectedLevel].name = step.Substring(7);
                    app.MarkDirty(rebuildInspector: true);
                    yield return new WaitForSecondsRealtime(1f);   // past the undo pause: one step
                }
                else if (step == "exportrun")
                {
                    app.StartExport();
                    while (app.IsExporting) yield return null;
                    Debug.Log($"[StudioCapture] export result: {app.Exporter?.Outcome?.Success} {app.Exporter?.Outcome?.ExePath} {app.Exporter?.Outcome?.Error} | {app.Exporter?.Status}");
                }
                else if (step.StartsWith("preset:"))
                {
                    PuzzleStudio.Studio.Themes.ThemePresets.Apply(app.Project.Pack, step.Substring(7));
                    app.ShowPanel("theme");
                    app.MarkDirty(rebuildInspector: true);
                    yield return new WaitForSecondsRealtime(1f);
                }
                else if (step.Length > 0 && step != "welcome") app.ShowPanel(step);
                yield return new WaitForSecondsRealtime(step == "victory" ? 2.5f : 1.2f);

                string path = Path.GetFullPath(paths[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield return null;
                Debug.Log($"[StudioCapture] {step} → {path}");
            }
            if (HasFlag("-captureQuit"))
            {
                yield return new WaitForSecondsRealtime(0.5f);
                app.ForceQuit();
            }
        }
    }
}
