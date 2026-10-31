using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
[DefaultExecutionOrder(10000)]
public sealed partial class TitleScreenController : MonoBehaviour
{
    private static readonly Color GhostBlue = new Color32(76, 204, 230, 255);
    private static readonly Color BackgroundBlue = new Color32(2, 8, 19, 255);
    private static readonly Color DisabledBlue = new Color(0.18f, 0.28f, 0.35f, 1f);

    [SerializeField] private Font titleFont;

    private Canvas titleCanvas;
    private RectTransform mainOptionsRoot;
    private Button continueButton;
    private Text mainStatus;

    private void Awake()
    {
        Time.timeScale = 1f;
        titleCanvas = GetComponent<Canvas>();
        titleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureRenderPriority();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        // Keep the complete terminal visible at 4:3 and ultrawide resolutions.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        EnsureEventSystem();
        BuildInterface();
        StartCoroutine(BootTerminal());
        if (GameSaveSystem.Instance != null) GameSaveSystem.Instance.Changed += RefreshSaveMessage;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void LateUpdate()
    {
        EnsureRenderPriority();
    }

    private void Update()
    {
        if (!terminalReady || RetroSceneLoadReveal.IsBlockingInput) return;
        if (SaveSlotPanel.IsOpen || SaveSlotPanel.LastClosedFrame == Time.frameCount) return;
        if (progressOptionsRoot != null && progressOptionsRoot.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        { progressBack?.Invoke(); return; }
        RestoreTerminalSelection();
    }

    public void StartGame()
    {
        ShowNewGameSlots();
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
        BuildTerminalInterface();
    }

    private void OpenLoadSlots()
    {
        HideOptions();
        SaveSlotPanel.Open(mainOptionsRoot.parent, titleFont, false, true, ShowProfileActions);
    }

    private static void ConfigureOptionsRoot(RectTransform optionsRoot)
    {
        optionsRoot.anchorMin = optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        optionsRoot.pivot = new Vector2(0.5f, 0.5f);
        optionsRoot.anchoredPosition = new Vector2(310f, 0f);
        optionsRoot.sizeDelta = new Vector2(440f, 440f);
    }

    public void ShowMainMenu()
    {
        if (progressOptionsRoot != null) progressOptionsRoot.gameObject.SetActive(false);
        if (mainOptionsRoot != null)
        {
            mainOptionsRoot.gameObject.SetActive(true);
        }
        if (mainStatus != null) mainStatus.text = "";
        RefreshContinueButton();
        RestoreTerminalSelection();
    }

    private Text CreateTitleText(
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
        rect.sizeDelta = new Vector2(540f, fontSize + 16f);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = titleFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = GhostBlue;
        text.raycastTarget = false;
        TitleScreenGlow.Attach(text);
        EnhanceTerminalGlow(text);
        return text;
    }

    private Button CreateOption(
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
        bool mainMenu = parent.name == "Title Options";
        rect.sizeDelta = mainMenu ? new Vector2(400f, 48f) : new Vector2(440f, 48f);

        Image image = optionObject.GetComponent<Image>();
        image.color = Color.clear;

        Button button = optionObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        button.transition = Selectable.Transition.None;
        if (action != null)
        {
            button.onClick.AddListener(() =>
            {
                if (terminalReady && button.IsInteractable() && !RetroSceneLoadReveal.IsBlockingInput)
                {
                    PlayTerminalConfirm();
                    action();
                }
            });
        }

        GameObject textObject = CreateUiObject("Label", rect, typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(36f, 2f);
        textRect.offsetMax = new Vector2(-32f, -2f);
        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = titleFont;
        text.fontSize = mainMenu && interactable ? 30 : 22;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = interactable ? GhostBlue : DisabledBlue;
        text.raycastTarget = false;
        TitleScreenGlow.Attach(text);
        EnhanceTerminalGlow(text);
        var feedback = optionObject.AddComponent<TitleTerminalOption>();
        feedback.Configure(text, GhostBlue, DisabledBlue, this);
        return button;
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
        CRTScreenEffect.RegisterCanvas(titleCanvas, enableGlow: true);
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
        EnhanceTerminalGlow(image);
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
