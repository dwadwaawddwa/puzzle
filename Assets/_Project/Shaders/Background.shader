// Full-screen background behind the board: solid / gradient / animated gradient, optional pattern
// (dots, stripes, grid, waves) and vignette. Everything is procedural (no texture).
Shader "PuzzleStudio/Background"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.96, 0.94, 0.9, 1)
        _ColorB ("Color B", Color) = (0.92, 0.86, 0.78, 1)
        _Angle ("Gradient angle (degrees)", Float) = 135
        _Animate ("Animate (0/1)", Float) = 0
        _Speed ("Animation speed", Float) = 0.2
        _Pattern ("Pattern (0 none, 1 dots, 2 stripes, 3 grid, 4 waves)", Float) = 0
        _PatternColor ("Pattern color", Color) = (0, 0, 0, 1)
        _PatternOpacity ("Pattern opacity", Float) = 0.06
        _PatternScale ("Pattern scale", Float) = 1
        _Vignette ("Vignette", Float) = 0.15
        _Aspect ("Screen aspect", Float) = 1.7777
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            fixed4 _ColorA, _ColorB, _PatternColor;
            float _Angle, _Animate, _Speed, _Pattern, _PatternOpacity, _PatternScale, _Vignette, _Aspect;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float band(float x, float center, float halfWidth, float aa)
            {
                return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(x - center));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0);
                float tm = _Time.y * _Speed * _Animate;

                // ---- gradient
                float a = radians(_Angle);
                float2 dir = float2(cos(a), -sin(a));
                float extent = 0.5 * (abs(dir.x) * _Aspect + abs(dir.y));
                float t = dot(p, dir) / max(extent, 1e-3) * 0.5 + 0.5;
                if (_Animate > 0.5)
                {
                    t += 0.10 * sin(tm * 1.7 + p.x * 1.3) + 0.07 * sin(tm * 1.1 + p.y * 2.3);
                }
                fixed3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(t));
                if (_Animate > 0.5)
                {
                    // Two slow soft light blobs drifting around.
                    float2 c1 = float2(sin(tm * 0.6) * 0.45 * _Aspect, cos(tm * 0.45) * 0.3);
                    float2 c2 = float2(cos(tm * 0.38 + 1.7) * 0.5 * _Aspect, sin(tm * 0.52 + 0.4) * 0.35);
                    float b1 = 1.0 - smoothstep(0.0, 0.75, length(p - c1));
                    float b2 = 1.0 - smoothstep(0.0, 0.65, length(p - c2));
                    col = lerp(col, _ColorB.rgb, b1 * 0.35);
                    col = lerp(col, _ColorA.rgb, b2 * 0.30);
                }

                // ---- pattern (about 22 cells per screen height at scale 1)
                float mask = 0.0;
                float2 q = p * 22.0 / max(_PatternScale, 0.1);
                float aa = fwidth(q.y) * 0.75;
                if (_Pattern > 0.5 && _Pattern < 1.5)          // dots
                {
                    float2 cell = frac(q + float2(0.5 * floor(q.y), 0.0)) - 0.5;
                    float d = length(cell) - 0.13;
                    mask = 1.0 - smoothstep(-aa, aa, d);
                }
                else if (_Pattern > 1.5 && _Pattern < 2.5)     // diagonal stripes
                {
                    float s = frac((q.x + q.y) * 0.35 + tm * 0.2);
                    mask = band(s, 0.5, 0.18, aa * 0.5);
                }
                else if (_Pattern > 2.5 && _Pattern < 3.5)     // grid lines
                {
                    float2 g = abs(frac(q * 0.5) - 0.5);
                    float edgeDist = 0.5 - max(g.x, g.y);
                    mask = 1.0 - smoothstep(0.02, 0.02 + aa, edgeDist);
                }
                else if (_Pattern > 3.5)                       // waves
                {
                    float w = q.y * 0.5 + sin(q.x * 0.35 + tm * 1.5) * 0.6;
                    mask = band(frac(w), 0.5, 0.07, aa * 0.6);
                }
                col = lerp(col, _PatternColor.rgb, mask * _PatternOpacity);

                // ---- vignette
                float v = smoothstep(0.45, 1.05, length((i.uv - 0.5) * float2(1.15, 1.35)) * 1.25);
                col *= 1.0 - v * _Vignette;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
