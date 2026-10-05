using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>Audio, display, language, accessibility and progress reset. Works as a page or over the pause menu.</summary>
    public sealed class SettingsScreen : GameScreen
    {
        readonly VisualElement _content;
        readonly Button _back;
        VisualElement _firstControl;
        public bool AsOverlay { get; set; }

        public SettingsScreen(GameFlow flow) : base(flow)
        {
            Root = MakeRoot("pz-settings");
            _back = Pz.MakeIconButton(Icon.Back, null, Pz.Ghost, () => Back());
            Root.Add(Pz.MakeBox("pz-screen-header", _back, Pz.MakeLabel(flow.Loc.T("settings.title"), $"pz-screen-title {Pz.Text} {Pz.Heading}")));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pz-settings-scroll");
            _content = Pz.MakeBox($"{Pz.Card} {Pz.Surface} pz-settings-card");
            scroll.Add(_content);
            Root.Add(scroll);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
        }

        public override VisualElement DefaultFocus => _firstControl ?? _back;
        public override bool OnBack() { Back(); return true; }

        void Back()
        {
            Flow.Save.SaveSettings();
            if (AsOverlay) Flow.CloseOverlay();
            else Flow.ShowMenu();
        }

        public override void OnHide() => Flow.Save.SaveSettings();

        public override void OnShow()
        {
            // As an overlay (from pause) the settings page covers the game completely.
            Root.style.backgroundColor = AsOverlay ? Flow.Theme.Palette.Background : new Color(0, 0, 0, 0);
            _content.Clear();
            _firstControl = null;
            var loc = Flow.Loc;
            var s = Flow.Save.Settings;
            var pack = Flow.Pack;

            // ---- audio
            AddSection(loc.T("settings.audio"));
            AddSlider(loc.T("settings.master"), s.masterVolume, v => { s.masterVolume = v; Flow.ApplyVolumes(); });
            AddSlider(loc.T("settings.music"), SettingsApplier.Music(s, pack), v => { s.musicVolume = v; Flow.ApplyVolumes(); });
            AddSlider(loc.T("settings.sfx"), SettingsApplier.Sfx(s, pack), v => { s.sfxVolume = v; Flow.ApplyVolumes(); }, playSample: true);

            // ---- display (not in the Studio preview: it would resize the Studio window)
            if (!Flow.IsPreview)
            {
                AddSection(loc.T("settings.display"));
                var modes = new List<string> { loc.T("settings.fullscreen"), loc.T("settings.borderless"), loc.T("settings.windowed") };
                AddSelector(loc.T("settings.mode"), modes, (int)SettingsApplier.CurrentMode(), i =>
                {
                    s.displayMode = (DisplayMode)i;
                    SettingsApplier.ApplyDisplay(s);
                });

                var resolutions = SettingsApplier.Resolutions();
                var now = new Vector2Int(Screen.width, Screen.height);
                if (!resolutions.Contains(now)) resolutions.Insert(0, now);
                int current = resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
                AddSelector(loc.T("settings.resolution"), resolutions.Select(r => $"{r.x} × {r.y}").ToList(), Math.Max(0, current), i =>
                {
                    s.resolutionWidth = resolutions[i].x;
                    s.resolutionHeight = resolutions[i].y;
                    if (s.displayMode == DisplayMode.Borderless) s.displayMode = DisplayMode.Windowed;
                    SettingsApplier.ApplyDisplay(s);
                });

                var fpsNames = SettingsApplier.FpsChoices.Select(f => f == 0 ? loc.T("settings.unlimited") : f.ToString()).ToList();
                VisualElement fpsRow = null;
                AddToggle(loc.T("settings.vsync"), s.vSync, v =>
                {
                    s.vSync = v;
                    SettingsApplier.ApplyFrameRate(s);
                    fpsRow?.SetEnabled(!v);
                });
                fpsRow = AddSelector(loc.T("settings.fps"), fpsNames, Math.Max(0, Array.IndexOf(SettingsApplier.FpsChoices, s.fpsLimit)), i =>
                {
                    s.fpsLimit = SettingsApplier.FpsChoices[i];
                    SettingsApplier.ApplyFrameRate(s);
                });
                fpsRow.SetEnabled(!s.vSync);
            }

            // ---- language
            var languages = Flow.AvailableLanguages();
            if (languages.Count > 1)
            {
                AddSection(loc.T("settings.language"));
                int li = languages.FindIndex(l => l.code == loc.Language);
                AddSelector(loc.T("settings.language"), languages.Select(l => l.name).ToList(), Math.Max(0, li), i => Flow.SetLanguage(languages[i].code));
            }

            // ---- accessibility
            AddSection(loc.T("settings.accessibility"));
            if (!Flow.IsPreview)
            {
                var sizes = SettingsApplier.UiScales.Select(x => Mathf.RoundToInt(x * 100) + " %").ToList();
                int si = Array.FindIndex(SettingsApplier.UiScales, x => Mathf.Approximately(x, s.uiScale));
                AddSelector(loc.T("settings.textSize"), sizes, si < 0 ? 1 : si, i => Flow.SetUiScale(SettingsApplier.UiScales[i]));
            }
            AddToggle(loc.T("settings.colorblind"), s.colorblindMode, v => { s.colorblindMode = v; Flow.Theme.ColorblindMode = v; });
            AddToggle(loc.T("settings.reduceMotion"), s.reduceMotion, v => { s.reduceMotion = v; UiAnim.ReduceMotion = v; Flow.ReduceMotionChanged(); });

            // ---- progress
            AddSection(loc.T("settings.progress"));
            var reset = Pz.MakeButton(loc.T("settings.reset"), Pz.Danger, () =>
                Flow.Confirm(loc.T("settings.resetConfirm"), loc.T("settings.resetYes"), () =>
                {
                    Flow.Save.ResetProgress();
                    Flow.Audio?.PlaySfx("undo");
                }));
            reset.AddToClassList("pz-button--small");
            AddRow(loc.T("settings.resetLabel"), reset);

            Flow.Theme.Apply(Root);
        }

        // ------------------------------------------------------------------ builders

        void AddSection(string title) => _content.Add(Pz.MakeLabel(title.ToUpperInvariant(), $"pz-settings-section {Pz.TextMuted} {Pz.Heading}"));

        VisualElement AddRow(string label, VisualElement control)
        {
            var row = Pz.MakeBox("pz-setting-row", Pz.MakeLabel(label, $"pz-setting-label {Pz.Text}"), control);
            control.AddToClassList("pz-setting-control");
            _content.Add(row);
            if (_firstControl == null && control.focusable) _firstControl = control;
            return row;
        }

        void AddSlider(string label, float value, Action<float> onChange, bool playSample = false)
        {
            var slider = new Slider(0f, 1f) { value = value };
            slider.AddToClassList("pz-slider");
            var pct = Pz.MakeLabel(Mathf.RoundToInt(value * 100) + "%", $"pz-slider-value {Pz.TextMuted}");
            slider.RegisterValueChangedCallback(e =>
            {
                pct.text = Mathf.RoundToInt(e.newValue * 100) + "%";
                onChange(e.newValue);
            });
            if (playSample) slider.RegisterCallback<PointerUpEvent>(_ => Flow.Audio?.PlaySfx("snap"), TrickleDown.TrickleDown);
            var box = Pz.MakeBox("pz-slider-box", slider, pct);
            AddRow(label, box);
            if (_firstControl == null) _firstControl = slider;
        }

        VisualElement AddSelector(string label, List<string> choices, int index, Action<int> onChange)
        {
            var sel = new PzSelector(choices, index);
            sel.OnChanged += i => { Flow.Audio?.PlaySfx("click"); onChange(i); };
            return AddRow(label, sel);
        }

        void AddToggle(string label, bool value, Action<bool> onChange)
        {
            var t = new PzToggle(value);
            t.OnChanged += v => { Flow.Audio?.PlaySfx("click"); onChange(v); };
            AddRow(label, t);
        }
    }
}
