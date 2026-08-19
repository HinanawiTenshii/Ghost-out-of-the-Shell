using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runtime four-digit password UI shared by password-locked doors.</summary>
[DisallowMultipleComponent]
public sealed class DoorPasswordPanel : MonoBehaviour
{
    private static readonly Color AccentColor = ZeldaUiPalette.Ghost;
    private static readonly Color SelectedColor = ZeldaUiPalette.Primary;
    private static readonly Color PanelColor =
        new Color(0.025f, 0.07f, 0.12f, 0.98f);
    private static DoorPasswordPanel activePanel;
    private static Sprite triangleSprite;

    private readonly int[] enteredDigits = new int[4];
    private readonly Text[] digitTexts = new Text[4];
    private readonly Image[] digitBackgrounds = new Image[4];

    private GameObject canvasObject;
    private Canvas passwordCanvas;
    private Font passwordFont;
    private string correctPassword = "1111";
    private Action<bool> completionCallback;
    private int selectedDigit;
    private int openedFrame;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool isOpen;

    public static bool IsOpen => activePanel != null && activePanel.isOpen;

    public void Open(Font configuredFont, string configuredPassword, Action<bool> callback)
    {
        if (isOpen || (activePanel != null && activePanel != this))
        {
            return;
        }

        passwordFont = configuredFont;
        correctPassword = NormalizePassword(configuredPassword);
        completionCallback = callback;
        selectedDigit = 0;
        for (int i = 0; i < enteredDigits.Length; i++)
        {
            enteredDigits[i] = 0;
        }

        EnsureUiBuilt();
        if (canvasObject == null)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        openedFrame = Time.frameCount;
        isOpen = true;
        activePanel = this;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        canvasObject.SetActive(true);
        UpdateDigitVisuals();
        EnsureRenderPriority();
    }

    private void Update()
    {
        if (!isOpen || Time.frameCount <= openedFrame)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            selectedDigit = (selectedDigit + 3) % 4;
            UpdateDigitVisuals();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            selectedDigit = (selectedDigit + 1) % 4;
            UpdateDigitVisuals();
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            ChangeDigit(selectedDigit, 1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            ChangeDigit(selectedDigit, -1);
        }

        int typedDigit = ReadTypedDigit();
        if (typedDigit >= 0)
        {
            enteredDigits[selectedDigit] = typedDigit;
            selectedDigit = (selectedDigit + 1) % 4;
            UpdateDigitVisuals();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            SubmitAndClose();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close(false, false);
        }
    }

    private void LateUpdate()
    {
        if (isOpen)
        {
            EnsureRenderPriority();
        }
    }

    private void ChangeDigit(int index, int delta)
    {
        if (!isOpen || index < 0 || index >= enteredDigits.Length)
        {
            return;
        }

        selectedDigit = index;
        enteredDigits[index] = (enteredDigits[index] + delta + 10) % 10;
        UpdateDigitVisuals();
    }

    private void SubmitAndClose()
    {
        string enteredPassword = string.Concat(
            enteredDigits[0],
            enteredDigits[1],
            enteredDigits[2],
            enteredDigits[3]);
        bool correct = string.Equals(
            enteredPassword,
            correctPassword,
            StringComparison.Ordinal);
        Close(true, correct);
    }

    public void CloseWithoutSubmission()
    {
        Close(false, false);
    }

    private void Close(bool invokeCallback, bool result)
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        if (activePanel == this)
        {
            activePanel = null;
        }

        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }

        Action<bool> callback = completionCallback;
        completionCallback = null;
        if (invokeCallback)
        {
            callback?.Invoke(result);
        }
    }

    private void EnsureUiBuilt()
    {
        if (canvasObject != null)
        {
            return;
        }

        if (passwordFont == null)
        {
            ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
            passwordFont = ui != null ? ui.PermissionLabelFont : null;
        }
        if (passwordFont == null)
        {
            passwordFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        EnsureEventSystem();
        EnsureTriangleSprite();

        canvasObject = new GameObject(
            name + " Password Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        if (gameObject.scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        }
        passwordCanvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = CreateUiObject(
            "Password Window Root",
            canvasObject.transform,
            typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image dimmer = root.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.76f);
        dimmer.raycastTarget = true;

        GameObject panelObject = CreateUiObject(
            "Password Panel",
            rootRect,
            typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 22f);
        panelRect.sizeDelta = new Vector2(800f, 340f);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;
        CreateBorder(panelRect, AccentColor, 3f);

        float[] horizontalPositions = { -270f, -90f, 90f, 270f };
        for (int i = 0; i < enteredDigits.Length; i++)
        {
            CreateDigitColumn(panelRect, i, horizontalPositions[i]);
        }

        GameObject confirmObject = CreateUiObject(
            "Confirm Label",
            rootRect,
            typeof(Text));
        RectTransform confirmRect = confirmObject.GetComponent<RectTransform>();
        confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.pivot = new Vector2(0.5f, 0.5f);
        confirmRect.anchoredPosition = new Vector2(0f, -206f);
        confirmRect.sizeDelta = new Vector2(320f, 54f);
        Text confirmText = confirmObject.GetComponent<Text>();
        confirmText.text = "按[E]确认";
        confirmText.font = passwordFont;
        confirmText.fontSize = 25;
        confirmText.alignment = TextAnchor.MiddleCenter;
        confirmText.color = AccentColor;
        confirmText.raycastTarget = false;
        passwordFont.RequestCharactersInTexture(
            "按[E]确认0123456789",
            48,
            FontStyle.Normal);

        canvasObject.SetActive(false);
    }

    private void CreateDigitColumn(RectTransform parent, int index, float x)
    {
        CreateArrowButton(parent, index, 1, x, 112f, false);

        GameObject digitObject = CreateUiObject(
            "Digit " + (index + 1),
            parent,
            typeof(Image));
        RectTransform digitRect = digitObject.GetComponent<RectTransform>();
        digitRect.anchorMin = digitRect.anchorMax = new Vector2(0.5f, 0.5f);
        digitRect.pivot = new Vector2(0.5f, 0.5f);
        digitRect.anchoredPosition = new Vector2(x, 0f);
        digitRect.sizeDelta = new Vector2(112f, 112f);
        Image digitBackground = digitObject.GetComponent<Image>();
        digitBackground.color = new Color(0.035f, 0.12f, 0.20f, 1f);
        digitBackground.raycastTarget = false;
        digitBackgrounds[index] = digitBackground;
        CreateBorder(digitRect, AccentColor, 3f);

        GameObject textObject = CreateUiObject(
            "Value",
            digitRect,
            typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        Text digitText = textObject.GetComponent<Text>();
        digitText.text = "0";
        digitText.font = passwordFont;
        digitText.fontSize = 44;
        digitText.alignment = TextAnchor.MiddleCenter;
        digitText.color = AccentColor;
        digitText.raycastTarget = false;
        digitTexts[index] = digitText;

        CreateArrowButton(parent, index, -1, x, -112f, true);
    }

    private void CreateArrowButton(
        RectTransform parent,
        int index,
        int delta,
        float x,
        float y,
        bool pointDown)
    {
        GameObject buttonObject = CreateUiObject(
            (pointDown ? "Down " : "Up ") + (index + 1),
            parent,
            typeof(Image),
            typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(96f, 62f);
        rect.localEulerAngles = pointDown
            ? new Vector3(0f, 0f, 180f)
            : Vector3.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = triangleSprite;
        image.color = AccentColor;
        image.preserveAspect = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = AccentColor;
        colors.highlightedColor = Color.Lerp(AccentColor, Color.white, 0.38f);
        colors.pressedColor = SelectedColor;
        colors.selectedColor = AccentColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        int capturedIndex = index;
        button.onClick.AddListener(() => ChangeDigit(capturedIndex, delta));
    }

    private void UpdateDigitVisuals()
    {
        for (int i = 0; i < digitTexts.Length; i++)
        {
            if (digitTexts[i] != null)
            {
                digitTexts[i].text = enteredDigits[i].ToString();
                digitTexts[i].color = i == selectedDigit
                    ? SelectedColor
                    : AccentColor;
            }
            if (digitBackgrounds[i] != null)
            {
                digitBackgrounds[i].color = i == selectedDigit
                    ? new Color(0.05f, 0.18f, 0.28f, 1f)
                    : new Color(0.035f, 0.12f, 0.20f, 1f);
            }
        }
    }

    private void EnsureRenderPriority()
    {
        if (passwordCanvas == null)
        {
            return;
        }

        passwordCanvas.overrideSorting = true;
        passwordCanvas.sortingLayerID = FindHighestSortingLayerId();
        passwordCanvas.sortingOrder = short.MaxValue;
        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            passwordCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            passwordCanvas.worldCamera = uiCamera;
            passwordCanvas.planeDistance = uiCamera.nearClipPlane + 0.01f;
        }
        else
        {
            passwordCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            passwordCanvas.worldCamera = null;
        }
    }

    private static int ReadTypedDigit()
    {
        KeyCode[] topRow =
        {
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7,
            KeyCode.Alpha8, KeyCode.Alpha9
        };
        KeyCode[] keypad =
        {
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
            KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7,
            KeyCode.Keypad8, KeyCode.Keypad9
        };
        for (int i = 0; i < 10; i++)
        {
            if (Input.GetKeyDown(topRow[i]) || Input.GetKeyDown(keypad[i]))
            {
                return i;
            }
        }
        return -1;
    }

    public static string NormalizePassword(string value)
    {
        char[] result = { '0', '0', '0', '0' };
        int outputIndex = 0;
        if (!string.IsNullOrEmpty(value))
        {
            for (int i = 0; i < value.Length && outputIndex < result.Length; i++)
            {
                if (value[i] >= '0' && value[i] <= '9')
                {
                    result[outputIndex++] = value[i];
                }
            }
        }
        return new string(result);
    }

    private static void EnsureTriangleSprite()
    {
        if (triangleSprite != null)
        {
            return;
        }

        const int width = 64;
        const int height = 44;
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);
        texture.name = "Door Password Triangle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float fromApex = (height - 1 - y) / (float)(height - 1);
            float halfWidth = fromApex * center;
            for (int x = 0; x < width; x++)
            {
                bool inside = Mathf.Abs(x - center) <= halfWidth;
                bool edge = inside &&
                    (Mathf.Abs(Mathf.Abs(x - center) - halfWidth) <= 2.2f ||
                     y <= 2);
                texture.SetPixel(x, y, edge ? Color.white : Color.clear);
            }
        }
        texture.Apply(false, true);
        triangleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            64f);
        triangleSprite.name = "Door Password Triangle";
    }

    private static void CreateBorder(RectTransform parent, Color color, float thickness)
    {
        CreateBorderEdge(parent, "Top", color,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero);
        CreateBorderEdge(parent, "Bottom", color,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateBorderEdge(parent, "Left", color,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateBorderEdge(parent, "Right", color,
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private static void CreateBorderEdge(
        RectTransform parent,
        string edgeName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edgeObject = CreateUiObject(edgeName, parent, typeof(Image));
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
        params Type[] components)
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
        {
            return;
        }

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

    private void OnDisable()
    {
        if (isOpen)
        {
            Close(false, false);
        }
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            Close(false, false);
        }
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }
    }
}
