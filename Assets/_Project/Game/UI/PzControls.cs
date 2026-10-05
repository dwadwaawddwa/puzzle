using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>Game-styled on/off switch (theme colors, keyboard/gamepad friendly).</summary>
    public sealed class PzToggle : VisualElement
    {
        readonly VisualElement _track, _knob;
        bool _value;
        public event Action<bool> OnChanged;
        Color _on = Color.green, _off = Color.gray, _knobColor = Color.white;

        public PzToggle(bool value)
        {
            AddToClassList("pz-toggle");
            focusable = true;
            _track = new VisualElement();
            _track.AddToClassList("pz-toggle__track");
            _knob = new VisualElement();
            _knob.AddToClassList("pz-toggle__knob");
            _track.Add(_knob);
            Add(_track);
            RegisterCallback<ClickEvent>(_ => Set(!_value, true));
            RegisterCallback<NavigationSubmitEvent>(_ => Set(!_value, true));
            Set(value, false);
        }

        public bool Value => _value;

        public void SetColors(Color on, Color off, Color knob)
        {
            _on = on; _off = off; _knobColor = knob;
            Refresh();
        }

        public void Set(bool value, bool notify)
        {
            _value = value;
            Refresh();
            if (notify) OnChanged?.Invoke(value);
        }

        void Refresh()
        {
            _track.style.backgroundColor = _value ? _on : _off;
            _knob.style.backgroundColor = _knobColor;
            _knob.EnableInClassList("pz-toggle__knob--on", _value);
        }
    }

    /// <summary>"‹ value ›" selector — friendlier than a dropdown in games and with a gamepad.</summary>
    public sealed class PzSelector : VisualElement
    {
        readonly Label _label;
        readonly Button _prev, _next;
        List<string> _choices;
        int _index;
        public event Action<int> OnChanged;

        public PzSelector(List<string> choices, int index)
        {
            AddToClassList("pz-selector");
            focusable = true;
            _prev = new Button(() => Step(-1)) { text = "‹" };
            _next = new Button(() => Step(+1)) { text = "›" };
            foreach (var b in new[] { _prev, _next })
            {
                b.RemoveFromClassList(Button.ussClassName);
                b.AddToClassList("pz-selector__arrow");
                b.focusable = false;
            }
            _label = new Label();
            _label.AddToClassList("pz-selector__value");
            Add(_prev);
            Add(_label);
            Add(_next);
            RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction == NavigationMoveEvent.Direction.Left) { Step(-1); e.StopPropagation(); }
                else if (e.direction == NavigationMoveEvent.Direction.Right) { Step(+1); e.StopPropagation(); }
            });
            SetChoices(choices, index);
        }

        public int Index => _index;
        public VisualElement PrevButton => _prev;
        public VisualElement NextButton => _next;

        public void SetChoices(List<string> choices, int index)
        {
            _choices = choices ?? new List<string>();
            _index = Mathf.Clamp(index, 0, Math.Max(0, _choices.Count - 1));
            Refresh();
        }

        void Step(int delta)
        {
            if (_choices.Count == 0) return;
            _index = (_index + delta + _choices.Count) % _choices.Count;
            Refresh();
            OnChanged?.Invoke(_index);
        }

        void Refresh() => _label.text = _choices.Count > 0 ? _choices[_index] : "—";
    }

    /// <summary>Lock badge drawn over locked level cards.</summary>
    public sealed class LockElement : VisualElement
    {
        public LockElement(Color color)
        {
            AddToClassList("pz-lock");
            pickingMode = PickingMode.Ignore;
            var icon = new IconElement(Icon.Lock) { Color = color };
            icon.AddToClassList("pz-lock__icon");
            Add(icon);
        }
    }
}
