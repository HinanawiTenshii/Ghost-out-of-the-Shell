Shader "Cogitans/Warm Light Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _Intensity ("Glow Intensity", Range(0,6)) = 1
        _RadialFalloff ("Procedural Disc Falloff", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _Intensity, _RadialFalloff;
            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                float2 p = input.texcoord * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(p,p));
                radial *= radial;
                color.a *= lerp(1.0, radial, saturate(_RadialFalloff));
                // Premultiplied additive light; ordinary world sorting lets the vision mask cover it.
                return fixed4(color.rgb * color.a * _Intensity, color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
