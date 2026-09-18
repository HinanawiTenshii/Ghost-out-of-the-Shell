Shader "Hidden/Cogitans/CameraVisibilityStencil"
{
    SubShader
    {
        Tags
        {
            "Queue"="Transparent-10"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 37
                Comp Always
                Pass Replace
            }
        }
    }
}
