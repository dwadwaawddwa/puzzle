using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Game.UI;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Preview;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    /// <summary>
    /// "Layout" tab: drag &amp; resize the blocks of the gameplay screen and of the main menu directly in the preview,
    /// with grid / element snapping, alignment buttons and reset.
    /// </summary>
    public sealed class LayoutPanel : StudioPanel
    {
        static readonly List<string> ScreenNames = new List<string> { "Gameplay", "Main menu" };
        LayoutEditor _editor;
        bool _active;

        public LayoutPanel(StudioApp app) : base(app) { }
        public override string Id => "layout";
        public override string Title => "Layout";

        LayoutEditor Editor
        {
            get
            {
                if (_editor == null)
                {
                    _editor = new LayoutEditor(() => App.Project.Pack.layout, () => App.Project.Pack, () => App.Preview.Game?.Flow);
                    _editor.SelectionChanged += () => App.RebuildInspector();
                    _editor.Edited += live =>
                    {
                        App.MarkDirty(false, refreshPreview: false);
                        if (!live) App.RebuildInspector();
                    };
                }
                return _editor;
            }
        }

        /// <summary>Debug/automation: choose the edited screen and selected block.</summary>
        public void DebugSet(string screen, string select)
        {
            Editor.Screen = screen;
            if (_active) App.Preview.SetEditor(Editor, PreviewScreen);
            Editor.Select(select);
        }

        StartScreen PreviewScreen => _editor != null && _editor.Screen == LayoutConfig.MenuScreen ? StartScreen.Menu : StartScreen.Gameplay;

        public override void OnActivated()
        {
            _active = true;
            App.Preview.SetEditor(Editor, PreviewScreen);
        }

        public override void OnDeactivated()
        {
            _active = false;
            App.Preview.SetEditor(null, StartScreen.Gameplay);
        }

        public override void Build(VisualElement content)
        {
            var ed = Editor;
            var layout = Pack.layout;

            content.Add(Fields.Section("Screen",
                Fields.Dropdown("Edit", ScreenNames, ed.Screen == LayoutConfig.MenuScreen ? 1 : 0, i =>
                {
                    ed.Screen = i == 1 ? LayoutConfig.MenuScreen : LayoutConfig.GameplayScreen;
                    ed.Select(null);
                    if (_active) App.Preview.SetEditor(ed, PreviewScreen);
                    App.RebuildInspector();
                }),
                Fields.Hint("Drag blocks in the preview to move them. Drag the blue corner handle to resize. " +
                            "The game is paused while you edit.")));

            var gridSize = Fields.IntSlider("Grid size", 5, 80, ed.GridSize, v => { ed.GridSize = v; ed.MarkDirtyRepaint(); });
            content.Add(Fields.Section("Snapping",
                Fields.Toggle("Snap to grid", ed.SnapToGrid, v => ed.SnapToGrid = v),
                Fields.Toggle("Show grid", ed.ShowGrid, v => { ed.ShowGrid = v; ed.MarkDirtyRepaint(); }),
                gridSize,
                Fields.Toggle("Snap to other blocks", ed.SnapToElements, v => ed.SnapToElements = v),
                Fields.Hint("Pink lines appear when a block lines up with the screen center/edges or another block.")));

            // Element list
            var list = new VisualElement();
            list.AddToClassList("studio-layout-list");
            foreach (var id in ed.Ids)
            {
                string label = id == LayoutService.BoardId ? "Puzzle area" : LayoutService.FindSlot(ed.Screen, id).Label;
                bool exists = id == "decorLeft" ? !string.IsNullOrEmpty(Pack.theme.background.decorLeft)
                            : id == "decorRight" ? !string.IsNullOrEmpty(Pack.theme.background.decorRight)
                            : true;
                bool custom = id == LayoutService.BoardId ? layout.boardArea != null : layout.Get(ed.Screen, id) != null;
                var b = Fields.Button(label + (custom ? "  •" : ""), () => ed.Select(id), "studio-btn--small studio-layout-item");
                b.EnableInClassList("studio-layout-item--selected", id == ed.Selected);
                b.SetEnabled(exists);
                if (!exists) b.tooltip = "Not shown on this screen (no picture chosen in the Theme tab).";
                list.Add(b);
            }
            content.Add(Fields.Section("Blocks", list, Fields.Hint("•  = moved from its default place.")));

            if (ed.Selected != null) content.Add(BuildSelection(ed, layout));

            content.Add(Fields.Section("Reset",
                Fields.Button($"Reset the whole {ScreenNames[ed.Screen == LayoutConfig.MenuScreen ? 1 : 0].ToLowerInvariant()} screen", () =>
                    App.Modal.Confirm("Reset layout", "Put every block of this screen back to its original place and size?", "Reset", () =>
                    {
                        layout.For(ed.Screen).Clear();
                        if (ed.Screen == LayoutConfig.GameplayScreen) layout.boardArea = null;
                        ApplyAndRefresh();
                    }, "studio-btn--danger"), "studio-btn--danger")));
        }

        VisualElement BuildSelection(LayoutEditor ed, LayoutConfig layout)
        {
            string id = ed.Selected;
            bool board = id == LayoutService.BoardId;
            var section = Fields.Section(board ? "Puzzle area" : LayoutService.FindSlot(ed.Screen, id).Label);

            if (board)
            {
                var r = LayoutService.BoardArea(Pack, 16f / 9f, 1f);
                var b = layout.boardArea;
                section.Add(Fields.FloatSlider("Left %", 0, 80, (b?.x ?? r.x) * 100f, v => EditBoard(rect => rect.x = v / 100f)));
                section.Add(Fields.FloatSlider("Top %", 0, 80, (b?.y ?? r.y) * 100f, v => EditBoard(rect => rect.y = v / 100f)));
                section.Add(Fields.FloatSlider("Width %", 15, 100, (b?.w ?? r.width) * 100f, v => EditBoard(rect => rect.w = v / 100f)));
                section.Add(Fields.FloatSlider("Height %", 15, 100, (b?.h ?? r.height) * 100f, v => EditBoard(rect => rect.h = v / 100f)));
                section.Add(Fields.Hint("The picture keeps its ratio and is centered inside this area."));
            }
            else
            {
                var slot = LayoutService.FindSlot(ed.Screen, id);
                var item = layout.Get(ed.Screen, id);
                float x = item?.x ?? slot.DefaultPosition.x, y = item?.y ?? slot.DefaultPosition.y;
                section.Add(Fields.FloatSlider("X %", 0, 100, x * 100f, v => EditItem(it => it.x = v / 100f)));
                section.Add(Fields.FloatSlider("Y %", 0, 100, y * 100f, v => EditItem(it => it.y = v / 100f)));
                section.Add(Fields.FloatSlider("Size", 0.3f, 3f, item?.scale ?? slot.DefaultScale, v => EditItem(it => it.scale = v)));
                if (slot.CanHide)
                    section.Add(Fields.Toggle("Visible", item?.visible ?? true, v => EditItem(it => it.visible = v)));
            }

            var alignH = Fields.Row(
                Fields.Button("Left", () => ed.Align(-1, null), "studio-btn--small"),
                Fields.Button("Center", () => ed.Align(0, null), "studio-btn--small"),
                Fields.Button("Right", () => ed.Align(1, null), "studio-btn--small"));
            var alignV = Fields.Row(
                Fields.Button("Top", () => ed.Align(null, -1), "studio-btn--small"),
                Fields.Button("Middle", () => ed.Align(null, 0), "studio-btn--small"),
                Fields.Button("Bottom", () => ed.Align(null, 1), "studio-btn--small"));
            var hLabel = new Label("Align");
            hLabel.AddToClassList("studio-path-label");
            section.Add(Fields.Row(hLabel, alignH));
            var vLabel = new Label("");
            vLabel.AddToClassList("studio-path-label");
            section.Add(Fields.Row(vLabel, alignV));

            section.Add(Fields.Row(Fields.Button("Reset this block", () =>
            {
                if (board) layout.boardArea = null;
                else layout.For(ed.Screen).Remove(id);
                ApplyAndRefresh();
            }, "studio-btn--small")));
            return section;
        }

        void EditItem(System.Action<LayoutItem> change)
        {
            var ed = _editor;
            var dict = Pack.layout.For(ed.Screen);
            if (!dict.TryGetValue(ed.Selected, out var item))
            {
                var slot = LayoutService.FindSlot(ed.Screen, ed.Selected);
                item = new LayoutItem { x = slot.DefaultPosition.x, y = slot.DefaultPosition.y, scale = slot.DefaultScale };
                dict[ed.Selected] = item;
            }
            change(item);
            Live();
        }

        void EditBoard(System.Action<LayoutRect> change)
        {
            if (Pack.layout.boardArea == null)
            {
                var r = LayoutService.BoardArea(Pack, 16f / 9f, 1f);
                Pack.layout.boardArea = new LayoutRect(r.x, r.y, r.width, r.height);
            }
            change(Pack.layout.boardArea);
            Live();
        }

        void Live()
        {
            App.Preview.Game?.Flow?.SetLayout(Pack.layout);
            _editor.MarkDirtyRepaint();
            App.MarkDirty(false, refreshPreview: false);
        }

        void ApplyAndRefresh()
        {
            Live();
            App.RebuildInspector();
        }
    }
}
