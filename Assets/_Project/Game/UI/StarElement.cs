using UnityEngine;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.UI
{
    /// <summary>Vector star drawn with Painter2D (no font glyph or texture needed).</summary>
    public sealed class StarElement : VisualElement
    {
        Color _fill = Color.yellow;
        Color _empty = new Color(0, 0, 0, 0.15f);
        bool _filled;

        public StarElement()
        {
            AddToClassList("pz-star");
            generateVisualContent += Draw;
        }

        public bool Filled
        {
            get => _filled;
            set { _filled = value; MarkDirtyRepaint(); }
        }

        public void SetColors(Color fill, Color empty)
        {
            _fill = fill;
            _empty = empty;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            var p = mgc.painter2D;
            Vector2 c = r.center + new Vector2(0, r.height * 0.04f);
            float outer = Mathf.Min(r.width, r.height) * 0.5f;
            float inner = outer * 0.48f;

            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0) ? outer * 0.92f : inner;
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.fillColor = _filled ? _fill : _empty;
            p.Fill();
            p.lineWidth = outer * 0.12f;
            p.strokeColor = _filled ? _fill : _empty;
            p.Stroke();
        }
    }
}
