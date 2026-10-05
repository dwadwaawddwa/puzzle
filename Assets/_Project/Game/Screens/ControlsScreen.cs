using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>Overlay listing every action for mouse, keyboard and gamepad (opened from the settings).</summary>
    public sealed class ControlsScreen : GameScreen
    {
        readonly Button _close;

        public ControlsScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-overlay pz-overlay--in pz-controls");

            var table = Pz.MakeBox("pz-controls-table");
            table.Add(Row(Header(loc.T("controls.action")), Header(loc.T("controls.mouse")), Header(loc.T("controls.keyboard")), Header(loc.T("controls.gamepad"))));
            table.Add(Row(Action(loc.T("controls.pick")), Text(loc.T("controls.mouse.pick")), Text(loc.T("controls.keys.pick")), Pads(PadButton.DPad, PadButton.A)));
            table.Add(Row(Action(loc.T("controls.rotateBack")), Text(loc.T("controls.mouse.rotateBack")), Text("Q"), Pads(PadButton.RB)));
            table.Add(Row(Action(loc.T("controls.slide")), Text(loc.T("controls.mouse.slide")), Text(loc.T("controls.keys.slide")), Pads(PadButton.DPad)));
            table.Add(Row(Action(loc.T("controls.hint")), Text(loc.T("controls.mouse.button")), Text("H"), Pads(PadButton.X)));
            table.Add(Row(Action(loc.T("controls.preview")), Text(loc.T("controls.mouse.hold")), Text(loc.T("controls.keys.preview")), Pads(PadButton.Y)));
            table.Add(Row(Action(loc.T("controls.undo")), Text(loc.T("controls.mouse.button")), Text("Ctrl+Z"), Pads(PadButton.LB)));
            table.Add(Row(Action(loc.T("controls.restart")), Text(loc.T("controls.mouse.button")), Text("R"), Pads(PadButton.View)));
            table.Add(Row(Action(loc.T("controls.pause")), Text(loc.T("controls.mouse.button")), Text(loc.T("key.esc")), Pads(PadButton.Menu)));

            _close = Pz.MakeIconButton(Icon.Back, loc.T("button.back"), Pz.Primary, flow.CloseOverlay);
            var card = Pz.MakeBox($"{Pz.Card} {Pz.Surface} pz-controls-card",
                Pz.MakeLabel(loc.T("controls.title"), $"pz-card-title {Pz.Text} {Pz.Heading}"),
                table,
                Pz.MakeBox("pz-card-buttons", _close));
            Root.Add(card);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
        }

        public override VisualElement DefaultFocus => _close;
        public override bool OnBack() { Flow.CloseOverlay(); return true; }
        public override void OnShow() => Flow.Theme.Apply(Root);

        static VisualElement Row(params VisualElement[] cells)
        {
            var row = Pz.MakeBox("pz-controls-row");
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].AddToClassList(i == 0 ? "pz-controls-cell--action" : "pz-controls-cell");
                row.Add(cells[i]);
            }
            return row;
        }

        static VisualElement Header(string text) => Pz.MakeLabel(text, $"pz-controls-header {Pz.TextMuted} {Pz.Heading}");
        static VisualElement Action(string text) => Pz.MakeLabel(text, $"pz-controls-action {Pz.Text} {Pz.Heading}");
        static VisualElement Text(string text) => Pz.MakeLabel(text, $"pz-controls-text {Pz.Text}");

        /// <summary>Gamepad glyphs, always visible here (not only while a gamepad is used).</summary>
        static VisualElement Pads(params PadButton[] buttons)
        {
            var box = Pz.MakeBox("pz-controls-pads");
            foreach (var b in buttons)
            {
                var p = PromptElement.Pad(b);
                p.RemoveFromClassList(PromptElement.PadClass);
                box.Add(p);
            }
            return box;
        }
    }
}
