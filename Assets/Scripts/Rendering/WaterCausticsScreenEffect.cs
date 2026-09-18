using UnityEngine;

/// <summary>Soft drifting water-reflection light fields for the Built-in render pipeline.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
[AddComponentMenu("Rendering/Water Caustics Screen Effect")]
public sealed class WaterCausticsScreenEffect : MonoBehaviour
{
    [Header("Underground Water Reflections")]
    [Range(0f, 1f)] public float intensity = 0.65f;
    public Color reflectionColor = new Color(0.35f, 0.78f, 0.92f, 1f);
    [Range(0f, 2f)] public float reflectionBrightness = 0.5f;
    [Tooltip("Subtle cool ambient tint; zero keeps the original scene colour.")]
    [Range(0f, 0.5f)] public float ambientTint = 0.08f;
    [Header("Moving Light Pattern")]
    [Tooltip("Relative density of broad light patches. Larger values make smaller patches.")]
    [Range(0.1f, 8f)] public float patternScale = 1.2f;
    [Range(0f, 2f)] public float animationSpeed = 0.8f;
    [Range(0f, 1f)] public float distortion = 0.65f;
    [Tooltip("Softness of the transitions between light and dark patches.")]
    [Range(0f, 1f)] public float gradientSoftness = 0.7f;
    [Tooltip("Depth of the moving blue shadows between reflected light patches.")]
    [Range(0f, 1f)] public float shadowStrength = 0.4f;
    [Tooltip("Main drift direction in the XY plane. Secondary currents deform the patches independently.")]
    public Vector2 flowDirection = new Vector2(0.22f, 0.14f);
    [Tooltip("Keep reflections attached to the environment as the orthographic camera moves. Perspective cameras use screen coordinates.")]
    public bool anchorToWorld = true;
    [Tooltip("Suppress reflections over near-black visibility masks; this does not change actual vision or reveal hidden objects.")]
    [Range(0.001f, 0.2f)] public float darknessThreshold = 0.035f;
    public bool animateWhilePaused = true;
    public bool previewInEditor = true;

    private Camera effectCamera;
    private CRTScreenEffect crt;
    private Material material;
    private static readonly int ReflectionId = Shader.PropertyToID("_ReflectionColor");
    private static readonly int SettingsId = Shader.PropertyToID("_Settings");
    private static readonly int PatternId = Shader.PropertyToID("_Pattern");
    private static readonly int FlowId = Shader.PropertyToID("_Flow");
    private static readonly int ViewId = Shader.PropertyToID("_View");
    private static readonly int RotationId = Shader.PropertyToID("_ViewRotation");
    private static readonly int TimeId = Shader.PropertyToID("_AnimationTime");
    public bool ShouldRender => isActiveAndEnabled && intensity > 0f && (Application.isPlaying || previewInEditor);

    private void OnEnable()
    {
        effectCamera = GetComponent<Camera>();
        crt = GetComponent<CRTScreenEffect>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (crt == null) crt = GetComponent<CRTScreenEffect>();
        // The final CRT camera applies the optional filter exactly once.
        if (crt != null && crt.isActiveAndEnabled) Graphics.Blit(source, destination);
        else Render(source, destination);
    }

    public void Render(RenderTexture source, RenderTexture destination)
    {
        if (!ShouldRender) { Graphics.Blit(source, destination); return; }
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("Shaders/WaterCausticsScreen");
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (effectCamera == null) effectCamera = GetComponent<Camera>();
        float time = Application.isPlaying ? (animateWhilePaused ? Time.unscaledTime : Time.time) : Time.realtimeSinceStartup;
        bool world = anchorToWorld && effectCamera.orthographic;
        float height = world ? effectCamera.orthographicSize * 2f : 1f;
        Vector3 position = world ? effectCamera.transform.position : Vector3.zero;
        float angle = world ? effectCamera.transform.eulerAngles.z * Mathf.Deg2Rad : 0f;
        material.SetColor(ReflectionId, reflectionColor);
        material.SetVector(SettingsId, new Vector4(intensity, reflectionBrightness, ambientTint, Mathf.Max(0.001f, darknessThreshold)));
        material.SetVector(PatternId, new Vector4(Mathf.Max(0.1f, patternScale), distortion, gradientSoftness, shadowStrength));
        material.SetVector(FlowId, new Vector4(flowDirection.x, flowDirection.y, 0f, 0f));
        material.SetVector(ViewId, new Vector4(position.x, position.y, height * source.width / Mathf.Max(1f, source.height), height));
        material.SetVector(RotationId, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0f, 0f));
        material.SetFloat(TimeId, time * animationSpeed);
        Graphics.Blit(source, destination, material);
    }

    private void OnDisable()
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        material = null;
    }
}
