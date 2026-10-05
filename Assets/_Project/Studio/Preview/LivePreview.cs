using System;
using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Studio.Widgets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Preview
{
    /// <summary>
    /// Runs the real game runtime inside a frame of the Studio: the board camera renders into a RenderTexture
    /// shown as the frame background, and the game UI tree is hosted (scaled) on top of it, so the preview is
    /// fully playable and pixel-identical to the exported game.
    /// </summary>
    public sealed class LivePreview
    {
        public static readonly (string label, StartScreen screen)[] Screens =
        {
            ("Gameplay", StartScreen.Gameplay),
            ("Victory", StartScreen.Victory),
            ("Main menu", StartScreen.Menu),
            ("Level select", StartScreen.Levels),
            ("Settings", StartScreen.Settings),
            ("Credits", StartScreen.Credits),
            ("End screen", StartScreen.End),
            ("Splash", StartScreen.Splash),
        };

        public static readonly (string label, int w, int h)[] Resolutions =
        {
            ("1920 × 1080 (16:9)", 1920, 1080),
            ("1280 × 800 (Steam Deck)", 1280, 800),
            ("2560 × 1080 (21:9)", 2560, 1080),
            ("1024 × 768 (4:3)", 1024, 768),
        };

        public readonly VisualElement Area;     // fills the center of the Studio
        readonly VisualElement _frame;          // keeps the selected aspect ratio
        readonly Image _boardImage;
        readonly VisualElement _uiHost;         // 1080-high virtual screen, scaled to the frame
        readonly Label _emptyLabel;
        readonly PreviewViewport _viewport;
        readonly Transform _parent;

        GameObject _gameGo;
        GameRoot _game;
        RenderTexture _rt;
        int _resolution;
        int _level;
        StartScreen _screen = StartScreen.Gameplay;
        bool _muted = true;
        GamePackData _source;
        float _rebuildAt = -1f;

        public int LevelIndex => _level;
        public int ResolutionIndex => _resolution;
        public GameRoot Game => _game;

        public LivePreview(Transform parent)
        {
            _parent = parent;
            Area = new VisualElement();
            Area.AddToClassList("studio-preview-area");

            _frame = new VisualElement { focusable = true };
            _frame.AddToClassList("studio-preview-frame");
            _frame.RegisterCallback<PointerDownEvent>(_ => _frame.Focus(), TrickleDown.TrickleDown);
            Area.Add(_frame);

            _boardImage = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _boardImage.AddToClassList("studio-preview-board");
            _frame.Add(_boardImage);

            _uiHost = new GameUiMarker { pickingMode = PickingMode.Ignore };
            _uiHost.AddToClassList("studio-preview-ui");
            _uiHost.style.transformOrigin = new TransformOrigin(0, 0, 0);
            _frame.Add(_uiHost);

            _emptyLabel = new Label("Import images in the Levels tab to see your game here.");
            _emptyLabel.AddToClassList("studio-preview-empty");
            _frame.Add(_emptyLabel);

            _viewport = new PreviewViewport(this);
            Area.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            _frame.RegisterCallback<GeometryChangedEvent>(_ => EnsureTexture());
        }

        public VisualElement Frame => _frame;
        public VisualElement UiHost => _uiHost;
        internal RenderTexture Texture => _rt;
        public bool HasFocus => _frame.panel?.focusController?.focusedElement is VisualElement f && (f == _frame || _frame.Contains(f));

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                if (_game != null && _game.Audio != null) _game.Audio.Muted = value;
            }
        }

        public StartScreen CurrentScreen => _screen;

        /// <summary>Layout editing: the game is frozen (no input, no timer) and an editor layer covers the frame.</summary>
        public bool Locked { get; private set; }
        VisualElement _editorLayer;

        public void SetScreen(StartScreen screen)
        {
            _screen = screen;
            Rebuild();
        }

        public void SetEditor(VisualElement editorLayer, StartScreen screen)
        {
            _editorLayer?.RemoveFromHierarchy();
            _editorLayer = editorLayer;
            Locked = editorLayer != null;
            if (editorLayer != null)
            {
                editorLayer.style.position = Position.Absolute;
                editorLayer.style.left = 0; editorLayer.style.top = 0; editorLayer.style.right = 0; editorLayer.style.bottom = 0;
                _frame.Add(editorLayer);
            }
            _screen = screen;
            Rebuild();
        }

        public void SetResolution(int index)
        {
            _resolution = Mathf.Clamp(index, 0, Resolutions.Length - 1);
            Layout();
        }

        /// <summary>Shows a pack (call again after every change; rebuilds are debounced).</summary>
        public void Show(GamePackData pack, int level, bool immediate = false)
        {
            _source = pack;
            _level = level;
            if (immediate) Rebuild();
            else _rebuildAt = Time.unscaledTime + 0.25f;
        }

        public void Clear()
        {
            _source = null;
            DestroyGame();
            _emptyLabel.style.display = DisplayStyle.Flex;
        }

        public void ShowVictory() => _game?.Gameplay?.DebugSolve();

        public void Restart() => Rebuild();

        public void Tick()
        {
            if (_rebuildAt > 0f && Time.unscaledTime >= _rebuildAt) Rebuild();
        }

        void Rebuild()
        {
            _rebuildAt = -1f;
            DestroyGame();
            if (_source == null) return;
            bool hasLevels = _source.levels.Count > 0;
            _emptyLabel.style.display = hasLevels ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hasLevels) return;

            EnsureTexture();
            GameBootstrap.InitializeWithPack(GamePackWriter.Clone(_source), preview: true);
            GameRoot.PendingHost = new GameRoot.HostConfig
            {
                BoardTexture = _rt,
                UiParent = _uiHost,
                Viewport = _viewport,
                StartLevel = Mathf.Clamp(_level, 0, _source.levels.Count - 1),
                StartScreen = _screen,
                Muted = _muted,
            };
            _gameGo = new GameObject("PreviewGame");
            _gameGo.transform.SetParent(_parent, false);
            _game = _gameGo.AddComponent<GameRoot>();
            _boardImage.image = _rt;
            // Share the project's layout object so Layout-tab edits show up without a rebuild.
            _game.Flow?.SetLayout(_source.layout);
            if (Locked) _game.Gameplay?.SetPaused(true);
            _editorLayer?.BringToFront();
        }

        void DestroyGame()
        {
            if (_gameGo != null) UnityEngine.Object.DestroyImmediate(_gameGo);
            _gameGo = null;
            _game = null;
            _uiHost.Clear();
        }

        /// <summary>Fits the frame in the area with the selected aspect ratio and scales the hosted UI.</summary>
        void Layout()
        {
            var r = Resolutions[_resolution];
            float aspect = (float)r.w / r.h;
            float availW = Area.layout.width - 32f, availH = Area.layout.height - 32f;
            if (float.IsNaN(availW) || availW <= 10 || availH <= 10) return;
            float w = availW, h = availW / aspect;
            if (h > availH) { h = availH; w = h * aspect; }
            _frame.style.width = w;
            _frame.style.height = h;
            _frame.style.left = (Area.layout.width - w) * 0.5f;
            _frame.style.top = (Area.layout.height - h) * 0.5f;

            float k = h / GameViewport.ReferenceHeight;
            _uiHost.style.width = GameViewport.ReferenceHeight * aspect;
            _uiHost.style.height = GameViewport.ReferenceHeight;
            _uiHost.style.scale = new Scale(new Vector3(k, k, 1f));

            EnsureTexture();
        }

        void EnsureTexture()
        {
            float w = _frame.resolvedStyle.width, h = _frame.resolvedStyle.height;
            if (float.IsNaN(w) || w < 8) { var r = Resolutions[_resolution]; w = r.w / 2f; h = r.h / 2f; }
            int tw = Mathf.Clamp(Mathf.RoundToInt(w), 16, 4096), th = Mathf.Clamp(Mathf.RoundToInt(h), 16, 4096);
            if (_rt != null && _rt.width == tw && _rt.height == th) return;

            var old = _rt;
            _rt = new RenderTexture(tw, th, 16, RenderTextureFormat.ARGB32) { name = "PreviewBoard", antiAliasing = 4 };
            _rt.Create();
            _boardImage.image = _rt;
            if (_game != null && _game.BoardCamera != null) _game.BoardCamera.targetTexture = _rt;
            if (old != null) { old.Release(); UnityEngine.Object.Destroy(old); }
        }

        /// <summary>Maps the Studio pointer into preview surface pixels and answers UI hit tests.</summary>
        sealed class PreviewViewport : GameViewport
        {
            readonly LivePreview _owner;
            public PreviewViewport(LivePreview owner) { _owner = owner; }

            public override bool IsPreview => true;

            public override Vector2Int Size => _owner._rt != null ? new Vector2Int(_owner._rt.width, _owner._rt.height) : new Vector2Int(1920, 1080);

            public override bool KeyboardEnabled => _owner.HasFocus;

            public override bool TryGetPointer(out Vector2 surfacePosition)
            {
                surfacePosition = default;
                var p = Pointer.current;
                var panel = _owner._frame.panel;
                if (p == null || panel == null || _owner._rt == null) return false;
                Vector2 screen = p.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                Vector2 local = _owner._frame.WorldToLocal(panelPos);
                var rect = _owner._frame.contentRect;
                if (!rect.Contains(local)) return false;
                surfacePosition = new Vector2(local.x / rect.width * _owner._rt.width, (1f - local.y / rect.height) * _owner._rt.height);
                return true;
            }

            public override bool IsOverUI(Vector2 surfacePosition, VisualElement uiRoot)
            {
                var panel = _owner._frame.panel;
                var p = Pointer.current;
                if (panel == null || p == null) return true;
                Vector2 screen = p.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                var picked = panel.Pick(panelPos);
                if (picked == null) return false;
                // Over the frame/board itself → not UI; over a game button/panel → UI; anything else in the Studio → UI.
                if (picked == _owner._frame) return false;
                return true;
            }
        }
    }
}
