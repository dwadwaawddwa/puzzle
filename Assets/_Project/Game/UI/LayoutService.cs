using System.Collections.Generic;
using PuzzleStudio.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>
    /// Positions of the movable UI blocks (Studio "Layout" tab). Each block is absolutely positioned by its anchor
    /// point at a normalized screen position; the default positions reproduce the original layout.
    /// </summary>
    public static class LayoutService
    {
        public const string LayoutClass = "pz-layoutable";
        public const string BoardId = "board";

        public sealed class Slot
        {
            public string Id;
            public string Label;
            /// <summary>Point of the element (0..1 of its own size) placed at the position.</summary>
            public Vector2 Anchor;
            public Vector2 DefaultPosition;
            public float DefaultScale = 1f;
            public bool CanHide = true;
        }

        public static readonly Dictionary<string, Slot[]> Slots = new Dictionary<string, Slot[]>
        {
            {
                LayoutConfig.GameplayScreen, new[]
                {
                    new Slot { Id = "title", Label = "Level title", Anchor = new Vector2(0f, 0f), DefaultPosition = new Vector2(0.029f, 0.026f) },
                    new Slot { Id = "stats", Label = "Moves / time / pause", Anchor = new Vector2(1f, 0f), DefaultPosition = new Vector2(0.971f, 0.039f), CanHide = false },
                    new Slot { Id = "actions", Label = "Action buttons", Anchor = new Vector2(0.5f, 1f), DefaultPosition = new Vector2(0.5f, 0.97f), CanHide = false },
                    new Slot { Id = "decorLeft", Label = "Left decoration", Anchor = new Vector2(0.5f, 0.5f), DefaultPosition = new Vector2(0.085f, 0.55f) },
                    new Slot { Id = "decorRight", Label = "Right decoration", Anchor = new Vector2(0.5f, 0.5f), DefaultPosition = new Vector2(0.915f, 0.55f) },
                }
            },
            {
                LayoutConfig.MenuScreen, new[]
                {
                    new Slot { Id = "title", Label = "Title", Anchor = new Vector2(0f, 1f), DefaultPosition = new Vector2(0.078f, 0.40f) },
                    new Slot { Id = "buttons", Label = "Buttons", Anchor = new Vector2(0f, 0f), DefaultPosition = new Vector2(0.078f, 0.44f), CanHide = false },
                    new Slot { Id = "showcase", Label = "Picture cards", Anchor = new Vector2(0.5f, 0.5f), DefaultPosition = new Vector2(0.74f, 0.5f) },
                    new Slot { Id = "decorLeft", Label = "Left decoration", Anchor = new Vector2(0.5f, 1f), DefaultPosition = new Vector2(0.42f, 0.96f), DefaultScale = 0.75f },
                    new Slot { Id = "decorRight", Label = "Right decoration", Anchor = new Vector2(1f, 1f), DefaultPosition = new Vector2(0.985f, 0.985f), DefaultScale = 0.6f },
                }
            },
        };

        public static Slot FindSlot(string screen, string id)
        {
            if (!Slots.TryGetValue(screen, out var slots)) return null;
            foreach (var s in slots) if (s.Id == id) return s;
            return null;
        }

        /// <summary>Marks a block as movable; it becomes absolutely positioned.</summary>
        public static T Tag<T>(T e, string id) where T : VisualElement
        {
            e.name = id;
            e.AddToClassList(LayoutClass);
            e.style.position = Position.Absolute;
            return e;
        }

        /// <summary>Places every tagged block of a screen root (default position unless overridden).</summary>
        public static void Apply(VisualElement root, string screen, LayoutConfig config)
        {
            if (!Slots.TryGetValue(screen, out var slots)) return;
            foreach (var slot in slots)
            {
                var e = root.Q(slot.Id, LayoutClass);
                if (e == null) continue;
                var item = config?.Get(screen, slot.Id);
                Vector2 pos = item != null ? new Vector2(item.x, item.y) : slot.DefaultPosition;
                float scale = item?.scale ?? slot.DefaultScale;
                e.style.left = Length.Percent(pos.x * 100f);
                e.style.top = Length.Percent(pos.y * 100f);
                e.style.translate = new Translate(Length.Percent(-slot.Anchor.x * 100f), Length.Percent(-slot.Anchor.y * 100f));
                e.style.transformOrigin = new TransformOrigin(Length.Percent(slot.Anchor.x * 100f), Length.Percent(slot.Anchor.y * 100f));
                e.style.scale = new Scale(new Vector3(scale, scale, 1f));
                e.style.display = item == null || item.visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Normalized area (origin top-left) the puzzle board is fitted into.</summary>
        public static Rect BoardArea(GamePackData pack, float screenAspect, float uiScale)
        {
            var o = pack?.layout?.boardArea;
            if (o != null && o.w > 0.05f && o.h > 0.05f) return new Rect(o.x, o.y, o.w, o.h);
            bool decor = pack != null && (!string.IsNullOrEmpty(pack.theme.background.decorLeft) || !string.IsNullOrEmpty(pack.theme.background.decorRight));
            return DefaultBoardArea(screenAspect, uiScale, decor);
        }

        public static Rect DefaultBoardArea(float screenAspect, float uiScale, bool hasDecorations)
        {
            float refWidth = 1080f * Mathf.Max(0.5f, screenAspect);
            float side = 70f / refWidth;
            if (hasDecorations) side = Mathf.Max(side, 0.17f);
            float top = 150f * uiScale / 1080f, bottom = 150f * uiScale / 1080f;
            return new Rect(side, top, 1f - 2f * side, 1f - top - bottom);
        }
    }
}
