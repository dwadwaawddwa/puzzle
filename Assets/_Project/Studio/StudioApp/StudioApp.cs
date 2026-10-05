using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.Export;
using PuzzleStudio.Studio.Panels;
using PuzzleStudio.Studio.Preview;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.App
{
    /// <summary>
    /// The Studio application: window layout (top bar, tabs, live preview, inspector, status bar),
    /// project lifecycle (new/open/save/recent), shortcuts, drag &amp; drop, play test and export.
    /// The only object in Studio.unity.
    /// </summary>
    public sealed class StudioApp : MonoBehaviour
    {
        public const string Version = "Studio v1.1";

        public StudioProject Project { get; private set; }
        public StudioSettings Settings { get; private set; }
        public Modal Modal { get; private set; }
        public VisualElement PopupLayer { get; private set; }
        public Label ContrastHint { get; private set; }
        public int SelectedLevel { get; private set; }
        public bool Dirty { get; private set; }
        public GameExporter Exporter { get; private set; }
        public bool IsExporting { get; private set; }

        UIDocument _document;
        VisualElement _root, _body, _welcome, _inspectorContent;
        ScrollView _scroll;
        Label _inspectorHeader, _projectLabel, _status;
        Button _saveBtn, _testBtn, _exportBtn;
        DropdownField _levelDropdown, _resolutionDropdown;
        LivePreview _preview;
        readonly List<StudioPanel> _panels = new List<StudioPanel>();
        readonly Dictionary<string, Button> _nav = new Dictionary<string, Button>();
        StudioPanel _current;
        float _statusUntil;
        bool _quitConfirmed;

        // ------------------------------------------------------------------ startup

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            CreateCamera();
            _document = CreateDocument();
            Settings = StudioSettings.Load();
            BuildUi();
            Application.wantsToQuit += OnWantsToQuit;
        }

        void Start()
        {
            DragDropHook.Install();
            string forced = StudioDebug.Arg("-openProject");
            string last = forced ?? Settings.lastProject;
            if (!string.IsNullOrEmpty(last) && StudioProject.ResolveRoot(last) != null) OpenProject(last);
            else ShowWelcome();
            if (StudioDebug.Arg("-capture") != null) gameObject.AddComponent<StudioDebug>();
        }

        public LivePreview Preview => _preview;

        public T Panel<T>() where T : StudioPanel => _panels.OfType<T>().FirstOrDefault();

        /// <summary>Opens the same popup menu a DropdownField shows (visual check of the dropdown style).</summary>
        public void DebugOpenFirstDropdown()
        {
            var field = _inspectorContent.Q<DropdownField>();
            if (field == null) return;
            var menu = new GenericDropdownMenu();
            foreach (var choice in field.choices)
            {
                string c = choice;
                menu.AddItem(c, c == field.value, () => field.value = c);
            }
            menu.DropDown(field.worldBound, field, true);
        }

        public void DebugScrollInspector(float y) => _scroll.scrollOffset = new Vector2(0, y);

        public void DebugLayout(string screen, string select)
        {
            ShowPanel("layout");
            (_current as LayoutPanel)?.DebugSet(screen, string.IsNullOrEmpty(select) ? null : select);
        }

        /// <summary>Small preview of a pack-relative picture (null if none).</summary>
        public Texture2D ThumbnailFor(string relative) =>
            string.IsNullOrEmpty(relative) || Project == null ? null : ThumbnailCache.Get(Project.Pack.Resolve(relative), 96);

        /// <summary>Quits without the unsaved-changes prompt (debug captures).</summary>
        public void ForceQuit()
        {
            _quitConfirmed = true;
            Application.Quit();
        }

        public void DebugOpenFirstColorPicker()
        {
            ShowPanel("theme");
            _inspectorContent.schedule.Execute(() =>
            {
                _inspectorContent.Q<ColorField>()?.OpenPicker();
            }).ExecuteLater(300);
        }

        void OnDestroy()
        {
            Application.wantsToQuit -= OnWantsToQuit;
            DragDropHook.Uninstall();
        }

        void CreateCamera()
        {
            // Clears the window behind the UI; the game preview has its own camera rendering to a texture.
            var cam = new GameObject("Studio Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(21, 22, 26, 255);
            cam.cullingMask = 0;
            cam.depth = -10;
            cam.gameObject.AddComponent<AudioListener>();
        }

        UIDocument CreateDocument()
        {
            var go = new GameObject("Studio UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/StudioPanelSettings");
            go.SetActive(true);
            return doc;
        }

        // ------------------------------------------------------------------ layout

        void BuildUi()
        {
            var rootVe = _document.rootVisualElement;
            _root = new VisualElement();
            _root.AddToClassList("studio");
            rootVe.Add(_root);

            // Top bar
            var top = Box("studio-topbar");
            var logo = new Label("Puzzle Studio");
            logo.AddToClassList("studio-logo");
            top.Add(logo);
            _projectLabel = new Label("");
            _projectLabel.AddToClassList("studio-project-name");
            top.Add(_projectLabel);
            top.Add(Fields.Spacer());
            top.Add(Tip(Fields.Button("New", () => TryLeave(NewProjectDialog)), "New project (Ctrl+N)"));
            top.Add(Tip(Fields.Button("Open", () => TryLeave(OpenProjectDialog)), "Open a project (Ctrl+O)"));
            top.Add(Tip(Fields.Button("Recent", ShowRecent), "Recent projects"));
            _saveBtn = Tip(Fields.Button("Save", Save), "Save (Ctrl+S)");
            top.Add(_saveBtn);
            top.Add(Box("studio-topbar-sep"));
            _testBtn = Tip(Fields.Button("Play Test", PlayTestNow, "studio-btn--primary"), "Play the game in its own window (F5)");
            top.Add(_testBtn);
            _exportBtn = Tip(Fields.Button("Export Game", () => ShowPanel("export"), "studio-btn--success"), "Build the final game folder for Steam");
            top.Add(_exportBtn);
            _root.Add(top);

            // Body: nav | center | inspector
            _body = Box("studio-body");
            var nav = Box("studio-nav");
            _panels.Add(new ProjectPanel(this));
            _panels.Add(new LevelsPanel(this));
            _panels.Add(new GameplayPanel(this));
            _panels.Add(new ThemePanel(this));
            _panels.Add(new LayoutPanel(this));
            _panels.Add(new SteamPanel(this));
            _panels.Add(new ExportPanel(this));
            foreach (var p in _panels)
            {
                var panel = p;
                var b = Fields.Button(panel.Title, () => ShowPanel(panel.Id), "studio-nav-item");
                b.RemoveFromClassList("studio-btn");
                _nav[panel.Id] = b;
                nav.Add(b);
            }
            nav.Add(Fields.Spacer());
            var soon = new Label("Coming next:\nAudio · Texts");
            soon.AddToClassList("studio-nav-soon");
            nav.Add(soon);
            _body.Add(nav);

            var center = Box("studio-center");
            center.Add(BuildPreviewToolbar());
            _preview = new LivePreview(transform);
            center.Add(_preview.Area);
            _body.Add(center);

            var inspector = Box("studio-inspector");
            _inspectorHeader = new Label("");
            _inspectorHeader.AddToClassList("studio-inspector-header");
            inspector.Add(_inspectorHeader);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("studio-inspector-scroll");
            _inspectorContent = Box("studio-inspector-content");
            _scroll.Add(_inspectorContent);
            inspector.Add(_scroll);
            _body.Add(inspector);
            _root.Add(_body);

            _welcome = Box("studio-welcome");
            _welcome.style.display = DisplayStyle.None;
            _root.Add(_welcome);

            // Status bar
            var status = Box("studio-statusbar");
            _status = new Label("");
            _status.AddToClassList("studio-status");
            status.Add(_status);
            var version = new Label(Version);
            version.AddToClassList("studio-version");
            status.Add(version);
            _root.Add(status);

            ContrastHint = Fields.Hint("");

            PopupLayer = Box("studio-overlay");
            PopupLayer.pickingMode = PickingMode.Ignore;
            _root.Add(PopupLayer);
            var modalLayer = new VisualElement();
            _root.Add(modalLayer);
            Modal = new Modal(modalLayer);

            StudioFont.Apply(_root);
            RefreshTitle();
        }

        VisualElement BuildPreviewToolbar()
        {
            var bar = Box("studio-preview-toolbar");
            var title = new Label("LIVE PREVIEW");
            title.AddToClassList("studio-preview-title");
            bar.Add(title);
            _levelDropdown = new DropdownField(new List<string> { "—" }, 0);
            _levelDropdown.AddToClassList("studio-field");
            _levelDropdown.AddToClassList("studio-toolbar-dropdown");
            _levelDropdown.RegisterValueChangedCallback(e =>
            {
                int i = _levelDropdown.choices.IndexOf(e.newValue);
                if (i >= 0 && Project != null && i != SelectedLevel) SelectLevel(i);
            });
            bar.Add(_levelDropdown);
            var resNames = LivePreview.Resolutions.Select(r => r.label).ToList();
            _resolutionDropdown = new DropdownField(resNames, 0);
            _resolutionDropdown.AddToClassList("studio-field");
            _resolutionDropdown.AddToClassList("studio-toolbar-dropdown");
            _resolutionDropdown.RegisterValueChangedCallback(e =>
            {
                int i = resNames.IndexOf(e.newValue);
                _preview.SetResolution(i);
                if (Project != null) Project.File.previewResolution = LivePreview.Resolutions[Math.Max(0, i)].label;
            });
            bar.Add(_resolutionDropdown);
            var screenNames = LivePreview.Screens.Select(sc => sc.label).ToList();
            var screenDropdown = new DropdownField(screenNames, 0);
            screenDropdown.AddToClassList("studio-field");
            screenDropdown.AddToClassList("studio-toolbar-dropdown");
            screenDropdown.AddToClassList("studio-toolbar-dropdown--small");
            screenDropdown.tooltip = "Which game screen to preview";
            screenDropdown.RegisterValueChangedCallback(e =>
            {
                int i = screenNames.IndexOf(e.newValue);
                if (i >= 0) _preview.SetScreen(LivePreview.Screens[i].screen);
            });
            bar.Add(screenDropdown);
            bar.Add(Fields.Spacer());
            Button sound = null;
            sound = Tip(Fields.Button("Sound: off", () =>
            {
                _preview.Muted = !_preview.Muted;
                sound.text = _preview.Muted ? "Sound: off" : "Sound: on";
            }, "studio-btn--small"), "Play the game music and sounds in the preview");
            bar.Add(sound);
            bar.Add(Tip(Fields.Button("Reshuffle", () => _preview.Restart(), "studio-btn--small"), "Restart the preview (new shuffle)"));
            return bar;
        }

        // ------------------------------------------------------------------ panels / inspector

        public void ShowPanel(string id)
        {
            if (Project == null) return;
            PopupLayer.Clear();
            var previous = _current;
            _current = _panels.FirstOrDefault(p => p.Id == id) ?? _panels[0];
            if (previous != _current)
            {
                previous?.OnDeactivated();
                _current.OnActivated();
            }
            foreach (var kv in _nav) kv.Value.EnableInClassList("studio-nav-item--active", kv.Key == _current.Id);
            _scroll.scrollOffset = Vector2.zero;
            RebuildInspector();
        }

        public void RebuildInspector()
        {
            if (_current == null || Project == null) return;
            var offset = _scroll.scrollOffset;
            _inspectorContent.Clear();
            _inspectorHeader.text = _current.Title;
            _current.Build(_inspectorContent);
            StudioFont.Apply(_inspectorContent);
            _scroll.schedule.Execute(() => _scroll.scrollOffset = offset);
        }

        /// <summary>Called by panels after every edit.</summary>
        public void MarkDirty(bool rebuildInspector, bool refreshPreview = true)
        {
            Dirty = true;
            RefreshTitle();
            if (rebuildInspector) RebuildInspector();
            RefreshLevelDropdown();
            if (refreshPreview) _preview.Show(Project.Pack, SelectedLevel);
        }

        public void SelectLevel(int index, bool rebuild = true)
        {
            if (Project == null) return;
            SelectedLevel = Mathf.Clamp(index, 0, Math.Max(0, Project.Pack.levels.Count - 1));
            Project.File.previewLevel = SelectedLevel;
            RefreshLevelDropdown();
            _preview.Show(Project.Pack, SelectedLevel, immediate: true);
            if (rebuild && _current is LevelsPanel) RebuildInspector();
        }

        /// <summary>Automatic grid (cols, rows) of the selected level, ignoring its own override.</summary>
        public Vector2Int CurrentGrid()
        {
            var pack = Project?.Pack;
            if (pack == null || pack.levels.Count == 0) return new Vector2Int(4, 4);
            var level = pack.levels[SelectedLevel];
            ImageHeaderReader.TryReadSize(pack.Resolve(level.image), out int w, out int h);
            var saved = level.grid;
            level.grid = null;
            var setup = GridResolver.Resolve(pack, SelectedLevel, Math.Max(1, w), Math.Max(1, h));
            level.grid = saved;
            return new Vector2Int(setup.Layout.Cols, setup.Layout.Rows);
        }

        public void RefreshTitle()
        {
            if (Project == null) { _projectLabel.text = ""; WindowTitle.Set("Puzzle Studio"); return; }
            _projectLabel.text = $"{Project.Pack.game.title}{(Dirty ? "  •  unsaved" : "")}";
            WindowTitle.Set($"Puzzle Studio - {Project.Name}{(Dirty ? " *" : "")}");
        }

        public void RefreshContrast()
        {
            if (Project == null) return;
            string text = ThemePanel.ContrastSummary(Project.Pack);
            ContrastHint.text = text;
            bool ok = text.StartsWith("Colors are readable");
            ContrastHint.EnableInClassList("studio-text-success", ok);
            ContrastHint.EnableInClassList("studio-text-warning", !ok);
        }

        void RefreshLevelDropdown()
        {
            if (Project == null) return;
            var levels = Project.Pack.levels;
            var choices = levels.Count == 0
                ? new List<string> { "No levels" }
                : levels.Select((l, i) => $"{i + 1}. {(string.IsNullOrWhiteSpace(l.name) ? l.id : l.name)}").ToList();
            _levelDropdown.choices = choices;
            _levelDropdown.SetValueWithoutNotify(choices[Mathf.Clamp(SelectedLevel, 0, choices.Count - 1)]);
        }

        // ------------------------------------------------------------------ projects

        void ShowWelcome()
        {
            _body.style.display = DisplayStyle.None;
            _welcome.style.display = DisplayStyle.Flex;
            _welcome.Clear();
            _saveBtn.SetEnabled(false);
            _testBtn.SetEnabled(false);
            _exportBtn.SetEnabled(false);

            var card = Box("studio-welcome-card");
            var title = new Label("Puzzle Studio");
            title.AddToClassList("studio-welcome-title");
            card.Add(title);
            card.Add(Fields.Hint("Create picture puzzle games for Steam: add your images, choose a style, test, export.", "studio-welcome-sub"));
            card.Add(Fields.Row(
                Fields.Button("New project", NewProjectDialog, "studio-btn--primary studio-btn--big"),
                Fields.Button("Open project…", OpenProjectDialog, "studio-btn--big")));

            if (Directory.Exists(StudioPaths.SamplesDir))
            {
                var samples = Directory.GetDirectories(StudioPaths.SamplesDir)
                    .Where(d => File.Exists(Path.Combine(d, PackPaths.GameJson))).ToList();
                if (samples.Count > 0)
                {
                    card.Add(Section("START FROM A SAMPLE"));
                    var row = Fields.Row();
                    foreach (var dir in samples)
                    {
                        var d = dir;
                        row.Add(Fields.Button(Fields.Nicify(Path.GetFileName(d)), () => CreateFromSample(d), "studio-btn--small"));
                    }
                    card.Add(row);
                }
            }

            var recent = Settings.ExistingRecent();
            if (recent.Count > 0)
            {
                card.Add(Section("RECENT PROJECTS"));
                foreach (var path in recent.Take(6))
                {
                    var p = path;
                    var item = Fields.Button($"{Path.GetFileNameWithoutExtension(p)}    {Path.GetDirectoryName(p)}", () => OpenProject(p), "studio-recent-item");
                    card.Add(item);
                }
            }
            _welcome.Add(card);
            StudioFont.Apply(_welcome);
            RefreshTitle();
        }

        static Label Section(string text)
        {
            var l = new Label(text);
            l.AddToClassList("studio-section-title");
            l.AddToClassList("studio-welcome-section");
            return l;
        }

        void NewProjectDialog()
        {
            string location = StudioProject.DefaultProjectsRoot;
            var name = Fields.Text("Game title", "My Puzzle Game", null);
            var locLabel = new Label(location);
            locLabel.AddToClassList("studio-path");
            var body = new VisualElement();
            body.Add(name);
            body.Add(Fields.Row(new Label("Folder") { name = "lbl" }, locLabel, Fields.Button("Browse…", () =>
            {
                string f = FileDialogs.PickFolder("Where should the project be created?");
                if (!string.IsNullOrEmpty(f)) { location = f; locLabel.text = f; }
            }, "studio-btn--small")));
            body.Add(Fields.Hint("A folder \"<title>.puzzleproj\" is created there. Your images will be copied into it."));
            var error = Fields.Hint("", "studio-text-error");
            body.Add(error);

            Modal.Show("New project", body,
                new Modal.ButtonSpec("Cancel", null),
                new Modal.ButtonSpec("Create", null, "studio-btn--primary", () =>
                {
                    try
                    {
                        Directory.CreateDirectory(location);
                        var project = StudioProject.Create(location, name.value);
                        OpenProject(project.Root);
                        ShowPanel("levels");
                        return true;
                    }
                    catch (Exception e)
                    {
                        error.text = e.Message;
                        return false;
                    }
                }));
            name.schedule.Execute(() => name.Focus());
        }

        void OpenProjectDialog()
        {
            string file = FileDialogs.OpenFile("Open a project (project.json) or a game pack (game.json)",
                "Puzzle projects and packs\0project.json;game.json\0All files\0*.*\0\0", StudioProject.DefaultProjectsRoot);
            if (string.IsNullOrEmpty(file)) return;
            if (StudioProject.ResolveRoot(file) != null) { OpenProject(file); return; }
            if (Path.GetFileName(file).Equals(PackPaths.GameJson, StringComparison.OrdinalIgnoreCase))
            {
                string packDir = Path.GetDirectoryName(file);
                Modal.Confirm("Import game pack", $"This is a game pack, not a project. Create a new project from it in\n{StudioProject.DefaultProjectsRoot}?",
                    "Create project", () => CreateFromSample(packDir));
                return;
            }
            Modal.Message("Cannot open", "Choose the project.json of a Puzzle Studio project.");
        }

        void CreateFromSample(string packDir)
        {
            try
            {
                Directory.CreateDirectory(StudioProject.DefaultProjectsRoot);
                var p = StudioProject.CreateFromPack(packDir, StudioProject.DefaultProjectsRoot);
                OpenProject(p.Root);
                Toast($"Project created in {p.Root}");
            }
            catch (Exception e) { Modal.Message("Could not create the project", e.Message); }
        }

        public void OpenProject(string path)
        {
            StudioProject project;
            try { project = StudioProject.Open(path); }
            catch (Exception e)
            {
                Modal.Message("Could not open the project", e.Message);
                return;
            }

            Project = project;
            Dirty = false;
            if (StudioDebug.Arg("-capture") == null) // automated captures never touch the user's recent list
            {
                Settings.AddRecent(project.Root);
                Settings.Save();
            }

            _welcome.style.display = DisplayStyle.None;
            _body.style.display = DisplayStyle.Flex;
            _saveBtn.SetEnabled(true);
            _testBtn.SetEnabled(true);
            _exportBtn.SetEnabled(true);

            int res = Array.FindIndex(LivePreview.Resolutions, r => r.label == project.File.previewResolution);
            _resolutionDropdown.index = Math.Max(0, res);
            _preview.SetResolution(Math.Max(0, res));
            SelectedLevel = Mathf.Clamp(project.File.previewLevel, 0, Math.Max(0, project.Pack.levels.Count - 1));
            RefreshLevelDropdown();
            ShowPanel(project.Pack.levels.Count == 0 ? "levels" : _current?.Id ?? "project");
            _preview.Show(project.Pack, SelectedLevel, immediate: true);
            RefreshTitle();
            Toast($"Opened {project.Name}");
        }

        public void Save()
        {
            if (Project == null) return;
            try
            {
                Project.Save();
                Dirty = false;
                RefreshTitle();
                Toast("Saved");
            }
            catch (Exception e) { Modal.Message("Could not save", e.Message); }
        }

        void ShowRecent()
        {
            var recent = Settings.ExistingRecent();
            var body = new VisualElement();
            if (recent.Count == 0) body.Add(Fields.Hint("No recent projects."));
            foreach (var path in recent)
            {
                var p = path;
                body.Add(Fields.Button($"{Path.GetFileNameWithoutExtension(p)}    {Path.GetDirectoryName(p)}", () =>
                {
                    Modal.Close();
                    TryLeave(() => OpenProject(p));
                }, "studio-recent-item"));
            }
            Modal.Show("Recent projects", body, new Modal.ButtonSpec("Close", null));
        }

        /// <summary>Runs <paramref name="next"/> after asking to save unsaved changes.</summary>
        void TryLeave(Action next)
        {
            if (!Dirty || Project == null) { next(); return; }
            Modal.Show("Unsaved changes", Fields.Hint($"Save the changes to \"{Project.Name}\" first?", "studio-modal-text"),
                new Modal.ButtonSpec("Cancel", null),
                new Modal.ButtonSpec("Don't save", next, "studio-btn--danger"),
                new Modal.ButtonSpec("Save", () => { Save(); next(); }, "studio-btn--primary"));
        }

        bool OnWantsToQuit()
        {
            if (_quitConfirmed || !Dirty || Project == null) return true;
            Modal.Show("Quit Puzzle Studio", Fields.Hint($"\"{Project.Name}\" has unsaved changes.", "studio-modal-text"),
                new Modal.ButtonSpec("Cancel", null),
                new Modal.ButtonSpec("Quit without saving", () => { _quitConfirmed = true; Application.Quit(); }, "studio-btn--danger"),
                new Modal.ButtonSpec("Save and quit", () => { Save(); _quitConfirmed = true; Application.Quit(); }, "studio-btn--primary"));
            return false;
        }

        // ------------------------------------------------------------------ actions

        public void ImportPaths(IEnumerable<string> paths)
        {
            if (Project == null)
            {
                Modal.Message("No project", "Create or open a project first, then add your images.");
                return;
            }
            int before = Project.Pack.levels.Count;
            var result = LevelImporter.Import(Project.Pack, paths);
            if (result.Added.Count > 0)
            {
                SelectedLevel = before;
                ShowPanel("levels");
                MarkDirty(rebuildInspector: true);
                _preview.Show(Project.Pack, SelectedLevel, immediate: true);
            }
            string msg = $"Added {result.Added.Count} level{(result.Added.Count == 1 ? "" : "s")}";
            if (result.Skipped.Count > 0) msg += $" ({result.Skipped.Count} file(s) skipped: not PNG/JPG)";
            Toast(msg);
        }

        void PlayTestNow()
        {
            if (Project == null) return;
            if (Project.Pack.levels.Count == 0) { Modal.Message("Nothing to test", "Add some images in the Levels tab first."); return; }
            Save();
            if (PlayTest.Launch(Project, SelectedLevel, out string error)) Toast("Play test started in a new window");
            else Modal.Message("Play test", error);
        }

        public void StartExport()
        {
            if (Project == null || IsExporting) return;
            Save();
            StartCoroutine(RunExport());
        }

        IEnumerator RunExport()
        {
            IsExporting = true;
            Exporter = new GameExporter(Project, transform);
            ShowPanel("export");
            var run = Exporter.Run();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Modal.Message("Export failed", e.Message);
                    break;
                }
                (_current as ExportPanel)?.UpdateProgress();
                if (!more) break;
                yield return run.Current;
            }
            IsExporting = false;
            if (_current is ExportPanel) RebuildInspector();
            var o = Exporter.Outcome;
            if (o != null && o.Success) Toast($"Exported {Path.GetFileName(o.ExePath)}");
            else if (o?.Error != null) Toast("Export failed: " + o.Error);
        }

        public void Toast(string message)
        {
            _status.text = message;
            _statusUntil = Time.unscaledTime + 6f;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            _preview.Tick();

            if (_statusUntil > 0 && Time.unscaledTime > _statusUntil)
            {
                _status.text = Project != null ? $"{Project.Pack.levels.Count} levels · {Project.Root}" : "";
                _statusUntil = 0;
            }

            while (DragDropHook.TryDequeue(out var dropped)) HandleDrop(dropped);

            var kb = Keyboard.current;
            if (kb == null || Modal.IsOpen) return;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.sKey.wasPressedThisFrame) Save();
            else if (ctrl && kb.nKey.wasPressedThisFrame) TryLeave(NewProjectDialog);
            else if (ctrl && kb.oKey.wasPressedThisFrame) TryLeave(OpenProjectDialog);
            else if (kb.f5Key.wasPressedThisFrame) PlayTestNow();
        }

        void HandleDrop(List<string> paths)
        {
            if (paths.Count == 1 && StudioProject.ResolveRoot(paths[0]) != null)
            {
                string p = paths[0];
                TryLeave(() => OpenProject(p));
                return;
            }
            ImportPaths(paths);
        }

        // ------------------------------------------------------------------ helpers

        static VisualElement Box(string className)
        {
            var e = new VisualElement();
            e.AddToClassList(className);
            return e;
        }

        static T Tip<T>(T e, string tooltip) where T : VisualElement
        {
            e.tooltip = tooltip;
            return e;
        }
    }
}
