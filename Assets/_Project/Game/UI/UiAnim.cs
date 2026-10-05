using System;
using Easing = PuzzleStudio.Core.Util.Easing;
using Ease = PuzzleStudio.Core.Util.Ease;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace PuzzleStudio.Game.UI
{
    /// <summary>
    /// Small UI animations built on UI Toolkit's experimental animation API. All durations are scaled by the
    /// theme animation speed and become instant when the player enabled "reduce motion".
    /// </summary>
    public static class UiAnim
    {
        public static float Speed = 1f;
        public static bool ReduceMotion;

        static int Ms(float seconds) => ReduceMotion ? 0 : Mathf.Max(1, Mathf.RoundToInt(seconds * 1000f / Mathf.Max(0.1f, Speed)));

        public static void FadeIn(VisualElement e, float seconds = 0.25f, float fromY = 18f, Action done = null)
        {
            e.style.opacity = 0f;
            e.style.translate = new Translate(0, ReduceMotion ? 0 : fromY);
            int ms = Ms(seconds);
            if (ms == 0) { e.style.opacity = 1f; e.style.translate = new Translate(0, 0); done?.Invoke(); return; }
            e.experimental.animation.Start(0f, 1f, ms, (el, t) =>
            {
                float k = Easing.Evaluate(Ease.OutCubic, t);
                el.style.opacity = k;
                el.style.translate = new Translate(0, fromY * (1f - k));
            }).OnCompleted(() => done?.Invoke());
        }

        public static void FadeOut(VisualElement e, float seconds = 0.18f, Action done = null)
        {
            int ms = Ms(seconds);
            if (ms == 0) { e.style.opacity = 0f; done?.Invoke(); return; }
            float from = e.resolvedStyle.opacity;
            e.experimental.animation.Start(0f, 1f, ms, (el, t) => el.style.opacity = from * (1f - t))
                .OnCompleted(() => done?.Invoke());
        }

        /// <summary>Scale "pop" from 0 with overshoot (stars, badges).</summary>
        public static void Pop(VisualElement e, float seconds = 0.35f, float delay = 0f)
        {
            int ms = Ms(seconds);
            if (ms == 0) { e.style.scale = new Scale(Vector3.one); e.style.opacity = 1f; return; }
            e.style.scale = new Scale(Vector3.zero);
            e.style.opacity = 0f;
            e.schedule.Execute(() =>
            {
                e.style.opacity = 1f;
                e.experimental.animation.Start(0f, 1f, ms, (el, t) =>
                {
                    float k = Easing.Evaluate(Ease.OutBack, t);
                    el.style.scale = new Scale(new Vector3(k, k, 1f));
                });
            }).ExecuteLater(ReduceMotion ? 0 : (long)(delay * 1000f / Mathf.Max(0.1f, Speed)));
        }

        /// <summary>Little horizontal shake (locked level, invalid action).</summary>
        public static void Shake(VisualElement e)
        {
            if (ReduceMotion) return;
            e.experimental.animation.Start(0f, 1f, 360, (el, t) =>
            {
                float x = Mathf.Sin(t * Mathf.PI * 6f) * 10f * (1f - t);
                el.style.translate = new Translate(x, 0);
            });
        }
    }
}
