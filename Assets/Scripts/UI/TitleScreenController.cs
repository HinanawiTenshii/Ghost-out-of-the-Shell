using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
[DefaultExecutionOrder(10000)]
public sealed class TitleScreenController : MonoBehaviour
{
    private static readonly Color GhostBlue = ZeldaUiPalette.Ghost;
    private static readonly Color BackgroundBlue = new Color(0.018f, 0.055f, 0.095f, 1f);
    private static readonly Color ButtonBlue = new Color(0.035f, 0.12f, 0.20f, 0.92f);
    private static readonly Color DisabledBlue = new Color(0.18f, 0.28f, 0.35f, 1f);

    [SerializeField] private Font titleFont;
    [Header("Level Selection")]
    [SerializeField] private string tutorialSceneName = "TutorialLevel";
    [SerializeField] private string level0SceneName = "Level0";
    [SerializeField] private string level1SceneName = "Level1-Garden";

    private Canvas titleCanvas;
    private RectTransform mainOptionsRoot;
    private RectTransform levelSelectOptionsRoot;

    private void Awake()
    {
        Time.timeScale = 1f;
        titleCanvas = GetComponent<Canvas>();
        titleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureRenderPriority();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        BuildInterface();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void LateUpdate()
    {
        EnsureRenderPriority();
    }

    private void Update()
    {
        if (RetroSceneLoadReveal.IsBlockingInput) return;
        if (levelSelectOptionsRoot != null &&
            levelSelectOptionsRoot.gameObject.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            ShowMainMenu();
        }
    }

    public void StartGame()
    {
        ShowLevelSelection();
    }

    public void LoadTutorialLevel()
    {
        LoadFreshScene(tutorialSceneName);
    }

    public void LoadLevel0()
    {
        LoadFreshScene(level0SceneName);
    }

    public void LoadLevel1()
    {
        LoadFreshScene(level1SceneName);
    }

    private void LoadFreshScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("TitleScreenController needs a target scene name.", this);
            return;
        }

        RetroSceneLoadReveal.BeginTransition(sceneName.Trim(), () =>
        {
            GameSaveSystem.Instance?.BeginNewGame();
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
            SceneManager.LoadScene(sceneName.Trim());
        });
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildInterface()
    {
        GameObject backgroundObject = CreateUiObject(
            "Title Background",
            transform,
            typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        Stretch(backgroundRect);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = BackgroundBlue;
        background.raycastTarget = true;

        GameObject outerFrameObject = CreateUiObject(
            "Outer Frame",
            backgroundRect,
            typeof(Image));
        RectTransform outerFrameRect = outerFrameObject.GetComponent<RectTransform>();
        outerFrameRect.anchorMin = new Vector2(0.04f, 0.06f);
        outerFrameRect.anchorMax = new Vector2(0.96f, 0.94f);
        outerFrameRect.offsetMin = Vector2.zero;
        outerFrameRect.offsetMax = Vector2.zero;
        outerFrameObject.GetComponent<Image>().color =
            new Color(0.015f, 0.035f, 0.055f, 0.96f);
        CreateBorder(outerFrameRect, GhostBlue, 3f);

        GameObject titlePanelObject = CreateUiObject(
            "Title Geometry",
            outerFrameRect,
            typeof(Image));
        RectTransform titlePanelRect = titlePanelObject.GetComponent<RectTransform>();
        titlePanelRect.anchorMin = titlePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        titlePanelRect.pivot = new Vector2(0.5f, 0.5f);
        titlePanelRect.anchoredPosition = new Vector2(-245f, 0f);
        titlePanelRect.sizeDelta = new Vector2(420f, 390f);
        titlePanelObject.GetComponent<Image>().color =
            new Color(0.025f, 0.075f, 0.12f, 0.82f);
        CreateBorder(titlePanelRect, GhostBlue, 3f);

        CreateTitleText(titlePanelRect, "GHOST", new Vector2(0f, 64f), 84);
        CreateTitleText(titlePanelRect, "out of the", new Vector2(0f, 0f), 24);
        CreateTitleText(titlePanelRect, "SHELL", new Vector2(0f, -64f), 84);

        RectTransform optionsRoot = CreateUiObject(
            "Title Options",
            outerFrameRect).GetComponent<RectTransform>();
        optionsRoot.anchorMin = optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRoot.pivot = new Vector2(0.5f, 0.5f);
        optionsRoot.anchoredPosition = new Vector2(255f, 0f);
        optionsRoot.sizeDelta = new Vector2(330f, 390f);

        CreateOption(optionsRoot, "开始游戏", 105f, true, StartGame);
        CreateOption(optionsRoot, "载入游戏", 35f, true, OpenLoadSlots);
        CreateOption(optionsRoot, "游戏设置（暂未开放）", -35f, false, null);
        CreateOption(optionsRoot, "结束游戏", -105f, true, QuitGame);
        mainOptionsRoot = optionsRoot;

        levelSelectOptionsRoot = CreateUiObject(
            "Level Selection Options",
            outerFrameRect).GetComponent<RectTransform>();
        ConfigureOptionsRoot(levelSelectOptionsRoot);
        CreateOption(levelSelectOptionsRoot, "教学关卡", 120f, true, LoadTutorialLevel);
        CreateOption(levelSelectOptionsRoot, "Level 0-监牢", 60f, true, LoadLevel0);
        CreateOption(levelSelectOptionsRoot, "Level 1-城堡", 0f, true, LoadLevel1);
        CreateOption(levelSelectOptionsRoot, "未完待续", -60f, false, null);
        CreateOption(levelSelectOptionsRoot, "返回", -120f, true, ShowMainMenu);
        levelSelectOptionsRoot.gameObject.SetActive(false);
    }

    private void OpenLoadSlots()
    {
        mainOptionsRoot.gameObject.SetActive(false);
        SaveSlotPanel.Open(mainOptionsRoot.parent, titleFont, false, true, ShowMainMenu);
    }

    private static void ConfigureOptionsRoot(RectTransform optionsRoot)
    {
        optionsRoot.anchorMin = optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRoot.pivot = new Vector2(0.5f, 0.5f);
        optionsRoot.anchoredPosition = new Vector2(255f, 0f);
        optionsRoot.sizeDelta = new Vector2(330f, 390f);
    }

    private void ShowLevelSelection()
    {
        if (mainOptionsRoot != null)
        {
            mainOptionsRoot.gameObject.SetActive(false);
        }
        if (levelSelectOptionsRoot != null)
        {
            levelSelectOptionsRoot.gameObject.SetActive(true);
        }
    }

    public void ShowMainMenu()
    {
        if (levelSelectOptionsRoot != null)
        {
            levelSelectOptionsRoot.gameObject.SetActive(false);
        }
        if (mainOptionsRoot != null)
        {
            mainOptionsRoot.gameObject.SetActive(true);
        }
    }

    private void CreateTitleText(
        RectTransform parent,
        string value,
        Vector2 position,
        int fontSize)
    {
        GameObject textObject = CreateUiObject(value, parent, typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, fontSize + 8f);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = titleFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = GhostBlue;
        text.raycastTarget = false;
        TitleScreenGlow.Attach(text);
    }

    private void CreateOption(
        RectTransform parent,
        string label,
        float verticalPosition,
        bool interactable,
        UnityEngine.Events.UnityAction action)
    {
        GameObject optionObject = CreateUiObject(
            label,
            parent,
            typeof(Image),
            typeof(Button));
        RectTransform rect = optionObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, verticalPosition);
        rect.sizeDelta = new Vector2(320f, 56f);

        Image image = optionObject.GetComponent<Image>();
        Color normalColor = interactable ? ButtonBlue : new Color(0f, 0f, 0f, 0f);
        image.color = normalColor;

        Button button = optionObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        button.transition = Selectable.Transition.None;
        if (interactable)
        {
            PauseMenuButtonHover hover =
                optionObject.AddComponent<PauseMenuButtonHover>();
            hover.Configure(
                image,
                normalColor,
                Color.Lerp(normalColor, GhostBlue, 0.42f));
        }
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        GameObject textObject = CreateUiObject("Label", rect, typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(12f, 2f);
        textRect.offsetMax = new Vector2(-12f, -2f);
        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = titleFont;
        text.fontSize = interactable ? 23 : 19;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = interactable ? GhostBlue : DisabledBlue;
        text.raycastTarget = false;
        TitleScreenGlow.Attach(text);
    }

    private void EnsureRenderPriority()
    {
        if (titleCanvas == null)
        {
            titleCanvas = GetComponent<Canvas>();
        }

        titleCanvas.overrideSorting = true;
        titleCanvas.sortingOrder = short.MaxValue;

        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            titleCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            titleCanvas.worldCamera = uiCamera;
        }

        if (titleCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
            titleCanvas.worldCamera != null)
        {
            titleCanvas.planeDistance =
                titleCanvas.worldCamera.nearClipPlane + 0.01f;
        }
        CRTScreenEffect.RegisterCanvas(titleCanvas);
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

    private static void CreateBorder(RectTransform parent, Color color, float thickness)
    {
        CreateEdge(parent, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero);
        CreateEdge(parent, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateEdge(parent, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateEdge(parent, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private static void CreateEdge(
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
        TitleScreenGlow.Attach(image);
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
}
