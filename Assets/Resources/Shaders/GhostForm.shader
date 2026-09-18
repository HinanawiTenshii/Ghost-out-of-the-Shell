Shader "Game/GhostForm"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GhostTint ("Ghost Tint", Color) = (0.72,0.95,1,0.69)
        _EyeColor ("Original Eye Color", Vector) = (0.05,0.12,0.22,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            fixed4 _GhostTint;
            float4 _EyeColor;
            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(input.texcoord);
                // Match the authored eye palette, not all dark pixels (hair/boots also become blue).
                float3 difference = abs(source.rgb - _EyeColor.rgb);
                float eye = 1.0 - step(0.012, max(difference.r, max(difference.g, difference.b)));
                fixed4 result = fixed4(lerp(_GhostTint.rgb, fixed3(0,0,0), eye), source.a * _GhostTint.a);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
