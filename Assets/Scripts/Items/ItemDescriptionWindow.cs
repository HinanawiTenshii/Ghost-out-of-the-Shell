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
    private Font currentFont;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private int openedOnFrame = -1;
    private PersistentInventory.Slot pendingSlot;
    private string pendingDiscoveryId = string.Empty;

    public static bool IsOpen => instance != null && instance.isOpen;
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
        // Picking an item and opening this persistent window happen from the
        // same E key-down event. Because this component already exists after
        // the first pickup, Unity may run this Update later in that same frame;
        // never interpret the opening key press as an immediate close request.
        if (Time.frameCount > openedOnFrame &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CloseWindow();
        }
    }

    public void NotifyPickedUp(PickupItemBase pickedItem)
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
        PersistentInventory.Slot slot = FindPickedItemSlot(inventory, pickedItem);
        // Register discovery only after the matching inventory slot has been
        // found and its window has actually opened. During a scene restore the
        // selected-slot index can briefly still refer to the previous scene;
        // the old behaviour recorded the item as discovered before this check,
        // permanently suppressing its first-pickup window.
        if (slot != null && OpenForSlot(slot))
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

        nameText.text = displayName;
        descriptionText.text = string.IsNullOrWhiteSpace(details)
            ? "暂无物品说明"
            : details;
        iconImage.sprite = visual != null ? visual.InventoryIcon : null;
        iconImage.color = visual == null
            ? Color.white
            : slot.HasVisualColor
                ? visual.GetInventoryTint(slot.VisualColor)
                : visual.InventoryTint;
        iconImage.gameObject.SetActive(iconImage.sprite != null);

        RequestDisplayedCharacters(displayName, descriptionText.text);
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        openedOnFrame = Time.frameCount;
        inputBlockedThroughFrame = Time.frameCount;
        windowRoot.SetActive(true);
        EnsureRenderPriority();
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

        if (DocumentReader.IsDocumentOpen ||
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
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (windowRoot != null)
        {
            if (desiredFont != null && desiredFont != currentFont)
            {
                currentFont = desiredFont;
                nameText.font = currentFont;
                descriptionText.font = currentFont;
                closeHintText.font = currentFont;
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
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(620f, 560f);
        panelObject.GetComponent<Image>().color = new Color(0.015f, 0.045f, 0.075f, 0.94f);
        CreateFrame(panelRect, ZeldaUiPalette.Ghost, 3f);

        GameObject iconPanelObject = CreateImageObject("Item Icon Panel", panelRect);
        RectTransform iconPanelRect = iconPanelObject.GetComponent<RectTransform>();
        iconPanelRect.anchorMin = iconPanelRect.anchorMax = new Vector2(0.5f, 1f);
        iconPanelRect.pivot = new Vector2(0.5f, 1f);
        iconPanelRect.anchoredPosition = new Vector2(0f, -38f);
        iconPanelRect.sizeDelta = new Vector2(330f, 205f);
        iconPanelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);
        CreateFrame(iconPanelRect, ZeldaUiPalette.Ghost, 2f);

        GameObject iconObject = CreateImageObject("Item Icon", iconPanelRect);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(150f, 150f);
        iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;

        nameText = CreateText("Item Name", panelRect, 30, TextAnchor.MiddleCenter);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -257f);
        nameRect.sizeDelta = new Vector2(520f, 48f);

        descriptionText = CreateText("Item Description", panelRect, 21, TextAnchor.UpperLeft);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.offsetMin = new Vector2(55f, 62f);
        descriptionRect.offsetMax = new Vector2(-55f, -322f);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
        descriptionText.lineSpacing = 1.15f;

        closeHintText = CreateText("Close Hint", panelRect, 18, TextAnchor.MiddleCenter);
        RectTransform closeRect = closeHintText.rectTransform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 20f);
        closeRect.sizeDelta = new Vector2(300f, 32f);
        closeHintText.text = "按[E]关闭";

        windowRoot.SetActive(false);
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);
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

        currentFont.RequestCharactersInTexture(
            displayName + details + "按[E]关闭暂无物品说明",
            30,
            FontStyle.Normal);
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
    }

    private void OnDestroy()
    {
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
