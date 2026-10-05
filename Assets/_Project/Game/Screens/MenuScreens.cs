using System;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Save;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>Developer name/logo, then the game title, then the main menu. Any click or key skips.</summary>
    public sealed class SplashScreen : GameScreen
    {
        readonly VisualElement _dev, _title;
        float _t;
        bool _done;
        const float DevDuration = 1.6f, TitleDuration = 1.8f;

        public SplashScreen(GameFlow flow) : base(flow)
        {
            Root = MakeRoot("pz-splash");
            var pack = flow.Pack;
            _dev = Pz.MakeBox("pz-splash-block");
            var devLogo = flow.LoadPackImage(pack.game.devLogo);
            if (devLogo != null)
            {
                var img = Pz.MakeBox("pz-splash-devlogo");
                img.style.backgroundImage = Background.FromTexture2D(devLogo);
                _dev.Add(img);
            }
            else if (!string.IsNullOrWhiteSpace(pack.game.developer))
            {
                _dev.Add(Pz.MakeLabel(flow.Loc.T("splash.presents", pack.game.developer), $"pz-splash-dev {Pz.TextMuted} {Pz.Heading}"));
            }
            _title = Pz.MakeBox("pz-splash-block", Flow.BuildTitle("pz-splash-title"));
            Root.Add(_dev);
            Root.Add(_title);
            _dev.style.opacity = 0;
            _title.style.opacity = 0;
            Root.RegisterCallback<PointerDownEvent>(_ => Skip());
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
            if (_dev.childCount == 0) _t = DevDuration;
        }

        public void Skip()
        {
            if (_done) return;
            _done = true;
            Flow.ShowMenu();
        }

        public override bool OnBack() { Skip(); return true; }

        public override void Tick(float dt)
        {
            if (_done) return;
            _t += dt;
            _dev.style.opacity = Window(_t, 0f, DevDuration);
            _title.style.opacity = Window(_t, DevDuration, DevDuration + TitleDuration);
            if (_t >= DevDuration + TitleDuration) Skip();
        }

        /// <summary>0 → 1 → 0 opacity over [start, end] with 0.4 s fades.</summary>
        static float Window(float t, float start, float end)
        {
            if (t < start || t > end) return 0f;
            return Mathf.Clamp01(Mathf.Min((t - start) / 0.4f, (end - t) / 0.4f));
        }
    }

    public sealed class MainMenuScreen : GameScreen
    {
        readonly VisualElement _title, _titleBlock, _buttons, _showcase;
        Button _first;
        float _time;

        public MainMenuScreen(GameFlow flow) : base(flow)
        {
            Root = MakeRoot("pz-menu");
            var decorLeft = flow.BuildDecor(flow.Pack.theme.background.decorLeft, "decorLeft");
            var decorRight = flow.BuildDecor(flow.Pack.theme.background.decorRight, "decorRight");
            if (decorLeft != null) Root.Add(decorLeft);
            if (decorRight != null) Root.Add(decorRight);

            _title = flow.BuildTitle("pz-menu-title");
            _titleBlock = LayoutService.Tag(Pz.MakeBox("pz-menu-titleblock", _title), "title");
            if (!string.IsNullOrWhiteSpace(flow.Pack.game.subtitle))
                _titleBlock.Add(Pz.MakeLabel(flow.Pack.game.subtitle, $"pz-menu-subtitle {Pz.TextMuted}"));
            _buttons = LayoutService.Tag(Pz.MakeBox("pz-menu-buttons"), "buttons");
            _showcase = LayoutService.Tag(Pz.MakeBox("pz-menu-showcase"), "showcase");
            Root.Add(_showcase);
            Root.Add(_titleBlock);
            Root.Add(_buttons);

            var footer = Pz.MakeBox("pz-menu-footer",
                Pz.MakeLabel(string.IsNullOrWhiteSpace(flow.Pack.game.developer) ? "" : flow.Pack.game.developer, $"pz-footer-text {Pz.TextMuted}"),
                Pz.MakeLabel("v" + flow.Pack.game.version, $"pz-footer-text {Pz.TextMuted}"));
            Root.Add(footer);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            BuildShowcase();
            ApplyLayout();
        }

        public void ApplyLayout() => LayoutService.Apply(Root, LayoutConfig.MenuScreen, Flow.Pack.layout);

        public override VisualElement DefaultFocus => _first;

        public override void OnShow()
        {
            var loc = Flow.Loc;
            var pack = Flow.Pack;
            var progress = Flow.Save.Progress;
            _buttons.Clear();
            bool started = ProgressRules.HasStarted(pack, progress);
            bool allDone = ProgressRules.AllCompleted(pack, progress);

            if (!allDone)
                _first = Add(Pz.MakeIconButton(Icon.Play, loc.T(started ? "menu.continue" : "menu.play"), Pz.Primary,
                    () => Flow.PlayLevel(ProgressRules.ContinueIndex(pack, progress))));
            var levels = Add(Pz.MakeIconButton(Icon.Grid, loc.T("menu.levels"), allDone ? Pz.Primary : Pz.Ghost, Flow.ShowLevels));
            if (allDone) _first = levels;
            if (pack.steam.showAchievementsInGame && Flow.Achievements.All.Count > 0)
                Add(Pz.MakeIconButton(Icon.Trophy, loc.T("menu.achievements"), Pz.Ghost, Flow.ShowAchievements));
            Add(Pz.MakeIconButton(Icon.Gear, loc.T("menu.settings"), Pz.Ghost, () => Flow.ShowSettings(asOverlay: false)));
            Add(Pz.MakeIconButton(Icon.Info, loc.T("menu.credits"), Pz.Ghost, Flow.ShowCredits));
            if (!Flow.IsPreview) Add(Pz.MakeIconButton(Icon.Close, loc.T("menu.quit"), Pz.Ghost, Flow.Quit));

            Flow.Theme.Apply(Root);
            ApplyLayout();
            int i = 0;
            foreach (var b in _buttons.Children()) UiAnim.FadeIn(b, 0.3f, 16f + 6f * i++);
        }

        Button Add(Button b)
        {
            b.AddToClassList("pz-menu-button");
            _buttons.Add(b);
            return b;
        }

        void BuildShowcase()
        {
            var pack = Flow.Pack;
            int count = Mathf.Min(3, pack.levels.Count);
            float[] angles = { -7f, 5f, -1.5f };
            Vector2[] offsets = { new Vector2(-70, 40), new Vector2(80, 10), new Vector2(0, -30) };
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var card = Pz.MakeBox($"pz-showcase-card {Pz.Surface}");
                var shadow = Pz.MakeBox("pz-showcase-shadow");
                var photo = Pz.MakeBox("pz-showcase-photo");
                card.Add(photo);
                var holder = Pz.MakeBox("pz-showcase-holder", shadow, card);
                holder.style.rotate = new Rotate(angles[i]);
                holder.style.translate = new Translate(offsets[i].x, offsets[i].y);
                _showcase.Add(holder);
                bool done = Flow.Save.Progress.IsCompleted(pack.levels[index].id);
                Flow.Thumbs.Request(index, blurred: !done && index > 0, tex =>
                {
                    if (tex != null) photo.style.backgroundImage = Background.FromTexture2D(tex);
                });
            }
        }

        public override void Tick(float dt)
        {
            _time += dt;
            if (UiAnim.ReduceMotion) return;
            var anim = Flow.Pack.theme.uiStyle.titleAnimation;
            if (anim == TitleAnimation.Float) _title.style.translate = new Translate(0, Mathf.Sin(_time * 1.3f) * 7f);
            else if (anim == TitleAnimation.Pulse)
            {
                float k = 1f + Mathf.Sin(_time * 2f) * 0.018f;
                _title.style.scale = new Scale(new Vector3(k, k, 1));
            }
            int n = 0;
            foreach (var holder in _showcase.Children())
            {
                float y = Mathf.Sin(_time * 0.8f + n * 1.7f) * 5f;
                var baseOffset = n == 0 ? new Vector2(-70, 40) : n == 1 ? new Vector2(80, 10) : new Vector2(0, -30);
                holder.style.translate = new Translate(baseOffset.x, baseOffset.y + y);
                n++;
            }
        }
    }

    public sealed class LevelSelectScreen : GameScreen
    {
        readonly VisualElement _grid;
        readonly Label _counter, _stars;
        readonly ScrollView _scroll;
        VisualElement _firstCard;

        public LevelSelectScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-levels");
            var back = Pz.MakeIconButton(Icon.Back, null, Pz.Ghost, flow.ShowMenu);
            back.tooltip = loc.T("button.back");
            _counter = Pz.MakeLabel("", $"pz-levels-counter {Pz.Text} {Pz.Heading}");
            _stars = Pz.MakeLabel("", $"pz-levels-stars {Pz.TextMuted}");
            var star = new StarElement { Filled = true };
            star.AddToClassList("pz-star--small");
            star.SetColors(flow.Theme.Palette.Accent, Color.clear);
            var header = Pz.MakeBox("pz-screen-header",
                back,
                Pz.MakeLabel(loc.T("levels.title"), $"pz-screen-title {Pz.Text} {Pz.Heading}"),
                Pz.MakeBox("pz-spacer"),
                Pz.MakeBox($"{Pz.Pill} {Pz.Surface} pz-levels-progress", _counter, star, _stars));
            Root.Add(header);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("pz-levels-scroll");
            _grid = Pz.MakeBox("pz-levels-grid");
            _scroll.Add(_grid);
            Root.Add(_scroll);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
        }

        public override VisualElement DefaultFocus => _firstCard;
        public override bool OnBack() { Flow.ShowMenu(); return true; }

        public override void OnShow()
        {
            var pack = Flow.Pack;
            var progress = Flow.Save.Progress;
            var loc = Flow.Loc;
            _counter.text = loc.T("levels.completed", ProgressRules.CompletedCount(pack, progress), pack.levels.Count);
            _stars.text = $"{ProgressRules.TotalStars(pack, progress)} / {pack.levels.Count * 3}";
            _grid.Clear();
            _firstCard = null;
            int focusIndex = ProgressRules.ContinueIndex(pack, progress);

            for (int i = 0; i < pack.levels.Count; i++)
            {
                var card = BuildCard(i);
                _grid.Add(card);
                if (i == focusIndex) _firstCard = card;
            }
            Flow.Theme.Apply(Root);
            int k = 0;
            foreach (var c in _grid.Children()) { if (k < 16) UiAnim.FadeIn(c, 0.3f, 12f + k * 2f); k++; }
            if (_firstCard != null) _scroll.schedule.Execute(() => _scroll.ScrollTo(_firstCard)).ExecuteLater(50);
        }

        VisualElement BuildCard(int index)
        {
            var pack = Flow.Pack;
            var progress = Flow.Save.Progress;
            var level = pack.levels[index];
            var record = progress.Get(level.id);
            bool done = record?.completed == true;
            bool unlocked = ProgressRules.IsUnlocked(pack, progress, index);
            var palette = Flow.Theme.Palette;

            var card = Pz.MakeBox($"pz-level-card {Pz.Surface}");
            card.focusable = true;
            card.tabIndex = index;
            if (!unlocked) card.AddToClassList("pz-level-card--locked");

            var thumb = Pz.MakeBox("pz-level-thumb");
            var number = Pz.MakeLabel((index + 1).ToString(), $"pz-level-number {Pz.Heading}");
            number.style.backgroundColor = done ? palette.Primary : palette.Surface.WithAlpha(0.9f);
            number.style.color = done ? palette.OnPrimary : palette.Text;
            thumb.Add(number);
            if (!unlocked)
            {
                var veil = Pz.MakeBox("pz-level-veil");
                veil.style.backgroundColor = palette.Background.WithAlpha(0.45f);
                thumb.Add(veil);
                var lockBox = new LockElement(palette.Text);
                lockBox.style.backgroundColor = palette.Surface.WithAlpha(0.92f);
                thumb.Add(lockBox);
                if (pack.gameplay.unlockRule == UnlockRule.ByStars)
                    thumb.Add(Pz.MakeLabel(Flow.Loc.T("levels.needStars", ProgressRules.StarsRequired(pack, index)), $"pz-level-need {Pz.Text}"));
            }
            card.Add(thumb);
            Flow.Thumbs.Request(index, blurred: !done, tex => { if (tex != null) thumb.style.backgroundImage = Background.FromTexture2D(tex); });

            string name = string.IsNullOrWhiteSpace(level.name) ? Flow.Loc.T("level.defaultName", index + 1) : level.name;
            var stars = Pz.MakeBox("pz-level-stars");
            for (int s = 0; s < 3; s++)
            {
                var star = new StarElement { Filled = s < (record?.stars ?? 0) };
                star.AddToClassList("pz-star--small");
                star.SetColors(palette.Accent, palette.TextMuted.WithAlpha(0.25f));
                stars.Add(star);
            }
            var info = Pz.MakeBox("pz-level-info",
                Pz.MakeLabel(name, $"pz-level-card-name {Pz.Text} {Pz.Heading}"),
                Pz.MakeBox("pz-level-meta", stars,
                    Pz.MakeLabel(done ? Gameplay.GameSession.FormatTime(record.bestTime) : "", $"pz-level-time {Pz.TextMuted}")));
            card.Add(info);

            void Activate()
            {
                if (unlocked) Flow.PlayLevel(index);
                else
                {
                    Flow.Audio?.PlaySfx("locked");
                    UiAnim.Shake(card);
                }
            }
            card.RegisterCallback<ClickEvent>(_ => Activate());
            card.RegisterCallback<NavigationSubmitEvent>(_ => Activate());
            return card;
        }
    }

    /// <summary>Overlay shown over gameplay.</summary>
    public sealed class PauseScreen : GameScreen
    {
        Button _resume;

        public override void OnShow() => Flow.Theme.Apply(Root);

        public PauseScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-overlay pz-overlay--in pz-pause");
            _resume = Pz.MakeIconButton(Icon.Play, loc.T("pause.resume"), Pz.Primary, flow.Resume);
            var card = Pz.MakeBox($"{Pz.Card} {Pz.Surface} pz-menu-card",
                Pz.MakeLabel(loc.T("pause.title"), $"pz-card-title {Pz.Text} {Pz.Heading}"),
                Pz.MakeBox("pz-menu-buttons",
                    _resume,
                    Pz.MakeIconButton(Icon.Restart, loc.T("button.restart"), Pz.Ghost, () => { flow.Resume(); flow.RestartLevel(); }),
                    Pz.MakeIconButton(Icon.Gear, loc.T("menu.settings"), Pz.Ghost, () => flow.ShowSettings(asOverlay: true)),
                    Pz.MakeIconButton(Icon.Grid, loc.T("menu.levels"), Pz.Ghost, flow.ShowLevels),
                    Pz.MakeIconButton(Icon.Home, loc.T("pause.mainMenu"), Pz.Ghost, flow.ShowMenu)));
            foreach (var b in card.Query<Button>().ToList()) b.AddToClassList("pz-menu-button");
            Root.Add(card);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
        }

        public override VisualElement DefaultFocus => _resume;
        public override bool OnBack() { Flow.Resume(); return true; }
    }

    public sealed class ConfirmScreen : GameScreen
    {
        readonly Label _message;
        readonly Button _ok;
        Action _onOk;

        public ConfirmScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-overlay pz-overlay--in");
            _message = Pz.MakeLabel("", $"pz-confirm-text {Pz.Text}");
            _ok = Pz.MakeButton(loc.T("confirm.yes"), Pz.Danger, () => { flow.CloseOverlay(); _onOk?.Invoke(); });
            var cancel = Pz.MakeButton(loc.T("confirm.cancel"), Pz.Ghost, flow.CloseOverlay);
            Root.Add(Pz.MakeBox($"{Pz.Card} {Pz.Surface}", _message, Pz.MakeBox("pz-card-buttons", cancel, _ok)));
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
        }

        public void Setup(string message, string okText, Action onOk)
        {
            _message.text = message;
            _ok.text = okText;
            _onOk = onOk;
        }

        public override VisualElement DefaultFocus => _ok;
        public override void OnShow() => Flow.Theme.Apply(Root);
    }

    public sealed class CreditsScreen : GameScreen
    {
        readonly Button _back;

        public CreditsScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            var game = flow.Pack.game;
            Root = MakeRoot("pz-credits");
            _back = Pz.MakeIconButton(Icon.Back, null, Pz.Ghost, flow.ShowMenu);
            Root.Add(Pz.MakeBox("pz-screen-header", _back, Pz.MakeLabel(loc.T("credits.title"), $"pz-screen-title {Pz.Text} {Pz.Heading}")));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pz-credits-scroll");
            var col = Pz.MakeBox("pz-credits-col");
            col.Add(flow.BuildTitle("pz-credits-gametitle"));
            if (!string.IsNullOrWhiteSpace(game.developer))
                AddEntry(col, loc.T("credits.developer"), game.developer);
            foreach (var c in game.credits)
                if (c != null) AddEntry(col, c.role, string.Join("\n", c.names));
            AddEntry(col, loc.T("credits.fonts"), "Nunito, Inter, Fredoka, Playfair Display\nSIL Open Font License");
            AddEntry(col, loc.T("credits.audio"), loc.T("credits.audioText"));
            AddEntry(col, loc.T("credits.engine"), "Puzzle Studio · Unity");
            col.Add(Pz.MakeLabel(loc.T("credits.thanks"), $"pz-credits-thanks {Pz.Text} {Pz.Heading}"));
            scroll.Add(col);
            Root.Add(scroll);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
        }

        static void AddEntry(VisualElement col, string role, string names)
        {
            col.Add(Pz.MakeLabel(role, $"pz-credits-role {Pz.TextMuted}"));
            col.Add(Pz.MakeLabel(names, $"pz-credits-names {Pz.Text} {Pz.Heading}"));
        }

        public override VisualElement DefaultFocus => _back;
        public override bool OnBack() { Flow.ShowMenu(); return true; }
    }

    /// <summary>Shown once every level is completed.</summary>
    public sealed class EndScreen : GameScreen
    {
        readonly Label _stats;
        readonly StarElement _star;
        Button _menu;

        public EndScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-end");
            _star = new StarElement { Filled = true };
            _star.AddToClassList("pz-end-star");
            _star.SetColors(flow.Theme.Palette.Accent, Color.clear);
            _stats = Pz.MakeLabel("", $"pz-end-stats {Pz.Text}");
            _menu = Pz.MakeIconButton(Icon.Home, loc.T("pause.mainMenu"), Pz.Primary, flow.ShowMenu);
            var card = Pz.MakeBox($"{Pz.Card} {Pz.Surface} pz-end-card",
                _star,
                Pz.MakeLabel(loc.T("end.title"), $"pz-card-title {Pz.Text} {Pz.Heading}"),
                Pz.MakeLabel(loc.T("end.subtitle"), $"pz-end-subtitle {Pz.TextMuted}"),
                _stats,
                Pz.MakeBox("pz-card-buttons",
                    Pz.MakeIconButton(Icon.Info, loc.T("menu.credits"), Pz.Ghost, flow.ShowCredits),
                    Pz.MakeIconButton(Icon.Grid, loc.T("menu.levels"), Pz.Ghost, flow.ShowLevels),
                    _menu));
            Root.Add(card);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            flow.Theme.Apply(Root);
        }

        public override VisualElement DefaultFocus => _menu;
        public override bool OnBack() { Flow.ShowMenu(); return true; }

        public override void OnShow()
        {
            var pack = Flow.Pack;
            var p = Flow.Save.Progress;
            _stats.text = Flow.Loc.T("end.stats",
                ProgressRules.CompletedCount(pack, p), pack.levels.Count,
                ProgressRules.TotalStars(pack, p), pack.levels.Count * 3,
                Gameplay.GameSession.FormatTime(ProgressRules.TotalBestTime(pack, p)));
            UiAnim.Pop(_star, 0.6f, 0.2f);
            Flow.Audio?.PlaySfx("victory");
        }
    }
}
