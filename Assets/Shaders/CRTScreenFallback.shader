Shader "Hidden/Cogitans/CRTScreenFallback"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        [HideInInspector] _CRT_Curvature ("Curvature", Float) = 0.08
        [HideInInspector] _CRT_ScanlineIntensity ("Scanline Intensity", Float) = 0.28
        [HideInInspector] _CRT_ScanlineCount ("Scanline Count", Float) = 240
        [HideInInspector] _CRT_ChromaticAberration ("Chromatic Aberration", Float) = 1.25
        [HideInInspector] _CRT_Vignette ("Vignette", Float) = 0.7
        [HideInInspector] _CRT_MaskIntensity ("Mask Intensity", Float) = 0.14
        [HideInInspector] _CRT_NoiseIntensity ("Noise Intensity", Float) = 0.018
        [HideInInspector] _CRT_TimeValue ("Time", Float) = 0
        [HideInInspector] _CRT_Brightness ("Brightness", Float) = 1.08
        [HideInInspector] _CRT_Contrast ("Contrast", Float) = 1.12
        [HideInInspector] _CRT_Glow ("Phosphor Glow", Float) = 0.14
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "CRTScreen.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float4 color;
                CRT_float(float4(input.uv, 0.0, 1.0), color);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
