using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/CRT Screen Effect")]
[DefaultExecutionOrder(32000)]
public sealed class CRTScreenEffect : MonoBehaviour
{
    private const string UiCompositeCameraName = "UI CRT Composite Camera";

    [Header("Shader Graph")]
    [SerializeField] private Material crtMaterial;

    [Header("CRT Settings")]
    [SerializeField, Range(0f, 0.3f)] private float curvature = 0.08f;
    [SerializeField, Range(0f, 1f)] private float scanlineIntensity = 0.28f;
    [SerializeField, Min(1f)] private float scanlineCount = 240f;
    [SerializeField, Range(0f, 8f)] private float chromaticAberration = 1.25f;
    [SerializeField, Range(0f, 2f)] private float vignette = 0.7f;
    [SerializeField, Range(0f, 1f)] private float maskIntensity = 0.14f;
    [SerializeField, Range(0f, 0.2f)] private float noiseIntensity = 0.018f;
    [SerializeField, Range(0.5f, 2f)] private float brightness = 1.08f;
    [SerializeField, Range(0.8f, 1.5f)] private float contrast = 1.12f;
    [SerializeField, Range(0f, 0.5f)] private float phosphorGlow = 0.14f;

    private Material runtimeMaterial;
    private Camera attachedCamera;
    private CRTScreenEffect settingsSource;
    private Camera generatedUiCamera;
    private Camera activeUiCamera;
    private bool canvasCallbackRegistered;

    private static readonly int CurvatureId = Shader.PropertyToID("_CRT_Curvature");
    private static readonly int ScanlineIntensityId = Shader.PropertyToID("_CRT_ScanlineIntensity");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_CRT_ScanlineCount");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_CRT_ChromaticAberration");
    private static readonly int VignetteId = Shader.PropertyToID("_CRT_Vignette");
    private static readonly int MaskIntensityId = Shader.PropertyToID("_CRT_MaskIntensity");
    private static readonly int NoiseIntensityId = Shader.PropertyToID("_CRT_NoiseIntensity");
    private static readonly int TimeValueId = Shader.PropertyToID("_CRT_TimeValue");
    private static readonly int BrightnessId = Shader.PropertyToID("_CRT_Brightness");
    private static readonly int ContrastId = Shader.PropertyToID("_CRT_Contrast");
    private static readonly int GlowId = Shader.PropertyToID("_CRT_Glow");

    private Material EffectMaterial
    {
        get
        {
            if (crtMaterial != null
                && crtMaterial.shader != null
                && crtMaterial.shader.name == "Hidden/Cogitans/CRTScreenFallback")
                return crtMaterial;

            if (runtimeMaterial == null)
            {
                // A regular Shader Graph uses its mesh-oriented pass layout and
                // is not a reliable Graphics.Blit target in Built-in RP. Use the
                // dedicated full-screen pass generated from the same CRT HLSL.
                Shader shader = Shader.Find("Hidden/Cogitans/CRTScreenFallback");
                if (shader == null)
                    shader = Shader.Find("Shader Graphs/CRTScreen");

                if (shader != null)
                {
                    runtimeMaterial = new Material(shader)
                    {
                        name = "CRT Screen Effect (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            return runtimeMaterial;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (Application.isPlaying && !IsFinalCompositeCamera())
        {
            Graphics.Blit(source, destination);
            return;
        }

        Material material = EffectMaterial;
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        ApplyShaderSettings(material);
        Graphics.Blit(source, destination, material);
    }

    private void ApplyShaderSettings(Material material)
    {
        if (material == null)
            return;

        float timeValue =
            Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;

        // These uniforms live in the Custom Function include rather than in the
        // Shader Graph blackboard. They must therefore be supplied globally;
        // Material.SetFloat silently leaves them at zero on this Unity version.
        Shader.SetGlobalFloat(CurvatureId, curvature);
        Shader.SetGlobalFloat(ScanlineIntensityId, scanlineIntensity);
        Shader.SetGlobalFloat(ScanlineCountId, scanlineCount);
        Shader.SetGlobalFloat(ChromaticAberrationId, chromaticAberration);
        Shader.SetGlobalFloat(VignetteId, vignette);
        Shader.SetGlobalFloat(MaskIntensityId, maskIntensity);
        Shader.SetGlobalFloat(NoiseIntensityId, noiseIntensity);
        Shader.SetGlobalFloat(TimeValueId, timeValue);
        Shader.SetGlobalFloat(BrightnessId, brightness);
        Shader.SetGlobalFloat(ContrastId, contrast);
        Shader.SetGlobalFloat(GlowId, phosphorGlow);

        // The full-screen fallback exposes real material properties as well.
        // Setting both paths keeps the component compatible with either shader.
        material.SetFloat(CurvatureId, curvature);
        material.SetFloat(ScanlineIntensityId, scanlineIntensity);
        material.SetFloat(ScanlineCountId, scanlineCount);
        material.SetFloat(ChromaticAberrationId, chromaticAberration);
        material.SetFloat(VignetteId, vignette);
        material.SetFloat(MaskIntensityId, maskIntensity);
        material.SetFloat(NoiseIntensityId, noiseIntensity);
        material.SetFloat(TimeValueId, timeValue);
        material.SetFloat(BrightnessId, brightness);
        material.SetFloat(ContrastId, contrast);
        material.SetFloat(GlowId, phosphorGlow);
    }

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        attachedCamera = GetComponent<Camera>();
        if (Application.isPlaying)
        {
            RegisterCanvasRenderCallback();
            EnsureFinalCompositeTarget();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        EnsureFinalCompositeTarget();
    }

    private void EnsureFinalCompositeTarget()
    {
        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null)
            return;

        // A CRT component copied onto the UI camera is only the final image
        // effect endpoint. It must not create another UI camera beneath itself.
        if (attachedCamera.name == UiCompositeCameraName)
        {
            activeUiCamera = attachedCamera;
            ConfigureOverlayCanvases(attachedCamera);
            return;
        }

        EnsureDedicatedUiCamera();
        Camera finalCamera = FindFinalCamera();
        if (finalCamera == null)
            return;

        CRTScreenEffect finalEffect = finalCamera.GetComponent<CRTScreenEffect>();
        if (finalEffect == null)
            finalEffect = finalCamera.gameObject.AddComponent<CRTScreenEffect>();

        if (finalEffect != this)
        {
            finalEffect.settingsSource = settingsSource != null
                ? settingsSource
                : this;
            finalEffect.PullSettingsFromSource();
        }

        activeUiCamera = finalCamera;
        ConfigureOverlayCanvases(finalCamera);
    }

    public void RefreshSceneCameraSettings()
    {
        attachedCamera = GetComponent<Camera>();
        settingsSource = null;
        EnsureFinalCompositeTarget();
        if (activeUiCamera != null)
        {
            ConfigureOverlayCanvases(activeUiCamera);
        }
    }

    private void EnsureDedicatedUiCamera()
    {
        if (generatedUiCamera == null)
        {
            Transform existing = transform.Find(UiCompositeCameraName);
            GameObject cameraObject = existing != null
                ? existing.gameObject
                : new GameObject(UiCompositeCameraName);
            cameraObject.transform.SetParent(transform, false);

            generatedUiCamera = cameraObject.GetComponent<Camera>();
            if (generatedUiCamera == null)
                generatedUiCamera = cameraObject.AddComponent<Camera>();
        }

        generatedUiCamera.transform.localPosition = Vector3.zero;
        generatedUiCamera.transform.localRotation = Quaternion.identity;
        generatedUiCamera.orthographic = attachedCamera.orthographic;
        generatedUiCamera.orthographicSize = attachedCamera.orthographicSize;
        generatedUiCamera.fieldOfView = attachedCamera.fieldOfView;
        generatedUiCamera.nearClipPlane = attachedCamera.nearClipPlane;
        generatedUiCamera.farClipPlane = attachedCamera.farClipPlane;
        generatedUiCamera.rect = attachedCamera.rect;
        generatedUiCamera.targetDisplay = attachedCamera.targetDisplay;
        generatedUiCamera.clearFlags = CameraClearFlags.Depth;
        generatedUiCamera.depth = attachedCamera.depth + 3f;
        generatedUiCamera.enabled = true;

        int uiLayer = LayerMask.NameToLayer("UI");
        generatedUiCamera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : 0;
    }

    private void RegisterCanvasRenderCallback()
    {
        if (canvasCallbackRegistered)
            return;

        Canvas.preWillRenderCanvases += ConfigureCanvasesBeforeRender;
        canvasCallbackRegistered = true;
    }

    private void ConfigureCanvasesBeforeRender()
    {
        if (!isActiveAndEnabled || activeUiCamera == null)
            return;

        // This callback is later than every Update/LateUpdate and immediately
        // precedes UI geometry submission. A UI controller therefore cannot
        // switch itself back to Overlay after this point and bypass CRT.
        ConfigureOverlayCanvases(activeUiCamera);
    }

    private bool IsFinalCompositeCamera()
    {
        return FindFinalCamera() == attachedCamera;
    }

    private Camera FindFinalCamera()
    {
        Camera[] cameras = Camera.allCameras;
        Camera result = attachedCamera;
        float highestDepth = attachedCamera.depth;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null
                || !candidate.enabled
                || !candidate.gameObject.activeInHierarchy
                || candidate.targetDisplay != attachedCamera.targetDisplay)
                continue;

            if (candidate.depth > highestDepth)
            {
                highestDepth = candidate.depth;
                result = candidate;
            }
        }

        return result;
    }

    public static Camera FindActiveUiCamera()
    {
        Camera[] cameras = Camera.allCameras;
        Camera fallback = null;
        float fallbackDepth = float.NegativeInfinity;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
                continue;

            if (candidate.name == UiCompositeCameraName)
                return candidate;

            if (candidate.GetComponent<CRTScreenEffect>() != null &&
                candidate.depth > fallbackDepth)
            {
                fallback = candidate;
                fallbackDepth = candidate.depth;
            }
        }

        return fallback;
    }

    private void ConfigureOverlayCanvases(Camera finalCamera)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        int uiLayer = LayerMask.NameToLayer("UI");
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            if (canvas.GetComponent<CRTCanvasBypass>() != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Persistent canvases may still reference a camera destroyed
                // during scene travel. Always attach them to the current final
                // Blocks/UI camera, not only when converting a new overlay.
                canvas.worldCamera = finalCamera;
                canvas.planeDistance =
                    Mathf.Max(finalCamera.nearClipPlane + 0.1f, 1f);
            }

            // Runtime-created attribute windows start on Default. Once their
            // Overlay canvas is moved to the final Blocks/UI camera, Default is
            // outside that camera's culling mask and the whole window vanishes.
            if (uiLayer >= 0
                && canvas.renderMode == RenderMode.ScreenSpaceCamera
                && canvas.worldCamera == finalCamera)
            {
                SetLayerRecursively(canvas.gameObject, uiLayer);
            }
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void PullSettingsFromSource()
    {
        if (settingsSource == null || settingsSource == this)
            return;

        crtMaterial = settingsSource.crtMaterial;
        curvature = settingsSource.curvature;
        scanlineIntensity = settingsSource.scanlineIntensity;
        scanlineCount = settingsSource.scanlineCount;
        chromaticAberration = settingsSource.chromaticAberration;
        vignette = settingsSource.vignette;
        maskIntensity = settingsSource.maskIntensity;
        noiseIntensity = settingsSource.noiseIntensity;
        brightness = settingsSource.brightness;
        contrast = settingsSource.contrast;
        phosphorGlow = settingsSource.phosphorGlow;
    }

    private void OnDisable()
    {
        if (canvasCallbackRegistered)
        {
            Canvas.preWillRenderCanvases -= ConfigureCanvasesBeforeRender;
            canvasCallbackRegistered = false;
        }

        activeUiCamera = null;

        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }

    private void OnValidate()
    {
        scanlineCount = Mathf.Max(1f, scanlineCount);
    }
}
