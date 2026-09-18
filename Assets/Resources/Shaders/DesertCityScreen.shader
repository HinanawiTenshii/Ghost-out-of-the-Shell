Shader "Hidden/Cogitans/DesertCityScreen"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ShadowColor, _MidtoneColor, _HighlightColor;
            float4 _Settings; // intensity, colour preservation, desaturation, haze

            float4 frag(v2f_img input) : SV_Target
            {
                float4 scene = tex2D(_MainTex, input.uv);
                float value = saturate(dot(scene.rgb, float3(0.2126, 0.7152, 0.0722)));
                float peak = max(scene.r, max(scene.g, scene.b));
                float chroma = peak - min(scene.r, min(scene.g, scene.b));
                // Protect colourful sprites and bright geometry. This is a pixel
                // heuristic, not an object ID mask; no extra scene render needed.
                float colourful = smoothstep(0.08, 0.35, chroma) * smoothstep(0.16, 0.65, peak);
                float bright = smoothstep(0.4, 0.9, peak) * 0.8;
                float protection = max(colourful, bright) * saturate(_Settings.y);
                float3 low = lerp(_ShadowColor.rgb, _MidtoneColor.rgb, smoothstep(0.0, 0.5, value));
                float3 palette = lerp(low, _HighlightColor.rgb, smoothstep(0.5, 1.0, value));
                float3 warmSource = lerp(scene.rgb, value.xxx, _Settings.z * (1.0 - protection));
                warmSource *= float3(1.0, 0.985, 0.95);
                // Retain most of the source rather than using the original
                // desert's strong sepia remapping and highlight compression.
                float3 graded = lerp(warmSource, palette, lerp(0.52, 0.12, protection));
                graded = lerp(graded, _HighlightColor.rgb,
                    _Settings.w * (1.0 - protection) * (1.0 - value) * 0.5);
                // Leave black vision masks black; do not lift hidden areas into fog.
                float visible = smoothstep(0.008, 0.08, peak);
                float3 result = lerp(scene.rgb, graded, saturate(_Settings.x) * visible);
                return float4(max(result, 0.0), scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
