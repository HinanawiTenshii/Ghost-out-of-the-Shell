Shader "Hidden/Cogitans/CameraVisionReveal"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_REVEAL_SOURCES 8
            #define REVEAL_RAY_COUNT 64
            #define RAY_VECTORS_PER_SOURCE 16
            #define TOTAL_RAY_VECTORS 128

            fixed4 _Color;
            int _RevealSourceCount;
            float4 _RevealSources[MAX_REVEAL_SOURCES];
            float4 _RevealRayDistances[TOTAL_RAY_VECTORS];

            float ReadRevealRayDistance(int sourceIndex, int rayIndex)
            {
                int packedIndex = sourceIndex * RAY_VECTORS_PER_SOURCE +
                    rayIndex / 4;
                int componentIndex = rayIndex - (rayIndex / 4) * 4;
                float4 packedDistances =
                    _RevealRayDistances[packedIndex];
                if (componentIndex == 0) return packedDistances.x;
                if (componentIndex == 1) return packedDistances.y;
                if (componentIndex == 2) return packedDistances.z;
                return packedDistances.w;
            }

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition =
                    mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                for (int index = 0; index < MAX_REVEAL_SOURCES; index++)
                {
                    if (index >= _RevealSourceCount)
                    {
                        break;
                    }

                    float2 offset =
                        input.worldPosition - _RevealSources[index].xy;
                    float radius = _RevealSources[index].z;
                    float distanceFromSource = length(offset);
                    if (distanceFromSource > radius)
                    {
                        continue;
                    }

                    float normalizedAngle = frac(
                        atan2(offset.y, offset.x) /
                        (2.0 * UNITY_PI) + 1.0);
                    float rayPosition = normalizedAngle * REVEAL_RAY_COUNT;
                    int firstRay = (int)floor(rayPosition);
                    int secondRay = firstRay + 1;
                    if (secondRay >= REVEAL_RAY_COUNT)
                    {
                        secondRay = 0;
                    }
                    float interpolation = frac(rayPosition);
                    float firstDistance = ReadRevealRayDistance(
                        index,
                        firstRay);
                    float secondDistance = ReadRevealRayDistance(
                        index,
                        secondRay);
                    float visibleDistance = lerp(
                        firstDistance,
                        secondDistance,
                        interpolation);
                    if (distanceFromSource <= visibleDistance)
                    {
                        discard;
                    }
                }

                return _Color;
            }
            ENDCG
        }
    }
}
