using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Covers every newly loaded scene in black, then reveals it in horizontal
/// steps from the bottom of the screen to the top.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class RetroSceneLoadReveal : MonoBehaviour
{
    [SerializeField, Min(2)] private int revealSteps = 16;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float revealDuration = 0.42f;

    private static RetroSceneLoadReveal instance;

    private GameObject canvasObject;
    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform[] blackBands;
    private Coroutine revealRoutine;

    public static bool IsBlockingInput { get; private set; }

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
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        ShowFullBlack();
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
            IsBlockingInput = false;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ShowFullBlack();
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }

        revealRoutine = StartCoroutine(RevealAfterSceneLoad());
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
        }

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator RevealAfterSceneLoad()
    {
        // Keep at least one completely black rendered frame. This prevents scene
        // initialization and persistent-state restoration from flashing onscreen.
        yield return null;

        float restoreWaitDeadline = Time.realtimeSinceStartup + 3f;
        SceneTravelStateManager stateManager = SceneTravelStateManager.Instance;
        while (stateManager != null &&
               (stateManager.IsSceneTravelRestoreInProgress ||
                stateManager.IsQuickRestartPending) &&
               Time.realtimeSinceStartup < restoreWaitDeadline)
        {
            yield return null;
        }

        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        }

        float stepDuration = revealDuration / blackBands.Length;
        for (int index = 0; index < blackBands.Length; index++)
        {
            // Array order follows screen coordinates from bottom to top.
            blackBands[index].gameObject.SetActive(false);
            if (stepDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
            }
            else
            {
                yield return null;
            }
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasObject.SetActive(false);
        IsBlockingInput = false;
        revealRoutine = null;
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
    }
}
