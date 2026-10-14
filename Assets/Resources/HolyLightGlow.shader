Shader "Cogitans/Holy Light Glow"
{
    Properties
    {
        _CoreFraction ("Column Core Width", Range(0.01,1)) = 0.52
        _Intensity ("Glow Intensity", Range(0,2)) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            float _CoreFraction, _Intensity;
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float distance = abs(input.uv.y * 2.0 - 1.0);
                float core = 1.0 - smoothstep(_CoreFraction * 0.5, _CoreFraction, distance);
                float halo = 1.0 - smoothstep(0.0, 1.0, distance);
                // Preserve separate gold and ivory shafts, instead of bleaching every core white.
                float alpha = saturate(core * 0.85 + halo * halo * halo * 0.35) * input.color.a;
                return fixed4(input.color.rgb * _Intensity, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
