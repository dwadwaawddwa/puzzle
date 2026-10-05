using PuzzleStudio.Core.Steam;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Screens
{
    /// <summary>Every achievement with its state (also useful outside Steam: itch.io builds, offline play).</summary>
    public sealed class AchievementsScreen : GameScreen
    {
        readonly VisualElement _list;
        readonly Label _counter;
        readonly Button _back;

        public AchievementsScreen(GameFlow flow) : base(flow)
        {
            var loc = flow.Loc;
            Root = MakeRoot("pz-achievements");
            _back = Pz.MakeIconButton(Icon.Back, null, Pz.Ghost, flow.ShowMenu);
            _back.tooltip = loc.T("button.back");
            _counter = Pz.MakeLabel("", $"pz-levels-counter {Pz.Text} {Pz.Heading}");
            Root.Add(Pz.MakeBox("pz-screen-header",
                _back,
                Pz.MakeLabel(loc.T("achievements.title"), $"pz-screen-title {Pz.Text} {Pz.Heading}"),
                Pz.MakeBox("pz-spacer"),
                Pz.MakeBox($"{Pz.Pill} {Pz.Surface} pz-levels-progress", _counter)));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pz-levels-scroll");
            _list = Pz.MakeBox("pz-ach-list");
            scroll.Add(_list);
            Root.Add(scroll);
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
        }

        public override VisualElement DefaultFocus => _back;
        public override bool OnBack() { Flow.ShowMenu(); return true; }

        public override void OnShow()
        {
            var loc = Flow.Loc;
            var service = Flow.Achievements;
            var palette = Flow.Theme.Palette;
            _counter.text = loc.T("achievements.count", service.UnlockedCount, service.All.Count);
            _list.Clear();

            foreach (var def in service.All)
            {
                bool unlocked = service.IsUnlocked(def);
                bool secret = def.hidden && !unlocked;

                var icon = new IconElement(unlocked ? Icon.Trophy : Icon.Lock)
                {
                    Color = unlocked ? palette.OnPrimary : palette.TextMuted,
                };
                icon.AddToClassList("pz-ach-icon");
                var badge = Pz.MakeBox("pz-ach-badge", icon);
                badge.style.backgroundColor = unlocked ? palette.Primary : palette.TextMuted.WithAlpha(0.14f);

                var texts = Pz.MakeBox("pz-ach-texts",
                    Pz.MakeLabel(secret ? loc.T("achievements.hidden.name") : service.NameOf(def, loc), $"pz-ach-name {Pz.Text} {Pz.Heading}"),
                    Pz.MakeLabel(secret ? loc.T("achievements.hidden.desc") : service.DescriptionOf(def, loc), $"pz-ach-desc {Pz.TextMuted}"));

                var row = Pz.MakeBox($"pz-ach-row {Pz.Surface}", badge, texts);
                if (!unlocked) row.AddToClassList("pz-ach-row--locked");

                var (current, target) = AchievementTracker.ProgressOf(def, Flow.Pack, Flow.Save.Progress);
                if (!unlocked && !secret && target > 1)
                    row.Add(Pz.MakeLabel(loc.T("achievements.progress", current, target), $"pz-ach-progress {Pz.TextMuted}"));
                else if (unlocked)
                {
                    var check = new IconElement(Icon.Check) { Color = palette.Success };
                    check.AddToClassList("pz-ach-check");
                    row.Add(check);
                }
                _list.Add(row);
            }

            Flow.Theme.Apply(Root);
            int k = 0;
            foreach (var c in _list.Children()) { if (k < 12) UiAnim.FadeIn(c, 0.3f, 12f + k * 2f); k++; }
        }
    }
}
