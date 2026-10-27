using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-overlay requirement display for doors and levers. Its screen position
/// follows the object's initial world position, while scene renderers can never
/// draw over it.
/// </summary>
public sealed class ZeldaRequirementWindow : MonoBehaviour
{
    private const int MaximumIconCount = 10;
    // Screen-space overlay remains above the world, but stays below all regular UI canvases.
    private const int OverlaySortingOrder = 31000;
    private const int DimmerSortingOrder = 30000;
    private const float FadeDuration = 0.12f;
    private const float OverlayDisplayScale = 0.5f;
    private static readonly Vector2 PanelSize = new Vector2(270f, 84f);

    private static readonly Color SwordBrown = new Color(0.48f, 0.27f, 0.12f, 1f);
    private static readonly Color WrenchGray = new Color(0.48f, 0.5f, 0.54f, 1f);
    private static readonly Color GhostBlue = ZeldaUiPalette.Ghost;
    private static Sprite frameSprite;
    private static Sprite swordSprite;
    private static Sprite wrenchSprite;
    private static Sprite solidSprite;

    private readonly Image[] icons = new Image[MaximumIconCount];
    private DoorData doorData;
    private DoorHingeInteraction doorInteraction;
    private RectTransform skillRow;
    private Text skillLabel;
    private int lastUnlockSkill = -1;
    private LeverData leverData;
    private Component skillSource;
    private int skillRequirement;
    private Transform SourceTransform => doorData != null ? doorData.transform :
        leverData != null ? leverData.transform : skillSource != null ? skillSource.transform : null;
    private bool SourceAvailable => SourceTransform != null &&
        (!(skillSource is Behaviour behaviour) || behaviour.isActiveAndEnabled) &&
        (!(skillSource is ElectricGatePillar pillar) || pillar.HasPanel);

    // Runtime attachment also covers existing scene instances without prefab migration.
    public static ZeldaRequirementWindow AttachSkillRequirement(Component source, int required)
    {
        if (!Application.isPlaying || source == null) return null;
        var window = source.GetComponentInChildren<ZeldaRequirementWindow>(true);
        if (window == null)
        {
            var root = new GameObject("Skill Requirement Window");
            root.transform.SetParent(source.transform, false);
            window = root.AddComponent<ZeldaRequirementWindow>();
        }
        window.skillSource = source;
        window.skillRequirement = required;
        window.ConfigureCommon();
        return window;
    }
    private GameObject overlayObject;
    private Canvas overlayCanvas;
    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private Text requirementLabel;
    private readonly AttributeSourceHighlight sourceHighlight = new AttributeSourceHighlight();
    private bool isVisible;
    private float visibilityAlpha;
    private int lastValue = int.MinValue;
    private bool hasFixedWorldAnchor;
    private Vector3 fixedWorldPosition;

    public void Configure(DoorData source)
    {
        doorData = source;
        doorInteraction = source.GetComponentInParent<DoorHingeInteraction>();
        leverData = null;
        ConfigureCommon();
    }

    public void Configure(LeverData source)
    {
        leverData = source;
        doorData = null;
        doorInteraction = null;
        ConfigureCommon();
    }

    private void ConfigureCommon()
    {
        sourceHighlight.Hide();
        if (!hasFixedWorldAnchor)
        {
            Transform sourceTransform = SourceTransform;
            fixedWorldPosition = sourceTransform.position + Vector3.up * 0.55f;
            hasFixedWorldAnchor = true;
        }

        EnsureVisuals();
        EnsureWorldDimmer();
        ResolveUiFont();
        Refresh(true);
        SetVisible(false, true);
    }

    private void LateUpdate()
    {
        if (!SourceAvailable)
        {
            if (canvasGroup != null) { SetVisible(false, true); ApplySourceHighlight(); }
            return;
        }
        EnsureVisuals();
        ResolveUiFont();
        UpdateScreenPosition();

        bool shouldBeVisible = !DocumentReader.IsInputBlocked &&
                               !ClockworkPuppetRuntime.BlocksCharacterInput &&
                               Input.GetMouseButton(1) &&
                               IsSourceInsideCameraVision();
        if (shouldBeVisible != isVisible)
        {
            SetVisible(shouldBeVisible, false);
        }

        Refresh(false);
        UpdateFade(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        sourceHighlight.Dispose();
        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }
    }

    private void OnDisable()
    {
        if (canvasGroup != null) { SetVisible(false, true); ApplySourceHighlight(); }
    }

    private void EnsureVisuals()
    {
        if (overlayObject != null)
        {
            if (overlayCanvas == null)
            {
                overlayCanvas = overlayObject.GetComponent<Canvas>();
            }
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = OverlaySortingOrder;
            requirementLabel.text = "需求：";
            return;
        }

        RemoveLegacyWorldRenderers();
        EnsureSprites();
        overlayObject = new GameObject(name + " Requirement Screen Overlay", typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) overlayObject.layer = uiLayer;
        overlayCanvas = overlayObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = OverlaySortingOrder;
        ConfigureResponsiveScaling(overlayObject);

        canvasGroup = overlayObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject panelObject = new GameObject("Requirement Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(overlayObject.transform, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = PanelSize;
        panelRect.localScale = Vector3.one * OverlayDisplayScale;
        Image frame = panelObject.GetComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = false;
        frame.raycastTarget = false;

        GameObject labelObject = new GameObject("Requirement Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(panelRect, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(14f, 0f);
        labelRect.sizeDelta = new Vector2(78f, 0f);
        requirementLabel = labelObject.GetComponent<Text>();
        requirementLabel.text = "需求：";
        requirementLabel.fontSize = 24;
        requirementLabel.alignment = TextAnchor.MiddleLeft;
        requirementLabel.color = ZeldaUiPalette.Primary;
        requirementLabel.raycastTarget = false;

        Sprite iconSprite = doorData != null ? swordSprite : wrenchSprite;
        for (int i = 0; i < icons.Length; i++)
        {
            GameObject iconObject = new GameObject("Requirement Icon " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(panelRect, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(102f + i * 16f, 0f);
            iconRect.sizeDelta = new Vector2(22f, 22f);
            icons[i] = iconObject.GetComponent<Image>();
            icons[i].sprite = iconSprite;
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
        }
        if (doorData != null)
        {
            skillRow = new GameObject("Unlock Skill Requirement", typeof(RectTransform)).GetComponent<RectTransform>();
            skillRow.SetParent(panelRect, false);
            skillRow.anchorMin = new Vector2(0f, .5f);
            skillRow.anchorMax = new Vector2(1f, .5f);
            skillRow.sizeDelta = new Vector2(0f, 40f);
            skillRow.anchoredPosition = new Vector2(0f, -24f);
            skillLabel = Instantiate(requirementLabel, skillRow);
            skillLabel.text = "技巧：";
            for (int i = 0; i < CharacterSkillRequirements.DoorUnlock; i++)
            {
                Image icon = Instantiate(icons[i], skillRow);
                icon.sprite = wrenchSprite;
                icon.color = WrenchGray;
                icon.enabled = true;
            }
            skillRow.gameObject.SetActive(false);
        }
        CRTScreenEffect.RegisterCanvas(overlayCanvas, enableGlow: true);
    }

    private static void ConfigureResponsiveScaling(GameObject canvasObject)
    {
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    private void RemoveLegacyWorldRenderers()
    {
        // Removes windows generated by the former SpriteRenderer implementation,
        // including instances kept alive when scripts are reloaded during Play Mode.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<SpriteRenderer>() != null || child.GetComponent<TextMesh>() != null)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }

    private void UpdateScreenPosition()
    {
        Camera camera = Camera.main;
        if (camera == null || panelRect == null)
        {
            return;
        }

        Vector3 screenPosition = camera.WorldToScreenPoint(fixedWorldPosition);
        PositionPanelAtScreenPoint(screenPosition);
        canvasGroup.alpha = screenPosition.z > 0f ? visibilityAlpha : 0f;
    }

    private void PositionPanelAtScreenPoint(Vector2 screenPosition)
    {
        RectTransform canvasRect = overlayCanvas != null
            ? overlayCanvas.transform as RectTransform
            : null;
        if (canvasRect == null)
            return;

        Camera canvasCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvasCamera, out Vector2 localPoint))
        {
            panelRect.anchoredPosition = localPoint;
        }
    }

    private bool IsSourceInsideCameraVision()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Transform source = SourceTransform;
        Vector3 viewportPosition = camera.WorldToViewportPoint(source.position);
        if (viewportPosition.z <= 0f || viewportPosition.x < 0f || viewportPosition.x > 1f ||
            viewportPosition.y < 0f || viewportPosition.y > 1f)
        {
            return false;
        }

        CameraCircularVision circularVision = camera.GetComponent<CameraCircularVision>();
        return circularVision == null || circularVision.IsWorldPositionVisible(source.position, source);
    }

    private void ResolveUiFont()
    {
        if (requirementLabel == null)
        {
            return;
        }

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        Font font = ui != null ? ui.PermissionLabelFont : null;
        if (font != null && requirementLabel.font != font)
        {
            requirementLabel.font = font;
            if (skillLabel != null) skillLabel.font = font;
        }

        requirementLabel.text = "需求：";
    }

    private void Refresh(bool force)
    {
        int value = GetRequirementValue();
        int unlockSkill = doorInteraction != null ? doorInteraction.RequiredUnlockSkill : 0;
        if (!force && value == lastValue && unlockSkill == lastUnlockSkill)
        {
            return;
        }

        int displayedIconCount = value == 0 ? 1 : value;
        Color iconColor = value == 0 ? GhostBlue : doorData != null ? SwordBrown : WrenchGray;
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].enabled = i < displayedIconCount;
            icons[i].color = iconColor;
            icons[i].rectTransform.anchoredPosition = new Vector2(102f + i * 16f, unlockSkill > 0 ? 24f : 0f);
        }

        if (skillRow != null) skillRow.gameObject.SetActive(unlockSkill > 0);
        panelRect.sizeDelta = unlockSkill > 0 ? new Vector2(270f, 132f) : PanelSize;
        requirementLabel.rectTransform.anchoredPosition = new Vector2(14f, unlockSkill > 0 ? 24f : 0f);
        lastUnlockSkill = unlockSkill;
        lastValue = value;
    }

    private void SetVisible(bool visible, bool immediate)
    {
        isVisible = visible;
        if (visible)
        {
            overlayObject.SetActive(true);
            lastValue = int.MinValue;
            Refresh(true);
        }

        if (immediate)
        {
            visibilityAlpha = visible ? 1f : 0f;
            canvasGroup.alpha = visibilityAlpha;
            if (!visible)
            {
                overlayObject.SetActive(false);
            }
        }
    }

    private void UpdateFade(float deltaTime)
    {
        visibilityAlpha = Mathf.MoveTowards(visibilityAlpha, isVisible ? 1f : 0f, deltaTime / FadeDuration);
        canvasGroup.alpha = visibilityAlpha;
        ApplySourceHighlight();
        if (!isVisible && visibilityAlpha <= 0f)
        {
            overlayObject.SetActive(false);
        }
    }

    private void ApplySourceHighlight()
    {
        sourceHighlight.Show(SourceTransform, visibilityAlpha);
    }

    private int GetRequirementValue()
    {
        return Mathf.Clamp(doorData != null ? doorData.Durability :
            leverData != null ? leverData.Complexity : skillRequirement, 0, MaximumIconCount);
    }

    private static void EnsureWorldDimmer()
    {
        ZeldaCharacterWorldDimmer dimmer = ZeldaCharacterWorldDimmer.GetOrCreate();

        dimmer.Configure(solidSprite, GetTopSortingLayerId(), DimmerSortingOrder, FadeDuration);
    }

    private static int GetTopSortingLayerId()
    {
        int topLayerId = 0;
        int highestValue = int.MinValue;
        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].value > highestValue)
            {
                highestValue = layers[i].value;
                topLayerId = layers[i].id;
            }
        }

        return topLayerId;
    }

    private static void EnsureSprites()
    {
        if (frameSprite != null)
        {
            return;
        }

        frameSprite = CreateFrameSprite();
        swordSprite = ZeldaHealthHeartsUI.CreateStrengthIcon(out _);
        wrenchSprite = ZeldaHealthHeartsUI.CreateSkillIcon(out _);
        solidSprite = CreateSolidSprite();
    }

    private static Sprite CreateFrameSprite()
    {
        const int width = 90;
        const int height = 28;
        Texture2D texture = NewTexture(width, height, "Requirement Frame");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                texture.SetPixel(x, y, border ? Color.white : Color.black);
            }
        }

        return FinishSprite(texture, width, height, 24f, "Requirement Frame");
    }

    private static Sprite CreatePatternSprite(string[] rows, string spriteName)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        Texture2D texture = NewTexture(width, height, spriteName);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, rows[y][x] == '#' ? Color.white : Color.clear);
            }
        }

        return FinishSprite(texture, width, height, width, spriteName);
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = NewTexture(1, 1, "Requirement Dimmer Pixel");
        texture.SetPixel(0, 0, Color.white);
        return FinishSprite(texture, 1, 1, 1f, "Requirement Dimmer Pixel");
    }

    private static Texture2D NewTexture(int width, int height, string textureName)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static Sprite FinishSprite(Texture2D texture, int width, int height, float pixelsPerUnit, string spriteName)
    {
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        sprite.name = spriteName;
        return sprite;
    }
}
