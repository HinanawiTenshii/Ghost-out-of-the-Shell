using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-overlay character attribute display. It follows its character in
/// screen space and therefore cannot be hidden by world renderers.
/// </summary>
public sealed class ZeldaCharacterStatsWindow : MonoBehaviour
{
    private const int MaxIconsPerRow = 10;
    // Character attributes stay above door/lever requirement windows (31000),
    // while remaining below the persistent HUD (32760) and modal UI.
    private const int OverlaySortingOrder = 31100;
    private const int DimmerSortingOrder = 30000;
    private const float FadeDuration = 0.12f;
    private const float WindowDisplayScale = 0.5f;
    private const float DefaultConfiguredScale = 2.2f;
    private static readonly Vector2 PanelSize = new Vector2(276f, 156f);

    private static readonly Color SwordBrown = new Color(0.48f, 0.27f, 0.12f, 1f);
    private static readonly Color WrenchGray = new Color(0.48f, 0.5f, 0.54f, 1f);
    private static readonly Color GhostBlue = ZeldaUiPalette.Ghost;

    private static Sprite frameSprite;
    private static Sprite swordSprite;
    private static Sprite wrenchSprite;
    private static Sprite rectangleSprite;
    private static Sprite discoveryFrameSprite;
    private static Sprite discoveryPlusSprite;
    private static readonly Sprite[] PermissionDigitSprites = new Sprite[10];
    private static readonly Sprite[] NegativePermissionDigitSprites = new Sprite[2];
    private static ZeldaCharacterWorldDimmer worldDimmer;

    private readonly Image[] attackIcons = new Image[MaxIconsPerRow];
    private readonly Image[] skillIcons = new Image[MaxIconsPerRow];
    private readonly Image[] costIcons = new Image[MaxIconsPerRow];
    private readonly Image[] requirementIcons = new Image[MaxIconsPerRow];
    private int lastRequirement = int.MinValue;
    private Vector2 energyIconSize;
    private ZeldaCharacterData data;
    private ZeldaFourWayMover characterMover;
    private Vector2 windowLocalOffset;
    private GameObject overlayObject;
    private Canvas overlayCanvas;
    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private Image permissionImage;
    private bool isVisible;
    private float visibilityAlpha;
    private int lastAttackPower = int.MinValue;
    private int lastSkillValue = int.MinValue;
    private int lastPossessionCost = int.MinValue;
    private int lastPermissionLevel = int.MinValue;
    private GameObject skillDiscoveryBadge;
    private Image skillDiscoveryIcon;
    private string cachedNativeSkillId;
    private PlayerGrowthAttributes.SkillDefinition nativeSkillDefinition;
    private readonly AttributeSourceHighlight sourceHighlight = new AttributeSourceHighlight();

    public void Configure(ZeldaCharacterData source, Vector2 localOffset, float displayScale)
    {
        data = source;
        characterMover = source.GetComponent<ZeldaFourWayMover>();
        windowLocalOffset = localOffset;
        EnsureVisuals();
        panelRect.localScale = Vector3.one * Mathf.Max(0.05f, displayScale / DefaultConfiguredScale * WindowDisplayScale);
        EnsureWorldDimmer();
        Refresh(true);
        SetWindowVisible(false, true);
    }

    private void LateUpdate()
    {
        if (data == null)
        {
            sourceHighlight.Hide();
            return;
        }

        bool isControlledCharacter = characterMover != null && characterMover.isActiveAndEnabled;
        bool shouldBeVisible = Input.GetMouseButton(1) &&
                               !DocumentReader.IsInputBlocked &&
                               !ClockworkPuppetRuntime.BlocksCharacterInput &&
                               !isControlledCharacter &&
                               IsCharacterInsideCameraVision();
        UpdateWindow(shouldBeVisible, Time.unscaledDeltaTime);
    }

    private void UpdateWindow(bool shouldBeVisible, float deltaTime)
    {
        // Keep the lightweight input/visibility check alive, but do not touch
        // the hidden Canvas, icons or screen position once fade-out finishes.
        if (!shouldBeVisible && !isVisible && visibilityAlpha <= 0f)
            return;

        EnsureVisuals();
        bool opening = shouldBeVisible && !isVisible;
        if (shouldBeVisible != isVisible)
        {
            SetWindowVisible(shouldBeVisible);
        }

        UpdateScreenPosition();
        // Opening already forces a complete refresh, including changes made
        // while hidden. Visible and fading windows still update every frame.
        if (!opening)
            Refresh(false);
        UpdateFade(deltaTime);
    }

    private void OnDisable()
    {
        sourceHighlight.Hide();
        // The overlay is a separate root, so streaming/death would otherwise
        // leave the last visible panel behind with no LateUpdate to fade it.
        isVisible = false;
        visibilityAlpha = 0f;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (overlayObject != null)
            overlayObject.SetActive(false);
    }

    private void OnDestroy()
    {
        sourceHighlight.Dispose();
        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }
    }

    private void EnsureVisuals()
    {
        if (overlayObject != null)
        {
            if (overlayCanvas == null)
            {
                overlayCanvas = overlayObject.GetComponent<Canvas>();
            }
            if (!overlayCanvas.overrideSorting)
                overlayCanvas.overrideSorting = true;
            if (overlayCanvas.sortingOrder != OverlaySortingOrder)
                overlayCanvas.sortingOrder = OverlaySortingOrder;
            return;
        }

        RemoveLegacyWorldRenderers();
        EnsureSprites();
        overlayObject = new GameObject(name + " Character Attribute Screen Overlay", typeof(RectTransform));
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

        GameObject panelObject = CreateUiObject("Character Attribute Panel", overlayObject.transform, typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = PanelSize;
        Image frame = panelObject.GetComponent<Image>();
        frame.sprite = frameSprite;
        frame.raycastTarget = false;

        CreateRow(attackIcons, "Attack Power", swordSprite, 50f, new Vector2(22f, 22f));
        CreateRow(skillIcons, "Skill Value", wrenchSprite, 18f, new Vector2(22f, 22f));
        Vector2 energySize = ZeldaHealthHeartsUI.Instance != null
            ? ZeldaHealthHeartsUI.Instance.PossessionEnergyDisplaySize
            : new Vector2(18.48f, 10.56f);
        energyIconSize = energySize;
        CreateRow(costIcons, "Possession Energy", rectangleSprite, -14f, energySize);
        for (int i = 0; i < costIcons.Length; i++)
        {
            costIcons[i].preserveAspect = false;
            costIcons[i].rectTransform.anchoredPosition = new Vector2(20f + i * (energySize.x + 3.3f), -14f);
        }

        CreateRow(requirementIcons, "Possession Requirement", ZeldaHealthHeartsUI.GetSpentPossessionEnergyIcon(), -56f, energySize);
        foreach (Image icon in requirementIcons) icon.preserveAspect = false;
        GameObject requirementLabel = CreateUiObject("Requirement Label", panelRect, typeof(Text));
        Text label = requirementLabel.GetComponent<Text>();
        label.font = ZeldaHealthHeartsUI.Instance != null ? ZeldaHealthHeartsUI.Instance.PermissionLabelFont : null;
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = "需求：";
        label.fontSize = 16;
        label.color = ZeldaUiPalette.Primary;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.anchoredPosition = new Vector2(12f, -56f);
        label.rectTransform.sizeDelta = new Vector2(50f, 26f);

        GameObject permissionObject = CreateUiObject("Permission Level", panelRect, typeof(Image));
        RectTransform permissionRect = permissionObject.GetComponent<RectTransform>();
        permissionRect.anchorMin = permissionRect.anchorMax = new Vector2(0f, 0.5f);
        permissionRect.anchoredPosition = new Vector2(242f, 0f);
        permissionRect.sizeDelta = new Vector2(48f, 72f);
        permissionImage = permissionObject.GetComponent<Image>();
        permissionImage.preserveAspect = true;
        permissionImage.raycastTarget = false;
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

    private void CreateRow(Image[] row, string rowName, Sprite sprite, float y, Vector2 iconSize)
    {
        for (int i = 0; i < row.Length; i++)
        {
            GameObject iconObject = CreateUiObject(rowName + " " + (i + 1), panelRect, typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(20f + i * 18f, y);
            iconRect.sizeDelta = iconSize;
            row[i] = iconObject.GetComponent<Image>();
            row[i].sprite = sprite;
            row[i].preserveAspect = true;
            row[i].raycastTarget = false;
        }
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        System.Type[] allComponents = new System.Type[components.Length + 2];
        allComponents[0] = typeof(RectTransform);
        allComponents[1] = typeof(CanvasRenderer);
        for (int i = 0; i < components.Length; i++)
        {
            allComponents[i + 2] = components[i];
        }

        GameObject result = new GameObject(objectName, allComponents);
        result.transform.SetParent(parent, false);
        return result;
    }

    private void RemoveLegacyWorldRenderers()
    {
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

        // Follow the live head of articulated creatures, but retain the root's
        // authored offset axes/scale so a turning head does not orbit the window.
        Vector3 worldPosition = data.AttributeWindowAnchor.position +
            data.transform.TransformVector(new Vector3(windowLocalOffset.x, windowLocalOffset.y, 0f));
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
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

    private bool IsCharacterInsideCameraVision()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Vector3 worldPosition = data.AttributeWindowAnchor.position;
        Vector3 viewportPosition = camera.WorldToViewportPoint(worldPosition);
        if (viewportPosition.z <= 0f || viewportPosition.x < 0f || viewportPosition.x > 1f ||
            viewportPosition.y < 0f || viewportPosition.y > 1f)
        {
            return false;
        }

        CameraCircularVision circularVision = camera.GetComponent<CameraCircularVision>();
        return circularVision == null || circularVision.IsWorldPositionVisible(worldPosition, data.transform);
    }

    private void Refresh(bool force)
    {
        RefreshSkillDiscoveryBadge(force);
        int attackPower = Mathf.Clamp(data.FinalAttackPower, 0, MaxIconsPerRow);
        int skillValue = Mathf.Clamp(data.SkillValue, 0, MaxIconsPerRow);
        int possessionCost = Mathf.Clamp(data.FinalPossessionEnergy, 0, MaxIconsPerRow);
        int requirement = Mathf.Clamp(data.PossessionCost, 0, MaxIconsPerRow);
        if (force || requirement != lastRequirement)
        {
            SetVisibleCount(requirementIcons, requirement);
            float scale = requirement > 0 ? Mathf.Min(1f, 142f / (requirement * (energyIconSize.x + 3.3f))) : 1f;
            for (int i = 0; i < requirementIcons.Length; i++)
            {
                requirementIcons[i].rectTransform.sizeDelta = energyIconSize * scale;
                requirementIcons[i].rectTransform.anchoredPosition = new Vector2(
                    62f + energyIconSize.x * scale * 0.5f + i * (energyIconSize.x + 3.3f) * scale, -56f);
            }
            lastRequirement = requirement;
        }
        int permissionLevel = data is GhostZeldaCharacterData
            ? 0
            : Mathf.Clamp(
                data.PermissionLevel,
                ZeldaCharacterData.MinimumPermissionLevel,
                ZeldaCharacterData.MaximumPermissionLevel);

        if (force || attackPower != lastAttackPower)
        {
            SetVisibleCount(attackIcons, attackPower);
            lastAttackPower = attackPower;
        }
        if (force || skillValue != lastSkillValue)
        {
            SetVisibleCount(skillIcons, skillValue);
            lastSkillValue = skillValue;
        }
        SetVisibleCount(costIcons, Mathf.Clamp(data.PossessionEnergyDisplaySlotCount, 0, MaxIconsPerRow));
        for (int i = 0; i < costIcons.Length; i++)
            ZeldaHealthHeartsUI.ConfigureEnergyIcon(costIcons[i], i, data);
        lastPossessionCost = possessionCost;
        if (force || permissionLevel != lastPermissionLevel)
        {
            permissionImage.sprite = GetPermissionDigitSprite(permissionLevel);
            permissionImage.color = data is GhostZeldaCharacterData
                ? GhostBlue
                : ZeldaUiPalette.Primary;
            lastPermissionLevel = permissionLevel;
        }

        SetRowColor(attackIcons, SwordBrown);
        SetRowColor(skillIcons, WrenchGray);
        SetRowColor(costIcons, ZeldaUiPalette.Primary);
        SetRowColor(requirementIcons, ZeldaUiPalette.Primary);
    }

    private static void SetVisibleCount(Image[] row, int count)
    {
        for (int i = 0; i < row.Length; i++)
        {
            row[i].enabled = i < count;
        }
    }

    private static void SetRowColor(Image[] row, Color color)
    {
        for (int i = 0; i < row.Length; i++)
        {
            row[i].color = color;
        }
    }

    private void SetWindowVisible(bool visible, bool immediate = false)
    {
        isVisible = visible;
        if (visible)
        {
            overlayObject.SetActive(true);
            Refresh(true);
        }
        if (immediate)
        {
            visibilityAlpha = visible ? 1f : 0f;
            canvasGroup.alpha = visibilityAlpha;
            if (!visible)
            {
                sourceHighlight.Hide();
                overlayObject.SetActive(false);
            }
        }
    }

    private void UpdateFade(float deltaTime)
    {
        visibilityAlpha = Mathf.MoveTowards(visibilityAlpha, isVisible ? 1f : 0f, deltaTime / FadeDuration);
        canvasGroup.alpha = visibilityAlpha;
        sourceHighlight.Show(data != null ? data.transform : null, visibilityAlpha);
        if (!isVisible && visibilityAlpha <= 0f)
        {
            overlayObject.SetActive(false);
        }
    }

    private static void EnsureSprites()
    {
        if (frameSprite != null) return;
        frameSprite = CreateFrameSprite();
        swordSprite = ZeldaHealthHeartsUI.CreateStrengthIcon(out _);
        wrenchSprite = ZeldaHealthHeartsUI.CreateSkillIcon(out _);
        rectangleSprite = CreateSolidSprite();
        for (int level = 0; level < PermissionDigitSprites.Length; level++) PermissionDigitSprites[level] = CreatePermissionDigitSprite(level);
        for (int absoluteValue = 1; absoluteValue <= NegativePermissionDigitSprites.Length; absoluteValue++)
            NegativePermissionDigitSprites[absoluteValue - 1] = CreatePermissionDigitSprite(-absoluteValue);
    }

    private void RefreshSkillDiscoveryBadge(bool force)
    {
        string id = data.NativeCharacterSkillId;
        bool skillChanged = id != cachedNativeSkillId;
        if (skillChanged)
        {
            cachedNativeSkillId = id;
            nativeSkillDefinition = null;
            // Match the base character skill, not an upgrade unlocked later.
            // No lambda/scene search or per-frame skill-icon allocation.
            foreach (var skill in PlayerGrowthAttributes.Skills)
                if (skill.id == id && skill.requiredPossessedSkillId == id)
                {
                    nativeSkillDefinition = skill;
                    break;
                }
        }

        var growth = PlayerGrowthAttributes.Instance;
        bool show = !data.IsDead && !data.IsGhostLike && nativeSkillDefinition != null &&
            (growth == null || !growth.IsSkillDiscovered(nativeSkillDefinition));
        if (!show)
        {
            if (skillDiscoveryBadge != null && skillDiscoveryBadge.activeSelf)
                skillDiscoveryBadge.SetActive(false);
            return;
        }

        // Ordinary NPCs never allocate extra UI. Once built, the indicator is
        // reused and follows the existing hidden-window/fade/streaming path.
        bool created = skillDiscoveryBadge == null;
        if (created) CreateSkillDiscoveryBadge();
        if (!skillDiscoveryBadge.activeSelf) skillDiscoveryBadge.SetActive(true);
        if (created || force || skillChanged)
        {
            skillDiscoveryIcon.sprite = data.CharacterSkillIcon;
            skillDiscoveryIcon.enabled = skillDiscoveryIcon.sprite != null;
        }
    }

    private void CreateSkillDiscoveryBadge()
    {
        skillDiscoveryBadge = CreateUiObject("Possession Skill Discovery", panelRect, typeof(Image));
        RectTransform badgeRect = skillDiscoveryBadge.GetComponent<RectTransform>();
        badgeRect.anchorMin = badgeRect.anchorMax = Vector2.one;
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(0f, 6f);
        badgeRect.sizeDelta = new Vector2(52f, 52f);
        Image background = skillDiscoveryBadge.GetComponent<Image>();
        background.color = new Color32(8, 19, 46, 255);
        background.raycastTarget = false;

        if (discoveryFrameSprite == null)
        {
            const int width = 26, height = 26;
            Texture2D texture = NewPixelTexture(width, height, "Skill Discovery Border");
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, x == 0 || y == 0 || x == width - 1 || y == height - 1
                    ? Color.white : Color.clear);
            discoveryFrameSprite = FinishSprite(texture, width, height, 1f, "Skill Discovery Border");
        }
        Image border = CreateUiObject("Cyan Border", badgeRect, typeof(Image)).GetComponent<Image>();
        border.rectTransform.anchorMin = Vector2.zero; border.rectTransform.anchorMax = Vector2.one;
        border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;
        border.sprite = discoveryFrameSprite; border.color = ZeldaUiPalette.Primary; border.raycastTarget = false;

        skillDiscoveryIcon = CreateUiObject("Native Skill Icon", badgeRect, typeof(Image)).GetComponent<Image>();
        skillDiscoveryIcon.rectTransform.anchorMin = skillDiscoveryIcon.rectTransform.anchorMax = new Vector2(.5f, .5f);
        skillDiscoveryIcon.rectTransform.anchoredPosition = new Vector2(-2f, -2f);
        skillDiscoveryIcon.rectTransform.sizeDelta = new Vector2(32f, 32f);
        skillDiscoveryIcon.preserveAspect = true; skillDiscoveryIcon.color = ZeldaUiPalette.Primary;
        skillDiscoveryIcon.raycastTarget = false;

        Image plusBackground = CreateUiObject("New Skill Mark", badgeRect, typeof(Image)).GetComponent<Image>();
        plusBackground.rectTransform.anchorMin = plusBackground.rectTransform.anchorMax = Vector2.one;
        plusBackground.rectTransform.anchoredPosition = new Vector2(-9f, -9f);
        plusBackground.rectTransform.sizeDelta = new Vector2(16f, 16f);
        plusBackground.color = background.color; plusBackground.raycastTarget = false;
        if (discoveryPlusSprite == null)
            discoveryPlusSprite = CreatePatternSprite(new[] { "..##..", "..##..", "######", "######", "..##..", "..##.." }, "New Skill Plus");
        Image plus = CreateUiObject("Plus Glyph", plusBackground.transform, typeof(Image)).GetComponent<Image>();
        plus.rectTransform.sizeDelta = new Vector2(12f, 12f);
        plus.sprite = discoveryPlusSprite; plus.color = ZeldaUiPalette.Primary; plus.raycastTarget = false;
    }

    private static void EnsureWorldDimmer()
    {
        if (worldDimmer == null)
            worldDimmer = ZeldaCharacterWorldDimmer.GetOrCreate();
        int layerId = 0;
        int highest = int.MinValue;
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.value > highest) { highest = layer.value; layerId = layer.id; }
        }
        worldDimmer.Configure(rectangleSprite, layerId, DimmerSortingOrder, FadeDuration);
    }

    private static Sprite CreateFrameSprite()
    {
        const int width = 92;
        const int height = 52;
        Texture2D texture = NewPixelTexture(width, height, "World Attribute Frame");
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool border = x < 2 || x >= width - 2 || y < 2 || y >= height - 2 || (x >= 70 && x < 72) || (x < 70 && y >= 13 && y < 14);
            texture.SetPixel(x, y, border ? Color.white : Color.black);
        }
        return FinishSprite(texture, width, height, 24f, "World Attribute Frame");
    }

    private static Sprite CreatePatternSprite(string[] rows, string spriteName)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        Texture2D texture = NewPixelTexture(width, height, spriteName);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) texture.SetPixel(x, y, rows[y][x] == '#' ? Color.white : Color.clear);
        return FinishSprite(texture, width, height, width, spriteName);
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = NewPixelTexture(1, 1, "World Pixel Rectangle");
        texture.SetPixel(0, 0, Color.white);
        return FinishSprite(texture, 1, 1, 1f, "World Pixel Rectangle");
    }

    private static Sprite CreatePermissionDigitSprite(int level)
    {
        string[][] patterns =
        {
            new[] { "###", "#.#", "#.#", "#.#", "###" }, new[] { ".#.", "##.", ".#.", ".#.", "###" },
            new[] { "###", "..#", "###", "#..", "###" }, new[] { "###", "..#", "###", "..#", "###" },
            new[] { "#.#", "#.#", "###", "..#", "..#" }, new[] { "###", "#..", "###", "..#", "###" },
            new[] { "###", "#..", "###", "#.#", "###" }, new[] { "###", "..#", ".#.", ".#.", ".#." },
            new[] { "###", "#.#", "###", "#.#", "###" }, new[] { "###", "#.#", "###", "..#", "###" }
        };
        string[] digitRows = patterns[Mathf.Clamp(Mathf.Abs(level), 0, 9)];
        string[] rows = digitRows;
        int width = 3;
        if (level < 0)
        {
            width = 7;
            rows = new string[digitRows.Length];
            for (int row = 0; row < digitRows.Length; row++)
                rows[row] = (row == 2 ? "###." : "....") + digitRows[row];
        }

        Texture2D texture = NewPixelTexture(width, 5, "World Permission Digit " + level);
        for (int row = 0; row < 5; row++)
        for (int x = 0; x < width; x++) texture.SetPixel(x, 4 - row, rows[row][x] == '#' ? Color.white : Color.clear);
        return FinishSprite(texture, width, 5, 5f, "World Permission Digit " + level);
    }

    private static Sprite GetPermissionDigitSprite(int level)
    {
        if (level >= 0)
            return PermissionDigitSprites[Mathf.Clamp(level, 0, PermissionDigitSprites.Length - 1)];

        int index = Mathf.Clamp(-level - 1, 0, NegativePermissionDigitSprites.Length - 1);
        return NegativePermissionDigitSprites[index];
    }

    private static Texture2D NewPixelTexture(int width, int height, string textureName)
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

/// <summary>Darkens the world behind attribute windows while leaving overlay UI untouched.</summary>
public sealed class ZeldaCharacterWorldDimmer : MonoBehaviour
{
    private const float MaximumDarkness = 0.68f;
    private const int DimmerCanvasSortingOrder = 30000;
    private Canvas dimmerCanvas;
    private CanvasGroup dimmerCanvasGroup;
    private Image dimmerImage;
    private float fadeDuration = 0.12f;
    private float currentAlpha;

    public static ZeldaCharacterWorldDimmer Instance { get; private set; }

    public static ZeldaCharacterWorldDimmer GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject dimmerObject = new GameObject(
            "Character Attribute World Dimmer",
            typeof(RectTransform));
        return dimmerObject.AddComponent<ZeldaCharacterWorldDimmer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Configure(Sprite sprite, int sortingLayerId, int sortingOrder, float duration)
    {
        EnsureCanvas(sprite);
        fadeDuration = Mathf.Max(0.01f, duration);
    }

    private void LateUpdate()
    {
        bool requested =
            !DocumentReader.IsInputBlocked &&
            !ClockworkPuppetRuntime.BlocksCharacterInput &&
            Input.GetMouseButton(1);
        float targetAlpha = requested ? MaximumDarkness : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime * MaximumDarkness / fadeDuration);
        if (dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.alpha = currentAlpha;
        }
        if (dimmerCanvas != null)
        {
            dimmerCanvas.enabled = currentAlpha > 0f;
        }
    }

    private void EnsureCanvas(Sprite sprite)
    {
        if (dimmerCanvas == null)
        {
            dimmerCanvas = gameObject.GetComponent<Canvas>();
            if (dimmerCanvas == null)
            {
                dimmerCanvas = gameObject.AddComponent<Canvas>();
            }
            dimmerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dimmerCanvas.overrideSorting = true;
            dimmerCanvas.sortingOrder = DimmerCanvasSortingOrder;

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                gameObject.layer = uiLayer;
            }

            dimmerCanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (dimmerCanvasGroup == null)
            {
                dimmerCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            dimmerCanvasGroup.alpha = 0f;
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = false;
        }

        if (dimmerImage == null)
        {
            GameObject imageObject = new GameObject(
                "Dimmer",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(transform, false);
            if (gameObject.layer >= 0)
            {
                imageObject.layer = gameObject.layer;
            }
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            dimmerImage = imageObject.GetComponent<Image>();
            dimmerImage.color = Color.black;
            dimmerImage.raycastTarget = false;
        }

        dimmerImage.sprite = sprite;
        dimmerCanvas.overrideSorting = true;
        dimmerCanvas.sortingOrder = DimmerCanvasSortingOrder;
        dimmerCanvas.enabled = currentAlpha > 0f;
        CRTScreenEffect.RegisterCanvas(dimmerCanvas);
    }
}
