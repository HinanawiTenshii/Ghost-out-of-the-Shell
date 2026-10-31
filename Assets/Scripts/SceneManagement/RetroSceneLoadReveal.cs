using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps state restoration hidden behind a terminal-style top-to-bottom refresh.
/// Presentation never advances or short-circuits the actual loading gates.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed partial class RetroSceneLoadReveal : MonoBehaviour
{
    [SerializeField, Min(2)] private int revealSteps = 16;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float revealDuration = 0.42f;
    [SerializeField, Min(0.01f)] private float coverDuration = 0.42f;
    private bool transitionPending;
    private bool waitingForSceneLoaded;
    private AsyncOperation pendingLoad;
    private bool loadClockPaused;
    private float loadTimeScale;
    private EventSystem blockedEventSystem;
    private bool previousNavigationEvents;

    private void Update()
    {
        if (!IsBlockingInput) return;
        BlockMenuNavigation();
        UpdateLoadingStatus();
    }

    private void BlockMenuNavigation()
    {
        EventSystem current = EventSystem.current;
        if (current == blockedEventSystem) return;
        RestoreMenuNavigation();
        if (current == null) return;
        blockedEventSystem = current;
        previousNavigationEvents = current.sendNavigationEvents;
        current.sendNavigationEvents = false;
        current.SetSelectedGameObject(null);
    }

    private void RestoreMenuNavigation()
    {
        if (blockedEventSystem != null) blockedEventSystem.sendNavigationEvents = previousNavigationEvents;
        blockedEventSystem = null;
    }

    private static RetroSceneLoadReveal instance;

    private GameObject canvasObject;
    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform[] blackBands;
    private Coroutine revealRoutine;

    public static bool IsBlockingInput { get; private set; }
    // Separate from input blocking: local panels/settlement need no world rebuild.
    public static bool IsPreparingGameplay { get; private set; }
    public static bool IsChangingScenes => instance != null && instance.pendingLoad != null;

    /// <summary>Start only after the transition has fully covered gameplay.</summary>
    public static void LoadSceneAsync(string sceneName)
    {
        CreateBeforeFirstSceneLoad();
        if (instance.pendingLoad != null) throw new System.InvalidOperationException("已有场景正在加载。");
        instance.loadTimeScale = Time.timeScale;
        instance.loadClockPaused = true;
        Time.timeScale = 0f; // Do not simulate combat or consume buffs during disk loading.
        try
        {
            SceneLoadMetrics.BeginEngineLoad();
            instance.pendingLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (instance.pendingLoad == null) throw new System.InvalidOperationException("无法异步加载场景：" + sceneName);
        }
        catch
        {
            instance.ResumeLoadClock();
            throw;
        }
    }

    private void ResumeLoadClock()
    {
        if (!loadClockPaused) return;
        Time.timeScale = loadTimeScale;
        loadClockPaused = false;
    }

    public static bool BeginTransition(string sceneName, System.Action loadScene)
    {
        CreateBeforeFirstSceneLoad();
        if (instance.transitionPending || IsBlockingInput) return false;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("无法加载场景：" + sceneName);
            return false;
        }
        instance.transitionPending = true;
        instance.ConfigurePresentation(sceneName, false);
        IsPreparingGameplay = true;
        SceneLoadMetrics.Begin(sceneName);
        SoulMarkRuntime.ClearForSceneTransition();
        DominoSkillRuntime.ClearForSceneTransition();
        IsBlockingInput = true;
        instance.BlockMenuNavigation();
        instance.StartCoroutine(instance.CoverBeforeLoad(loadScene));
        return true;
    }

    /// <summary>Same-scene travel uses the same black bands without reloading or resetting gameplay.</summary>
    public static bool BeginLocalTransition(System.Action moveUnderCover)
    {
        CreateBeforeFirstSceneLoad();
        if (moveUnderCover == null || instance.transitionPending || IsBlockingInput) return false;
        instance.transitionPending = true;
        instance.ConfigurePresentation(null, true);
        IsBlockingInput = true;
        instance.BlockMenuNavigation();
        instance.StartCoroutine(instance.CoverBeforeLoad(moveUnderCover, true));
        return true;
    }

    /// <summary>Arena layout reloads keep all scene-loading gates, but omit the region announcement.</summary>
    public static bool BeginLayoutTransition(string sceneName, System.Action loadScene)
    {
        if (!BeginTransition(sceneName, loadScene)) return false;
        instance.localPresentation = true;
        return true;
    }

    private IEnumerator CoverBeforeLoad(System.Action loadScene, bool localOnly = false)
    {
        // Stop outgoing music as the first black band appears, rather than
        // waiting for scene unload. Include disabled components whose sources
        // may still be playing; new scene players retain their normal startup.
        if (!localOnly) foreach (var music in FindObjectsOfType<LoopingBackgroundMusicPlayer>(true))
        {
            music.Stop();
        }
        if (!localOnly) foreach (var desert in FindObjectsOfType<DesertEncounterAudio>(true))
        {
            desert.StopForSceneTransition();
        }
        canvasObject.SetActive(true);
        canvasGroup.blocksRaycasts = true;
        yield return ScanScreen(false, coverDuration);
        BeginLoadingWait();
        // Present a fully black frame before capture/reset and asynchronous loading.
        yield return null;
        if (localOnly)
        {
            try { loadScene(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            revealRoutine = StartCoroutine(RevealAfterSceneLoad());
            yield break;
        }
        waitingForSceneLoaded = true;
        try { loadScene(); }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
        // Load failures must not leave the old scene permanently covered.
        yield return null;
        while (waitingForSceneLoaded && pendingLoad != null && !pendingLoad.isDone)
            yield return null;
        if (waitingForSceneLoaded)
        {
            waitingForSceneLoaded = false;
            pendingLoad = null;
            ResumeLoadClock();
            SceneTravelStateManager.Instance?.CancelPendingSceneLoad();
            GameSaveSystem.Instance?.CancelPendingSceneLoad();
            revealRoutine = StartCoroutine(RevealAfterSceneLoad());
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeFirstSceneLoad()
    {
        if (instance != null)
        {
            return;
        }

        GameObject revealObject = new GameObject("Retro Scene Load Reveal");
        revealObject.AddComponent<RetroSceneLoadReveal>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        IsPreparingGameplay = true;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        ConfigurePresentation(null, false);
        ShowFullBlack();
        BeginLoadingWait();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ResumeLoadClock();
            RestoreMenuNavigation();
            IsBlockingInput = false;
            IsPreparingGameplay = false;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pendingLoad = null;
        IsPreparingGameplay = true;
        SceneLoadMetrics.SceneLoaded();
        waitingForSceneLoaded = false;
        if (!transitionPending) localPresentation = false;
        SetPresentationDestination(scene.name);
        ShowFullBlack();
        SetLoadingPhase(LoadingPhase.Restoring);
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }

        revealRoutine = StartCoroutine(RevealAfterSceneLoad(true));
    }

    private void BuildOverlay()
    {
        canvasObject = new GameObject(
            "Retro Scene Load Black Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(CRTCanvasBypass));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerID = FindHighestSortingLayerId();
        overlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        var inputShield = new GameObject("Transition Input Shield", typeof(RectTransform), typeof(Image));
        inputShield.transform.SetParent(canvasObject.transform, false);
        var shieldRect = (RectTransform)inputShield.transform;
        shieldRect.anchorMin = Vector2.zero;
        shieldRect.anchorMax = Vector2.one;
        shieldRect.offsetMin = shieldRect.offsetMax = Vector2.zero;
        inputShield.GetComponent<Image>().color = Color.clear;

        int stepCount = Mathf.Max(2, revealSteps);
        blackBands = new RectTransform[stepCount];
        for (int index = 0; index < stepCount; index++)
        {
            GameObject bandObject = new GameObject(
                "Black Reveal Band " + index,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bandObject.transform.SetParent(canvasObject.transform, false);

            RectTransform band = bandObject.GetComponent<RectTransform>();
            float lowerAnchor = index / (float)stepCount;
            float upperAnchor = (index + 1f) / stepCount;
            band.anchorMin = new Vector2(0f, lowerAnchor);
            band.anchorMax = new Vector2(1f, upperAnchor);
            band.offsetMin = new Vector2(0f, -1f);
            band.offsetMax = new Vector2(0f, 1f);
            band.pivot = new Vector2(0.5f, 0.5f);

            Image image = bandObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            blackBands[index] = band;
        }

        BuildTerminalPresentation();
        CRTScreenEffect.RegisterCanvas(overlayCanvas);
    }

    private void ShowFullBlack()
    {
        if (canvasObject == null)
        {
            BuildOverlay();
        }

        canvasObject.SetActive(true);
        overlayCanvas.sortingLayerID = FindHighestSortingLayerId();
        overlayCanvas.sortingOrder = short.MaxValue;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        IsBlockingInput = true;

        for (int index = 0; index < blackBands.Length; index++)
        {
            blackBands[index].gameObject.SetActive(true);
            bandImages[index].color = Color.black;
        }
        SetScanVisible(false);

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator RevealAfterSceneLoad(bool prepareStreaming = false)
    {
        // Keep at least one completely black rendered frame. This prevents scene
        // initialization and persistent-state restoration from flashing onscreen.
        yield return null;

        SetLoadingPhase(LoadingPhase.Restoring);
        SceneTravelStateManager stateManager = SceneTravelStateManager.Instance;
        while (stateManager != null &&
               (stateManager.IsSceneTravelRestoreInProgress ||
                stateManager.IsQuickRestartPending))
        {
            yield return null;
        }

        // Entry snapshots and auto-save must finish before uncovering gameplay.
        while (GameSaveSystem.IsPreparingSceneSave || GameSaveSystem.IsLoading)
            yield return null;

        if (prepareStreaming)
        {
            SetLoadingPhase(LoadingPhase.Preparing);
            // The arrival position (including trapdoors/save restores) is final
            // now. Do not prepare against the camera's authored scene position.
            foreach (var follow in FindObjectsOfType<CameraFollowActiveZeldaMover>())
                follow.PrepareLoadedSceneView();
            foreach (var streamer in FindObjectsOfType<CameraVisionObjectStreaming>())
            {
                var preparation = streamer.PrepareInitialView();
                try
                {
                    while (true)
                    {
                        bool more;
                        try { more = preparation.MoveNext(); }
                        catch (System.Exception ex) { Debug.LogException(ex, streamer); break; }
                        if (!more) break;
                        yield return preparation.Current;
                    }
                }
                finally { (preparation as System.IDisposable)?.Dispose(); }
            }
            // Let Start/renderer registration from newly awakened objects finish
            // before the first band uncovers gameplay.
            yield return null;
        }

        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        }

        yield return ScanScreen(true, revealDuration);
        FinishPresentation();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasObject.SetActive(false);
        IsBlockingInput = false;
        IsPreparingGameplay = false;
        ResumeLoadClock();
        transitionPending = false;
        RestoreMenuNavigation();
        revealRoutine = null;
        SceneLoadMetrics.Complete();
    }

    private static int FindHighestSortingLayerId()
    {
        int highestLayerId = 0;
        int highestLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestLayerValue)
            {
                highestLayerValue = sortingLayer.value;
                highestLayerId = sortingLayer.id;
            }
        }

        return highestLayerId;
    }

    private void OnValidate()
    {
        revealSteps = Mathf.Max(2, revealSteps);
        blackHoldDuration = Mathf.Max(0f, blackHoldDuration);
        revealDuration = Mathf.Max(0.01f, revealDuration);
        coverDuration = Mathf.Max(0.01f, coverDuration);
    }
}
