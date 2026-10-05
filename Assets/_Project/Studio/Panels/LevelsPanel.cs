using System.Collections.Generic;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Core.Util.Win32;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    public sealed class LevelsPanel : StudioPanel
    {
        public LevelsPanel(StudioApp app) : base(app) { }
        public override string Id => "levels";
        public override string Title => "Levels";

        readonly List<VisualElement> _rows = new List<VisualElement>();
        int _dragFrom = -1, _dropAt = -1;

        public override void Build(VisualElement content)
        {
            var levels = Pack.levels;

            content.Add(Fields.Section("Images",
                Fields.Row(
                    Fields.Button("+ Add images…", AddImages, "studio-btn--primary"),
                    Fields.Button("+ Add folder…", AddFolder),
                    Fields.Spacer(),
                    Fields.Button("Sort A-Z", () => { LevelImporter.SortByFileName(Pack); Changed(rebuildInspector: true); }, "studio-btn--small")),
                Fields.Hint("Each image becomes one level. Tip: drag & drop images or a whole folder from Windows Explorer onto this window.")));

            var report = PackValidator.Validate(Pack);
            var header = new Label(levels.Count == 0 ? "No levels yet" : $"{levels.Count} level{(levels.Count > 1 ? "s" : "")}");
            header.AddToClassList("studio-section-title");
            content.Add(header);

            _rows.Clear();
            for (int i = 0; i < levels.Count; i++)
            {
                var row = BuildRow(levels[i], i, report);
                _rows.Add(row);
                content.Add(row);
                if (i == App.SelectedLevel) content.Add(BuildDetails(levels[i], i, report));
            }
            if (levels.Count > 1) content.Add(Fields.Hint("Drag a level by its dots to change the order."));
        }

        // ------------------------------------------------------------------ drag to reorder

        VisualElement BuildGrip(int index)
        {
            var grip = new GripElement { tooltip = "Drag to reorder" };
            grip.RegisterCallback<PointerDownEvent>(e =>
            {
                _dragFrom = index;
                _dropAt = index;
                grip.CapturePointer(e.pointerId);
                _rows[index].AddToClassList("studio-level--dragging");
                e.StopPropagation();
            });
            grip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragFrom < 0 || !grip.HasPointerCapture(e.pointerId)) return;
                int target = DropIndex(e.position.y);
                if (target == _dropAt) return;
                _dropAt = target;
                for (int i = 0; i < _rows.Count; i++)
                {
                    _rows[i].EnableInClassList("studio-level--drop-above", i == target && target < _dragFrom);
                    _rows[i].EnableInClassList("studio-level--drop-below", i == target && target > _dragFrom);
                }
            });
            grip.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                grip.ReleasePointer(e.pointerId);
                FinishDrag();
            });
            return grip;
        }

        /// <summary>Index of the row under a panel-space y (clamped to the list).</summary>
        int DropIndex(float y)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (y < _rows[i].worldBound.center.y + (i < _dragFrom ? 0f : _rows[i].worldBound.height * 0.5f))
                    return i;
            return _rows.Count - 1;
        }

        void FinishDrag()
        {
            int from = _dragFrom, to = _dropAt;
            _dragFrom = _dropAt = -1;
            if (from >= 0 && to >= 0 && LevelImporter.Move(Pack, from, to))
            {
                App.SelectLevel(to, rebuild: false);
                Changed(rebuildInspector: true);
            }
            else App.RebuildInspector();
        }

        /// <summary>Same reordering without a mouse (debug captures, tests).</summary>
        public void DebugMove(int from, int to)
        {
            _dragFrom = from;
            _dropAt = to;
            FinishDrag();
        }

        VisualElement BuildRow(LevelConfig level, int index, ValidationReport report)
        {
            var row = new VisualElement();
            row.AddToClassList("studio-level");
            row.AddToClassList("studio-row");
            if (index == App.SelectedLevel) row.AddToClassList("studio-level--selected");
            row.RegisterCallback<ClickEvent>(e =>
            {
                // Ignore clicks on buttons and inside the name field.
                if (e.target is Button || e.target is GripElement || (e.target is VisualElement ve && (ve is TextField || ve.GetFirstAncestorOfType<TextField>() != null))) return;
                if (App.SelectedLevel != index) App.SelectLevel(index);
            });

            row.Add(BuildGrip(index));
            var idx = new Label((index + 1).ToString());
            idx.AddToClassList("studio-level-index");
            row.Add(idx);

            var thumb = new VisualElement();
            thumb.AddToClassList("studio-level-thumb");
            var tex = ThumbnailCache.Get(Pack.Resolve(level.image));
            if (tex != null) thumb.style.backgroundImage = Background.FromTexture2D(tex);
            row.Add(thumb);

            var main = new VisualElement();
            main.AddToClassList("studio-level-main");
            var name = new TextField { value = level.name };
            name.AddToClassList("studio-level-name");
            name.RegisterValueChangedCallback(e => { level.name = e.newValue; Changed(); });
            main.Add(name);
            int issues = report.Issues.Count(i => i.levelId == level.id && i.severity != IssueSeverity.Info);
            var sub = new Label(issues > 0 ? $"{issues} issue{(issues > 1 ? "s" : "")} — select to see" : ModeLabel(level));
            sub.AddToClassList(issues > 0 ? "studio-level-warning" : "studio-level-sub");
            main.Add(sub);
            row.Add(main);

            var levels = Pack.levels;
            var up = Fields.Button("↑", () => Move(index, -1), "studio-btn--small studio-btn--icon");
            up.SetEnabled(index > 0);
            var down = Fields.Button("↓", () => Move(index, +1), "studio-btn--small studio-btn--icon");
            down.SetEnabled(index < levels.Count - 1);
            var dup = Fields.Button("Copy", () =>
            {
                LevelImporter.Duplicate(Pack, level);
                App.SelectLevel(index + 1, rebuild: false);
                Changed(rebuildInspector: true);
            }, "studio-btn--small");
            dup.tooltip = "Duplicate this level";
            var del = Fields.Button("×", () => App.Modal.Confirm("Delete level",
                $"Delete \"{level.name}\"? Its image is removed from the project folder.", "Delete", () =>
                {
                    LevelImporter.Remove(Pack, level);
                    App.SelectLevel(Mathf.Clamp(index, 0, Pack.levels.Count - 1), rebuild: false);
                    Changed(rebuildInspector: true);
                }, "studio-btn--danger"), "studio-btn--small studio-btn--icon studio-btn--danger");
            del.tooltip = "Delete this level";
            row.Add(up);
            row.Add(down);
            row.Add(dup);
            row.Add(del);
            return row;
        }

        VisualElement BuildDetails(LevelConfig level, int index, ValidationReport report)
        {
            var box = new VisualElement();
            box.AddToClassList("studio-level-details");

            string path = Pack.Resolve(level.image);
            string size = ImageHeaderReader.TryReadSize(path, out int w, out int h) ? $"{w} × {h}" : "unreadable";
            box.Add(Fields.Hint($"{level.image}  ·  {size} px"));

            // Crop
            bool cropped = !CropMath.IsFull(level.crop);
            var cropRow = Fields.Row(
                Fields.Hint(cropped ? $"Cropped: keeps {Mathf.RoundToInt(level.crop.w * 100)} % × {Mathf.RoundToInt(level.crop.h * 100)} %" : "Whole picture"),
                Fields.Spacer(),
                Fields.Button("Crop…", () => OpenCrop(level, w, h), "studio-btn--small"),
                cropped ? Fields.Button("Reset", () => { level.crop = CropMath.Full; Changed(rebuildInspector: true); }, "studio-btn--small") : null);
            cropRow.AddToClassList("studio-crop-row");
            box.Add(cropRow);

            // Mode override
            var modes = PuzzleModeRegistry.Ids.OrderBy(m => m).ToList();
            var choices = new List<string> { $"Default ({Fields.Nicify(Pack.gameplay.defaultMode)})" };
            choices.AddRange(modes.Select(Fields.Nicify));
            int current = level.mode == null ? 0 : modes.IndexOf(level.mode) + 1;
            box.Add(Fields.Dropdown("Mode", choices, Mathf.Max(0, current), i =>
            {
                level.mode = i <= 0 ? null : modes[i - 1];
                Changed();
            }));

            // Grid override
            var preview = App.CurrentGrid();
            bool custom = level.grid?.cols != null && level.grid?.rows != null;
            var cols = Fields.IntSlider("Columns", 2, GridResolver.MaxGrid, level.grid?.cols ?? preview.x, v => { level.grid ??= new GridOverride(); level.grid.cols = v; Changed(); });
            var rows = Fields.IntSlider("Rows", 2, GridResolver.MaxGrid, level.grid?.rows ?? preview.y, v => { level.grid ??= new GridOverride(); level.grid.rows = v; Changed(); });
            cols.SetEnabled(custom);
            rows.SetEnabled(custom);
            box.Add(Fields.Toggle("Custom grid", custom, v =>
            {
                if (v) level.grid = new GridOverride { cols = cols.value, rows = rows.value };
                else level.grid = null;
                cols.SetEnabled(v);
                rows.SetEnabled(v);
                Changed();
            }));
            box.Add(cols);
            box.Add(rows);
            box.Add(Fields.Hint(custom ? "This level uses its own grid." : $"Automatic grid from the difficulty curve: {preview.x} × {preview.y}."));

            foreach (var issue in report.Issues.Where(i => i.levelId == level.id))
            {
                var l = Fields.Hint((issue.severity == IssueSeverity.Error ? "Error: " : issue.severity == IssueSeverity.Warning ? "Warning: " : "") + issue.message);
                l.AddToClassList(issue.severity == IssueSeverity.Error ? "studio-text-error" : issue.severity == IssueSeverity.Warning ? "studio-text-warning" : "studio-text-muted");
                box.Add(l);
            }
            return box;
        }

        /// <summary>Crop dialog for one level (the preview shows the result right away).</summary>
        public void OpenCrop(LevelConfig level, int width, int height)
        {
            var picture = ThumbnailCache.Get(Pack.Resolve(level.image), 960);
            var editor = new CropEditor(picture, width, height, level.crop);
            App.Modal.Show("Crop picture", editor,
                new Modal.ButtonSpec("Cancel", null),
                new Modal.ButtonSpec("Apply", () =>
                {
                    level.crop = editor.Value;
                    Changed(rebuildInspector: true);
                }, "studio-btn--primary"));
        }

        string ModeLabel(LevelConfig level)
        {
            string mode = Fields.Nicify(level.mode ?? Pack.gameplay.defaultMode);
            return level.grid?.cols != null ? $"{mode} · {level.grid.cols} × {level.grid.rows}" : mode;
        }

        void Move(int index, int delta)
        {
            var levels = Pack.levels;
            int target = index + delta;
            if (target < 0 || target >= levels.Count) return;
            (levels[index], levels[target]) = (levels[target], levels[index]);
            App.SelectLevel(target, rebuild: false);
            Changed(rebuildInspector: true);
        }

        void AddImages()
        {
            var files = FileDialogs.OpenFiles("Add images", FileDialogs.ImageFilter, multiSelect: true);
            if (files.Count > 0) App.ImportPaths(files);
        }

        void AddFolder()
        {
            string folder = FileDialogs.PickFolder("Choose a folder of images (each image = one level)");
            if (!string.IsNullOrEmpty(folder)) App.ImportPaths(new[] { folder });
        }
    }
}
