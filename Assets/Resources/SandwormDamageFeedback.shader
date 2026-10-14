Shader "Cogitans/Sandworm Damage Feedback"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _WorldShakeOffset ("Visual-only World Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex HitVert
            #pragma fragment SpriteFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _WorldShakeOffset;
            v2f HitVert(appdata_t input)
            {
                v2f output = SpriteVert(input);
                // Vector (w=0), not a point: preserve camera translation and
                // give rotated segments exactly the same world-space offset.
                output.vertex += mul(UNITY_MATRIX_VP, float4(_WorldShakeOffset.xyz, 0.0));
                return output;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
