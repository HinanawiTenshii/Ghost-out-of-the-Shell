Shader "Hidden/Cogitans/DesertScreen"
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ShadowColor, _HighlightColor, _SandColor, _Grade, _Sand, _Screen;
            float4 _MidtoneColor;
            float _HighlightProtection;
            float _ObjectColorPreservation;

            float hash21(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i+float2(1,0)), f.x),
                    lerp(hash21(i+float2(0,1)), hash21(i+1), f.x), f.y);
            }
            float sandLayer(float2 uv, float grid, float speed, float seed)
            {
                float aspect = _Screen.x / max(1.0, _Screen.y);
                // Increasing time moves each grain toward screen lower-left.
                float2 drift = float2(-0.8, -0.6) * _Screen.w * _Sand.z * speed;
                float2 p = (uv * float2(aspect, 1) - drift) * grid;
                float2 cell = floor(p);
                float result = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = cell + float2(x,y);
                    float h = hash21(id + seed);
                    float k = hash21(id + seed + 17.7);
                    float2 jitter = float2(sin(_Screen.w * (0.8 + h) + k * 6.283),
                        cos(_Screen.w * (0.6 + k) + h * 6.283)) * _Sand.w * 0.12;
                    float2 center = id + 0.2 + float2(h,k) * 0.6 + jitter;
                    float2 delta = (p - center) / grid * _Screen.y;
                    float along = dot(delta, float2(0.8,0.6));
                    float across = dot(delta, float2(-0.6,0.8));
                    float radius = _Screen.z * lerp(0.55,1.2,k);
                    float distance = length(float2(along / lerp(1.0,2.8,h), across));
                    float coverage = 1 - smoothstep(max(0.0,radius-0.6),radius+0.6,distance);
                    float selected = step(h, _Sand.x * 0.42);
                    result += coverage * selected * lerp(0.35,1.0,k);
                }
                return saturate(result);
            }
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float4 scene = tex2D(_MainTex, uv);
                float luminance = dot(scene.rgb, float3(0.2126,0.7152,0.0722));
                float3 muted = lerp(scene.rgb, luminance.xxx, _Grade.y);
                // Three-point colour mapping: cold midtones become ochre, not a
                // desaturated interpolation towards a near-white highlight.
                float value = saturate(luminance);
                float3 warmHighlight = _HighlightColor.rgb * lerp(float3(1,1,1), float3(0.86,0.72,0.52), _HighlightProtection);
                float3 low = lerp(_ShadowColor.rgb, _MidtoneColor.rgb, smoothstep(0.0,0.55,value));
                float3 sepia = lerp(low, warmHighlight, smoothstep(0.55,1.0,value));
                float3 graded = lerp(muted * warmHighlight, sepia, 0.94);
                float aspect = _Screen.x / max(1.0,_Screen.y);
                float2 windUV = uv * float2(aspect,1) + float2(0.8,0.6) * _Screen.w * _Sand.z;
                float mist = noise(windUV * 3.0) * 0.65 + noise(windUV * 7.0 + 11.0) * 0.35;
                graded = lerp(graded, _MidtoneColor.rgb, _Grade.z * mist);
                float grain = hash21(floor(uv * _Screen.xy) + floor(_Screen.w*12)) - 0.5;
                graded += grain * _Grade.w;
                float sand = sandLayer(uv, 9.0, 1.0, 2.1) + sandLayer(uv, 14.0, 1.6, 31.8) * 0.6;
                // Desaturate the residual source too, otherwise intensity < 1
                // reintroduces blue that cancels the warm palette into grey.
                float3 residual = lerp(scene.rgb, luminance.xxx, _Grade.x * _Grade.y);
                float3 desert = lerp(residual, max(graded,0), _Grade.x);
                // Value (not luminance) also protects saturated blue/green items.
                // This is a colour/value heuristic, not a semantic object mask:
                // deep background stays warm while visible sprites retain hue.
                float peak = max(scene.r, max(scene.g, scene.b));
                float chroma = peak - min(scene.r, min(scene.g, scene.b));
                // Higher thresholds keep dim blue floors/vision overlays from
                // receiving the same protection as a bright foreground sprite.
                float visibleColour = smoothstep(0.18, 0.65, peak);
                float saturatedColour = smoothstep(0.18, 0.50, peak) * smoothstep(0.10, 0.30, chroma);
                float protection = max(visibleColour, saturatedColour) * saturate(_ObjectColorPreservation) * 0.72;
                // Retain hue variation, but apply warm light and moderate saturation
                // reduction to protected colours too. Never restore the raw image.
                float3 warmObject = lerp(scene.rgb, luminance.xxx, 0.18);
                warmObject *= float3(0.96, 0.82, 0.59);
                warmObject += float3(0.045, 0.020, 0.004) * (1.0 - value);
                warmObject = lerp(scene.rgb, warmObject, _Grade.x);
                float3 result = lerp(desert, warmObject, protection);
                // Keep sand independent of colour protection; it is a sparse
                // atmospheric overlay, not a recolouring of the object's pixels.
                result = lerp(result, _SandColor.rgb, saturate(sand * _Sand.y * _Grade.x));
                return float4(result, scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
