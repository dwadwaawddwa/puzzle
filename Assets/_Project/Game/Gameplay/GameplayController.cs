using System.Collections;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Game.UI;
using UnityEngine;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>Runs levels: loads the image, creates the mode, wires board + input + HUD, plays sounds, scores and saves.</summary>
    public sealed class GameplayController : MonoBehaviour
    {
        GameFlow _flow;
        GamePackData _pack;
        SaveSystem _save;
        ThemeService _theme;

        BoardView _board;
        InputController _input;
        GameplayScreen _screen;
        GameViewport _viewport = new GameViewport();

        IPuzzleMode _mode;
        ICardMode _cards;   // Memory mode, else null
        GameSession _session;
        Texture2D _texture;
        int _levelIndex = -1;
        int _attempt;
        int _lastScreenW, _lastScreenH;
        bool _paused;
        int _correctStreak;
        int _lastSnapFrame = -1;

        public IPuzzleMode Mode => _mode;
        public BoardView Board => _board;
        public GameSession Session => _session;
        public int LevelIndex => _levelIndex;
        public bool IsPlaying => _session != null;
        /// <summary>Gamepad / keyboard cursor cell (-1 = hidden).</summary>
        public int CursorCell => _board.CursorVisible ? _board.CursorCell : -1;

        bool Slides => _mode != null && _mode.DragStyle == DragStyle.Slide;

        public void Init(GameFlow flow, Camera cam, GameViewport viewport)
        {
            _flow = flow;
            if (viewport != null) _viewport = viewport;
            _pack = flow.Pack;
            _save = flow.Save;
            _theme = flow.Theme;

            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(transform, false);
            _board = boardGo.AddComponent<BoardView>();
            _board.Init(cam, _theme);
            _board.UiScale = () => _viewport.UiScale;
            _board.Area = () =>
            {
                var size = _viewport.Size;
                return LayoutService.BoardArea(_pack, (float)size.x / Mathf.Max(1, size.y), _viewport.UiScale);
            };

            _input = gameObject.AddComponent<InputController>();
            _input.Board = _board;
            _input.Camera = cam;
            _input.Viewport = _viewport;
            _input.InputEnabled = false;
            _input.CanPick = cell => _mode != null && _mode.CanPick(cell);
            _input.AllowFreeDrag = () => _mode == null || _mode.DragStyle == DragStyle.Swap || _mode.DragStyle == DragStyle.Insert;
            _input.DirectionSlides = () => Slides;
            _input.OnDirection += (dx, dy) =>
            {
                if (_mode != null && _mode.DragStyle == DragStyle.Slide) Submit(PuzzleInput.Direction(dx, dy));
            };
            _input.OnTap += cell => Submit(PuzzleInput.Tap(cell));
            _input.OnDragStart += _ => _flow.Audio.PlaySfx("pick");
            _input.OnDrop += OnDrop;
            _input.OnSecondaryTap += cell => Submit(PuzzleInput.RotateCcw(cell));
            _input.OnUndo += Undo;
            _input.OnHint += RequestHint;
            _input.OnRestart += Restart;
            _input.OnPreview += SetPreview;
            InputModeTracker.Changed += OnInputModeChanged;
        }

        /// <summary>The cursor is shown while a gamepad or the keyboard is used, hidden with the mouse.</summary>
        void OnInputModeChanged(InputMode mode)
        {
            if (_session == null || !_session.IsRunning) return;
            if (mode == InputMode.Pointer || Slides) _board.HideCursor();
            else _board.ShowCursor(_board.HoverCell);
            RefreshHud();
        }

        /// <summary>Connects (or reconnects, after a language change) the HUD screen.</summary>
        public void BindScreen(GameplayScreen screen)
        {
            _screen = screen;
            _input.UiRoot = screen.Root;
            RefreshFeatures();
            screen.OnUndo += Undo;
            screen.OnHint += RequestHint;
            screen.OnRestart += Restart;
            screen.OnReplay += Restart;
            screen.OnPause += () => _flow.Pause();
            screen.OnNext += () => _flow.NextAfterVictory();
            screen.OnLevels += () => _flow.ShowLevels();
            screen.OnPreview += SetPreview;
            if (_session != null) RefreshHud();
        }

        /// <summary>HUD elements of the pack, minus the timer when the player hid it (accessibility).</summary>
        public void RefreshFeatures()
        {
            if (_screen == null) return;
            var g = _pack.gameplay;
            _screen.ConfigureFeatures(g.showMoves, g.showTimer && _save.Settings.showTimer, g.allowUndo, g.allowHints, g.allowPreview);
        }

        public void Restart()
        {
            if (_levelIndex >= 0) StartLevel(_levelIndex, _attempt + 1);
        }

        public void StartLevel(int index, int attempt = 0)
        {
            if (_pack.levels.Count == 0) return;
            StopAllCoroutines();
            index = Mathf.Clamp(index, 0, _pack.levels.Count - 1);
            var level = _pack.levels[index];

            var tex = TextureLoader.Load(_pack.Resolve(level.image));
            if (tex == null)
            {
                Debug.LogError($"[Puzzle] Could not load image for level {level.id}: {level.image}");
                tex = TextureLoader.Placeholder();
            }

            var setup = GridResolver.Resolve(_pack, index, tex.width, tex.height);
            _mode = PuzzleModeRegistry.Create(setup.ModeId);
            _mode.Setup(setup.Layout, setup.Settings);
            _mode.Shuffle(setup.Seed + attempt * 7919);
            _cards = _mode as ICardMode;
            _mode.OnMove += HandleMove;
            _mode.OnPieceCorrect += HandlePieceCorrect;
            _mode.OnSolved += HandleSolved;

            ApplyLevelColors(tex);
            _flow.RefreshBackground(index);

            _board.gameObject.SetActive(true);
            _board.Build(_mode, tex, _pack.theme.pieces);
            if (InputModeTracker.UsesNavigation && _mode.DragStyle != DragStyle.Slide) _board.ShowCursor();
            if (_texture != null && _texture != tex) Destroy(_texture);
            _texture = tex;

            _levelIndex = index;
            _attempt = attempt;
            _correctStreak = 0;
            _session = new GameSession(index, _mode);
            _paused = false;
            _input.InputEnabled = true;
            RefreshHud();

            _save.Progress.lastPlayedLevelId = level.id;
            if (!_viewport.IsPreview) _save.SaveProgress();
            Debug.Log($"[Puzzle] Level {index + 1}/{_pack.levels.Count} \"{LevelName(index)}\" mode={setup.ModeId} grid={setup.Layout.Cols}x{setup.Layout.Rows} par={_session.Par}");
        }

        /// <summary>Leaves the level (back to menus): hides the board and frees the picture.</summary>
        public void StopLevel()
        {
            StopAllCoroutines();
            _session = null;
            _mode = null;
            _cards = null;
            _input.InputEnabled = false;
            _flow.Particles.Clear();
            _board.Clear();
            _board.gameObject.SetActive(false);
            if (_texture != null) Destroy(_texture);
            _texture = null;
            if (_theme.ColorOverride != null)
            {
                _theme.SetColorOverride(null);
                _flow.OnThemeColorsChanged();
            }
        }

        /// <summary>"Colors from pictures": derives the gameplay colors from this level's picture.</summary>
        void ApplyLevelColors(Texture2D picture)
        {
            var mode = _pack.theme.autoColors;
            if (mode == AutoColors.Off || picture == null || TextureLoader.IsPlaceholder(picture))
            {
                if (_theme.ColorOverride != null) { _theme.SetColorOverride(null); _flow.OnThemeColorsChanged(); }
                return;
            }
            var palette = PaletteExtractor.FromTexture(picture);
            _theme.SetColorOverride(ThemeDerivation.FromPalette(_pack.theme.colors, palette, mode));
            _flow.OnThemeColorsChanged();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (_session != null) _session.IsPaused = paused;
            _input.InputEnabled = !paused && _session != null && _session.IsRunning;
            if (paused) _board.SetPreview(false);
        }

        /// <summary>Clears a tile selection; returns false if there was none (Escape then opens the pause menu).</summary>
        public bool CancelSelection()
        {
            if (_mode == null || _mode.SelectedCell < 0) return false;
            _mode.HandleInput(PuzzleInput.Cancel());
            _board.RefreshHighlights();
            return true;
        }

        string LevelName(int index)
        {
            var level = _pack.levels[index];
            return string.IsNullOrWhiteSpace(level.name) ? _flow.Loc.T("level.defaultName", index + 1) : level.name;
        }

        void RefreshHud()
        {
            if (_screen == null || _session == null) return;
            _screen.SetLevel(_levelIndex, _pack.levels.Count, LevelName(_levelIndex));
            _screen.SetInstructions(HelpText());
            _screen.SetMoves(_session.Moves);
            _screen.SetHintsLeft(_pack.gameplay.maxHintsPerLevel - _session.HintsUsed);
            _screen.SetUndoAvailable(_mode.CanUndo && _session.IsRunning);
        }

        /// <summary>How-to-play text for the device in use ("mode.SwapTiles.help.pad"…), falling back to the mouse text.</summary>
        string HelpText()
        {
            string key = "mode." + _mode.Id + ".help";
            var mode = InputModeTracker.Current;
            string variant = mode == InputMode.Gamepad ? key + ".pad" : mode == InputMode.Keyboard ? key + ".keys" : null;
            return variant != null && _flow.Loc.Has(variant) ? _flow.Loc.T(variant) : _flow.Loc.T(key);
        }

        void Update()
        {
            if (_session == null) return;
            _session.Tick(Time.unscaledDeltaTime);
            _screen?.SetTime(_session.Elapsed);

            var size = _viewport.Size;
            if (size.x != _lastScreenW || size.y != _lastScreenH)
            {
                _lastScreenW = size.x;
                _lastScreenH = size.y;
                _board.Relayout();
            }
        }

        public void Relayout() => _board.Relayout();

        /// <summary>Solves the current level instantly (Studio preview of the victory screen, debug).</summary>
        public void DebugSolve()
        {
            if (_mode == null || _session == null || !_session.IsRunning) return;
            _mode.ForceSolve();
        }

        void OnDestroy()
        {
            InputModeTracker.Changed -= OnInputModeChanged;
            Rumble.Stop();
            if (_texture != null) Destroy(_texture);
        }

        // ------------------------------------------------------------------ actions

        void Submit(in PuzzleInput input)
        {
            if (_mode == null || _session == null || !_session.IsRunning || _paused) return;
            var result = _mode.HandleInput(input);
            switch (result)
            {
                case MoveResult.Selected:
                    _flow.Audio.PlaySfx("pick");
                    _board.RefreshHighlights();
                    break;
                case MoveResult.Deselected:
                    _board.RefreshHighlights();
                    break;
                case MoveResult.Blocked:
                    _flow.Audio.PlaySfx("locked", 1f, 0.6f);
                    Rumble.Play(0.25f, 0f, 0.06f);
                    _board.RefreshHighlights();
                    break;
            }
        }

        void OnDrop(int from, int to)
        {
            if (from >= 0 && to >= 0 && from != to) Submit(PuzzleInput.Drop(from, to));
            // Invalid drop or no change: send the piece back to its cell.
            _board.Sync(animate: true);
        }

        void Undo()
        {
            if (!_pack.gameplay.allowUndo || _mode == null || _session == null || !_session.IsRunning || _paused) return;
            if (_mode.Undo()) _flow.Audio.PlaySfx("undo");
        }

        public void RequestHint()
        {
            if (!_pack.gameplay.allowHints || _mode == null || _session == null || !_session.IsRunning || _paused) return;
            if (_session.HintsUsed >= _pack.gameplay.maxHintsPerLevel) return;
            var hint = _mode.GetHint();
            if (!hint.IsValid) return;
            _session.CountHint();
            _board.ShowHint(hint);
            // With a gamepad the cursor jumps to the piece to move (Memory: to the card to turn over).
            int hintCell = _cards != null && _cards.IsFaceUp(hint.FromCell) ? hint.ToCell : hint.FromCell;
            if (_board.CursorVisible && hintCell >= 0 && !Slides) _board.ShowCursor(hintCell);
            _flow.Audio.PlaySfx("hint");
            _screen.SetHintsLeft(_pack.gameplay.maxHintsPerLevel - _session.HintsUsed);
        }

        public void RefreshHighlights() => _board.RefreshHighlights();

        void SetPreview(bool visible)
        {
            if (!_pack.gameplay.allowPreview || _session == null) return;
            if (visible && _session.IsRunning && !_paused) _session.MarkPreviewUsed();
            _board.SetPreview(visible && _session.IsRunning && !_paused);
        }

        // ------------------------------------------------------------------ mode events

        void HandleMove(PuzzleMove move)
        {
            if (!move.IsUndo)
            {
                _session.CountMove(move.Count);
                if (_cards != null) CardMoved(move);
                else _flow.Audio.PlaySfx(_mode.DragStyle == DragStyle.None ? "pick" : "drop");
            }
            _board.Sync(animate: true);
            _screen.SetMoves(_session.Moves);
            _screen.SetUndoAvailable(_mode.CanUndo);
        }

        // ------------------------------------------------------------------ Memory

        /// <summary>Time two different cards stay visible before they are turned back.</summary>
        public const float MismatchDelay = 1.05f;

        void CardMoved(PuzzleMove move)
        {
            if (!_cards.IsFaceUp(move.PrimaryPiece))
            {
                _flow.Audio.PlaySfx("drop", 0.85f, 0.7f);     // two different cards turned back
                return;
            }
            _flow.Audio.PlaySfx("pick");
            if (_cards.HasMismatch)
            {
                Rumble.Play(0.15f, 0f, 0.05f);
                StartCoroutine(TurnBackLater(_cards, _cards.Attempts));
            }
        }

        IEnumerator TurnBackLater(ICardMode cards, int attempt)
        {
            yield return new WaitForSecondsRealtime(MismatchDelay);
            while (_paused) yield return null;
            // Not if the player already went on (a third tap turns them back at once).
            if (_cards == cards && cards.HasMismatch && cards.Attempts == attempt) cards.ResolveMismatch();
        }

        /// <summary>Pair found: the sound when the second symbol shows, sparkles when the symbols make way for the picture.</summary>
        IEnumerator CardMatched(int pieceId)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            PlaySnapFeedback();
            yield return new WaitForSecondsRealtime(BoardView.CardHold - 0.2f);
            if (_mode != null && !_mode.IsSolved()) _flow.Particles.PlaySnap(_board.PieceWorldPosition(pieceId), _board.CellSize);
        }

        void HandlePieceCorrect(int pieceId)
        {
            if (_cards != null)
            {
                StartCoroutine(CardMatched(pieceId));
                return;
            }
            _board.PlayCorrect(pieceId);
            if (!_mode.IsSolved()) _flow.Particles.PlaySnap(_board.PieceWorldPosition(pieceId), _board.CellSize);
            PlaySnapFeedback();
        }

        void PlaySnapFeedback()
        {
            // Rising pitch while the player chains correct placements (one sound per frame: strips can fix several at once).
            if (_lastSnapFrame == Time.frameCount) return;
            _lastSnapFrame = Time.frameCount;
            _correctStreak++;
            _flow.Audio.PlaySfx("snap", 1f + Mathf.Min(_correctStreak, 8) * 0.03f);
            Rumble.Play(0.1f, 0.25f, 0.07f);
        }

        void HandleSolved()
        {
            _session.Finish();
            _input.InputEnabled = false;
            _board.HideCursor();
            _board.SetPreview(false);
            _screen.SetUndoAvailable(false);

            var level = _pack.levels[_levelIndex];
            int stars = _session.Stars(_pack.gameplay.starRules);
            bool wasAllDone = ProgressRules.AllCompleted(_pack, _save.Progress);
            bool perfect = _session.Par > 0 && _session.Moves <= _session.Par;
            var record = _save.Progress.Submit(level.id, stars, _session.Elapsed, _session.Moves, _session.UsedPreview, _session.HintsUsed > 0, perfect);
            _save.SaveProgress();
            _flow.Achievements.Check();
            Debug.Log($"[Puzzle] Solved {level.id}: {_session.Moves} moves (par {_session.Par}), {_session.Elapsed:0.0}s, {stars} stars");

            int next = ProgressRules.NextPlayable(_pack, _save.Progress, _levelIndex);
            bool allDone = ProgressRules.AllCompleted(_pack, _save.Progress);
            int nextKind = next >= 0 ? 0 : allDone ? 1 : 2;
            if (allDone && !wasAllDone && next >= 0) nextKind = 0;
            StartCoroutine(VictorySequence(stars, record, nextKind));
        }

        IEnumerator VictoryParticles()
        {
            yield return new WaitForSecondsRealtime(0.75f);
            _flow.Particles.PlayVictory(_board.WorldBoardRect);
        }

        IEnumerator VictorySequence(int stars, RecordResult record, int nextKind)
        {
            yield return new WaitForSecondsRealtime(_board.SettleTime);
            _board.PlayVictory();
            _flow.Audio.PlaySfx("victory");
            Rumble.Play(0.35f, 0.6f, 0.35f);
            StartCoroutine(VictoryParticles());
            yield return new WaitForSecondsRealtime(UiAnim.ReduceMotion ? 0.3f : 1.0f);
            var r = _save.Progress.Get(_pack.levels[_levelIndex].id);
            bool newRecord = !record.FirstCompletion && (record.NewBestMoves || record.NewBestTime);
            _screen.ShowVictory(stars, _session.Moves, _session.Elapsed, newRecord, r?.bestMoves ?? _session.Moves, r?.bestTime ?? _session.Elapsed, nextKind);
        }
    }
}
