using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>Gameplay HUD (level, moves, time, actions, pause) + victory panel. Clicks pass through to the board.</summary>
    public sealed class GameplayScreen : GameScreen
    {
        public event Action OnUndo, OnHint, OnRestart, OnPause, OnNext, OnReplay, OnLevels;
        public event Action<bool> OnPreview;

        readonly Label _levelLabel, _levelName, _movesValue, _timeValue, _instructions;
        readonly VisualElement _tip, _tipRow;
        IVisualElementScheduledItem _tipHide;
        readonly VisualElement _movesPill, _timePill;
        readonly Button _undoBtn, _hintBtn, _restartBtn, _pauseBtn;
        readonly VisualElement _previewBtn, _hintHost;
        readonly Label _hintCount;

        readonly VisualElement _victory, _victoryCard;
        readonly Label _victoryMoves, _victoryTime, _victoryRecord, _victoryBest;
        readonly StarElement[] _stars = new StarElement[3];
        readonly Button _victoryNext, _victoryLevels;
        readonly Label _victoryNextLabel;

        int _shownSeconds = -1;
        public bool VictoryVisible { get; private set; }

        public GameplayScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            var theme = flow.Theme;
            Root = MakeRoot("pz-gameplay");

            // ---- top bar
            _levelLabel = Pz.MakeLabel("", $"pz-level-label {Pz.TextMuted} {Pz.Heading}");
            _levelName = Pz.MakeLabel("", $"pz-level-name {Pz.Text} {Pz.Heading}");
            _instructions = Pz.MakeLabel("", $"pz-level-help {Pz.Text}");
            _tip = Pz.MakeBox($"pz-tip {Pz.Surface}", _instructions);
            _tipRow = Pz.MakeBox("pz-tip-row", _tip);
            var titleBlock = LayoutService.Tag(Pz.MakeBox("pz-title-block", _levelLabel, _levelName), "title");

            _movesValue = Pz.MakeLabel("0", $"pz-pill-value {Pz.Text} {Pz.Heading}");
            _movesPill = Pz.MakeBox($"{Pz.Pill} {Pz.Surface}", Pz.MakeLabel(loc.T("hud.moves"), $"pz-pill-label {Pz.TextMuted}"), _movesValue);
            _timeValue = Pz.MakeLabel("00:00", $"pz-pill-value {Pz.Text} {Pz.Heading}");
            _timePill = Pz.MakeBox($"{Pz.Pill} {Pz.Surface}", Pz.MakeLabel(loc.T("hud.time"), $"pz-pill-label {Pz.TextMuted}"), _timeValue);
            _pauseBtn = Pz.MakeIconButton(Icon.Pause, null, Pz.Ghost, () => OnPause?.Invoke());
            _pauseBtn.tooltip = loc.T("button.pause");
            var stats = LayoutService.Tag(Pz.MakeBox("pz-stats", _movesPill, _timePill, _pauseBtn), "stats");
            Root.Add(titleBlock);
            Root.Add(stats);

            // ---- bottom bar
            _restartBtn = Pz.MakeIconButton(Icon.Restart, loc.T("button.restart"), Pz.Ghost, () => OnRestart?.Invoke());
            _undoBtn = Pz.MakeIconButton(Icon.Undo, loc.T("button.undo"), Pz.Ghost, () => OnUndo?.Invoke());
            _hintBtn = Pz.MakeIconButton(Icon.Hint, loc.T("button.hint"), Pz.Secondary, () => OnHint?.Invoke());
            _hintCount = Pz.MakeLabel("", "pz-badge");
            _hintHost = Pz.MakeBox("pz-badge-host", _hintBtn, _hintCount);
            _previewBtn = BuildHoldButton(loc.T("button.preview"));
            Root.Add(LayoutService.Tag(Pz.MakeBox("pz-actions", _restartBtn, _undoBtn, _hintHost, _previewBtn), "actions"));
            Root.Add(_tipRow);
            var decorLeft = flow.BuildDecor(flow.Pack.theme.background.decorLeft, "decorLeft");
            var decorRight = flow.BuildDecor(flow.Pack.theme.background.decorRight, "decorRight");
            if (decorLeft != null) Root.Add(decorLeft);
            if (decorRight != null) Root.Add(decorRight);

            // ---- victory overlay
            var title = Pz.MakeLabel(loc.T("victory.title"), $"pz-card-title {Pz.Text} {Pz.Heading}");
            var starsRow = Pz.MakeBox("pz-stars");
            for (int i = 0; i < 3; i++) { _stars[i] = new StarElement(); starsRow.Add(_stars[i]); }
            _victoryMoves = Pz.MakeLabel("", $"pz-card-stat {Pz.Text}");
            _victoryTime = Pz.MakeLabel("", $"pz-card-stat {Pz.Text}");
            _victoryBest = Pz.MakeLabel("", $"pz-card-best {Pz.TextMuted}");
            _victoryRecord = Pz.MakeLabel(loc.T("victory.newRecord"), $"pz-card-record {Pz.Heading}");
            var statsRow = Pz.MakeBox("pz-card-stats", _victoryMoves, _victoryTime);
            _victoryLevels = Pz.MakeIconButton(Icon.Grid, null, Pz.Ghost, () => OnLevels?.Invoke());
            _victoryLevels.tooltip = loc.T("menu.levels");
            var replay = Pz.MakeIconButton(Icon.Restart, loc.T("victory.replay"), Pz.Ghost, () => OnReplay?.Invoke());
            _victoryNext = Pz.MakeIconButton(Icon.Next, loc.T("victory.next"), Pz.Primary, () => OnNext?.Invoke(), iconAfter: true);
            _victoryNextLabel = _victoryNext.Q<Label>();
            var buttons = Pz.MakeBox("pz-card-buttons", _victoryLevels, replay, _victoryNext);
            _victoryCard = Pz.MakeBox($"{Pz.Card} {Pz.Surface} {Pz.Blocking}", title, starsRow, statsRow, _victoryBest, _victoryRecord, buttons);
            _victory = Pz.MakeBox($"pz-overlay {Pz.Blocking}", _victoryCard);
            Pz.SetVisible(_victory, false);
            Root.Add(_victory);

            // Decorations go behind the HUD blocks.
            decorLeft?.SendToBack();
            decorRight?.SendToBack();

            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            ApplyTheme();
            ApplyLayout();
            Pz.ConfigurePicking(Root);
        }

        /// <summary>Re-applies the (possibly per-level) theme colors.</summary>
        public void ApplyTheme()
        {
            var theme = Flow.Theme;
            theme.Apply(Root);
            foreach (var s in _stars) s.SetColors(theme.Palette.Accent, theme.Palette.TextMuted.WithAlpha(0.22f));
            _victoryRecord.style.color = theme.Palette.Primary;
            _hintCount.style.backgroundColor = theme.Palette.Primary;
            _hintCount.style.color = theme.Palette.OnPrimary;
        }

        public void ApplyLayout() => LayoutService.Apply(Root, LayoutConfig.GameplayScreen, Flow.Pack.layout);

        public override VisualElement DefaultFocus => VictoryVisible ? _victoryNext : null;

        public override bool OnBack()
        {
            if (VictoryVisible) { OnLevels?.Invoke(); return true; }
            return false;
        }

        VisualElement BuildHoldButton(string text)
        {
            // Plain element (not Button): Button's Clickable swallows the pointer-down needed for "hold".
            var e = Pz.MakeBox($"{Pz.ButtonClass} {Pz.Ghost} {Pz.Blocking} pz-button--with-icon");
            e.Add(new IconElement(Icon.Eye));
            e.Add(Pz.MakeLabel(text, $"pz-button-label {Pz.Heading}"));
            e.RegisterCallback<PointerDownEvent>(ev => { e.CapturePointer(ev.pointerId); OnPreview?.Invoke(true); });
            e.RegisterCallback<PointerUpEvent>(ev => { e.ReleasePointer(ev.pointerId); OnPreview?.Invoke(false); });
            e.RegisterCallback<PointerCaptureOutEvent>(_ => OnPreview?.Invoke(false));
            return e;
        }

        public void SetLevel(int index, int count, string name)
        {
            _levelLabel.text = Flow.Loc.T("hud.level", index + 1, count);
            _levelName.text = name;
            SetMoves(0);
            _shownSeconds = -1;
            SetTime(0f);
            HideVictory();
        }

        /// <summary>How-to-play bubble, shown for a few seconds when a level starts.</summary>
        public void SetInstructions(string text)
        {
            _instructions.text = text;
            bool show = !string.IsNullOrEmpty(text) && !text.StartsWith("mode.");
            Pz.SetVisible(_tipRow, show);
            _tipHide?.Pause();
            if (!show) return;
            UiAnim.FadeIn(_tip, 0.35f, 12f);
            _tipHide = _tip.schedule.Execute(() => UiAnim.FadeOut(_tip, 0.6f, () => Pz.SetVisible(_tipRow, false)));
            _tipHide.ExecuteLater(5500);
        }

        public void ConfigureFeatures(bool showMoves, bool showTimer, bool allowUndo, bool allowHints, bool allowPreview)
        {
            Pz.SetVisible(_movesPill, showMoves);
            Pz.SetVisible(_timePill, showTimer);
            Pz.SetVisible(_undoBtn, allowUndo);
            Pz.SetVisible(_hintHost, allowHints);
            Pz.SetVisible(_previewBtn, allowPreview);
        }

        public void SetMoves(int moves) => _movesValue.text = moves.ToString();

        public void SetHintsLeft(int left)
        {
            _hintCount.text = left.ToString();
            _hintBtn.SetEnabled(left > 0);
            Pz.SetVisible(_hintCount, left > 0);
        }

        public void SetUndoAvailable(bool available) => _undoBtn.SetEnabled(available);

        /// <summary>Only rebuilds the string when the displayed second changes.</summary>
        public void SetTime(float seconds)
        {
            int s = (int)seconds;
            if (s == _shownSeconds) return;
            _shownSeconds = s;
            _timeValue.text = Gameplay.GameSession.FormatTime(seconds);
        }

        /// <param name="nextKind">0 = next level, 1 = finish (all done), 2 = none (next level locked).</param>
        void HideTip()
        {
            _tipHide?.Pause();
            Pz.SetVisible(_tipRow, false);
        }

        public void ShowVictory(int stars, int moves, float seconds, bool newRecord, int bestMoves, float bestTime, int nextKind)
        {
            var loc = Flow.Loc;
            _victoryMoves.text = loc.T(moves == 1 ? "victory.move" : "victory.moves", moves);
            _victoryTime.text = loc.T("victory.time", Gameplay.GameSession.FormatTime(seconds));
            _victoryBest.text = loc.T("victory.best", bestMoves, Gameplay.GameSession.FormatTime(bestTime));
            Pz.SetVisible(_victoryRecord, newRecord);
            _victoryNextLabel.text = nextKind == 1 ? loc.T("victory.finish") : loc.T("victory.next");
            Pz.SetVisible(_victoryNext, nextKind != 2);

            HideTip();
            Pz.SetVisible(_victory, true);
            VictoryVisible = true;
            _victory.AddToClassList("pz-overlay--in");
            UiAnim.FadeIn(_victoryCard, 0.35f, 40f);
            for (int i = 0; i < 3; i++)
            {
                var star = _stars[i];
                star.Filled = i < stars;
                UiAnim.Pop(star, 0.4f, 0.35f + i * 0.28f);
                if (i < stars)
                {
                    int idx = i;
                    star.schedule.Execute(() => Flow.Audio?.PlaySfx("star", 1f + idx * 0.12f))
                        .ExecuteLater(UiAnim.ReduceMotion ? 0 : (long)((0.4f + i * 0.28f) * 1000));
                }
            }
            if (newRecord) UiAnim.Pop(_victoryRecord, 0.4f, 0.35f + 3 * 0.28f);
            _victoryCard.schedule.Execute(() => _victoryNext.Focus()).ExecuteLater(400);
        }

        public void HideVictory()
        {
            VictoryVisible = false;
            _victory.RemoveFromClassList("pz-overlay--in");
            Pz.SetVisible(_victory, false);
        }
    }
}
