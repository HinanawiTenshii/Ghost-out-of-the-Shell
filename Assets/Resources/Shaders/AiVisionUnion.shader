Shader "Hidden/Cogitans/AiVisionUnion"
{
    Properties
    {
        _Color ("Vision Color", Color) = (0.45, 0.8, 1, 0.22)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        // Bit 7 belongs only to NPC vision. The first cone colours this pixel;
        // later overlapping cones cannot blend into it again. Camera visibility
        // uses the lower six bits and must preserve/ignore this bit respectively.
        Stencil
        {
            Ref 128
            ReadMask 128
            WriteMask 128
            Comp NotEqual
            Pass Replace
        }
        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                // Fully invisible cones must not reserve pixels from others.
                clip(_Color.a - 0.0001);
                fixed4 color = _Color;
                color.rgb *= color.a; // Same premultiplied blend as Sprites/Default.
                return color;
            }
            ENDCG
        }
    }
}
