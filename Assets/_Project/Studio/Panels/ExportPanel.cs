using System.IO;
using System.Linq;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Export;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    public sealed class ExportPanel : StudioPanel
    {
        VisualElement _issues;
        VisualElement _progressFill;
        Label _progressLabel;
        VisualElement _resultBox;

        public ExportPanel(StudioApp app) : base(app) { }
        public override string Id => "export";
        public override string Title => "Export";

        public override void Build(VisualElement content)
        {
            var export = App.Project.File.export;

            // ---- executable
            var exeResult = Fields.Hint("");
            void UpdateExe() => exeResult.text = $"→ {App.Project.ExeName}.exe  +  {App.Project.ExeName}_Data";
            var exe = Fields.Text("Executable name", export.exeName, v => { export.exeName = v; UpdateExe(); App.MarkDirty(false, refreshPreview: false); });
            exe.textEdition.placeholder = App.Project.Pack.game.title.Replace(" ", "");
            UpdateExe();

            var iconThumb = new VisualElement();
            iconThumb.AddToClassList("studio-icon-thumb");
            string iconPath = App.Project.ResolveProjectPath(export.icon);
            if (iconPath == null && Pack.levels.Count > 0) iconPath = Pack.Resolve(Pack.levels[0].image);
            var tex = ThumbnailCache.Get(iconPath, 96);
            if (tex != null) iconThumb.style.backgroundImage = Background.FromTexture2D(tex);
            var iconButtons = new VisualElement();
            iconButtons.Add(Fields.Hint(export.icon == null ? "Automatic: first level picture with rounded corners." : $"Custom: {export.icon}"));
            iconButtons.Add(Fields.Row(
                Fields.Button("Choose PNG…", ChooseIcon, "studio-btn--small"),
                export.icon != null ? Fields.Button("Use automatic", () => { export.icon = null; App.MarkDirty(true, refreshPreview: false); }, "studio-btn--small") : null));
            var iconRow = Fields.Row(iconThumb, iconButtons);
            iconRow.AddToClassList("studio-icon-row");

            content.Add(Fields.Section("Executable", exe, exeResult, iconRow,
                Fields.Hint("The icon is written into the .exe (Explorer, taskbar). A .ico copy is saved next to the export for Steamworks.")));

            // ---- output
            content.Add(Fields.Section("Output",
                Fields.PathRow("Folder", App.Project.ExportRoot, "Browse…", () =>
                {
                    string folder = FileDialogs.PickFolder("Where should exported games go?");
                    if (!string.IsNullOrEmpty(folder)) { export.outputDir = folder; App.MarkDirty(true, refreshPreview: false); }
                }),
                Fields.Toggle("Also create a .zip", export.zip, v => { export.zip = v; App.MarkDirty(false, refreshPreview: false); }),
                Fields.Toggle("Open the folder when done", export.openFolder, v => { export.openFolder = v; App.MarkDirty(false, refreshPreview: false); })));

            // ---- Steam
            long appId = Pack.game.steamAppId;
            content.Add(Fields.Section("Steam",
                Fields.Toggle("Create the Steamworks folder", export.steamFiles, v => { export.steamFiles = v; App.MarkDirty(false, refreshPreview: false); }),
                Fields.Hint($"\"{App.Project.ExeName}_Steamworks\" next to the game: achievement icons and list, store images, screenshots, Rich Presence files, SteamPipe upload script and a README of what to enter in Steamworks."),
                Fields.Toggle("Add steam_appid.txt (test outside Steam)", export.steamAppIdTxt, v => { export.steamAppIdTxt = v; App.MarkDirty(false, refreshPreview: false); }),
                Fields.Hint(appId > 0
                    ? "Lets the exported .exe use Steam (achievements, overlay) when started by double-click. Leave it off for the build you upload."
                    : "Needs a Steam App ID (Steam tab).")));

            // ---- validation
            _issues = new VisualElement();
            content.Add(Fields.Section("Check", Fields.Row(Fields.Button("Validate", ShowIssues, "studio-btn--small")), _issues));
            ShowIssues();

            // ---- export
            var exportBtn = Fields.Button("Export Game", App.StartExport, "studio-btn--success studio-btn--big");
            exportBtn.SetEnabled(!App.IsExporting);
            var track = new VisualElement();
            track.AddToClassList("studio-progress");
            _progressFill = new VisualElement();
            _progressFill.AddToClassList("studio-progress-fill");
            track.Add(_progressFill);
            _progressLabel = Fields.Hint("");
            _resultBox = new VisualElement();
            var section = Fields.Section("Export", exportBtn, track, _progressLabel, _resultBox);
            if (!StudioPaths.TemplateAvailable)
                section.Add(Fields.Hint($"Player Template missing ({StudioPaths.TemplateDir}). Reinstall Puzzle Studio or run Build > Player Template.", "studio-text-error"));
            section.Add(Fields.Hint("The exported folder is the complete game. Upload it with steampipe\\upload.bat from the Steamworks folder (see its README)."));
            content.Add(section);
            UpdateProgress();
        }

        public void UpdateProgress()
        {
            if (_progressFill == null) return;
            var ex = App.Exporter;
            float p = ex?.Progress ?? 0f;
            _progressFill.style.width = Length.Percent(p * 100f);
            _progressLabel.text = ex?.Status ?? "";
            _resultBox.Clear();
            var outcome = ex?.Outcome;
            if (!App.IsExporting && outcome != null && outcome.Success)
            {
                _resultBox.Add(Fields.Hint($"Game exported to {outcome.GameDir}", "studio-text-success"));
                _resultBox.Add(Fields.Row(
                    Fields.Button("Open folder", () => FileDialogs.Reveal(outcome.ExePath), "studio-btn--small"),
                    Fields.Button("Play exported game", () => { try { System.Diagnostics.Process.Start(outcome.ExePath); } catch { } }, "studio-btn--small"),
                    outcome.ZipPath != null ? Fields.Button("Show zip", () => FileDialogs.Reveal(outcome.ZipPath), "studio-btn--small") : null,
                    outcome.SteamworksDir != null ? Fields.Button("Steamworks folder", () => FileDialogs.Reveal(Path.Combine(outcome.SteamworksDir, SteamworksExporter.Readme)), "studio-btn--small") : null));
            }
            else if (!App.IsExporting && outcome != null && !outcome.Success && outcome.Error != null)
                _resultBox.Add(Fields.Hint(outcome.Error, "studio-text-error"));
            StudioFont.Apply(_resultBox);
        }

        void ShowIssues()
        {
            _issues.Clear();
            var report = PackValidator.Validate(Pack);
            int errors = report.Count(IssueSeverity.Error), warnings = report.Count(IssueSeverity.Warning);
            _issues.Add(Fields.Hint(errors == 0
                ? warnings == 0 ? "Ready to export." : $"Ready to export ({warnings} warning{(warnings > 1 ? "s" : "")})."
                : $"{errors} error{(errors > 1 ? "s" : "")} must be fixed before exporting.",
                errors > 0 ? "studio-text-error" : "studio-text-success"));
            foreach (var issue in report.Issues.OrderByDescending(i => i.severity).Take(30))
            {
                string where = issue.levelId != null ? $"[{LevelName(issue.levelId)}] " : "";
                var l = Fields.Hint($"{(issue.severity == IssueSeverity.Error ? "Error" : issue.severity == IssueSeverity.Warning ? "Warning" : "Info")}: {where}{issue.message}");
                l.AddToClassList(issue.severity == IssueSeverity.Error ? "studio-text-error" : issue.severity == IssueSeverity.Warning ? "studio-text-warning" : "studio-text-muted");
                _issues.Add(l);
            }
            StudioFont.Apply(_issues);
        }

        string LevelName(string id)
        {
            int i = Pack.IndexOfLevel(id);
            return i >= 0 ? $"{i + 1}. {Pack.levels[i].name}" : id;
        }

        void ChooseIcon()
        {
            string file = FileDialogs.OpenFile("Choose an icon image (square PNG, 256 × 256 or more)", FileDialogs.PngFilter);
            if (string.IsNullOrEmpty(file)) return;
            string dst = Path.Combine(App.Project.Root, "icon" + Path.GetExtension(file).ToLowerInvariant());
            if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(dst), System.StringComparison.OrdinalIgnoreCase))
                File.Copy(file, dst, true);
            App.Project.File.export.icon = Path.GetFileName(dst);
            App.MarkDirty(true, refreshPreview: false);
        }
    }
}
