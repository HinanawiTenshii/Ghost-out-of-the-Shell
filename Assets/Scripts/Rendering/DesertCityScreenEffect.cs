using UnityEngine;

/// <summary>Light, colour-preserving desert-city grade without sand or grain.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
[AddComponentMenu("Rendering/Desert City Screen Effect")]
public sealed class DesertCityScreenEffect : MonoBehaviour
{
    [Header("City Palette / 淡沙色")]
    [Range(0f, 1f)] public float intensity = 0.45f;
    public Color shadowColor = new Color(0.14f, 0.105f, 0.065f, 1f);
    public Color midtoneColor = new Color(0.55f, 0.43f, 0.28f, 1f);
    public Color highlightColor = new Color(0.96f, 0.89f, 0.75f, 1f);
    [Range(0f, 1f), Tooltip("减小鲜明颜色和明亮物体的偏色。按像素颜色判断，不依赖角色/物品层。")]
    public float objectColorPreservation = 0.9f;
    [Range(0f, 0.3f)] public float desaturation = 0.06f;
    [Range(0f, 0.15f), Tooltip("轻微静态暖色薄雾，不包含扬沙粒子或动态噪点。")]
    public float haze = 0.025f;
    public bool previewInEditor = true;

    private Material material;
    private CRTScreenEffect crt;
    public bool ShouldRender => isActiveAndEnabled && intensity > 0f && (Application.isPlaying || previewInEditor);

    private void OnEnable() => crt = GetComponent<CRTScreenEffect>();

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (crt == null) crt = GetComponent<CRTScreenEffect>();
        // The final CRT compositor also covers Blocks and UI; do not grade twice.
        if (crt != null && crt.isActiveAndEnabled) Graphics.Blit(source, destination);
        else Render(source, destination);
    }

    public void Render(RenderTexture source, RenderTexture destination)
    {
        if (!ShouldRender) { Graphics.Blit(source, destination); return; }
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("Shaders/DesertCityScreen");
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        material.SetColor("_ShadowColor", shadowColor);
        material.SetColor("_MidtoneColor", midtoneColor);
        material.SetColor("_HighlightColor", highlightColor);
        material.SetVector("_Settings", new Vector4(intensity, objectColorPreservation, desaturation, haze));
        Graphics.Blit(source, destination, material);
    }

    private void OnDisable()
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        material = null;
    }
}
