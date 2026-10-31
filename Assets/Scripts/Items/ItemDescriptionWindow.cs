using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent item-information window and first-pickup discovery history.
/// Persistent scene travel keeps this object alive; fresh scene-session resets
/// explicitly clear the history through SceneTravelStateManager.
/// </summary>
[DefaultExecutionOrder(11000)]
public sealed class ItemDescriptionWindow : MonoBehaviour
{
    private static ItemDescriptionWindow instance;
    private static int inputBlockedThroughFrame = -1;

    private readonly HashSet<string> discoveredItems = new HashSet<string>();

    private GameObject windowRoot;
    private Canvas windowCanvas;
    private Image iconImage;
    private Text nameText;
    private Text descriptionText;
    private Text closeHintText;
    private Text headingText, requirementLabel, requirementCount, scrollHintText;
    private RectTransform panelRect, descriptionViewport, scrollTrack, scrollThumb;
    private readonly List<Image> requirementIcons = new List<Image>();
    private Sprite wrenchSprite;
    private Texture2D wrenchTexture;
    private float scrollOffset, scrollMaximum;
    private float descriptionLayoutWidth, descriptionLayoutScale;
    private readonly TextGenerator descriptionLayout = new TextGenerator();
    private readonly List<Text> descriptionBlocks = new List<Text>();
    private Font currentFont;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private int openedOnFrame = -1;
    private PersistentInventory.Slot pendingSlot;
    private string pendingDiscoveryId = string.Empty;
    private float pendingDiscoveryReadyAt;

    public static bool IsOpen => instance != null && instance.isOpen;
    public static string[] CaptureDiscoveryHistory() => instance == null
        ? new string[0] : new List<string>(instance.discoveredItems).ToArray();

    public static void ApplyDiscoveryHistory(string[] ids)
    {
        var window = GetOrCreate();
        window.ClearPendingDiscovery();
        window.discoveredItems.Clear();
        if (ids != null) window.discoveredItems.UnionWith(ids);
    }
    public static bool BlocksInput => IsOpen || Time.frameCount <= inputBlockedThroughFrame;

    public static ItemDescriptionWindow GetOrCreate()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<ItemDescriptionWindow>();
        if (instance != null)
            return instance;

        GameObject systemObject = new GameObject("Item Description Window");
        return systemObject.AddComponent<ItemDescriptionWindow>();
    }

    public static void ResetDiscoveryHistoryIfPresent()
    {
        if (instance == null)
        {
            instance = FindObjectOfType<ItemDescriptionWindow>();
        }

        if (instance != null)
        {
            instance.discoveredItems.Clear();
            instance.ClearPendingDiscovery();
            instance.CloseWindow();
        }
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
    }

    private void Update()
    {
        if (!isOpen)
        {
            TryOpenPendingDiscovery();
            return;
        }

        EnsureRenderPriority();
        FitPanelToCanvas();
        Font uiFont = ZeldaHealthHeartsUI.Instance != null ? ZeldaHealthHeartsUI.Instance.PermissionLabelFont : null;
        if (uiFont != null && uiFont != currentFont)
        {
            SetFont(uiFont);
            RebuildDescriptionLayout(false);
        }
        // Picking an item and opening this persistent window happen from the
        // same E key-down event. Because this component already exists after
        // the first pickup, Unity may run this Update later in that same frame;
        // never interpret the opening key press as an immediate close request.
        if (Time.frameCount > openedOnFrame &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CloseWindow();
            return;
        }
        // Read the wheel directly while this modal owns input. This works while
        // paused, without an EventSystem, and through the CRT camera's render texture.
        // PersistentInventory already ignores wheel selection while BlocksInput is true.
        if (Time.frameCount > openedOnFrame) ScrollDescription(Input.mouseScrollDelta.y);
    }

    public void NotifyPickedUp(PickupItemBase pickedItem, float displayDelay = 0f)
    {
        if (pickedItem == null)
            return;

        string discoveryId = pickedItem.DescriptionDiscoveryId;
        if (string.IsNullOrWhiteSpace(discoveryId))
            return;

        discoveryId = discoveryId.Trim();
        if (discoveredItems.Contains(discoveryId))
            return;

        PersistentInventory inventory = PersistentInventory.Instance;
        PersistentInventory.Slot slot;
        if (pickedItem.EntersInventoryOnPickup) slot = FindPickedItemSlot(inventory, pickedItem);
        else
        {
            // Display-only record: never insert the consumed relic into the inventory.
            slot = new PersistentInventory.Slot();
            slot.Set(pickedItem.ItemId, 1, "", pickedItem.ItemName, pickedItem.ItemDescription,
                pickedItem.ItemVisual != null, pickedItem.ItemVisual != null ? pickedItem.ItemVisual.DisplayColor : Color.white);
        }
        // Register discovery only after the matching inventory slot has been
        // found and its window has actually opened. During a scene restore the
        // selected-slot index can briefly still refer to the previous scene;
        // the old behaviour recorded the item as discovered before this check,
        // permanently suppressing its first-pickup window.
        if (slot != null && displayDelay > 0f)
        {
            // Store display data, not the soon-destroyed world object or a live
            // inventory slot that can be moved/consumed before the pose finishes.
            pendingSlot = new PersistentInventory.Slot();
            pendingSlot.Set(slot.ItemId, slot.Quantity, slot.ItemInstanceId, slot.ItemName, slot.ItemDescription,
                slot.HasVisualColor, slot.VisualColor, slot.HasStoredCharge, slot.StoredCharge, slot.ItemState);
            pendingDiscoveryId = discoveryId;
            pendingDiscoveryReadyAt = Time.unscaledTime + displayDelay;
        }
        else if (slot != null && OpenForSlot(slot))
        {
            discoveredItems.Add(discoveryId);
        }
        else if (slot != null)
        {
            // A second pickup can arrive while the previous first-pickup
            // window is still open (or while another modal UI owns input).
            // Queue it rather than incorrectly discarding its discovery.
            pendingSlot = slot;
            pendingDiscoveryId = discoveryId;
            pendingDiscoveryReadyAt = 0f;
        }
    }

    private static PersistentInventory.Slot FindPickedItemSlot(
        PersistentInventory inventory,
        PickupItemBase pickedItem)
    {
        if (inventory == null || pickedItem == null)
            return null;

        string instanceId = pickedItem.ConfiguredUniqueInstanceId;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            for (int index = 0; index < inventory.SlotCount; index++)
            {
                PersistentInventory.Slot candidate = inventory.GetSlot(index);
                if (candidate != null && !candidate.IsEmpty &&
                    string.Equals(
                        candidate.ItemInstanceId,
                        instanceId.Trim(),
                        System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
        }

        PersistentInventory.Slot selected =
            inventory.GetSlot(inventory.SelectedSlotIndex);
        if (selected != null && !selected.IsEmpty &&
            string.Equals(
                selected.ItemId,
                pickedItem.ItemId,
                System.StringComparison.Ordinal))
        {
            return selected;
        }

        for (int index = 0; index < inventory.SlotCount; index++)
        {
            PersistentInventory.Slot candidate = inventory.GetSlot(index);
            if (candidate != null && !candidate.IsEmpty &&
                string.Equals(
                    candidate.ItemId,
                    pickedItem.ItemId,
                    System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    public bool OpenForSlot(PersistentInventory.Slot slot)
    {
        if (slot == null || slot.IsEmpty || isOpen)
            return false;

        PersistentInventory inventory = PersistentInventory.Instance;
        PickupItemBase itemPrefab = inventory != null
            ? inventory.ResolveItemPrefab(slot.ItemId)
            : null;
        if (itemPrefab == null)
            itemPrefab = System.Array.Find(SaveGameCatalog.LoadPickupTemplates(), item => item != null && item.ItemId == slot.ItemId);
        PickupItemVisualBase visual = itemPrefab != null ? itemPrefab.ItemVisual : null;

        EnsureWindowBuilt();
        if (windowRoot == null)
            return false;

        string displayName = !string.IsNullOrWhiteSpace(slot.ItemName)
            ? slot.ItemName
            : itemPrefab != null ? itemPrefab.ItemName : "未命名物品";
        string details = !string.IsNullOrWhiteSpace(slot.ItemDescription)
            ? slot.ItemDescription
            : itemPrefab != null ? itemPrefab.ItemDescription : string.Empty;
        if (itemPrefab is PotionPickupItem potion)
            details = AlchemyRecipeCatalog.ResolveInventoryDescription(
                potion.Kind, slot.ItemDescription, potion.ItemDescription);

        // Old saves may contain a captured description from before requirements existed.
        if (itemPrefab != null) details = itemPrefab.FormatDescriptionWithRequirements(details, false);
        nameText.text = displayName;
        descriptionText.text = string.IsNullOrWhiteSpace(details)
            ? "暂无物品说明"
            : details;
        iconImage.sprite = visual != null ? visual.GetInventoryIcon(slot) : null;
        iconImage.color = visual == null
            ? Color.white
            : slot.HasVisualColor
                ? visual.GetInventoryTint(slot.VisualColor)
                : visual.InventoryTint;
        iconImage.gameObject.SetActive(iconImage.sprite != null);
        ShowRequirements(itemPrefab != null ? itemPrefab.RequiredUseSkill : -1);

        RequestDisplayedCharacters(displayName, descriptionText.text);
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        openedOnFrame = Time.frameCount;
        inputBlockedThroughFrame = Time.frameCount;
        windowRoot.SetActive(true);
        EnsureRenderPriority();
        FitPanelToCanvas();
        RebuildDescriptionLayout(true);
        return true;
    }

    private void TryOpenPendingDiscovery()
    {
        if (pendingSlot == null || pendingSlot.IsEmpty ||
            string.IsNullOrWhiteSpace(pendingDiscoveryId))
        {
            ClearPendingDiscovery();
            return;
        }

        if (Time.unscaledTime < pendingDiscoveryReadyAt || DocumentReader.IsDocumentOpen ||
            DoorPasswordPanel.IsOpen ||
            PauseMenuController.IsPaused ||
            TabJournalMenuController.BlocksInput ||
            RetroSceneLoadReveal.IsBlockingInput)
        {
            return;
        }

        PersistentInventory.Slot slot = pendingSlot;
        string discoveryId = pendingDiscoveryId;
        if (OpenForSlot(slot))
        {
            discoveredItems.Add(discoveryId);
            ClearPendingDiscovery();
        }
    }

    private void ClearPendingDiscovery()
    {
        pendingSlot = null;
        pendingDiscoveryId = string.Empty;
        pendingDiscoveryReadyAt = 0f;
    }

    private void LateUpdate()
    {
        if (!isOpen || descriptionText == null) return;
        if (!Mathf.Approximately(descriptionLayoutWidth, descriptionText.GetPixelAdjustedRect().width) ||
            !Mathf.Approximately(descriptionLayoutScale, descriptionText.pixelsPerUnit))
            RebuildDescriptionLayout(false);
    }

    public void CloseWindow()
    {
        if (!isOpen)
            return;

        isOpen = false;
        openedOnFrame = -1;
        if (windowRoot != null)
        {
            windowRoot.SetActive(false);
        }
        scrollOffset = 0f;
        Time.timeScale = previousTimeScale;
        inputBlockedThroughFrame = Time.frameCount;
        // Keep this component ticking so a queued first-pickup description can
        // open on the following frame after the current window releases input.
        enabled = true;
    }

    private void EnsureWindowBuilt()
    {
        Font desiredFont = ZeldaHealthHeartsUI.Instance != null
            ? ZeldaHealthHeartsUI.Instance.PermissionLabelFont
            : null;
        if (desiredFont == null)
        {
            // Scene services can initialize later than the first pickup. Prefer
            // the already-loaded project font over a fallback without Chinese glyphs.
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
                if (font.name == "ZLabsPixel_12px_M_CN") { desiredFont = font; break; }
        }
        if (desiredFont == null) desiredFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (windowRoot != null)
        {
            if (desiredFont != null && desiredFont != currentFont)
            {
                SetFont(desiredFont);
            }
            return;
        }

        currentFont = desiredFont;
        windowRoot = new GameObject(
            "Item Description Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        windowRoot.transform.SetParent(transform, false);
        windowCanvas = windowRoot.GetComponent<Canvas>();
        windowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = windowRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = CreateImageObject("Item Description Panel", windowRoot.transform);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 520f);
        panelObject.GetComponent<Image>().color = new Color32(4, 12, 23, 255);
        CreateFrame(panelRect, ZeldaUiPalette.Ghost, 2f);
        CreateCornerAccents();

        headingText = CreateText("Detail Heading", panelRect, 18, TextAnchor.MiddleLeft);
        headingText.text = "物品详情";
        headingText.color = Color.Lerp(ZeldaUiPalette.Ghost, ZeldaUiPalette.Primary, .55f);
        Place(headingText.rectTransform, 30f, 24f, 112f, 28f);
        CreateRule("Header Rule", 154f, 38f, 576f);

        GameObject iconPanelObject = CreateImageObject("Item Icon Panel", panelRect);
        RectTransform iconPanelRect = iconPanelObject.GetComponent<RectTransform>();
        Place(iconPanelRect, 30f, 84f, 176f, 176f);
        iconPanelObject.GetComponent<Image>().color = new Color32(12, 27, 47, 255);
        CreateFrame(iconPanelRect, new Color32(24, 60, 104, 255), 1f);

        GameObject iconObject = CreateImageObject("Item Icon", iconPanelRect);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(144f, 144f);
        iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;

        nameText = CreateText("Item Name", panelRect, 32, TextAnchor.MiddleLeft);
        Place(nameText.rectTransform, 238f, 76f, 490f, 66f);
        nameText.color = ZeldaUiPalette.Primary;
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMinSize = 24;
        nameText.resizeTextMaxSize = 32;

        descriptionViewport = new GameObject("Description Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        descriptionViewport.SetParent(panelRect, false);
        Place(descriptionViewport, 238f, 154f, 472f, 216f);
        descriptionText = CreateText("Item Description", descriptionViewport, 22, TextAnchor.UpperLeft);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 1f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.pivot = new Vector2(.5f, 1f);
        descriptionRect.sizeDelta = new Vector2(0f, 216f);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        descriptionText.lineSpacing = 1.15f;
        descriptionText.color = Color.Lerp(ZeldaUiPalette.Ghost, ZeldaUiPalette.Primary, .3f);

        scrollTrack = CreateImageObject("Description Scroll Track", panelRect).GetComponent<RectTransform>();
        Place(scrollTrack, 723f, 154f, 4f, 216f);
        scrollTrack.GetComponent<Image>().color = new Color32(24, 60, 104, 255);
        scrollThumb = CreateImageObject("Description Scroll Thumb", scrollTrack).GetComponent<RectTransform>();
        Place(scrollThumb, 0f, 0f, 4f, 40f);
        scrollThumb.GetComponent<Image>().color = ZeldaUiPalette.Primary;

        CreateRule("Requirement Rule", 238f, 398f, 490f);
        requirementLabel = CreateText("Requirement Label", panelRect, 18, TextAnchor.MiddleLeft);
        requirementLabel.text = "使用需求";
        Place(requirementLabel.rectTransform, 238f, 409f, 100f, 30f);
        requirementLabel.color = headingText.color;
        wrenchSprite = ZeldaHealthHeartsUI.CreateSkillIcon(out wrenchTexture);
        for (int i = 0; i < 8; i++)
        {
            var icon = CreateImageObject("Required Skill " + (i + 1), panelRect).GetComponent<Image>();
            Place(icon.rectTransform, 350f + i * 32f, 412f, 24f, 24f);
            icon.sprite = wrenchSprite;
            icon.color = ZeldaUiPalette.Primary;
            icon.preserveAspect = true;
            requirementIcons.Add(icon);
        }
        requirementCount = CreateText("Requirement Count", panelRect, 22, TextAnchor.MiddleLeft);
        Place(requirementCount.rectTransform, 388f, 407f, 320f, 34f);

        CreateRule("Footer Rule", 30f, 460f, 700f);
        scrollHintText = CreateText("Scroll Hint", panelRect, 16, TextAnchor.MiddleLeft);
        Place(scrollHintText.rectTransform, 30f, 477f, 200f, 26f);
        scrollHintText.text = "鼠标滚轮翻阅";

        closeHintText = CreateText("Close Hint", panelRect, 18, TextAnchor.MiddleCenter);
        RectTransform closeRect = closeHintText.rectTransform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        closeRect.sizeDelta = new Vector2(300f, 32f);
        closeHintText.text = "[E] / [Esc] 关闭";
        closeHintText.color = ZeldaUiPalette.Primary;

        CRTScreenEffect.RegisterCanvas(windowCanvas, enableGlow: true);
        windowRoot.SetActive(false);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void CreateRule(string name, float x, float y, float width)
    {
        var image = CreateImageObject(name, panelRect).GetComponent<Image>();
        Place(image.rectTransform, x, y, width, 1f);
        image.color = new Color32(24, 60, 104, 255);
    }

    private void CreateCornerAccents()
    {
        for (int i = 0; i < 4; i++)
        {
            bool right = (i & 1) != 0, bottom = i >= 2;
            var horizontal = CreateImageObject("Corner Horizontal " + i, panelRect).GetComponent<Image>();
            var vertical = CreateImageObject("Corner Vertical " + i, panelRect).GetComponent<Image>();
            Place(horizontal.rectTransform, right ? 734f : 0f, bottom ? 517f : 0f, 26f, 3f);
            Place(vertical.rectTransform, right ? 757f : 0f, bottom ? 504f : 0f, 3f, 16f);
            horizontal.color = vertical.color = ZeldaUiPalette.Primary;
        }
    }

    private void ShowRequirements(int required)
    {
        // Keep arbitrary editor-authored requirements bounded. Large values use
        // the same icon plus a count instead of allocating thousands of images.
        bool asCount = required <= 0 || required > requirementIcons.Count;
        int count = asCount ? 1 : required;
        for (int i = 0; i < requirementIcons.Count; i++)
        {
            requirementIcons[i].gameObject.SetActive(i < count);
            requirementIcons[i].color = required <= 0 ? ZeldaUiPalette.Ghost : ZeldaUiPalette.Primary;
        }
        requirementCount.gameObject.SetActive(asCount);
        requirementCount.text = required < 0 ? "—" : "×" + required;
        requirementCount.color = required <= 0 ? ZeldaUiPalette.Ghost : ZeldaUiPalette.Primary;
    }

    private void SetFont(Font font)
    {
        currentFont = font;
        foreach (Text label in windowRoot.GetComponentsInChildren<Text>(true)) label.font = font;
        RequestDisplayedCharacters(nameText.text, descriptionText.text);
    }

    private void FitPanelToCanvas()
    {
        if (panelRect == null || windowCanvas == null) return;
        Rect bounds = ((RectTransform)windowCanvas.transform).rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        float scale = Mathf.Min(1f, (bounds.width - 32f) / 760f, (bounds.height - 32f) / 520f);
        panelRect.localScale = Vector3.one * Mathf.Max(.1f, scale);
    }

    private void RebuildDescriptionLayout(bool resetScroll)
    {
        Canvas.ForceUpdateCanvases();
        float normalized = scrollMaximum > 0f ? scrollOffset / scrollMaximum : 0f;
        descriptionLayoutWidth = descriptionText.GetPixelAdjustedRect().width;
        descriptionLayoutScale = descriptionText.pixelsPerUnit;
        var settings = descriptionText.GetGenerationSettings(new Vector2(descriptionLayoutWidth, 0f));
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        float preferred = descriptionText.cachedTextGeneratorForLayout.GetPreferredHeight(descriptionText.text, settings) / descriptionLayoutScale;
        float height = Mathf.Max(descriptionViewport.rect.height, Mathf.Ceil(preferred) + 8f);
        descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        RebuildDescriptionBlocks(settings, preferred);
        scrollMaximum = Mathf.Max(0f, height - descriptionViewport.rect.height);
        scrollOffset = resetScroll ? 0f : normalized * scrollMaximum;
        UpdateScrollPosition();
    }

    private void RebuildDescriptionBlocks(TextGenerationSettings settings, float totalHeight)
    {
        // The shared glow expands each glyph into 25 copies. Keep long texts in
        // wrapped-line-sized blocks so they retain the same glow as short items
        // without hitting uGUI's vertex ceiling. Only rebuild on open/font/scale changes.
        const int blockCharacters = 256;
        string details = descriptionText.text;
        descriptionText.enabled = details.Length <= blockCharacters;
        int used = 0;
        if (!descriptionText.enabled)
        {
            descriptionLayout.Populate(details, settings);
            var lines = descriptionLayout.lines;
            int start = 0, startLine = 0;
            float consumed = 0f;
            while (start < details.Length)
            {
                int nextLine = startLine + 1;
                while (nextLine < lines.Count && lines[nextLine].startCharIdx - start <= blockCharacters) nextLine++;
                if (nextLine < lines.Count || details.Length - start > blockCharacters)
                    nextLine = Mathf.Max(startLine + 1, nextLine - 1);
                int end = nextLine < lines.Count ? lines[nextLine].startCharIdx : details.Length;
                end = Mathf.Min(end, start + blockCharacters);
                if (end <= start) break;
                if (end < details.Length && char.IsHighSurrogate(details[end - 1])) end--;
                float height = end < details.Length && nextLine < lines.Count
                    ? (lines[startLine].topY - lines[nextLine].topY) / descriptionLayoutScale
                    : Mathf.Max(0f, totalHeight - consumed);
                Text block;
                if (used < descriptionBlocks.Count) block = descriptionBlocks[used];
                else
                {
                    block = CreateText("Description Block " + (used + 1), descriptionText.transform, descriptionText.fontSize, TextAnchor.UpperLeft);
                    TitleScreenGlow.Attach(block);
                    descriptionBlocks.Add(block);
                }
                block.font = currentFont;
                block.fontSize = descriptionText.fontSize;
                block.color = descriptionText.color;
                block.lineSpacing = descriptionText.lineSpacing;
                block.verticalOverflow = VerticalWrapMode.Overflow;
                block.text = details.Substring(start, end - start);
                Place(block.rectTransform, 0f, consumed, descriptionText.rectTransform.rect.width, height);
                block.gameObject.SetActive(true);
                consumed += height; start = end; startLine = nextLine; used++;
            }
        }
        for (int i = used; i < descriptionBlocks.Count; i++) descriptionBlocks[i].gameObject.SetActive(false);
    }

    private void ScrollDescription(float wheel)
    {
        if (scrollMaximum <= 0f || Mathf.Abs(wheel) < .001f) return;
        scrollOffset = Mathf.Clamp(scrollOffset - wheel * 48f, 0f, scrollMaximum);
        UpdateScrollPosition();
    }

    private void UpdateScrollPosition()
    {
        descriptionText.rectTransform.anchoredPosition = new Vector2(0f, scrollOffset);
        bool overflow = scrollMaximum > .5f;
        scrollTrack.gameObject.SetActive(overflow);
        scrollHintText.gameObject.SetActive(overflow);
        if (!overflow) return;
        float visible = descriptionViewport.rect.height;
        float thumbHeight = Mathf.Max(18f, visible * visible / (visible + scrollMaximum));
        scrollThumb.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, thumbHeight);
        scrollThumb.anchoredPosition = new Vector2(0f, -(visible - thumbHeight) * scrollOffset / scrollMaximum);
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;
        Text text = textObject.GetComponent<Text>();
        text.font = currentFont;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = ZeldaUiPalette.Ghost;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateImageObject(string objectName, Transform parent)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = null;
        image.raycastTarget = false;
        return imageObject;
    }

    private static void CreateFrame(RectTransform parent, Color color, float thickness)
    {
        CreateFrameEdge(parent, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(0f, thickness), color);
        CreateFrameEdge(parent, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
        CreateFrameEdge(parent, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
        CreateFrameEdge(parent, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), new Vector2(thickness, 0f), color);
    }

    private static void CreateFrameEdge(
        RectTransform parent,
        string edgeName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        GameObject edgeObject = CreateImageObject(edgeName, parent);
        RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
        edgeRect.anchorMin = anchorMin;
        edgeRect.anchorMax = anchorMax;
        edgeRect.pivot = new Vector2(0.5f, 0.5f);
        edgeRect.anchoredPosition = anchoredPosition;
        edgeRect.sizeDelta = sizeDelta;
        edgeObject.GetComponent<Image>().color = color;
    }

    private void RequestDisplayedCharacters(string displayName, string details)
    {
        if (currentFont == null)
            return;

        // Populate each real display size; Chinese pixel glyphs must not fall
        // back to another face or lose their UI material after changing items.
        foreach (Text label in windowRoot.GetComponentsInChildren<Text>(true))
            currentFont.RequestCharactersInTexture(label.text, label.fontSize, FontStyle.Normal);
    }

    private void EnsureRenderPriority()
    {
        if (windowCanvas == null)
            return;

        int highestLayerId = 0;
        int highestLayerValue = int.MinValue;
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.value > highestLayerValue)
            {
                highestLayerValue = layer.value;
                highestLayerId = layer.id;
            }
        }

        windowCanvas.overrideSorting = true;
        windowCanvas.sortingLayerID = highestLayerId;
        windowCanvas.sortingOrder = short.MaxValue;

        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            windowCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            windowCanvas.worldCamera = uiCamera;
            windowCanvas.planeDistance = uiCamera.nearClipPlane + 0.01f;
        }
        CRTScreenEffect.RegisterCanvas(windowCanvas, enableGlow: true);
    }

    private void OnDestroy()
    {
        ((System.IDisposable)descriptionLayout).Dispose();
        if (wrenchSprite != null) Destroy(wrenchSprite);
        if (wrenchTexture != null) Destroy(wrenchTexture);
        if (isOpen)
        {
            Time.timeScale = previousTimeScale;
        }
        if (instance == this)
        {
            instance = null;
        }
    }
}
