Shader "Hidden/Cogitans/SelectiveWorldBloom"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _SelectionTex, _VisibilityTex, _NearTex, _FarTex;
        float4 _MainTex_TexelSize, _Settings, _BlurDirection;
        float _Strength;
        struct maskVarying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
        maskVarying maskVertex(appdata_img v)
        {
            maskVarying o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o;
        }
        half4 selectionFragment(maskVarying i) : SV_Target
        {
            return tex2D(_MainTex, i.uv).a * _Strength;
        }
        half4 visibleFragment(maskVarying i) : SV_Target { return 1; }
        float2 auxiliaryUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
            #endif
            return uv;
        }
        half4 extractFragment(v2f_img i) : SV_Target
        {
            half3 color = tex2D(_MainTex, i.uv).rgb;
            float2 maskUV = auxiliaryUV(i.uv);
            half visible = tex2D(_VisibilityTex, maskUV).r;
            half selected = tex2D(_SelectionTex, maskUV).r;
            half bright = max(color.r, max(color.g, color.b));
            half contribution = smoothstep(_Settings.x - _Settings.y, _Settings.x + _Settings.y, bright);
            return half4(color * contribution * selected * visible, 0);
        }
        half4 blurFragment(v2f_img i) : SV_Target
        {
            float2 stepUV = _BlurDirection.xy;
            half4 color = tex2D(_MainTex, i.uv) * 0.227027;
            color += tex2D(_MainTex, i.uv + stepUV * 1.384615) * 0.316216;
            color += tex2D(_MainTex, i.uv - stepUV * 1.384615) * 0.316216;
            color += tex2D(_MainTex, i.uv + stepUV * 3.230769) * 0.070270;
            color += tex2D(_MainTex, i.uv - stepUV * 3.230769) * 0.070270;
            return color;
        }
        half4 compositeFragment(v2f_img i) : SV_Target
        {
            half4 original = tex2D(_MainTex, i.uv);
            float2 uv = auxiliaryUV(i.uv);
            half visibility = tex2D(_VisibilityTex, uv).r;
            half3 glow = tex2D(_NearTex, uv).rgb * _Settings.z + tex2D(_FarTex, uv).rgb * _Settings.w;
            // Clip again AFTER blur: no halo crosses an occluded / unrevealed boundary.
            // Limit added energy and retain headroom instead of whitening sprite cores.
            original.rgb += min(glow, 0.45) * visibility * (1 - saturate(original.rgb) * 0.65);
            return original;
        }
        ENDCG
        // 0: Whitelisted sprite silhouettes only. Overlaps take maximum, never accumulate.
        Pass
        {
            Blend One One
            BlendOp Max
            CGPROGRAM
            #pragma vertex maskVertex
            #pragma fragment selectionFragment
            ENDCG
        }
        // 1: Same exact visibility polygon as the gameplay stencil, including extra reveal sources.
        Pass
        {
            CGPROGRAM
            #pragma vertex maskVertex
            #pragma fragment visibleFragment
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extractFragment
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blurFragment
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment compositeFragment
            ENDCG
        }
    }
    Fallback Off
}
