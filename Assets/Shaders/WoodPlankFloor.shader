Shader "Cogitans/Wood Plank Floor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WoodColor ("Wood", Color) = (0.155, 0.17, 0.2725, 1)
        _DarkWood ("Dark Board", Color) = (0.105, 0.12, 0.235, 1)
        _LightWood ("Light Board", Color) = (0.205, 0.22, 0.31, 1)
        _PlankLength ("Plank Length (World Units)", Float) = 1.84
        _PlankWidth ("Plank Width (World Units)", Float) = 0.46
        [Enum(Horizontal,0,Vertical,1)] _Direction ("Board Direction (World XY)", Float) = 0
        [HideInInspector] [PerRendererData] _DirectionOverride ("Per Object Direction", Float) = -1
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
            fixed4 _Color, _WoodColor, _DarkWood, _LightWood;
            float _PlankLength, _PlankWidth, _Direction, _DirectionOverride;
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
            fixed4 BoardColor(float2 grid)
            {
                float row = floor(grid.y);
                // Half-length stagger; floor/frac also align boards at negative world coordinates.
                float offset = frac(row * 0.5);
                float column = floor(grid.x + offset);
                // Adjacent rows differ on both sides of every staggered joint,
                // so equal-color neighbors never merge into staircase-shaped patches.
                float index = column + offset * 2.0;
                index -= 3.0 * floor(index / 3.0);
                fixed4 wood = lerp(_WoodColor, _DarkWood, step(0.5, index));
                return lerp(wood, _LightWood, step(1.5, index));
            }
            fixed4 frag(v2f input) : SV_Target
            {
                // Like Checker Tile Floor: one sprite, world-sized boards, no texture or extra geometry.
                float2 p = input.worldXY - _GridOrigin.xy;
                float direction = _DirectionOverride >= 0.0 ? _DirectionOverride : _Direction;
                if (direction > 0.5) p = p.yx;
                float2 grid = p / max(float2(_PlankLength, _PlankWidth), float2(0.01, 0.01));
                float2 dx = ddx(grid) * 0.25;
                float2 dy = ddy(grid) * 0.25;
                // Filter just the color boundaries: no outlines, grain, nails or drawn seams.
                fixed4 wood = (BoardColor(grid + dx + dy) + BoardColor(grid + dx - dy)
                    + BoardColor(grid - dx + dy) + BoardColor(grid - dx - dy)) * 0.25;
                float2 footprint = fwidth(grid);
                fixed4 average = (_WoodColor + _DarkWood + _LightWood) / 3.0;
                wood = lerp(wood, average, smoothstep(0.35, 0.9, max(footprint.x, footprint.y)));
                fixed4 result = wood * input.color;
                result.a *= tex2D(_MainTex, input.uv).a;
                return result;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
