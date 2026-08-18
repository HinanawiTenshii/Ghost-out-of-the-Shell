using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class DocumentReader : MonoBehaviour
{
    [SerializeField, TextArea(6, 20)] private string documentText = "在此输入文档内容。";
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.2f;
    [SerializeField] private Font documentFont;
    [SerializeField] private Color paperColor = new Color(0.92f, 0.89f, 0.76f, 1f);
    [SerializeField] private Color inkColor = new Color(0.20f, 0.18f, 0.15f, 1f);
    [SerializeField, Range(0f, 1f)] private float windowOpacity = 0.82f;
    [SerializeField] private Vector2 promptOffset = new Vector2(0f, 0.9f);
    [Header("Optional Task Objective")]
    [SerializeField] private bool setTemporaryTaskOnOpen;
    [SerializeField] private bool setTaskOnlyOnce = true;
    [SerializeField] private Vector2 temporaryTaskWorldPosition;
    [SerializeField] private string temporaryTaskLabel = string.Empty;
    [Header("Optional Quest Journal Entry")]
    [SerializeField] private bool addJournalEntryOnOpen;
    [SerializeField] private bool addJournalEntryOnlyOnce = true;
    [SerializeField] private string journalEntryId = string.Empty;
    [SerializeField] private string journalEntryTitle = string.Empty;
    [SerializeField, TextArea(4, 12)] private string journalEntryDetails = string.Empty;
    [Header("Journal Changes When Read")]
    [SerializeField, Tooltip("Additional add/update/remove operations applied when this document is opened.")]
    private QuestJournalEventChange[] journalChangesOnRead;
    [SerializeField] private bool applyJournalChangesOnlyOnce = true;

    private static DocumentReader activeDocument;
    private static int inputBlockedThroughFrame = -1;

    private SpriteRenderer documentRenderer;
    private Sprite documentSprite;
    private Texture2D documentTexture;
    private GameObject readingCanvasObject;
    private Text contentText;
    private Canvas readingCanvas;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Material interactionPromptMaterial;
    private float previousTimeScale = 1f;
    private bool temporaryTaskWasSet;
    private bool journalEntryWasAdded;
    private bool journalChangesWereApplied;

    public static bool IsDocumentOpen => activeDocument != null;
    public static bool IsInputBlocked =>
        activeDocument != null ||
        ItemDescriptionWindow.BlocksInput ||
        DoorPasswordPanel.IsOpen ||
        PauseMenuController.IsPaused ||
        TabJournalMenuController.BlocksInput ||
        RetroSceneLoadReveal.IsBlockingInput ||
        Time.frameCount <= inputBlockedThroughFrame;
    public string DocumentText => documentText;

    private void Awake()
    {
        documentRenderer = GetComponent<SpriteRenderer>();
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(0.82f, 1.05f);

        CreateDocumentVisual();
        CreateReadingCanvas();
        CreateInteractionPrompt();
        QuestJournalInteractionMarker.Configure(
            gameObject,
            addJournalEntryOnOpen,
            journalEntryId,
            journalChangesOnRead);
    }

    private void Update()
    {
        if (activeDocument != null)
        {
            SetInteractionPromptVisible(false, null);
            if (activeDocument == this)
            {
                EnsureReadingCanvasOnTop();
            }
            if (activeDocument == this && Input.GetKeyDown(KeyCode.E))
            {
                CloseDocument();
            }

            return;
        }

        ZeldaFourWayMover nearbyCharacter = GetControlledCharacterNearby();
        if (nearbyCharacter != null)
        {
            ZeldaInteractionArbiter.OfferInteraction(
                this,
                nearbyCharacter,
                KeyCode.E,
                transform.position,
                SetPromptFromArbiter);
        }
        else
        {
            SetInteractionPromptVisible(false, null);
        }
        if (Input.GetKeyDown(KeyCode.E) && nearbyCharacter != null)
        {
            ZeldaInteractionArbiter.Submit(
                this,
                nearbyCharacter,
                KeyCode.E,
                transform.position,
                OpenDocument);
        }
    }

    private void SetPromptFromArbiter(bool visible)
    {
        ZeldaFourWayMover controlledCharacter = visible
            ? GetControlledCharacterNearby()
            : null;
        SetInteractionPromptVisible(visible, controlledCharacter);
    }

    private void OpenDocument()
    {
        activeDocument = this;
        inputBlockedThroughFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        contentText.text = documentText;
        EnsureReadingCanvasOnTop();
        readingCanvasObject.SetActive(true);
        SetInteractionPromptVisible(false, null);
        if (setTemporaryTaskOnOpen && (!setTaskOnlyOnce || !temporaryTaskWasSet))
        {
            ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
            if (ui != null)
            {
                ui.PushTaskPointerTarget(
                    temporaryTaskWorldPosition,
                    temporaryTaskLabel);
                temporaryTaskWasSet = true;
            }
        }

        if (addJournalEntryOnOpen &&
            (!addJournalEntryOnlyOnce || !journalEntryWasAdded))
        {
            QuestJournalManager.GetOrCreate().AddOrUpdateEntry(
                journalEntryId,
                journalEntryTitle,
                journalEntryDetails,
                gameObject.scene.name);
            journalEntryWasAdded = true;
        }

        if (journalChangesOnRead != null &&
            journalChangesOnRead.Length > 0 &&
            (!applyJournalChangesOnlyOnce || !journalChangesWereApplied))
        {
            QuestJournalManager.GetOrCreate().ApplyEventChanges(
                journalChangesOnRead,
                gameObject.scene.name);
            journalChangesWereApplied = true;
        }
    }

    private void CloseDocument()
    {
        if (activeDocument != this)
        {
            return;
        }

        readingCanvasObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        activeDocument = null;
        inputBlockedThroughFrame = Time.frameCount;
    }

    private ZeldaFourWayMover GetControlledCharacterNearby()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null)
            return null;

        ZeldaCharacterData data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead &&
               Vector2.Distance(transform.position, mover.transform.position) <= interactionDistance
            ? mover
            : null;
    }

    private void CreateInteractionPrompt()
    {
        interactionPromptObject = new GameObject(name + " Read Prompt");
        interactionPromptText = interactionPromptObject.AddComponent<TextMesh>();
        if (documentFont != null)
        {
            documentFont.RequestCharactersInTexture(
                "按[E]阅读文件",
                72,
                FontStyle.Normal);
        }
        interactionPromptText.text = "按[E]阅读文件";
        interactionPromptText.font = documentFont;
        interactionPromptText.fontSize = 72;
        interactionPromptText.characterSize = 0.035f;
        interactionPromptText.anchor = TextAnchor.MiddleCenter;
        interactionPromptText.alignment = TextAlignment.Center;
        interactionPromptText.color = ZeldaUiPalette.Primary;

        MeshRenderer promptRenderer = interactionPromptObject.GetComponent<MeshRenderer>();
        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestSortingLayerValue)
            {
                highestSortingLayerValue = sortingLayer.value;
                highestSortingLayerId = sortingLayer.id;
            }
        }
        promptRenderer.sortingLayerID = highestSortingLayerId;
        promptRenderer.sortingOrder = short.MaxValue - 2;
        if (documentFont != null)
        {
            interactionPromptMaterial = new Material(documentFont.material)
            {
                name = name + " Read Prompt Font Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            interactionPromptMaterial.mainTexture = documentFont.material.mainTexture;
            promptRenderer.sharedMaterial = interactionPromptMaterial;
        }

        interactionPromptObject.SetActive(false);
    }

    private void SetInteractionPromptVisible(bool visible, ZeldaFourWayMover controlledCharacter)
    {
        if (interactionPromptObject == null)
            return;

        if (!visible || controlledCharacter == null || DocumentReader.IsInputBlocked)
        {
            interactionPromptObject.SetActive(false);
            return;
        }

        interactionPromptObject.transform.position =
            controlledCharacter.transform.position + (Vector3)promptOffset;
        interactionPromptObject.transform.rotation = Quaternion.identity;
        interactionPromptObject.SetActive(true);

        if (documentFont != null)
        {
            documentFont.RequestCharactersInTexture(
                "按[E]阅读文件",
                72,
                FontStyle.Normal);
            if (interactionPromptMaterial != null)
            {
                interactionPromptMaterial.mainTexture = documentFont.material.mainTexture;
            }
        }
    }

    private void CreateDocumentVisual()
    {
        const int width = 14;
        const int height = 18;
        documentTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        documentTexture.name = "Runtime Pixel Document";
        documentTexture.filterMode = FilterMode.Point;
        documentTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool outsideFold = y >= height - 4 && x >= width - (height - y);
                Color pixelColor = outsideFold ? Color.clear : paperColor;
                bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                if (!outsideFold && border)
                {
                    pixelColor = inkColor;
                }

                bool textLine = y >= 4 && y <= 12 && y % 3 == 0 && x >= 3 && x <= 10;
                if (!outsideFold && textLine)
                {
                    pixelColor = inkColor;
                }

                bool foldedCorner = x + y == width + height - 5 && x >= width - 4;
                if (foldedCorner)
                {
                    pixelColor = inkColor;
                }

                documentTexture.SetPixel(x, y, pixelColor);
            }
        }

        documentTexture.Apply(false, true);
        documentSprite = Sprite.Create(
            documentTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            18f);
        documentSprite.name = "Runtime Pixel Document";
        documentRenderer.sprite = documentSprite;
        documentRenderer.color = Color.white;
        documentRenderer.sortingOrder = 2;
    }

    private void CreateReadingCanvas()
    {
        readingCanvasObject = new GameObject(
            name + " Reading Window",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        readingCanvas = readingCanvasObject.GetComponent<Canvas>();
        readingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureReadingCanvasOnTop();

        CanvasScaler scaler = readingCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Document Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(readingCanvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 460f);
        Image panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, windowOpacity);
        panel.raycastTarget = false;

        contentText = CreateText("Document Content", panelRect, documentFont, 22, TextAnchor.UpperLeft);
        RectTransform contentRect = contentText.rectTransform;
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(42f, 72f);
        contentRect.offsetMax = new Vector2(-42f, -38f);
        contentText.text = documentText;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Truncate;
        contentText.lineSpacing = 1.15f;

        Text closeHint = CreateText("Close Hint", panelRect, documentFont, 18, TextAnchor.MiddleCenter);
        RectTransform hintRect = closeHint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 24f);
        hintRect.sizeDelta = new Vector2(300f, 32f);
        closeHint.text = "按[E]关闭";

        readingCanvasObject.SetActive(false);
    }

    private void EnsureReadingCanvasOnTop()
    {
        if (readingCanvas == null && readingCanvasObject != null)
        {
            readingCanvas = readingCanvasObject.GetComponent<Canvas>();
        }

        if (readingCanvas == null)
            return;

        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestSortingLayerValue)
            {
                highestSortingLayerValue = sortingLayer.value;
                highestSortingLayerId = sortingLayer.id;
            }
        }

        readingCanvas.overrideSorting = true;
        readingCanvas.sortingLayerID = highestSortingLayerId;
        readingCanvas.sortingOrder = short.MaxValue;
    }

    private static Text CreateText(string objectName, Transform parent, Font font, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.color = ZeldaUiPalette.Primary;
        text.raycastTarget = false;
        return text;
    }

    private void OnDisable()
    {
        SetInteractionPromptVisible(false, null);
        if (activeDocument == this)
        {
            CloseDocument();
        }
    }

    private void OnDestroy()
    {
        if (activeDocument == this)
        {
            CloseDocument();
        }

        if (readingCanvasObject != null)
        {
            Destroy(readingCanvasObject);
        }

        if (interactionPromptObject != null)
        {
            Destroy(interactionPromptObject);
        }

        if (interactionPromptMaterial != null)
        {
            Destroy(interactionPromptMaterial);
        }

        if (documentSprite != null)
        {
            Destroy(documentSprite);
        }

        if (documentTexture != null)
        {
            Destroy(documentTexture);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        windowOpacity = Mathf.Clamp01(windowOpacity);
    }
#endif
}
