using System.Collections.Generic;
using System.IO;
using PuzzleStudio.Core.Audio;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Localization;
using PuzzleStudio.Core.Pack;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Steam;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.FX;
using PuzzleStudio.Game.Gameplay;
using PuzzleStudio.Game.Steam;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    public enum StartScreen { Auto, Splash, Menu, Levels, Gameplay, Victory, Settings, Credits, End }

    /// <summary>
    /// Navigation between screens (splash → menu → levels → gameplay → victory/end), music, pause,
    /// language and UI scale. One instance per running game (also inside the Studio preview).
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public GamePackData Pack { get; private set; }
        public SaveSystem Save { get; private set; }
        public LocalizationService Loc { get; private set; }
        public ThemeService Theme { get; private set; }
        public AudioService Audio { get; private set; }
        public ThumbnailService Thumbs { get; private set; }
        public GameViewport Viewport { get; private set; }
        public GameplayController Gameplay { get; private set; }
        public BackgroundView BackgroundPicture { get; private set; }
        public BackgroundFill BackgroundFill { get; private set; }
        public ParticleFactory Particles { get; private set; }
        public AchievementService Achievements { get; private set; }
        public ISteamService Steam { get; private set; }
        public bool IsPreview => Viewport.IsPreview;
        public GameScreen CurrentPage => _router?.Page;
        public GameScreen TopScreen => _router?.Top;

        VisualElement _uiRoot;
        Camera _camera;
        ScreenRouter _router;
        SplashScreen _splash;
        MainMenuScreen _menu;
        LevelSelectScreen _levels;
        GameplayScreen _gameplayScreen;
        PauseScreen _pause;
        SettingsScreen _settings;
        CreditsScreen _credits;
        EndScreen _end;
        ConfirmScreen _confirm;
        AchievementsScreen _achievements;
        VisualElement _toasts;
        readonly Dictionary<string, Texture2D> _images = new Dictionary<string, Texture2D>();

        /// <summary>Raised when the UI scale changes (GameRoot updates the panel settings).</summary>
        public System.Action<float> UiScaleChanged;

        public void Init(Camera camera, VisualElement uiRoot, GameViewport viewport, AudioService audio)
        {
            _camera = camera;
            _uiRoot = uiRoot;
            Viewport = viewport;
            Pack = ServiceHub.Pack;
            Save = ServiceHub.Save;
            Loc = ServiceHub.Loc;
            Theme = ServiceHub.Theme;
            Audio = audio;
            Thumbs = new ThumbnailService(Pack);

            var s = Save.Settings;
            Theme.ColorblindMode = s.colorblindMode;
            UiAnim.ReduceMotion = s.reduceMotion;
            UiAnim.Speed = Pack.theme.uiStyle.animationSpeed;
            Audio.Init(Pack, s.masterVolume, SettingsApplier.Music(s, Pack), SettingsApplier.Sfx(s, Pack));

            _router = new ScreenRouter(uiRoot);
            // Every button click makes a sound.
            uiRoot.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) Audio.PlaySfx("click");
            }, TrickleDown.TrickleDown);

            BackgroundPicture = BackgroundView.Create(transform, camera);
            BackgroundFill = BackgroundFill.Create(transform, camera);
            Particles = new GameObject("Particles").AddComponent<ParticleFactory>();
            Particles.transform.SetParent(transform, false);
            Particles.Init(camera, Theme, Pack.theme.particles);
            Particles.ReduceMotion = s.reduceMotion;
            Gameplay = gameObject.AddComponent<GameplayController>();
            Gameplay.Init(this, _camera, Viewport);

            Steam = ServiceHub.Steam ?? new NullSteamService();
            Steam.OverlayToggled += OnSteamOverlay;
            Achievements = new AchievementService(Pack, Save, Steam, persist: !IsPreview);
            Achievements.Unlocked += OnAchievementUnlocked;
            Achievements.SyncOnStart();
            BuildScreens();
        }

        void BuildScreens()
        {
            _splash = new SplashScreen(this);
            _menu = new MainMenuScreen(this);
            _levels = new LevelSelectScreen(this);
            _gameplayScreen = new GameplayScreen(this);
            _pause = new PauseScreen(this);
            _settings = new SettingsScreen(this);
            _credits = new CreditsScreen(this);
            _end = new EndScreen(this);
            _confirm = new ConfirmScreen(this);
            _achievements = new AchievementsScreen(this);
            Gameplay.BindScreen(_gameplayScreen);
        }

        public void Begin(StartScreen start, int level)
        {
            RefreshBackground(-1);
            switch (start)
            {
                case StartScreen.Splash:
                case StartScreen.Auto:
                    _router.ShowPage(_splash);
                    Audio.PlayMusic(Pack.audio.musicMenu);
                    break;
                case StartScreen.Menu: ShowMenu(); break;
                case StartScreen.Levels: ShowLevels(); break;
                case StartScreen.Settings: ShowSettings(false); break;
                case StartScreen.Credits: ShowCredits(); break;
                case StartScreen.End: ShowEnd(); break;
                case StartScreen.Victory:
                    PlayLevel(level);
                    Gameplay.DebugSolve();
                    break;
                default: PlayLevel(level); break;
            }
        }

        // ------------------------------------------------------------------ navigation

        public void ShowMenu() => ShowMenuPage(_menu);
        public void ShowLevels() => ShowMenuPage(_levels);
        public void ShowCredits() => ShowMenuPage(_credits);
        public void ShowAchievements() => ShowMenuPage(_achievements);

        public void ShowEnd()
        {
            ShowMenuPage(_end);
            Presence("#Finished");
        }

        void ShowMenuPage(GameScreen page)
        {
            LeaveGameplay();
            RefreshBackground(-1);
            Audio.PlayMusic(Pack.audio.musicMenu);
            _router.ShowPage(page);
            Presence("#Menu");
        }

        public void ShowSettings(bool asOverlay)
        {
            _settings.AsOverlay = asOverlay;
            if (asOverlay) _router.PushOverlay(_settings);
            else
            {
                LeaveGameplay();
                _router.ShowPage(_settings);
            }
        }

        public void PlayLevel(int index)
        {
            if (Pack.levels.Count == 0) return;
            index = Mathf.Clamp(index, 0, Pack.levels.Count - 1);
            if (_router.Page != _gameplayScreen) _router.ShowPage(_gameplayScreen);
            else _router.CloseAllOverlays();
            Audio.PlayMusic(Pack.audio.musicGame);
            Gameplay.StartLevel(index);
            Gameplay.SetPaused(false);
            Presence("#Playing", index + 1);
        }

        // ------------------------------------------------------------------ Steam / achievements

        /// <summary>Rich Presence: what Steam friends see ("Solving puzzle 3 of 12").</summary>
        void Presence(string token, int level = 0)
        {
            if (IsPreview || !Pack.steam.richPresence || !Steam.IsAvailable) return;
            string status = Loc.T(SteamworksFiles.PresenceKey(token), level, Pack.levels.Count);
            Steam.SetPresence(token, level, Pack.levels.Count, $"{Pack.game.title} - {status}");
        }

        void OnSteamOverlay(bool active)
        {
            if (active) Pause();
        }

        void OnAchievementUnlocked(AchievementDef def)
        {
            Debug.Log($"[Puzzle] Achievement unlocked: {def.id}");
            // With Steam the overlay shows its own popup; otherwise the game shows a toast.
            if (Steam.IsAvailable && Steam.OverlayEnabled) return;
            ShowToast(Loc.T("achievements.toast"), Achievements.NameOf(def, Loc));
        }

        /// <summary>Small card at the top of the screen for a few seconds (stacks when several arrive).</summary>
        public void ShowToast(string title, string text)
        {
            if (_toasts == null)
            {
                _toasts = Pz.MakeBox("pz-toasts");
                _toasts.pickingMode = PickingMode.Ignore;
                _uiRoot.Add(_toasts);
            }
            _toasts.BringToFront();
            var icon = new IconElement(Icon.Trophy) { Color = Theme.Palette.OnPrimary };
            icon.AddToClassList("pz-toast-icon");
            var badge = Pz.MakeBox("pz-toast-badge", icon);
            badge.style.backgroundColor = Theme.Palette.Primary;
            var toast = Pz.MakeBox($"pz-toast {Pz.Surface}", badge,
                Pz.MakeBox("pz-toast-texts",
                    Pz.MakeLabel(title, $"pz-toast-title {Pz.TextMuted}"),
                    Pz.MakeLabel(text, $"pz-toast-text {Pz.Text} {Pz.Heading}")));
            toast.pickingMode = PickingMode.Ignore;
            _toasts.Add(toast);
            if (!_toasts.styleSheets.Contains(GameSheet)) _toasts.styleSheets.Add(GameSheet);
            Theme.Apply(toast);
            UiAnim.FadeIn(toast, 0.35f, -18f);
            Audio.PlaySfx("star");
            toast.schedule.Execute(() => UiAnim.FadeOut(toast, 0.5f, () => toast.RemoveFromHierarchy())).ExecuteLater(4800);
        }

        static StyleSheet GameSheet => Resources.Load<StyleSheet>("UI/Game");

        // ------------------------------------------------------------------ looks

        /// <summary>
        /// Background picture for the current screen: the theme image, or the blurred picture of the level
        /// (in menus: of the level "Continue" would open). <paramref name="levelIndex"/> -1 = menus.
        /// </summary>
        public void RefreshBackground(int levelIndex)
        {
            var bg = Pack.theme.background;
            Texture tex = null;
            if (bg.type == BackgroundType.Image) tex = LoadPackImage(bg.image, 2048);
            else if (bg.type == BackgroundType.BlurredLevel && Pack.levels.Count > 0)
                tex = Thumbs.GetBlur(levelIndex >= 0 ? levelIndex : ProgressRules.ContinueIndex(Pack, Save.Progress));
            BackgroundPicture.Show(tex, bg.imageOpacity);
            _camera.backgroundColor = Theme.Palette.Background;
            BackgroundFill.Apply(bg, Theme.Palette, Theme.ColorOverride != null, UiAnim.ReduceMotion);
        }

        /// <summary>Called when per-level colors change (or are cleared): repaints the gameplay HUD and background.</summary>
        public void OnThemeColorsChanged()
        {
            _camera.backgroundColor = Theme.Palette.Background;
            BackgroundFill?.Apply(Pack.theme.background, Theme.Palette, Theme.ColorOverride != null, UiAnim.ReduceMotion);
            _gameplayScreen?.ApplyTheme();
        }

        public void ReduceMotionChanged()
        {
            Particles.ReduceMotion = UiAnim.ReduceMotion;
            BackgroundFill.Apply(Pack.theme.background, Theme.Palette, Theme.ColorOverride != null, UiAnim.ReduceMotion);
        }

        /// <summary>Studio Layout tab: replaces the layout and re-places every block live.</summary>
        public void SetLayout(LayoutConfig layout)
        {
            Pack.layout = layout ?? new LayoutConfig();
            _gameplayScreen?.ApplyLayout();
            _menu?.ApplyLayout();
            Gameplay.Relayout();
        }

        /// <summary>A decoration picture block (null if no picture is set).</summary>
        public VisualElement BuildDecor(string reference, string layoutId)
        {
            var tex = LoadPackImage(reference, 1024);
            if (tex == null) return null;
            var e = LayoutService.Tag(Pz.MakeBox("pz-decor"), layoutId);
            e.style.backgroundImage = Background.FromTexture2D(tex);
            const float height = 380f;
            e.style.height = height;
            e.style.width = height * tex.width / Mathf.Max(1, tex.height);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public void RestartLevel() => Gameplay.Restart();

        /// <summary>Victory "Next": the next level, the end screen, or the level list.</summary>
        public void NextAfterVictory()
        {
            int current = Gameplay.LevelIndex;
            int next = ProgressRules.NextPlayable(Pack, Save.Progress, current);
            if (next >= 0) PlayLevel(next);
            else if (ProgressRules.AllCompleted(Pack, Save.Progress)) ShowEnd();
            else ShowLevels();
        }

        public void Pause()
        {
            if (_router.Page != _gameplayScreen || _router.HasOverlay || _gameplayScreen.VictoryVisible) return;
            Gameplay.SetPaused(true);
            _router.PushOverlay(_pause);
        }

        public void Resume()
        {
            _router.CloseAllOverlays();
            Gameplay.SetPaused(false);
        }

        public void CloseOverlay()
        {
            _router.PopOverlay();
            if (!_router.HasOverlay && _router.Page == _gameplayScreen) Gameplay.SetPaused(false);
        }

        public void Confirm(string message, string okText, System.Action onOk)
        {
            _confirm.Setup(message, okText, onOk);
            _router.PushOverlay(_confirm);
        }

        public void Quit()
        {
            Save.SaveProgress();
            Save.SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void LeaveGameplay()
        {
            if (_router.Page == _gameplayScreen) Gameplay.StopLevel();
            _router.CloseAllOverlays();
        }

        // ------------------------------------------------------------------ settings hooks

        public void ApplyVolumes()
        {
            var s = Save.Settings;
            Audio.SetVolumes(s.masterVolume, SettingsApplier.Music(s, Pack), SettingsApplier.Sfx(s, Pack));
        }

        public void SetUiScale(float scale)
        {
            Save.Settings.uiScale = scale;
            UiScaleChanged?.Invoke(scale);
        }

        public List<(string code, string name)> AvailableLanguages()
        {
            var list = new List<(string, string)> { ("en", "English") };
            if (Resources.Load<TextAsset>("Localization/fr") != null) list.Add(("fr", "Français"));
            if (!string.IsNullOrEmpty(Pack.RootPath))
            {
                string dir = Path.Combine(Pack.RootPath, PackPaths.LocaleDir);
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                        if (!list.Exists(l => l.Item1 == code)) list.Add((code, LanguageName(code)));
                    }
            }
            return list;
        }

        static string LanguageName(string code)
        {
            switch (code)
            {
                case "de": return "Deutsch";
                case "es": return "Español";
                case "it": return "Italiano";
                case "pt": return "Português";
                case "ja": return "Japanese";
                case "zh": return "Chinese";
                default: return code.ToUpperInvariant();
            }
        }

        /// <summary>Switches language and rebuilds every screen (texts are set when screens are built).</summary>
        public void SetLanguage(string code)
        {
            Save.Settings.language = code;
            Save.SaveSettings();
            Loc = GameBootstrap.LoadLocalization(Pack, code);
            ServiceHub.Loc = Loc;
            bool overlay = _settings.AsOverlay;
            bool inGame = _router.Page == _gameplayScreen;
            _router.CloseAllOverlays();
            _router.Page?.Root.RemoveFromHierarchy();
            BuildScreens();
            _router = new ScreenRouter(_uiRoot);
            if (inGame)
            {
                _router.ShowPage(_gameplayScreen);
                Gameplay.SetPaused(true);
                _router.PushOverlay(_pause);
                ShowSettings(true);
            }
            else ShowSettings(overlay);
        }

        // ------------------------------------------------------------------ helpers for screens

        /// <summary>The game logo image if the pack has one, otherwise the title as styled text.</summary>
        public VisualElement BuildTitle(string className)
        {
            var logo = LoadPackImage(Pack.game.logo);
            if (logo != null)
            {
                var img = Pz.MakeBox($"{className} pz-logo-image");
                img.style.backgroundImage = Background.FromTexture2D(logo);
                img.style.width = 640;
                img.style.height = 640f * logo.height / Mathf.Max(1, logo.width);
                return img;
            }
            return Pz.MakeLabel(Pack.game.title, $"{className} {Pz.Text} {Pz.Heading}");
        }

        public Texture2D LoadPackImage(string reference, int maxSize = 1024)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            if (_images.TryGetValue(reference, out var t)) return t;
            t = TextureLoader.Load(Pack.Resolve(reference), maxSize, false);
            _images[reference] = t;
            return t;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _router?.Tick(dt);
            Thumbs?.Tick();
            HandleBackInput();
        }

        void HandleBackInput()
        {
            if (_router == null || !Viewport.KeyboardEnabled) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool start = pad != null && pad.startButton.wasPressedThisFrame;
            bool any = (kb != null && kb.anyKey.wasPressedThisFrame) || (pad != null && (pad.buttonSouth.wasPressedThisFrame || start));

            if (_router.Page == _splash && any) { _splash.Skip(); return; }

            if (_router.Page == _gameplayScreen && !_router.HasOverlay && !_gameplayScreen.VictoryVisible)
            {
                if (start || (back && !Gameplay.CancelSelection())) Pause();
                return;
            }
            if (back || (start && _router.Top == _pause)) _router.Back();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && !IsPreview && _router?.Page == _gameplayScreen) Pause();
        }

        void OnDestroy()
        {
            if (Steam != null) Steam.OverlayToggled -= OnSteamOverlay;
            Thumbs?.Dispose();
            foreach (var t in _images.Values) if (t != null) Destroy(t);
            _images.Clear();
        }
    }
}
