// Puzzle piece: samples a sub-rectangle (_UVRect) of the level texture on a unit quad,
// with SDF rounded corners, border, highlight rim and tint. One shared material,
// per-piece values come from a MaterialPropertyBlock.
// Memory cards add a patterned card back, a colored outline and a round badge behind the symbol.
Shader "PuzzleStudio/Piece"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _UVRect ("UV Rect (x, y, w, h)", Vector) = (0, 0, 1, 1)
        _Size ("Size in world units (w, h)", Vector) = (1, 1, 0, 0)
        _Radius ("Corner radius (world)", Float) = 0.08
        _Border ("Border width (world)", Float) = 0.02
        _BorderColor ("Border color", Color) = (1, 1, 1, 1)
        _Highlight ("Highlight (rgb, amount in a)", Color) = (1, 0.8, 0.3, 0)
        _HighlightWidth ("Highlight ring width (world)", Float) = 0.05
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge softness (world, shadows)", Float) = 0
        _Hollow ("Outline only (board cursor)", Float) = 0
        _Back ("Card back (Memory)", Float) = 0
        _BackColor ("Card back color", Color) = (0.3, 0.4, 0.8, 1)
        _BackColor2 ("Card back pattern color", Color) = (0.5, 0.6, 1, 1)
        _Outline ("Card outline color (a = amount)", Color) = (0, 0, 0, 0)
        _OutlineWidth ("Card outline width (world)", Float) = 0
        _Badge ("Symbol badge color (a = amount)", Color) = (0, 0, 0, 0)
        _BadgeRadius ("Symbol badge radius (world)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _UVRect;
            float4 _Size;
            float _Radius;
            float _Border;
            fixed4 _BorderColor;
            fixed4 _Highlight;
            float _HighlightWidth;
            fixed4 _Tint;
            float _Softness;
            float _Hollow;
            float _Back;
            fixed4 _BackColor;
            fixed4 _BackColor2;
            fixed4 _Outline;
            float _OutlineWidth;
            fixed4 _Badge;
            float _BadgeRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 local : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.uv;
                o.uv = _UVRect.xy + v.uv * _UVRect.zw;
                return o;
            }

            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Card back: soft vertical gradient, diagonal lattice, inner frame and a diamond in the middle.
            fixed3 cardBack(float2 p, float2 hs, float r, float aa)
            {
                float m = min(hs.x, hs.y);
                float2 q = p / m;                                   // -1..1 across the short side
                fixed3 baseCol = _BackColor.rgb * (1.0 + 0.10 * q.y);
                fixed3 c2 = _BackColor2.rgb;

                float inset = m * 0.16;
                float dIn = sdRoundBox(p, hs - inset, max(r - inset, m * 0.06));
                float inside = saturate(0.5 - dIn / aa);

                float2 g = float2(q.x + q.y, q.x - q.y) * 2.6;
                float dl = min(abs(frac(g.x + 0.5) - 0.5), abs(frac(g.y + 0.5) - 0.5));
                float lattice = saturate(0.5 + (0.05 - dl) / max(fwidth(dl), 1e-5));
                fixed3 col = lerp(baseCol, c2, lattice * inside * 0.32);

                float frame = saturate(0.5 + (m * 0.022 - abs(dIn)) / aa);
                col = lerp(col, c2, frame * 0.9);

                float e = abs(q.x) + abs(q.y);
                float ae = max(fwidth(e), 1e-5);
                float diamond = saturate(0.5 + (0.30 - e) / ae);
                float hole = saturate(0.5 + (0.17 - e) / ae);
                col = lerp(col, c2, diamond);
                col = lerp(col, baseCol * 1.06, hole);
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 hs = _Size.xy * 0.5;
                float2 p = (i.local - 0.5) * _Size.xy;
                float r = min(_Radius, min(hs.x, hs.y));
                float d = sdRoundBox(p, hs, r);
                float aa = max(fwidth(d), 1e-5);
                float alpha = _Softness > 0.0 ? 1.0 - smoothstep(-_Softness, _Softness, d) : saturate(0.5 - d / aa);

                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;
                if (_Back > 0.5) col.rgb = cardBack(p, hs, r, aa) * _Tint.rgb;

                // Border: band of width _Border just inside the edge.
                float borderMask = _Border > 0.0 ? saturate(0.5 + (d + _Border) / aa) : 0.0;
                col.rgb = lerp(col.rgb, _BorderColor.rgb, borderMask * _BorderColor.a);

                // Memory "Colors" cards: thick colored outline, with dark hairlines on both sides so white
                // or black outlines stay visible on any picture and any background.
                if (_OutlineWidth > 0.0 && _Outline.a > 0.0)
                {
                    float w = _OutlineWidth;
                    float band = saturate(0.5 + (d + w) / aa);
                    float inner = saturate(0.5 + (d + w * 1.16) / aa) - band;
                    float outer = saturate(0.5 + (d + w * 0.12) / aa);
                    col.rgb = lerp(col.rgb, col.rgb * 0.45, inner * _Outline.a);
                    col.rgb = lerp(col.rgb, _Outline.rgb, band * _Outline.a);
                    col.rgb = lerp(col.rgb, _Outline.rgb * 0.55, outer * _Outline.a * 0.6);
                }

                // Round badge behind the card's number / letter (the text itself is a TextMesh above).
                if (_BadgeRadius > 0.0 && _Badge.a > 0.0)
                {
                    float dc = length(p) - _BadgeRadius;
                    float ac = max(fwidth(dc), 1e-5);
                    float disc = saturate(0.5 - dc / ac);
                    float rim = saturate(0.5 + (_BadgeRadius * 0.07 - abs(dc + _BadgeRadius * 0.035)) / ac);
                    col.rgb = lerp(col.rgb, _Badge.rgb, disc * _Badge.a);
                    col.rgb = lerp(col.rgb, fixed3(1, 1, 1), rim * 0.75 * saturate(_Badge.a * 1.5));
                }

                // Highlight: crisp ring along the edge + light wash, both scaled by the amount.
                float ring = saturate(0.5 + (d + _HighlightWidth) / aa);
                float amount = saturate(_Highlight.a);
                col.rgb = lerp(col.rgb, col.rgb + (1.0 - col.rgb) * 0.35, amount * 0.45);
                col.rgb = lerp(col.rgb, _Highlight.rgb, ring * saturate(amount * 1.6));

                // Board cursor: only the border band, in the border color.
                if (_Hollow > 0.5)
                    col = fixed4(_BorderColor.rgb, borderMask * _BorderColor.a);

                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
