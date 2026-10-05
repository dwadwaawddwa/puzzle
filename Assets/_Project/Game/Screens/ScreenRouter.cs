using System.Collections.Generic;
using PuzzleStudio.Game.UI;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>A full screen (page) or an overlay (pause, settings over gameplay...). Built in code.</summary>
    public abstract class GameScreen
    {
        public VisualElement Root { get; protected set; }
        protected readonly GameFlow Flow;

        protected GameScreen(GameFlow flow) { Flow = flow; }

        /// <summary>Called every time the screen becomes visible (refresh dynamic content here).</summary>
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        /// <summary>Back / Escape / gamepad B. Return true if handled.</summary>
        public virtual bool OnBack() => false;
        public virtual void Tick(float dt) { }

        /// <summary>Element focused when the screen opens (keyboard / gamepad navigation).</summary>
        public virtual VisualElement DefaultFocus => null;

        protected VisualElement MakeRoot(string classes)
        {
            var r = Pz.MakeBox($"{Pz.Root} pz-screen {classes}");
            return r;
        }
    }

    /// <summary>Shows one page at a time plus a stack of overlays, with fade/slide transitions.</summary>
    public sealed class ScreenRouter
    {
        readonly VisualElement _container;
        readonly List<GameScreen> _overlays = new List<GameScreen>();

        public GameScreen Page { get; private set; }
        public GameScreen Top => _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : Page;
        public bool HasOverlay => _overlays.Count > 0;

        public ScreenRouter(VisualElement container) { _container = container; }

        public void ShowPage(GameScreen screen)
        {
            CloseAllOverlays();
            var old = Page;
            if (old == screen) { screen.OnShow(); return; }
            Page = screen;
            if (old != null)
            {
                old.OnHide();
                var oldRoot = old.Root;
                oldRoot.pickingMode = PickingMode.Ignore;
                UiAnim.FadeOut(oldRoot, 0.15f, () => oldRoot.RemoveFromHierarchy());
            }
            Attach(screen);
            screen.OnShow();
            UiAnim.FadeIn(screen.Root, 0.28f, 14f);
            FocusDefault(screen);
        }

        public void PushOverlay(GameScreen screen)
        {
            if (_overlays.Contains(screen)) return;
            // Only the top overlay is visible: stacked translucent cards would show through each other.
            if (_overlays.Count > 0) _overlays[_overlays.Count - 1].Root.style.display = DisplayStyle.None;
            _overlays.Add(screen);
            Attach(screen);
            screen.OnShow();
            UiAnim.FadeIn(screen.Root, 0.22f, 10f);
            FocusDefault(screen);
        }

        public void PopOverlay()
        {
            if (_overlays.Count == 0) return;
            var top = _overlays[_overlays.Count - 1];
            _overlays.RemoveAt(_overlays.Count - 1);
            top.OnHide();
            var root = top.Root;
            UiAnim.FadeOut(root, 0.12f, () => root.RemoveFromHierarchy());
            if (_overlays.Count > 0) _overlays[_overlays.Count - 1].Root.style.display = DisplayStyle.Flex;
            if (Top != null) { Top.OnShow(); FocusDefault(Top); }
        }

        public void CloseAllOverlays()
        {
            while (_overlays.Count > 0)
            {
                var top = _overlays[_overlays.Count - 1];
                _overlays.RemoveAt(_overlays.Count - 1);
                top.OnHide();
                top.Root.style.display = DisplayStyle.Flex;
                top.Root.RemoveFromHierarchy();
            }
        }

        public bool IsOpen(GameScreen screen) => Page == screen || _overlays.Contains(screen);

        /// <summary>Escape / B: overlays first, then the page.</summary>
        public bool Back()
        {
            var top = Top;
            if (top == null) return false;
            if (top.OnBack()) return true;
            if (_overlays.Count > 0) { PopOverlay(); return true; }
            return false;
        }

        public void Tick(float dt)
        {
            Page?.Tick(dt);
            foreach (var o in _overlays) o.Tick(dt);
        }

        void Attach(GameScreen screen)
        {
            var root = screen.Root;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
            root.style.opacity = 1f;
            // Menus block the pointer normally; the gameplay HUD configures its own click-through picking.
            if (!(screen is GameplayScreen)) root.pickingMode = PickingMode.Position;
            _container.Add(root);
        }

        static void FocusDefault(GameScreen screen) =>
            screen.Root.schedule.Execute(() => screen.DefaultFocus?.Focus()).ExecuteLater(30);
    }
}
