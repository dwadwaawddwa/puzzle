using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Steam;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Export;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    /// <summary>Steam tab: App/depot IDs, Steam features, achievements editor, store images and screenshots.</summary>
    public sealed class SteamPanel : StudioPanel
    {
        static readonly List<string> AchievementModes = new List<string> { "Automatic", "Custom", "Off" };

        bool _busy;
        string _artStatus = "";

        public SteamPanel(StudioApp app) : base(app) { }
        public override string Id => "steam";
        public override string Title => "Steam";

        public override void Build(VisualElement content)
        {
            var g = Pack.game;
            var s = Pack.steam;

            // ---- app
            content.Add(Fields.Section("Steam app",
                Fields.Text("App ID", g.steamAppId.ToString(), v =>
                {
                    if (long.TryParse(v.Trim(), out long id) && id >= 0) { g.steamAppId = id; Saved(); }
                }),
                Fields.Hint("Leave 0 until Valve gives you an App ID (Steamworks → Create a new app). With 0 the game simply runs without Steam."),
                Fields.Text("Depot ID", s.depotId.ToString(), v =>
                {
                    if (long.TryParse(v.Trim(), out long id) && id >= 0) { s.depotId = id; Saved(); }
                }),
                Fields.Hint(g.steamAppId > 0 ? $"0 = App ID + 1 ({g.steamAppId + 1}), the depot Steamworks creates first." : "0 = App ID + 1, the depot Steamworks creates first."),
                Fields.Toggle("Relaunch via Steam", s.restartThroughSteam, v => { s.restartThroughSteam = v; Saved(); }),
                Fields.Hint("A player who double-clicks the .exe outside Steam gets the game restarted by Steam (overlay, achievements). Play Test is never relaunched.")));

            // ---- features
            var features = Fields.Section("Features",
                Fields.Toggle("Rich Presence", s.richPresence, v => { s.richPresence = v; Saved(); }),
                Fields.Hint("Friends see \"Solving puzzle 3 of 12\" in their Steam list (upload the files of the Steamworks folder)."),
                Fields.Toggle("Steam Cloud", s.cloudEnabled, v => { s.cloudEnabled = v; Saved(rebuild: true); }));
            if (s.cloudEnabled)
                features.Add(Fields.Hint($"Steamworks → Cloud → Auto-Cloud: Root WinAppDataLocalLow, Subdirectory {SteamworksFiles.CloudSubdirectory(Pack)}, Pattern *.json. Also written in the README of the Steamworks folder."));
            content.Add(features);

            BuildAchievements(content);
            BuildStoreArt(content);
        }

        // ------------------------------------------------------------------ achievements

        void BuildAchievements(VisualElement content)
        {
            var s = Pack.steam;
            int mode = !s.achievementsEnabled ? 2 : AchievementGenerator.IsCustom(Pack) ? 1 : 0;
            var section = Fields.Section("Achievements",
                Fields.Dropdown("Achievements", AchievementModes, mode, SetMode),
                Fields.Hint(mode == 0 ? "Generated from your levels. Choose Custom to edit them."
                    : mode == 1 ? "API names must match the achievements created in Steamworks (letters, digits, _)."
                    : "No achievements in this game."));
            if (mode != 2)
                section.Add(Fields.Toggle("In the game menu", s.showAchievementsInGame, v => { s.showAchievementsInGame = v; Changed(); }));

            if (mode == 0)
                foreach (var a in AchievementGenerator.Auto(Pack)) section.Add(ReadOnlyCard(a));
            else if (mode == 1)
            {
                for (int i = 0; i < s.achievements.Count; i++) section.Add(EditCard(i));
                section.Add(Fields.Row(Fields.Button("+ Add achievement", AddAchievement, "studio-btn--small")));
            }
            if (mode != 2)
                section.Add(Fields.Hint($"{AchievementGenerator.Effective(Pack).Count} achievements. Their 256 × 256 icons are generated at export (Steamworks folder)."));
            content.Add(section);
        }

        void SetMode(int mode)
        {
            var s = Pack.steam;
            switch (mode)
            {
                case 0:
                    s.achievementsEnabled = true;
                    s.achievements.Clear();
                    break;
                case 1:
                    s.achievementsEnabled = true;
                    if (s.achievements.Count == 0)
                    {
                        s.achievements.AddRange(AchievementGenerator.Auto(Pack).Select(a => a.Clone()));
                        if (s.achievements.Count == 0) s.achievements.Add(NewAchievement());
                    }
                    break;
                case 2:
                    s.achievementsEnabled = false;
                    break;
                default: return;
            }
            Changed(rebuildInspector: true);
        }

        VisualElement ReadOnlyCard(AchievementDef a)
        {
            var card = new VisualElement();
            card.AddToClassList("studio-ach");
            var name = new Label(a.name);
            name.AddToClassList("studio-ach-name");
            card.Add(name);
            card.Add(Fields.Hint(a.description));
            card.Add(Fields.Hint($"{a.id} · {RuleText(a)}", "studio-text-muted"));
            return card;
        }

        VisualElement EditCard(int index)
        {
            var list = Pack.steam.achievements;
            var a = list[index];
            var card = new VisualElement();
            card.AddToClassList("studio-ach");

            var idField = Fields.Text("API name", a.id, v => { a.id = v.Trim(); Saved(); });
            card.Add(idField);
            if (!AchievementGenerator.IsValidId(a.id))
                card.Add(Fields.Hint("Letters, digits and _ only (e.g. ACH_FIRST_PUZZLE).", "studio-text-error"));
            card.Add(Fields.Text("Name", a.name, v => { a.name = v; Saved(); }));
            card.Add(Fields.Text("Description", a.description, v => { a.description = v; Saved(); }));
            card.Add(Fields.Enum("Unlocked when", a.rule, v =>
            {
                a.rule = v;
                a.value = DefaultValue(v);
                Saved(rebuild: true);
            }));
            string valueLabel = ValueLabel(a.rule);
            if (valueLabel != null)
                card.Add(Fields.Text(valueLabel, a.value.ToString(CultureInfo.InvariantCulture), v =>
                {
                    if (float.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) { a.value = f; Saved(); }
                }));
            card.Add(Fields.Row(
                Fields.Toggle("Hidden", a.hidden, v => { a.hidden = v; Saved(); }),
                Fields.Spacer(),
                Fields.Button("Remove", () =>
                {
                    list.RemoveAt(index);
                    if (list.Count == 0) Pack.steam.achievementsEnabled = false;
                    Changed(rebuildInspector: true);
                }, "studio-btn--small studio-btn--danger")));
            return card;
        }

        void AddAchievement()
        {
            Pack.steam.achievements.Add(NewAchievement());
            Changed(rebuildInspector: true);
        }

        AchievementDef NewAchievement()
        {
            var ids = new HashSet<string>(Pack.steam.achievements.Select(x => x.id));
            int n = Pack.steam.achievements.Count + 1;
            while (ids.Contains($"ACH_CUSTOM_{n}")) n++;
            return new AchievementDef
            {
                id = $"ACH_CUSTOM_{n}",
                name = "New Achievement",
                description = "Complete 3 puzzles.",
                rule = AchievementRule.LevelsCompleted,
                value = Mathf.Min(3, Mathf.Max(1, Pack.levels.Count)),
            };
        }

        static string ValueLabel(AchievementRule rule)
        {
            switch (rule)
            {
                case AchievementRule.LevelsCompleted: return "Levels";
                case AchievementRule.PercentCompleted: return "Percent";
                case AchievementRule.FastLevel: return "Seconds";
                default: return null;
            }
        }

        static float DefaultValue(AchievementRule rule)
        {
            switch (rule)
            {
                case AchievementRule.PercentCompleted: return 100;
                case AchievementRule.FastLevel: return 60;
                default: return 1;
            }
        }

        static string RuleText(AchievementDef a)
        {
            string v = a.value.ToString(CultureInfo.InvariantCulture);
            switch (a.rule)
            {
                case AchievementRule.LevelsCompleted: return $"{v} level(s) completed";
                case AchievementRule.PercentCompleted: return $"{v} % of the levels completed";
                case AchievementRule.AllThreeStars: return "3 stars everywhere";
                case AchievementRule.NoPreviewLevel: return "a level without the preview";
                case AchievementRule.NoHintLevel: return "a level without hints";
                case AchievementRule.FastLevel: return $"a level in {v} s or less";
                case AchievementRule.PerfectLevel: return "a level in par moves";
                default: return a.rule.ToString();
            }
        }

        // ------------------------------------------------------------------ store images / screenshots

        void BuildStoreArt(VisualElement content)
        {
            var project = App.Project;
            var levelNames = Pack.levels.Select((l, i) => $"{i + 1}. {(string.IsNullOrWhiteSpace(l.name) ? l.id : l.name)}").ToList();
            var section = Fields.Section("Store page");
            if (levelNames.Count > 0)
                section.Add(Fields.Dropdown("Cover picture", levelNames, project.File.steamCoverLevel, i =>
                {
                    if (i >= 0) { project.File.steamCoverLevel = i; App.MarkDirty(false, refreshPreview: false); }
                }));

            var generate = Fields.Button("Generate store images", () => App.StartCoroutine(GenerateStoreImages()), "studio-btn--small");
            var shots = Fields.Button("Capture screenshots", () => App.StartCoroutine(CaptureScreenshots()), "studio-btn--small");
            generate.SetEnabled(!_busy && Pack.levels.Count > 0);
            shots.SetEnabled(!_busy && Pack.levels.Count > 0 && StudioPaths.TemplateAvailable);
            section.Add(Fields.Row(generate, shots, Fields.Button("Open folder", OpenSteamFolder, "studio-btn--small")));
            if (!string.IsNullOrEmpty(_artStatus)) section.Add(Fields.Hint(_artStatus));

            var grid = new VisualElement();
            grid.AddToClassList("studio-art-grid");
            string storeDir = SteamworksExporter.ProjectStoreDir(project);
            foreach (var spec in SteamArt.StoreImages)
                AddThumb(grid, Path.Combine(storeDir, spec.File), $"{spec.Name}\n{spec.Width} × {spec.Height}");
            string shotsDir = SteamworksExporter.ProjectScreenshotsDir(project);
            for (int i = 0; i < ScreenshotCapture.Count; i++)
                AddThumb(grid, Path.Combine(shotsDir, ScreenshotCapture.FileName(i)), $"Screenshot {i + 1}");
            section.Add(grid);
            section.Add(Fields.Hint("Generated from your cover picture, title and theme. Replace any file in the folder with your own art: the export uses what is there. Screenshots: the game opens for about 25 s at 1920 × 1080."));
            content.Add(section);
        }

        static void AddThumb(VisualElement grid, string path, string caption)
        {
            var item = new VisualElement();
            item.AddToClassList("studio-art-item");
            var thumb = new VisualElement();
            thumb.AddToClassList("studio-art-thumb");
            var tex = File.Exists(path) ? ThumbnailCache.Get(path, 192) : null;
            if (tex != null) thumb.style.backgroundImage = Background.FromTexture2D(tex);
            else thumb.AddToClassList("studio-art-thumb--empty");
            item.Add(thumb);
            item.Add(Fields.Hint(caption, "studio-art-caption"));
            grid.Add(item);
        }

        public IEnumerator GenerateStoreImages()
        {
            if (_busy) yield break;
            _busy = true;
            var renderer = new UiImageRenderer(App.transform);
            try
            {
                yield return SteamArt.RenderStoreImages(Pack, App.Project.File.steamCoverLevel, SteamworksExporter.ProjectStoreDir(App.Project), renderer, SetStatus);
                SetStatus($"{SteamArt.StoreImages.Length} store images saved in the project's steam\\store folder.");
            }
            finally
            {
                renderer.Dispose();
                _busy = false;
                App.RebuildInspector();
            }
        }

        public IEnumerator CaptureScreenshots()
        {
            if (_busy) yield break;
            _busy = true;
            App.Save();
            ScreenshotCapture.Result result = null;
            try
            {
                yield return ScreenshotCapture.Run(App.Project, SteamworksExporter.ProjectScreenshotsDir(App.Project), SetStatus, r => result = r);
                if (result.Error != null) SetStatus(result.Error);
                else if (result.Size.x < 1920) SetStatus($"{result.Files.Count} screenshots saved, but at {result.Size.x} × {result.Size.y}: your screen is smaller than 1920 × 1080 (Steam accepts 1280 × 720 and more).");
                else SetStatus($"{result.Files.Count} screenshots saved ({result.Size.x} × {result.Size.y}).");
            }
            finally
            {
                _busy = false;
                App.RebuildInspector();
            }
        }

        void SetStatus(string text)
        {
            _artStatus = text;
            App.Toast(text);
        }

        void OpenSteamFolder()
        {
            string dir = SteamworksExporter.ProjectSteamDir(App.Project);
            Directory.CreateDirectory(dir);
            FileDialogs.Reveal(dir);
        }

        /// <summary>Steam settings don't change the preview's look: no preview rebuild.</summary>
        void Saved(bool rebuild = false) => App.MarkDirty(rebuild, refreshPreview: false);
    }
}
