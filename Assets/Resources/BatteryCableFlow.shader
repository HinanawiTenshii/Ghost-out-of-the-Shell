Shader "Cogitans/Battery Cable Flow"
{
    Properties
    {
        _FlowTravel ("Distance from battery", Float) = 0
        _PulseSpacing ("Pulse spacing", Float) = 4.8
        _PulseLength ("Pulse length", Float) = 0.75
        _Intensity ("Intensity", Range(0,3)) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _FlowTravel, _PulseSpacing, _PulseLength, _Intensity;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // UV.x is WORLD arc length from the battery, not segment index or
                // a screen-space coordinate. Nothing lights ahead of the first wave.
                float behind = _FlowTravel - i.uv.x;
                float spacing = max(.001, _PulseSpacing);
                float phase = behind - floor(behind / spacing) * spacing;
                float t = saturate(phase / max(.001, _PulseLength));
                float pulse = step(0, behind) * smoothstep(0, .12, t) * (1 - smoothstep(.15, 1, t));
                float across = abs(i.uv.y * 2 - 1);
                float halo = saturate(1 - across); halo *= halo;
                float core = 1 - smoothstep(.18, .43, across);
                float3 red = float3(1, .14, .065);
                float3 warmHead = float3(1, .84, .64);
                float3 color = lerp(red, warmHead, core * (1 - smoothstep(.15, .75, t)));
                return fixed4(color * pulse * (core * .8 + halo * .5) * _Intensity, 0);
            }
            ENDCG
        }
    }
}
