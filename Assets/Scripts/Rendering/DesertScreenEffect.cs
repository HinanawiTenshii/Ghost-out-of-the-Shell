using UnityEngine;

/// <summary>Optional Built-in RP desert grading and screen-space windblown sand.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
[AddComponentMenu("Rendering/Desert Screen Effect")]
public sealed class DesertScreenEffect : MonoBehaviour
{
    [Header("Desert Palette")]
    [Range(0f, 1f)] public float intensity = 0.8f;
    public Color shadowColor = new Color(0.13f, 0.055f, 0.022f, 1f);
    public Color highlightColor = new Color(1f, 0.82f, 0.49f, 1f);
    [Tooltip("Dedicated warm middle tones prevent blue scenes from becoming grey/white.")]
    public Color midtoneColor = new Color(0.43f, 0.20f, 0.075f, 1f);
    [Range(0f, 1f), Tooltip("Warm highlight compression before CRT brightness/glow.")]
    public float highlightProtection = 0.7f;
    [Range(0f, 1f), Tooltip("Retain recognisable object hues within the warm desert grade. Even at one, objects retain a light sand tint rather than bypassing the filter.")]
    public float objectColorPreservation = 0.8f;
    [Range(0f, 1f)] public float desaturation = 0.65f;
    [Range(0f, 0.5f)] public float haze = 0.08f;
    [Range(0f, 0.1f)] public float grain = 0.012f;
    [Header("Windblown Sand")]
    [Range(0f, 1f)] public float sandDensity = 0.45f;
    [Range(0f, 1f)] public float sandOpacity = 0.55f;
    [Min(0f)] public float windSpeed = 0.07f;
    [Range(0f, 1f)] public float turbulence = 0.3f;
    [Range(0.5f, 4f)] public float sandPixelSize = 1.4f;
    public Color sandColor = new Color(1f, 0.8f, 0.46f, 1f);
    [Tooltip("Continue sand animation while gameplay is paused.")]
    public bool animateWhilePaused = true;
    [Tooltip("Preview in the camera Game view while not playing.")]
    public bool previewInEditor = true;
    private Material material;
    private CRTScreenEffect crt;
    public bool ShouldRender => isActiveAndEnabled && intensity > 0f && (Application.isPlaying || previewInEditor);

    private void OnEnable() { crt = GetComponent<CRTScreenEffect>(); }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (crt == null) crt = GetComponent<CRTScreenEffect>();
        // The city variant replaces this grade, including its sand, even without CRT.
        var city = GetComponent<DesertCityScreenEffect>();
        if (city != null && city.ShouldRender) { Graphics.Blit(source, destination); return; }
        // CRT's final compositor applies this once, before its own pass.
        if (crt != null && crt.isActiveAndEnabled) Graphics.Blit(source, destination);
        else Render(source, destination);
    }

    public void Render(RenderTexture source, RenderTexture destination)
    {
        if (!ShouldRender) { Graphics.Blit(source, destination); return; }
        if (material == null)
        {
            var shader = Resources.Load<Shader>("Shaders/DesertScreen");
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        float time = Application.isPlaying ? (animateWhilePaused ? Time.unscaledTime : Time.time) : Time.realtimeSinceStartup;
        material.SetColor("_ShadowColor", shadowColor);
        material.SetColor("_HighlightColor", highlightColor);
        material.SetColor("_MidtoneColor", midtoneColor);
        material.SetFloat("_HighlightProtection", highlightProtection);
        material.SetFloat("_ObjectColorPreservation", objectColorPreservation);
        material.SetColor("_SandColor", sandColor);
        material.SetVector("_Grade", new Vector4(intensity, desaturation, haze, grain));
        material.SetVector("_Sand", new Vector4(sandDensity, sandOpacity, windSpeed, turbulence));
        material.SetVector("_Screen", new Vector4(source.width, source.height, sandPixelSize, time));
        Graphics.Blit(source, destination, material);
    }

    private void OnDisable()
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        material = null;
    }
}
