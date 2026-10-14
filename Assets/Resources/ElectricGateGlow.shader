Shader "Cogitans/Electric Gate Glow"
{
    Properties
    {
        _Radial ("Radial Particle (0 for line)", Float) = 0
        _CoreFraction ("Bright Core Fraction", Range(0.01,1)) = 0.15
        _Intensity ("Glow Intensity", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        // Local additive light, rendered with the world BEFORE the existing vision mask.
        // No full-screen bloom pass, overlay queue, or stencil override is needed.
        Blend SrcAlpha One
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            float _Radial, _CoreFraction, _Intensity;
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                // Line UV.y spans the width; particles instead get a circular falloff.
                float distance = lerp(abs(p.y), length(p), saturate(_Radial));
                float core = 1.0 - smoothstep(_CoreFraction * 0.25, _CoreFraction, distance);
                float halo = 1.0 - smoothstep(0.0, 1.0, distance);
                halo = halo * halo * halo;
                float alpha = saturate(core * 0.8 + halo * 0.5) * input.color.a;
                // Preserve the signal hue even at the bright core (including blue passage sparks).
                float3 coreTint = lerp(input.color.rgb, float3(1.0, 1.0, 1.0), 0.8);
                float3 color = lerp(input.color.rgb, coreTint, core * 0.85);
                return fixed4(color * _Intensity, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
