using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private Camera cachedFinalCamera;
    private bool canvasCallbackRegistered;
    private bool sceneCallbackRegistered;
    private int lastCanvasConfigureFrame = -1;

    private static readonly HashSet<Canvas> RegisteredCanvases =
        new HashSet<Canvas>();
    private static readonly HashSet<Canvas> LayerConfiguredCanvases =
        new HashSet<Canvas>();
    private static readonly List<Canvas> DeadCanvases =
        new List<Canvas>();
    private static Camera cachedActiveUiCamera;
    private static bool canvasRegistrySeeded;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RegisteredCanvases.Clear();
        LayerConfiguredCanvases.Clear();
        DeadCanvases.Clear();
        cachedActiveUiCamera = null;
        canvasRegistrySeeded = false;
    }

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
        // Optional environment passes follow this camera's settings source to the final
        // compositor, so world, Blocks and UI receive one consistent treatment.
        var desertSource = settingsSource != null ? settingsSource : this;
        var desert = desertSource.GetComponent<DesertScreenEffect>();
        if (desert == null) desert = GetComponent<DesertScreenEffect>();
        var desertCity = desertSource.GetComponent<DesertCityScreenEffect>();
        if (desertCity == null) desertCity = GetComponent<DesertCityScreenEffect>();
        var water = desertSource.GetComponent<WaterCausticsScreenEffect>();
        if (water == null) water = GetComponent<WaterCausticsScreenEffect>();
        // City is an alternative desert grade, not an additional sand pass.
        bool renderCity = desertCity != null && desertCity.ShouldRender;
        bool renderDesert = !renderCity && desert != null && desert.ShouldRender;
        bool renderWarmGrade = renderCity || renderDesert;
        bool renderWater = water != null && water.ShouldRender;
        if (renderWarmGrade || renderWater)
        {
            var descriptor = source.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            var tinted = RenderTexture.GetTemporary(descriptor);
            RenderTexture reflected = null;
            try
            {
                if (renderCity) desertCity.Render(source, tinted);
                else if (renderDesert) desert.Render(source, tinted);
                else water.Render(source, tinted);
                RenderTexture composite = tinted;
                if (renderWarmGrade && renderWater)
                {
                    reflected = RenderTexture.GetTemporary(descriptor);
                    water.Render(tinted, reflected);
                    composite = reflected;
                }
                if (material != null)
                {
                    ApplyShaderSettings(material);
                    Graphics.Blit(composite, destination, material);
                }
                else Graphics.Blit(composite, destination);
            }
            finally
            {
                if (reflected != null) RenderTexture.ReleaseTemporary(reflected);
                RenderTexture.ReleaseTemporary(tinted);
            }
            return;
        }
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
            bool isFinalUiCamera = attachedCamera != null &&
                attachedCamera.name == UiCompositeCameraName;
            if (!isFinalUiCamera)
            {
                RegisterCanvasRenderCallback();
                RegisterSceneCallback();
                SeedCanvasRegistry(false);
            }
            EnsureFinalCompositeTarget();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;
        if (attachedCamera != null &&
            attachedCamera.name == UiCompositeCameraName)
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
            cachedFinalCamera = attachedCamera;
            cachedActiveUiCamera = attachedCamera;
            ConfigureRegisteredCanvases(attachedCamera, false);
            return;
        }

        EnsureDedicatedUiCamera();
        Camera finalCamera = FindFinalCamera();
        if (finalCamera == null)
            return;
        bool finalCameraChanged = activeUiCamera != finalCamera;

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
        cachedActiveUiCamera = finalCamera;
        if (finalCameraChanged)
        {
            ConfigureRegisteredCanvases(finalCamera, false);
        }
    }

    public void RefreshSceneCameraSettings()
    {
        attachedCamera = GetComponent<Camera>();
        settingsSource = null;
        EnsureFinalCompositeTarget();
        if (activeUiCamera != null)
        {
            SeedCanvasRegistry(true);
            ConfigureRegisteredCanvases(activeUiCamera, true);
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

    private void RegisterSceneCallback()
    {
        if (sceneCallbackRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneCallbackRegistered = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedFinalCamera = null;
        canvasRegistrySeeded = false;
        SeedCanvasRegistry(true);
        EnsureFinalCompositeTarget();
    }

    private void ConfigureCanvasesBeforeRender()
    {
        if (!isActiveAndEnabled || activeUiCamera == null)
            return;

        if (lastCanvasConfigureFrame == Time.frameCount)
            return;

        // This callback is later than every Update/LateUpdate and immediately
        // precedes UI geometry submission. A UI controller therefore cannot
        // switch itself back to Overlay after this point and bypass CRT.
        lastCanvasConfigureFrame = Time.frameCount;
        ConfigureRegisteredCanvases(activeUiCamera, false);
    }

    private bool IsFinalCompositeCamera()
    {
        return FindFinalCamera() == attachedCamera;
    }

    private Camera FindFinalCamera()
    {
        if (cachedFinalCamera != null &&
            cachedFinalCamera.enabled &&
            cachedFinalCamera.gameObject.activeInHierarchy &&
            cachedFinalCamera.targetDisplay == attachedCamera.targetDisplay)
        {
            return cachedFinalCamera;
        }

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

        cachedFinalCamera = result;
        return cachedFinalCamera;
    }

    public static Camera FindActiveUiCamera()
    {
        if (cachedActiveUiCamera != null &&
            cachedActiveUiCamera.enabled &&
            cachedActiveUiCamera.gameObject.activeInHierarchy)
        {
            return cachedActiveUiCamera;
        }

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
            {
                cachedActiveUiCamera = candidate;
                return candidate;
            }

            if (candidate.GetComponent<CRTScreenEffect>() != null &&
                candidate.depth > fallbackDepth)
            {
                fallback = candidate;
                fallbackDepth = candidate.depth;
            }
        }

        cachedActiveUiCamera = fallback;
        return cachedActiveUiCamera;
    }

    public static void RegisterCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        RegisteredCanvases.Add(canvas);
        LayerConfiguredCanvases.Remove(canvas);
        Camera uiCamera = FindActiveUiCamera();
        if (uiCamera != null)
        {
            ConfigureCanvas(canvas, uiCamera);
        }
    }

    private static void SeedCanvasRegistry(bool forceRefresh)
    {
        if (canvasRegistrySeeded && !forceRefresh)
            return;

        if (forceRefresh)
        {
            LayerConfiguredCanvases.Clear();
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
            {
                RegisteredCanvases.Add(canvases[i]);
            }
        }
        canvasRegistrySeeded = true;
    }

    private static void ConfigureRegisteredCanvases(
        Camera finalCamera,
        bool forceRegistryRefresh)
    {
        if (finalCamera == null)
            return;

        SeedCanvasRegistry(forceRegistryRefresh);
        DeadCanvases.Clear();
        foreach (Canvas canvas in RegisteredCanvases)
        {
            if (canvas == null)
            {
                DeadCanvases.Add(canvas);
                continue;
            }

            ConfigureCanvas(canvas, finalCamera);
        }
        for (int index = 0; index < DeadCanvases.Count; index++)
        {
            RegisteredCanvases.Remove(DeadCanvases[index]);
            LayerConfiguredCanvases.Remove(DeadCanvases[index]);
        }
    }

    private static void ConfigureCanvas(Canvas canvas, Camera finalCamera)
    {
        RuntimeUiVisibility.Register(canvas);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (canvas.GetComponent<CRTCanvasBypass>() != null)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.worldCamera != null)
                canvas.worldCamera = null;
            return;
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
            if (canvas.worldCamera != finalCamera)
                canvas.worldCamera = finalCamera;
            float planeDistance =
                Mathf.Max(finalCamera.nearClipPlane + 0.1f, 1f);
            if (!Mathf.Approximately(canvas.planeDistance, planeDistance))
                canvas.planeDistance = planeDistance;
        }

        // Runtime-created attribute windows start on Default. Once their
        // Overlay canvas is moved to the final Blocks/UI camera, Default is
        // outside that camera's culling mask and the whole window vanishes.
        if (uiLayer >= 0
            && canvas.renderMode == RenderMode.ScreenSpaceCamera
            && canvas.worldCamera == finalCamera
            && (!LayerConfiguredCanvases.Contains(canvas) ||
                canvas.gameObject.layer != uiLayer))
        {
            SetLayerRecursively(canvas.gameObject, uiLayer);
            LayerConfiguredCanvases.Add(canvas);
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
        if (sceneCallbackRegistered)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            sceneCallbackRegistered = false;
        }

        if (cachedActiveUiCamera == attachedCamera)
            cachedActiveUiCamera = null;
        activeUiCamera = null;
        cachedFinalCamera = null;

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

/// <summary>Presentation-only F12 toggle; menu/gameplay state continues unchanged.</summary>
[DefaultExecutionOrder(32001)]
public sealed class RuntimeUiVisibility : MonoBehaviour
{
    private static RuntimeUiVisibility instance;
    private static readonly HashSet<Canvas> canvases = new HashSet<Canvas>();
    private readonly Dictionary<CanvasGroup, float> hiddenGroups = new Dictionary<CanvasGroup, float>();
    private readonly Dictionary<Camera, int> cameraMasks = new Dictionary<Camera, int>();
    private bool hidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; canvases.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        instance = new GameObject("UI Visibility Toggle").AddComponent<RuntimeUiVisibility>();
        DontDestroyOnLoad(instance.gameObject);
    }

    public static void Register(Canvas canvas)
    {
        if (!Application.isPlaying || canvas == null) return;
        canvases.Add(canvas);
        if (instance != null && instance.hidden) instance.HideCanvas(canvas);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += SceneLoaded;
        Camera.onPreCull += BeforeCamera;
        Camera.onPostRender += AfterCamera;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        canvases.RemoveWhere(c => c == null);
        // Scene-boundary discovery only; runtime CRT canvases register as they are configured.
        foreach (var canvas in FindObjectsOfType<Canvas>(true)) Register(canvas);
    }

    private void Update()
    {
        RestoreGroups();
        if (Input.GetKeyDown(KeyCode.F12)) hidden = !hidden;
    }

    private void LateUpdate()
    {
        UpdateCursorVisibility();
        if (!hidden) return;
        foreach (var canvas in canvases) if (canvas != null) HideCanvas(canvas);
    }

    private void HideCanvas(Canvas canvas)
    {
        var group = canvas.GetComponent<CanvasGroup>();
        if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
        if (!hiddenGroups.ContainsKey(group)) hiddenGroups.Add(group, group.alpha);
        group.alpha = 0f;
    }

    private static void UpdateCursorVisibility()
    {
        // Use the same title-scene convention as the save system. Do not depend
        // on a controlled mover: it can temporarily disappear during possession.
        string sceneName = SceneManager.GetActiveScene().name;
        bool titleScene = sceneName.Replace(" ", "").Equals("TitleScreen", System.StringComparison.OrdinalIgnoreCase);
        bool show = titleScene || PauseMenuController.IsPaused || TabJournalMenuController.IsOpen || SaveSlotPanel.IsOpen;
        if (Cursor.visible != show) Cursor.visible = show;
        // Hide only the visual cursor; mouse aiming and right-click inspection
        // still need the pointer position instead of locking it to screen centre.
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreGroups()
    {
        foreach (var entry in hiddenGroups) if (entry.Key != null) entry.Key.alpha = entry.Value;
        hiddenGroups.Clear();
    }

    private void BeforeCamera(Camera camera)
    {
        if (!hidden || camera == null) return;
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) return;
        if (!cameraMasks.ContainsKey(camera)) cameraMasks.Add(camera, camera.cullingMask);
        camera.cullingMask &= ~(1 << uiLayer);
    }

    private void AfterCamera(Camera camera)
    {
        if (camera != null && cameraMasks.TryGetValue(camera, out int mask))
        {
            camera.cullingMask = mask;
            cameraMasks.Remove(camera);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Camera.onPreCull -= BeforeCamera;
        Camera.onPostRender -= AfterCamera;
        RestoreGroups();
        foreach (var entry in cameraMasks) if (entry.Key != null) entry.Key.cullingMask = entry.Value;
        cameraMasks.Clear();
    }
}
