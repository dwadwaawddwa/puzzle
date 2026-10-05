using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    public enum Icon { None, Pause, Play, Back, Restart, Undo, Hint, Eye, Gear, Home, Lock, Close, Grid, Next, Check, Info }

    /// <summary>Simple vector icons drawn with Painter2D (crisp at any scale, colored by the theme).</summary>
    public sealed class IconElement : VisualElement
    {
        Icon _icon;
        Color _color = Color.white;

        public IconElement(Icon icon)
        {
            _icon = icon;
            AddToClassList("pz-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public Icon Icon
        {
            get => _icon;
            set { _icon = value; MarkDirtyRepaint(); }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; MarkDirtyRepaint(); }
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 2 || _icon == Icon.None) return;
            var p = mgc.painter2D;
            float s = Mathf.Min(r.width, r.height);
            Vector2 c = r.center;
            float u = s / 24f; // design grid: 24 × 24
            Vector2 P(float x, float y) => c + new Vector2((x - 12) * u, (y - 12) * u);

            p.strokeColor = _color;
            p.fillColor = _color;
            p.lineWidth = 2.2f * u;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            switch (_icon)
            {
                case Icon.Pause:
                    Rect(p, P(6.5f, 5), P(10, 19), 1.2f * u);
                    Rect(p, P(14, 5), P(17.5f, 19), 1.2f * u);
                    break;
                case Icon.Play:
                case Icon.Next:
                    p.BeginPath(); p.MoveTo(P(8, 5)); p.LineTo(P(19, 12)); p.LineTo(P(8, 19)); p.ClosePath(); p.Fill();
                    break;
                case Icon.Back:
                    p.BeginPath(); p.MoveTo(P(15, 5)); p.LineTo(P(8, 12)); p.LineTo(P(15, 19)); p.Stroke();
                    break;
                case Icon.Restart:
                    p.BeginPath(); p.Arc(P(12, 12), 7 * u, 40f, 330f); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(19.5f, 4.5f)); p.LineTo(P(19.5f, 9.8f)); p.LineTo(P(14.3f, 9.8f)); p.Stroke();
                    break;
                case Icon.Undo:
                    p.BeginPath(); p.MoveTo(P(5, 10)); p.LineTo(P(14, 10));
                    p.ArcTo(P(20, 10), P(20, 16), 5 * u); p.LineTo(P(20, 15)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(9, 6)); p.LineTo(P(5, 10)); p.LineTo(P(9, 14)); p.Stroke();
                    break;
                case Icon.Hint:
                    p.BeginPath(); p.Arc(P(12, 10), 6 * u, 140f, 400f); p.LineTo(P(14.5f, 17)); p.LineTo(P(9.5f, 17)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(10, 20.5f)); p.LineTo(P(14, 20.5f)); p.Stroke();
                    break;
                case Icon.Eye:
                    p.BeginPath(); p.MoveTo(P(3, 12)); p.QuadraticCurveTo(P(12, 3), P(21, 12)); p.QuadraticCurveTo(P(12, 21), P(3, 12)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.Arc(P(12, 12), 3 * u, 0f, 360f); p.Fill();
                    break;
                case Icon.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.BeginPath(); p.MoveTo(c + dir * 6.5f * u); p.LineTo(c + dir * 10f * u); p.Stroke();
                    }
                    p.BeginPath(); p.Arc(P(12, 12), 6.5f * u, 0f, 360f); p.Stroke();
                    p.BeginPath(); p.Arc(P(12, 12), 2.5f * u, 0f, 360f); p.Stroke();
                    break;
                case Icon.Home:
                    p.BeginPath(); p.MoveTo(P(4, 11)); p.LineTo(P(12, 4)); p.LineTo(P(20, 11)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(6.5f, 10)); p.LineTo(P(6.5f, 19.5f)); p.LineTo(P(17.5f, 19.5f)); p.LineTo(P(17.5f, 10)); p.Stroke();
                    break;
                case Icon.Lock:
                    p.BeginPath(); p.Arc(P(12, 10), 4.5f * u, 180f, 360f); p.LineTo(P(16.5f, 11)); p.MoveTo(P(7.5f, 10)); p.LineTo(P(7.5f, 11)); p.Stroke();
                    Rect(p, P(5, 11), P(19, 21), 2f * u);
                    break;
                case Icon.Close:
                    p.BeginPath(); p.MoveTo(P(6, 6)); p.LineTo(P(18, 18)); p.MoveTo(P(18, 6)); p.LineTo(P(6, 18)); p.Stroke();
                    break;
                case Icon.Grid:
                    for (int y = 0; y < 2; y++)
                        for (int x = 0; x < 2; x++)
                            Rect(p, P(4.5f + x * 8.5f, 4.5f + y * 8.5f), P(11 + x * 8.5f, 11 + y * 8.5f), 1.5f * u);
                    break;
                case Icon.Check:
                    p.BeginPath(); p.MoveTo(P(5, 12.5f)); p.LineTo(P(10, 17.5f)); p.LineTo(P(19, 7)); p.Stroke();
                    break;
                case Icon.Info:
                    p.BeginPath(); p.Arc(P(12, 12), 9 * u, 0f, 360f); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(12, 11)); p.LineTo(P(12, 17)); p.Stroke();
                    p.BeginPath(); p.Arc(P(12, 7.5f), 1.2f * u, 0f, 360f); p.Fill();
                    break;
            }
        }

        static void Rect(Painter2D p, Vector2 a, Vector2 b, float radius)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(a.x + radius, a.y));
            p.ArcTo(new Vector2(b.x, a.y), new Vector2(b.x, b.y), radius);
            p.ArcTo(new Vector2(b.x, b.y), new Vector2(a.x, b.y), radius);
            p.ArcTo(new Vector2(a.x, b.y), new Vector2(a.x, a.y), radius);
            p.ArcTo(new Vector2(a.x, a.y), new Vector2(b.x, a.y), radius);
            p.ClosePath();
            p.Fill();
        }
    }
}
