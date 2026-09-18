Shader "Hidden/Cogitans/SpriteWaterFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Particle Texture", 2D) = "white" {}
        _SpriteTex ("Surface Alpha", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float horizontalBoost : TEXCOORD2;
                float4 variation : TEXCOORD3;
                float2 motion : TEXCOORD4;
            };
            sampler2D _SpriteTex;
            float _ClipToSurface, _UseSpriteAlpha;
            float4x4 _WorldToSurface;
            float4 _SurfaceBounds, _SpriteFlip, _UVOrigin, _UVAxisX, _UVAxisY;
            float4 _WorldFlowDirection;
            float _HorizontalVisibilityBoost;
            float _FlowTime;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                o.uv = v.uv.xy;
                // Stable seed, not world position: shapes never jump as they move.
                o.variation = frac(sin(v.uv.z * 317.17 + float4(1.7, 8.3, 21.4, 47.2)) * 43758.5453);
                float phase = o.variation.w * 6.2831853;
                float rate = lerp(0.7, 1.5, o.variation.z);
                o.motion = sin(float2(_FlowTime * rate + phase, _FlowTime * rate * 0.73 + phase + 1.9));
                // Compare to screen scanlines, not sprite-local axes. This also
                // handles rotated water surfaces and camera roll without a search
                // for Camera.main or an extra render pass.
                float2 screenFlow = mul((float3x3)UNITY_MATRIX_V, _WorldFlowDirection.xyz).xy;
                float horizontal = screenFlow.x * screenFlow.x / max(dot(screenFlow, screenFlow), 0.00001);
                o.horizontalBoost = horizontal * horizontal * saturate(_HorizontalVisibilityBoost);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float surfaceAlpha = 1.0;
                if (_ClipToSurface > 0.5)
                {
                    float2 localPos = mul(_WorldToSurface, float4(i.world, 1.0)).xy;
                    float2 edge = min(localPos - _SurfaceBounds.xy, _SurfaceBounds.zw - localPos);
                    clip(min(edge.x, edge.y));
                    float2 fadeWidth = max((_SurfaceBounds.zw - _SurfaceBounds.xy) * 0.015, 0.001);
                    surfaceAlpha = saturate(min(edge.x / fadeWidth.x, edge.y / fadeWidth.y));
                    if (_UseSpriteAlpha > 0.5)
                    {
                        localPos *= _SpriteFlip.xy;
                        float2 spriteUv = _UVOrigin.xy + localPos.x * _UVAxisX.xy + localPos.y * _UVAxisY.xy;
                        surfaceAlpha *= tex2D(_SpriteTex, spriteUv).a;
                    }
                }
                // Simple geometry to match the environment: straight bands,
                // angular offsets, staggered pairs and broken bands, not pixel arcs.
                float2 p = i.uv;
                float style = floor(i.variation.x * 4.0);
                float length = lerp(0.54, 0.82, i.variation.y) * (1.0 + 0.08 * i.motion.x);
                float centerX = 0.5 + 0.025 * i.motion.y;
                float left = centerX - length * 0.5;
                float right = centerX + length * 0.5;
                float u = saturate((p.x - left) / length);
                float bend = (i.variation.z - 0.5) * (0.22 + 0.06 * i.motion.x);
                // Linear ramp connects two flat segments with an angled join.
                float angular = saturate((u - 0.30) / 0.32) - 0.5;
                float centerY = 0.44 + 0.025 * i.motion.y;
                centerY += style == 1.0 ? bend * angular : bend * 0.22 * (u - 0.5);
                float aa = clamp(fwidth(p.y) * 0.5, 0.008, 0.06);
                float aaX = clamp(fwidth(p.x) * 0.5, 0.003, 0.04);
                float halfWidth = lerp(0.048, 0.065, i.variation.w) * (1.0 + i.horizontalBoost);
                float ends = smoothstep(left - aaX, left + aaX, p.x)
                    * (1.0 - smoothstep(right - aaX, right + aaX, p.x));
                float gapCenter = centerX + 0.035 * i.motion.x;
                float gap = style == 3.0 ? smoothstep(0.035 - aaX, 0.035 + aaX, abs(p.x - gapCenter)) : 1.0;
                float crest = (1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(p.y - centerY))) * gap;
                float secondEnds = smoothstep(0.30 - aaX, 0.30 + aaX, u)
                    * (1.0 - smoothstep(0.83 - aaX, 0.83 + aaX, u));
                float secondary = (1.0 - smoothstep(halfWidth * 0.8 - aa, halfWidth * 0.8 + aa,
                    abs(p.y - centerY - 0.23 - 0.02 * i.motion.x))) * secondEnds;
                secondary *= style == 2.0 ? 0.48 : 0.0;
                float trough = (1.0 - smoothstep(0.045 - aa, 0.045 + aa,
                    abs(p.y - centerY + 0.115))) * gap * 0.18;
                float taper = (0.88 + 0.08 * i.motion.y) * lerp(0.88, 1.0, i.variation.z);
                float light = max(crest * 0.72, secondary);
                float opacity = max(light, trough) * taper * ends * surfaceAlpha * i.color.a;
                opacity = saturate(opacity * (1.0 + i.horizontalBoost * 0.35));
                float3 darkWater = i.color.rgb * float3(0.3, 0.55, 0.72);
                float3 color = lerp(darkWater, i.color.rgb, saturate(light * 2.0));
                return fixed4(color, opacity);
            }
            ENDCG
        }
    }
}
