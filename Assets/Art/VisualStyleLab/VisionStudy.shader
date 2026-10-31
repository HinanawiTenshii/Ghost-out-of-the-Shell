Shader "Hidden/Cogitans/LabVisionStudy"
{
    Properties
    {
        _Color ("Fill", Color) = (.3,.62,.8,.065)
        _EdgeColor ("Contour", Color) = (.3,.62,.8,.27)
        _EdgePixels ("Contour pixels", Float) = 1.1
        _Closed ("Full circle", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        // Preserve the existing single-layer vision union and camera visibility bits.
        Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Replace }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _EdgeColor;
            float _EdgePixels, _Closed;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float arc=(1-i.uv.x)/max(fwidth(i.uv.x),.00001);
                float side=i.uv.y/max(fwidth(i.uv.y),.00001);
                float distance=min(arc,lerp(side,10000,_Closed));
                float edge=1-smoothstep(_EdgePixels-.4,_EdgePixels+.5,distance);
                fixed4 color=lerp(_Color,_EdgeColor,edge);
                clip(color.a-.0001); color.rgb*=color.a; return color;
            }
            ENDCG
        }
    }
}
