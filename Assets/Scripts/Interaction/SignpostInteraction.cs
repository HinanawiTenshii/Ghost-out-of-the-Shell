using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional directional text on a signpost, using the shared interaction arbiter.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-300)]
[AddComponentMenu("Interaction/Signpost Interaction")]
public sealed class SignpostInteraction : MonoBehaviour
{
    [Header("Directions / 指路文字（留空不显示）")]
    [SerializeField, TextArea(2, 5), Tooltip("上方的说明。空白或仅空格时隐藏该方向。")]
    private string upText = "";
    [SerializeField, TextArea(2, 5), Tooltip("下方的说明。")]
    private string downText = "";
    [SerializeField, TextArea(2, 5), Tooltip("左方的说明。")]
    private string leftText = "";
    [SerializeField, TextArea(2, 5), Tooltip("右方的说明。")]
    private string rightText = "";
    [Header("Interaction / 交互")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;
    [SerializeField] private Font displayFont;

    private static SignpostInteraction activeSignpost;
    private static int inputBlockedThroughFrame = -1;
    public static bool BlocksInput => activeSignpost != null || Time.frameCount <= inputBlockedThroughFrame;

    private GameObject windowObject;
    private Canvas windowCanvas;
    private static readonly Vector2 DirectionPanelSize = new Vector2(320f, 160f);
    // Fixed compass positions: hiding an empty direction must not move the others.
    private static readonly Vector2[] DirectionPanelPositions = {
        new Vector2(0f, 195f), new Vector2(0f, -195f),
        new Vector2(-355f, 0f), new Vector2(355f, 0f)
    };
    private static readonly Vector2[] DirectionArrowPositions = {
        new Vector2(0f, 48f), new Vector2(0f, -48f),
        new Vector2(-130f, 0f), new Vector2(130f, 0f)
    };
    private readonly RectTransform[] directionPanels = new RectTransform[4];
    private readonly Text[] directionLabels = new Text[4];
    private readonly Text[] directionContents = new Text[4];
    private Text emptyLabel, closeHint;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;
    private float previousTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeSignpost = null;
        inputBlockedThroughFrame = -1;
    }

    private void Update()
    {
        if (activeSignpost == this)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
                CloseWindow();
            else
                EnsureWindowOnTop();
            return;
        }

        ZeldaFourWayMover mover = DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput
            ? null : GetNearbyCharacter();
        if (mover == null)
        {
            SetPromptVisible(false);
            return;
        }

        // Register gameplay independently of HUD/font availability.
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, SetPromptVisible);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, OpenWindow);
    }

    private ZeldaFourWayMover GetNearbyCharacter()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled) return null;
        ZeldaCharacterData data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead &&
               ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject).handle == gameObject.scene.handle &&
               Vector2.Distance(mover.transform.position, transform.position) <= interactionDistance
            ? mover : null;
    }

    private string GetDirectionText(int index)
    {
        switch (index)
        {
            case 0: return upText;
            case 1: return downText;
            case 2: return leftText;
            default: return rightText;
        }
    }

    private int CountVisibleDirections()
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
            if (!string.IsNullOrWhiteSpace(GetDirectionText(i))) count++;
        return count;
    }

    private void OpenWindow()
    {
        if (DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput || GetNearbyCharacter() == null)
            return;
        EnsureWindowBuilt();
        RefreshDirections();
        previousTimeScale = Time.timeScale;
        activeSignpost = this;
        inputBlockedThroughFrame = Time.frameCount;
        Time.timeScale = 0f;
        windowObject.SetActive(true);
        EnsureWindowOnTop();
        SetPromptVisible(false);
    }

    private void CloseWindow()
    {
        if (activeSignpost != this) return;
        if (windowObject != null) windowObject.SetActive(false);
        activeSignpost = null;
        // Consume E/Escape for the entire closing frame, including the pause
        // menu and the late interaction arbiter; never reopen or trigger a door.
        inputBlockedThroughFrame = Time.frameCount;
        Time.timeScale = previousTimeScale;
    }

    private Font ResolveFont()
    {
        if (displayFont != null) return displayFont;
        if (ZeldaHealthHeartsUI.Instance != null && ZeldaHealthHeartsUI.Instance.PermissionLabelFont != null)
            return ZeldaHealthHeartsUI.Instance.PermissionLabelFont;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void SetPromptVisible(bool visible)
    {
        ZeldaFourWayMover mover = visible && !DocumentReader.IsInputBlocked ? GetNearbyCharacter() : null;
        if (mover == null)
        {
            if (promptObject != null) promptObject.SetActive(false);
            return;
        }
        Font font = ResolveFont();
        if (font == null) return;
        if (promptObject == null)
        {
            promptObject = new GameObject("Signpost Read Prompt");
            promptObject.transform.SetParent(transform, false);
            promptText = promptObject.AddComponent<TextMesh>();
            promptText.text = "按[E]查看路牌";
            promptText.fontSize = 72;
            promptText.characterSize = 0.035f;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.color = ZeldaUiPalette.Primary;
            MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
            promptMaterial = new Material(font.material);
            renderer.sharedMaterial = promptMaterial;
            renderer.sortingOrder = short.MaxValue - 2;
            ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptText.font = font;
        font.RequestCharactersInTexture(promptText.text, 72, FontStyle.Normal);
        promptMaterial.mainTexture = font.material.mainTexture;
        promptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, 0.9f));
        promptObject.transform.rotation = Quaternion.identity;
        promptObject.SetActive(true);
    }

    private static RectTransform MakeRect(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(objectName, typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Text MakeText(string objectName, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = MakeRect(objectName, parent, position, size);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.color = ZeldaUiPalette.Primary;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.supportRichText = false;
        label.raycastTarget = false;
        return label;
    }

    private void EnsureWindowBuilt()
    {
        if (windowObject != null) return;
        windowObject = new GameObject("Signpost Directions Window", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        windowObject.layer = LayerMask.NameToLayer("UI");
        windowObject.SetActive(false);
        windowCanvas = windowObject.GetComponent<Canvas>();
        windowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = windowObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        // Keep the whole compass and close hint visible on narrow/ultrawide screens.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // Transparent layout root, not a shared background/window. Each card
        // owns its background, border, arrow and description independently.
        RectTransform layoutRoot = MakeRect("Directions Layout", windowObject.transform,
            Vector2.zero, new Vector2(1060f, 680f));
        string[] labels = { "↑", "↓", "←", "→" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform card = MakeRect("Direction Window " + i, layoutRoot,
                DirectionPanelPositions[i], DirectionPanelSize);
            directionPanels[i] = card;
            Image background = card.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.07f, 0.12f, 0.97f);
            background.raycastTarget = false;
            var border = card.gameObject.AddComponent<Outline>();
            border.effectColor = ZeldaUiPalette.Ghost;
            border.effectDistance = new Vector2(2f, -2f);

            directionLabels[i] = MakeText("Arrow " + i, card,
                DirectionArrowPositions[i], new Vector2(44f, 40f), 36);
            directionLabels[i].text = labels[i];
            directionLabels[i].alignment = TextAnchor.MiddleCenter;
            Vector2 contentPosition = i < 2
                ? new Vector2(0f, i == 0 ? -18f : 18f)
                : new Vector2(i == 2 ? 20f : -20f, 0f);
            Vector2 contentSize = i < 2 ? new Vector2(280f, 90f) : new Vector2(230f, 128f);
            directionContents[i] = MakeText("Description " + i, card, contentPosition, contentSize, 24);
            directionContents[i].alignment = TextAnchor.MiddleCenter;
            directionContents[i].resizeTextForBestFit = true;
            directionContents[i].resizeTextMinSize = 12;
            directionContents[i].resizeTextMaxSize = 24;
        }
        emptyLabel = MakeText("Empty", layoutRoot, Vector2.zero, new Vector2(600f, 72f), 22);
        emptyLabel.text = "暂无指路信息";
        emptyLabel.alignment = TextAnchor.MiddleCenter;
        closeHint = MakeText("Close Hint", layoutRoot, new Vector2(0f, -318f), new Vector2(600f, 32f), 18);
        closeHint.text = "[E] / [Esc] 关闭";
        closeHint.alignment = TextAnchor.MiddleCenter;
    }

    private void RefreshDirections()
    {
        int count = CountVisibleDirections();
        for (int i = 0; i < 4; i++)
        {
            string content = GetDirectionText(i);
            bool visible = !string.IsNullOrWhiteSpace(content);
            directionPanels[i].gameObject.SetActive(visible);
            directionContents[i].text = visible ? content.Trim() : string.Empty;
        }
        emptyLabel.gameObject.SetActive(count == 0);
    }

    private void EnsureWindowOnTop()
    {
        windowCanvas.overrideSorting = true;
        int bestValue = int.MinValue;
        foreach (SortingLayer layer in SortingLayer.layers)
            if (layer.value > bestValue) { bestValue = layer.value; windowCanvas.sortingLayerID = layer.id; }
        windowCanvas.sortingOrder = short.MaxValue;
        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            windowCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            windowCanvas.worldCamera = uiCamera;
            windowCanvas.planeDistance = uiCamera.nearClipPlane + 0.01f;
        }
        CRTScreenEffect.RegisterCanvas(windowCanvas);
    }

    private void OnDisable()
    {
        CloseWindow();
        SetPromptVisible(false);
    }

    private void OnDestroy()
    {
        CloseWindow();
        if (windowObject != null) Destroy(windowObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }

    private void OnValidate() { interactionDistance = Mathf.Max(0.1f, interactionDistance); }
}
