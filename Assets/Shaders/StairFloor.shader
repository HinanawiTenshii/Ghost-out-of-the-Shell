Shader "Cogitans/Stair Floor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TreadColor ("Stone Tread", Color) = (0.235, 0.255, 0.40, 1)
        _RiserColor ("Riser Shadow", Color) = (0.125, 0.15, 0.26, 1)
        _EdgeColor ("Step Edge", Color) = (0.345, 0.375, 0.52, 1)
        [Enum(Up,0,Down,1,Left,2,Right,3)] _Direction ("Ascent Direction (World XY)", Float) = 0
        [HideInInspector] [PerRendererData] _StairDirectionOverride ("Per Object Ascent Direction", Float) = -1
        _StepDepth ("Step Depth (World Units)", Float) = 0.6
        _RiserWidth ("Riser Fraction", Range(0.05, 0.35)) = 0.12
        _EdgeWidth ("Edge Fraction", Range(0.02, 0.15)) = 0.045
        _GridOrigin ("Pattern Origin (World XY)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color, _TreadColor, _RiserColor, _EdgeColor;
            float _Direction, _StairDirectionOverride, _StepDepth, _RiserWidth, _EdgeWidth;
            float4 _GridOrigin;
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
            };
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldXY = mul(unity_ObjectToWorld, input.vertex).xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                // Same single-sprite world-space pattern approach as Checker Tile Floor.
                // Direction is the ascent axis; step edges run perpendicular to it.
                float2 p = input.worldXY - _GridOrigin.xy;
                float along = p.y;
                float direction = _StairDirectionOverride >= 0.0 ? _StairDirectionOverride : _Direction;
                if (direction > 2.5) along = p.x;
                else if (direction > 1.5) along = -p.x;
                else if (direction > 0.5) along = -p.y;
                float grid = along / max(_StepDepth, 0.01);
                float phase = frac(grid);
                float riser = clamp(_RiserWidth, 0.05, 0.35);
                float edge = clamp(_EdgeWidth, 0.02, 0.15);
                float aa = max(fwidth(grid), 0.0001);
                float treadStart = riser + edge;
                fixed4 stair = lerp(_RiserColor, _EdgeColor,
                    smoothstep(riser - aa, riser + aa, phase));
                stair = lerp(stair, _TreadColor,
                    smoothstep(treadStart - aa, treadStart + aa, phase));
                // Fade subpixel repetition to its average to avoid distant CRT moire.
                fixed4 average = _RiserColor * riser + _EdgeColor * edge
                    + _TreadColor * (1.0 - treadStart);
                stair = lerp(stair, average, smoothstep(0.3, 0.8, aa));
                fixed4 result = stair * input.color;
                result.a *= tex2D(_MainTex, input.uv).a;
                return result;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
