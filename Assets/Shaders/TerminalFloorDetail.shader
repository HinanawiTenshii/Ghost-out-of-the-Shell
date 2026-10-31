Shader "Game/Terminal Floor Detail"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Tile("Tile width / height", Vector) = (3,2,0,0)
        _Contrast("Seam contrast", Range(0,0.3)) = 0.045
        _SeamWidth("Indoor seam width (world units; 0 keeps legacy seams)", Range(0,0.06)) = 0
        _Variation("Sparse neighboring tile tones", Range(0,0.02)) = 0
        _Facet("Sparse short edge accent", Range(0,0.025)) = 0
        _Pattern("0 castle, 1 castle square, 2 garden, 3 sand, 4 prison, 5 town, 6 underground", Float) = 0
        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"
            sampler2D _MainTex; fixed4 _Color; float4 _Tile;
            float _Contrast, _Pattern, _SeamWidth, _Variation, _Facet;
            struct input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct output { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            output vert(input v)
            {
                output o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy;
                #ifdef PIXELSNAP_ON
                o.pos=UnityPixelSnap(o.pos);
                #endif
                return o;
            }
            fixed4 frag(output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                float2 p=i.world/max(_Tile.xy,.1);
                if(_Pattern<.5) p.x+=fmod(abs(floor(p.y)),2)*.5;
                // Separate material families, not just differently sized copies
                // of the castle floor. Prison slabs use a third-bond layout;
                // town squares align; underground narrow stone uses half-bond.
                if(_Pattern>3.5 && _Pattern<4.5) p.x+=fmod(abs(floor(p.y)),3)/3;
                if(_Pattern>5.5) p.x+=fmod(abs(floor(p.y)),2)*.5;
                bool outdoor=_Pattern>1.5 && _Pattern<3.5;
                float2 cell=frac(p); float2 aa=max(fwidth(p),.0001);
                float2 seam=1-smoothstep(float2(.004,.004),float2(.004,.004)+aa, min(cell,1-cell));
                // The lab uses .035-world-unit inset joints and only two gently
                // shifted slabs out of seven. Keep all colors relative to the
                // original floor tint; never replace a scene's base palette.
                if(_SeamWidth>0 && !outdoor)
                {
                    float2 width=_SeamWidth/max(_Tile.xy,.1);
                    seam=1-smoothstep(width-aa*.5,width+aa*.5,cell);
                    float variant=fmod(abs(floor(p.y)*17+floor(p.x)*31),7);
                    float bright=1-step(.5,variant);
                    float dark=1-step(.5,abs(variant-3));
                    c.rgb=saturate(c.rgb+_Variation*(bright-dark*.89));
                    float facet=bright*step(.055,cell.x)*step(cell.x,.405)*
                        (1-smoothstep(width.y*1.65-aa.y*.5,width.y*1.65+aa.y*.5,cell.y));
                    c.rgb=saturate(c.rgb+_Facet*facet*float3(.75,.9,1));
                    if((_Pattern>3.5 && _Pattern<4.5) || _Pattern>5.5)
                    {
                        // One short, quiet wear line on occasional slabs, no
                        // noisy grain or moving procedural texture.
                        float wear=1-smoothstep(.003,.003+aa.y,abs(cell.y-.32-(cell.x-.65)*.07));
                        wear*=bright*step(.65,cell.x)*step(cell.x,.77);
                        c.rgb*=1-wear*_Contrast*.35;
                    }
                }
                float mark=max(seam.x,seam.y);
                if(outdoor) // sparse short marks, not full tile grids on outdoors
                {
                    float seed=frac(sin(dot(floor(p),float2(12.9898,78.233)))*43758.5453);
                    mark=seam.y*step(.82,seed)*step(.12,cell.x)*step(cell.x,.34);
                }
                c.rgb*=1-mark*_Contrast;
                c.rgb*=c.a; return c;
            }
            ENDCG
        }
    }
}
