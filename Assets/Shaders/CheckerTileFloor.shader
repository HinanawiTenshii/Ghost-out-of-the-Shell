Shader "Cogitans/Checker Tile Floor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _DarkTile ("Dark Tile", Color) = (0.105, 0.12, 0.235, 1)
        _LightTile ("Light Tile", Color) = (0.205, 0.22, 0.31, 1)
        _TileSize ("Tile Size (World Units)", Float) = 0.92
        _GridOrigin ("Grid Origin (World XY)", Vector) = (0, 0, 0, 0)
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
            fixed4 _Color, _DarkTile, _LightTile;
            float _TileSize;
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
                // One quad, no tile sprites, loops, animation or runtime allocations.
                // World-space coordinates keep tiles square under nonuniform scaling.
                float tileSize = max(_TileSize, 0.01);
                float2 grid = (input.worldXY - _GridOrigin.xy) / tileSize;
                float2 cell = floor(grid);
                float alternate = frac((cell.x + cell.y) * 0.5) * 2.0;
                fixed4 tile = lerp(_DarkTile, _LightTile, alternate);

                fixed4 result = tile * input.color;
                result.a *= tex2D(_MainTex, input.uv).a;
                return result;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
