using PuzzleStudio.Core.Audio;
using PuzzleStudio.Game.Screens;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>
    /// The only object placed in Game.unity. Builds camera, UI document, audio and the game flow at runtime,
    /// so scenes never need manual setup. The Studio hosts the same component for its live preview
    /// (see <see cref="HostConfig"/>).
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public const float OrthoSize = 5.4f; // 1 world unit = 100 reference pixels at 1080p

        /// <summary>Hosting options (Studio preview, tests). Null = normal full-screen game.</summary>
        public sealed class HostConfig
        {
            /// <summary>The board camera renders here instead of the screen (null = screen).</summary>
            public RenderTexture BoardTexture;
            /// <summary>The game UI is added under this element instead of its own UIDocument (null = own document).</summary>
            public VisualElement UiParent;
            /// <summary>Null = full-screen viewport.</summary>
            public GameViewport Viewport;
            public StartScreen StartScreen = StartScreen.Gameplay;
            public int StartLevel;
            public bool Muted;
        }

        /// <summary>Set right before AddComponent&lt;GameRoot&gt;(); consumed in Awake.</summary>
        public static HostConfig PendingHost;

        HostConfig _host;
        Camera _camera;
        UIDocument _document;
        PanelSettings _panelSettings;
        VisualElement _errorBox;
        GameViewport _viewport;

        public GameFlow Flow { get; private set; }
        public AudioService Audio { get; private set; }
        public Camera BoardCamera => _camera;
        /// <summary>Kept for tools/tests: the gameplay controller of the flow.</summary>
        public Gameplay.GameplayController Gameplay => Flow != null ? Flow.Gameplay : null;

        void Awake()
        {
            _host = PendingHost;
            PendingHost = null;
            _viewport = _host?.Viewport ?? new GameViewport();

            if (_host == null)
            {
                GameBootstrap.EnsureInitialized();
                if (ServiceHub.Save != null) SettingsApplier.ApplyFrameRate(ServiceHub.Save.Settings);
            }

            _camera = CreateCamera();
            VisualElement uiRoot;
            if (_host?.UiParent != null) uiRoot = _host.UiParent;
            else
            {
                _document = CreateDocument();
                uiRoot = _document.rootVisualElement;
            }

            if (!ServiceHub.IsReady || ServiceHub.Pack.levels.Count == 0)
            {
                ShowError(uiRoot, ServiceHub.LoadError ?? ServiceHub.Loc?.T("error.noLevels") ?? "No levels.");
                return;
            }

            _camera.backgroundColor = ServiceHub.Theme.Palette.Background;
            if (_document != null && !_viewport.IsPreview) ApplyUiScale(ServiceHub.Save.Settings.uiScale);

            Audio = new GameObject("Audio").AddComponent<AudioService>();
            Audio.transform.SetParent(transform, false);
            Audio.Muted = _host?.Muted ?? DebugCapture.HasFlag("-mute");

            Flow = gameObject.AddComponent<GameFlow>();
            Flow.Init(_camera, uiRoot, _viewport, Audio);
            Flow.UiScaleChanged += s => { ApplyUiScale(s); Flow.Gameplay.Relayout(); };

            if (_host != null)
            {
                Flow.Begin(_host.StartScreen, Mathf.Clamp(_host.StartLevel, 0, ServiceHub.Pack.levels.Count - 1));
                return;
            }

            int startLevel = DebugCapture.StartLevelArg();
            var startScreen = DebugCapture.StartScreenArg();
            if (startLevel >= 0 && startScreen == StartScreen.Auto) startScreen = StartScreen.Gameplay;
            Flow.Begin(startScreen, Mathf.Max(0, startLevel));
            DebugCapture.InstallIfRequested(this);
        }

        void Update()
        {
            // Only the real game drives Steam (the Studio preview and tests never start it).
            if (_host == null) ServiceHub.Steam?.Tick();
        }

        void OnApplicationQuit()
        {
            if (_host == null) ServiceHub.Steam?.Shutdown();
        }

        void OnDestroy()
        {
            _errorBox?.RemoveFromHierarchy();
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        /// <summary>"Text size" setting: a smaller reference resolution makes the whole UI bigger.</summary>
        void ApplyUiScale(float scale)
        {
            if (_document == null) return;
            scale = Mathf.Clamp(scale <= 0 ? 1f : scale, 0.75f, 1.5f);
            if (_panelSettings == null)
            {
                _panelSettings = Instantiate(_document.panelSettings);
                _document.panelSettings = _panelSettings;
            }
            _panelSettings.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920 / scale), Mathf.RoundToInt(1080 / scale));
            _viewport.UiScale = scale;
        }

        Camera CreateCamera()
        {
            var go = new GameObject("Board Camera");
            if (_host?.BoardTexture == null) go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - 10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            if (_host?.BoardTexture != null) cam.targetTexture = _host.BoardTexture;
            else go.AddComponent<AudioListener>();
            return cam;
        }

        UIDocument CreateDocument()
        {
            var go = new GameObject("UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/GamePanelSettings");
            go.SetActive(true);
            doc.rootVisualElement.pickingMode = PickingMode.Ignore;
            return doc;
        }

        void ShowError(VisualElement root, string message)
        {
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            var title = Pz.MakeLabel(ServiceHub.Loc?.T("error.title") ?? "Could not load the game", "pz-error-title");
            var body = Pz.MakeLabel(message, "pz-error-body");
            _errorBox = Pz.MakeBox("pz-error", title, body);
            root.Add(_errorBox);
        }
    }
}
