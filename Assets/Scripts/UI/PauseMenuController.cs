using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
[DefaultExecutionOrder(10000)]
public sealed class PauseMenuController : MonoBehaviour
{
    private static readonly Color GhostBlue = ZeldaUiPalette.Ghost;
    private static readonly Color DarkBlue = new Color(0.025f, 0.07f, 0.12f, 0.97f);
    private static readonly Color DisabledBlue = new Color(0.08f, 0.13f, 0.18f, 0.96f);

    [SerializeField] private Font menuFont;
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField, Range(0f, 1f)] private float backgroundDimOpacity = 0.76f;
    [SerializeField] private int canvasSortingOrder = 32767;

    private GameObject menuRoot;
    private Canvas menuCanvas;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        menuCanvas = GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureRenderPriority();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        BuildMenu();
        SetMenuVisible(false);
    }

    private void Update()
    {
        if (RetroSceneLoadReveal.IsBlockingInput) return;
        if (SaveSlotPanel.IsOpen || SaveSlotPanel.LastClosedFrame == Time.frameCount || GameSaveSystem.IsLoading) return;
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (isOpen)
        {
            CloseMenu();
        }
        else if (!DocumentReader.IsInputBlocked)
        {
            OpenMenu();
        }
    }

    private void LateUpdate()
    {
        EnsureRenderPriority();
    }

    private void EnsureRenderPriority()
    {
        if (menuCanvas == null)
        {
            menuCanvas = GetComponent<Canvas>();
        }

        menuCanvas.overrideSorting = true;
        menuCanvas.sortingLayerID = FindHighestSortingLayerId();
        menuCanvas.sortingOrder = short.MaxValue;

        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            menuCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            menuCanvas.worldCamera = uiCamera;
        }

        if (menuCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
            menuCanvas.worldCamera != null)
        {
            // Keep the menu closer than every renderable world object. The CRT
            // compositor still sees this camera-space canvas and affects it.
            menuCanvas.planeDistance =
                menuCanvas.worldCamera.nearClipPlane + 0.01f;
        }
        CRTScreenEffect.RegisterCanvas(menuCanvas);
    }

    public void OpenMenu()
    {
        if (isOpen || DocumentReader.IsInputBlocked)
            return;

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        isOpen = true;
        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetMenuVisible(true);
    }

    public void CloseMenu()
    {
        if (!isOpen)
            return;

        isOpen = false;
        IsPaused = false;
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        SetMenuVisible(false);
    }

    public void ReturnToTitleScreen()
    {
        if (!isOpen)
            return;

        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning("PauseMenuController needs a title scene name.", this);
            return;
        }

        RetroSceneLoadReveal.BeginTransition(titleSceneName.Trim(), () =>
        {
            isOpen = false;
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
            SceneManager.LoadScene(titleSceneName.Trim());
        });
    }

    private void BuildMenu()
    {
        menuRoot = CreateUiObject("Pause Menu Root", transform, typeof(Image));
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image dimmer = menuRoot.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, backgroundDimOpacity);
        dimmer.raycastTarget = true;

        GameObject panelObject = CreateUiObject("Pause Menu Panel", rootRect, typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(410f, 590f);
        Image panel = panelObject.GetComponent<Image>();
        panel.color = DarkBlue;
        panel.raycastTarget = true;
        CreateBorder(panelRect, GhostBlue, 3f);

        CreateButton(panelRect, "回到游戏", 188f, true, CloseMenu);
        CreateButton(panelRect, "储存游戏", 84f, true, () => OpenSaveSlots(true));
        CreateButton(panelRect, "读取游戏", -20f, true, () => OpenSaveSlots(false));
        CreateButton(panelRect, "游戏设置（暂未开放）", -124f, false, null);
        CreateButton(panelRect, "标题界面", -228f, true, ReturnToTitleScreen);
    }

    private void OpenSaveSlots(bool saving)
    {
        menuRoot.SetActive(false);
        SaveSlotPanel.Open(transform, menuFont, saving, false, () => menuRoot.SetActive(true));
    }

    private void CreateButton(
        RectTransform parent,
        string label,
        float verticalPosition,
        bool interactable,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(
            label,
            parent,
            typeof(Image),
            typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, verticalPosition);
        rect.sizeDelta = new Vector2(270f, 72f);

        Image background = buttonObject.GetComponent<Image>();
        Color normalBackgroundColor = interactable
            ? new Color(0.035f, 0.12f, 0.20f, 1f)
            : DisabledBlue;
        background.color = normalBackgroundColor;
        CreateBorder(rect, interactable ? GhostBlue : new Color(0.2f, 0.3f, 0.38f, 1f), 3f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.interactable = interactable;
        button.transition = Selectable.Transition.None;
        if (interactable)
        {
            PauseMenuButtonHover hover =
                buttonObject.AddComponent<PauseMenuButtonHover>();
            hover.Configure(
                background,
                normalBackgroundColor,
                Color.Lerp(normalBackgroundColor, GhostBlue, 0.38f));
        }
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        GameObject textObject = CreateUiObject("Label", rect, typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = menuFont;
        text.fontSize = interactable ? 22 : 19;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = interactable ? GhostBlue : new Color(0.44f, 0.57f, 0.66f, 1f);
        text.raycastTarget = false;
    }

    private static void CreateBorder(RectTransform parent, Color color, float thickness)
    {
        CreateBorderEdge(parent, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero);
        CreateBorderEdge(parent, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateBorderEdge(parent, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateBorderEdge(parent, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private static void CreateBorderEdge(
        RectTransform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edgeObject = CreateUiObject(name, parent, typeof(Image));
        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = edgeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent,
        params System.Type[] components)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != typeof(RectTransform))
            {
                result.AddComponent(components[i]);
            }
        }
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private static int FindHighestSortingLayerId()
    {
        int highestId = 0;
        int highestValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestValue)
            {
                highestValue = sortingLayer.value;
                highestId = sortingLayer.id;
            }
        }

        return highestId;
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            CloseMenu();
        }
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            IsPaused = false;
            Time.timeScale = previousTimeScale;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        backgroundDimOpacity = Mathf.Clamp01(backgroundDimOpacity);
        canvasSortingOrder = Mathf.Clamp(canvasSortingOrder, -32768, 32767);
    }
#endif
}
