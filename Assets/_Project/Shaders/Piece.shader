// Puzzle piece: samples a sub-rectangle (_UVRect) of the level texture on a unit quad,
// with SDF rounded corners, border, highlight rim and tint. One shared material,
// per-piece values come from a MaterialPropertyBlock.
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

            fixed4 frag (v2f i) : SV_Target
            {
                float2 hs = _Size.xy * 0.5;
                float2 p = (i.local - 0.5) * _Size.xy;
                float r = min(_Radius, min(hs.x, hs.y));
                float d = sdRoundBox(p, hs, r);
                float aa = max(fwidth(d), 1e-5);
                float alpha = _Softness > 0.0 ? 1.0 - smoothstep(-_Softness, _Softness, d) : saturate(0.5 - d / aa);

                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;

                // Border: band of width _Border just inside the edge.
                float borderMask = _Border > 0.0 ? saturate(0.5 + (d + _Border) / aa) : 0.0;
                col.rgb = lerp(col.rgb, _BorderColor.rgb, borderMask * _BorderColor.a);

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
