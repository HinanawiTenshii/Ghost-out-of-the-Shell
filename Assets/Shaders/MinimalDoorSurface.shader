Shader "Cogitans/Minimal Door Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        // Local unit-square coordinates are deliberate: no extra geometry,
        // padding, glow or changed renderer bounds. Disable batching so Unity
        // cannot bake local positions into world space before this vertex stage.
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "IgnoreProjector"="True" "PreviewType"="Plane"
               "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            struct door_v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 local : TEXCOORD1;
                float light : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            door_v2f vert(appdata_t input)
            {
                door_v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 position = UnityFlipSprite(input.vertex, _Flip);
                output.vertex = UnityObjectToClipPos(position);
                output.uv = input.texcoord;
                output.local = input.vertex.xy + 0.5;
                output.color = input.color * _Color * _RendererColor;
                float2 normal = mul((float3x3)unity_ObjectToWorld, float3(0,1,0)).xy;
                normal /= max(length(normal), 0.0001);
                // Restrained orientation shading: changes continuously as the
                // existing leaf rotates, without moving its visible silhouette.
                output.light = 0.96 + 0.04 * dot(normal, float2(-0.6,0.8));
                return output;
            }
            fixed4 frag(door_v2f input) : SV_Target
            {
                float2 p = input.local;
                float face = step(0.018,p.x) * step(p.x,0.982)
                           * step(0.20,p.y) * step(p.y,0.86);
                // Handles are now two small child rectangles on the long sides.
                fixed3 rgb = input.color.rgb * lerp(0.66,1.0,face);
                float bevel = face * step(0.66,p.y);
                rgb = lerp(rgb, min(rgb * 1.12 + 0.02,1.0),bevel);
                rgb *= input.light;
                fixed alpha = SampleSpriteTexture(input.uv).a * input.color.a;
                return fixed4(rgb * alpha,alpha);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
