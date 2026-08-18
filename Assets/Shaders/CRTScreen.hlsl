#ifndef COGITANS_CRT_SCREEN_INCLUDED
#define COGITANS_CRT_SCREEN_INCLUDED

sampler2D _MainTex;
float4 _MainTex_TexelSize;

float _CRT_Curvature;
float _CRT_ScanlineIntensity;
float _CRT_ScanlineCount;
float _CRT_ChromaticAberration;
float _CRT_Vignette;
float _CRT_MaskIntensity;
float _CRT_NoiseIntensity;
float _CRT_TimeValue;
float _CRT_Brightness;
float _CRT_Contrast;
float _CRT_Glow;

float CRT_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

void CRT_float(float4 ScreenPosition, out float4 Out)
{
    float2 uv = ScreenPosition.xy;
    float2 centered = uv * 2.0 - 1.0;

    // Side-only CRT curvature. The vertical center line remains unchanged;
    // curvature increases toward the left and right edges without creating
    // the former radial bulge from the middle of the screen.
    float sideDistance = centered.x * centered.x;
    float2 warped = float2(
        centered.x,
        centered.y * (1.0 + _CRT_Curvature * sideDistance));
    uv = warped * 0.5 + 0.5;

    float inside = step(0.0, uv.x) * step(uv.x, 1.0)
                 * step(0.0, uv.y) * step(uv.y, 1.0);
    float2 safeUv = saturate(uv);

    float2 aberration = float2(_CRT_ChromaticAberration * _MainTex_TexelSize.x, 0.0);
    float red = tex2D(_MainTex, saturate(safeUv + aberration)).r;
    float green = tex2D(_MainTex, safeUv).g;
    float blue = tex2D(_MainTex, saturate(safeUv - aberration)).b;
    float3 color = float3(red, green, blue);

    // Vary only along the vertical UV axis so each band spans the full screen
    // width and reads as a traditional horizontal CRT scanline.
    float scanPhase = sin(safeUv.y * _CRT_ScanlineCount * 6.2831853);
    float scanline = lerp(1.0, 0.5 + 0.5 * scanPhase, _CRT_ScanlineIntensity);

    float pixelColumn = floor(safeUv.x * _ScreenParams.x);
    float3 phosphor = float3(
        0.72 + 0.28 * step(2.0, fmod(pixelColumn, 3.0)),
        0.72 + 0.28 * step(2.0, fmod(pixelColumn + 2.0, 3.0)),
        0.72 + 0.28 * step(2.0, fmod(pixelColumn + 1.0, 3.0)));
    color *= lerp(1.0.xxx, phosphor, _CRT_MaskIntensity);

    float2 vignetteUv = safeUv * (1.0 - safeUv.yx);
    float vignette = saturate(pow(saturate(vignetteUv.x * vignetteUv.y * 16.0), _CRT_Vignette));
    float noise = (CRT_Hash(floor(safeUv * _ScreenParams.xy) + _CRT_TimeValue) - 0.5)
                * _CRT_NoiseIntensity;

    color = color * scanline + noise;
    color = saturate((color - 0.5) * _CRT_Contrast + 0.5);
    float phosphorGlow = smoothstep(0.45, 1.0, max(color.r, max(color.g, color.b)));
    color += color * color * phosphorGlow * _CRT_Glow;
    color *= vignette * _CRT_Brightness * inside;
    Out = float4(saturate(color), 1.0);
}

#endif
