Shader "Hidden/Cogitans/WaterCausticsScreen"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ReflectionColor, _Settings, _Pattern, _View, _ViewRotation, _Flow;
            float _AnimationTime;

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Quintic interpolation keeps brightness AND its slope continuous across cells.
            float softNoise(float2 p)
            {
                float2 cell = floor(p), fraction = frac(p);
                float2 blend = fraction * fraction * fraction * (fraction * (fraction * 6.0 - 15.0) + 10.0);
                return lerp(lerp(hash21(cell), hash21(cell + float2(1, 0)), blend.x),
                            lerp(hash21(cell + float2(0, 1)), hash21(cell + float2(1, 1)), blend.x), blend.y);
            }

            float2 waterLightField(float2 p, float time)
            {
                // One low-frequency field owns the silhouette, keeping each bright patch
                // cohesive. Secondary currents only modulate its interior illumination.
                float2 drift = _Flow.xy * time;
                float2 bend = float2(softNoise(p * 0.67 + float2(time * 0.13, -time * 0.09)),
                                     softNoise(p * 0.61 + float2(-time * 0.11, time * 0.12) + 31.7));
                float2 warped = p - drift + (bend - 0.5) * (_Pattern.y * 0.65);
                float broad = softNoise(warped);
                float2 crossed = float2(warped.x * 0.8 - warped.y * 0.6,
                                        warped.x * 0.6 + warped.y * 0.8);
                float secondary = softNoise(crossed * 1.87 + float2(time * 0.16, -time * 0.1) + 13.4);
                // Higher threshold leaves most of the surface dark. A narrower soft rim
                // produces a filled, gently rounded patch rather than diffuse fragments.
                float transition = lerp(0.08, 0.19, saturate(_Pattern.z));
                float lightPatch = smoothstep(0.68 - transition, 0.68 + transition, broad);
                float darkPatch = 1.0 - lightPatch;
                // Keep a near-uniform bright core; subtle water glints cannot break its outline.
                float interiorLight = lerp(0.9, 1.0, smoothstep(0.25, 0.8, secondary));
                return float2(lightPatch * interiorLight, darkPatch);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float2 p = (i.uv - 0.5) * _View.zw;
                p = float2(p.x * _ViewRotation.x - p.y * _ViewRotation.y,
                           p.x * _ViewRotation.y + p.y * _ViewRotation.x);
                // Keep existing component density values useful, but enlarge the old cells.
                p = (p + _View.xy) * (_Pattern.x * 0.32);
                float t = _AnimationTime;
                float2 water = waterLightField(p, t);
                float luminance = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                float visibility = smoothstep(0.0, _Settings.w, luminance);
                float strength = saturate(_Settings.x) * visibility;
                float3 baseColor = source.rgb * lerp(float3(1, 1, 1), float3(0.80, 0.95, 1.0), strength * _Settings.z);
                // Bound the added light and protect highlights; never warp scene/UI geometry.
                float highlightProtection = 1.0 - smoothstep(0.35, 0.95, luminance);
                float shadow = water.y * saturate(_Pattern.w) * strength * highlightProtection;
                baseColor *= lerp(float3(1, 1, 1), float3(0.45, 0.65, 0.80), shadow);
                float3 reflected = _ReflectionColor.rgb * water.x * _Settings.y * strength * highlightProtection;
                return float4(baseColor + reflected * saturate(1.0 - baseColor), source.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
