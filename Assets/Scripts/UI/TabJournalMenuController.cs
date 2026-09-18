using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Empty, tabbed gameplay menu reserved for map, journal and ability content.
/// It shares the pause menu canvas so every scene using PauseMenu receives it.
/// </summary>
[DefaultExecutionOrder(10001)]
public sealed class TabJournalMenuController : MonoBehaviour
{
    private const string MiniMapResourceFolder = "MiniMaps";

    // Keep the unloaded-scene snapshots alive across scene changes.  The map
    // menu is recreated with each scene, while Resources may release assets
    // that are only referenced by the previous menu.  A failed one-off load
    // used to leave every non-current map empty and also lose its display name.
    private static readonly Dictionary<string, RuntimeMiniMapSceneData>
        MiniMapSnapshots =
            new Dictionary<string, RuntimeMiniMapSceneData>(
                StringComparer.OrdinalIgnoreCase);
    private static bool miniMapSnapshotsLoaded;

    private static readonly Color GhostBlue = ZeldaUiPalette.Ghost;
    private static readonly Color MapTransitionSkyBlue =
        new Color32(65, 175, 235, 255);
    private static readonly Color PanelBlue =
        new Color(0.025f, 0.07f, 0.12f, 0.985f);
    private static readonly Color TabBlue =
        new Color(0.035f, 0.12f, 0.20f, 1f);
    private static readonly Color DisabledBlue =
        new Color(0.08f, 0.13f, 0.18f, 0.98f);
    private static readonly Color DisabledText =
        new Color(0.35f, 0.5f, 0.62f, 1f);

    [SerializeField] private Font menuFont;
    [SerializeField, Range(0f, 1f)] private float backgroundDimOpacity = 0.76f;
    [SerializeField, Tooltip("Optional display name for this scene. The scene name is used when left empty.")]
    private string areaNameOverride;

    private GameObject menuRoot;
    private Canvas menuCanvas;
    private readonly Image[] tabBackgrounds = new Image[4];
    private readonly Image[] tabBottomEdges = new Image[4];
    private readonly Button[] tabButtons = new Button[4];
    private readonly Text[] tabLabels = new Text[4];
    private readonly GameObject[] contentPages = new GameObject[4];
    private RectTransform panelTopLeft, panelTopRight;
    private readonly SkillPageArt[] availableAbilityIcons = new SkillPageArt[25];
    private readonly Text[] availableAbilityNames = new Text[25];
    private readonly string[] availableAbilityIds = new string[25];
    private readonly Image[] availableAbilityBackgrounds = new Image[25];
    private static readonly Color AbilitySlotBackground = new Color(0.015f, 0.04f, 0.06f, 0.85f);
    private static readonly Color AbilitySlotHighlight = new Color(0.06f, 0.16f, 0.22f, 0.85f);
    private Vector3 equipmentOriginalScale;
    private readonly RectTransform[] equipmentSlots = new RectTransform[5];
    private readonly Image[] equipmentIcons = new Image[5];
    private RectTransform abilityPage, abilityRight;
    private GameObject equipmentOverlay;
    private int equipmentSelection = -1;
    private bool previousNavigation;
    private Text mapAreaNameText;
    private RuntimeMiniMapGraphic miniMapGraphic;
    private RectTransform mapSceneListContent;
    private RectTransform mapTransitionLabelLayer;
    private RectTransform mapPointLayer;
    private GameObject mapPointTooltipRoot;
    private RectTransform mapPointTooltipRect;
    private Text mapPointTooltipText;
    private string selectedMapSceneName;
    private readonly List<MiniMapTransitionLabel> mapTransitionLabels =
        new List<MiniMapTransitionLabel>();
    private readonly List<MiniMapPointIcon> mapPointIcons =
        new List<MiniMapPointIcon>();
    private MapPointOfInterestManager mapPointManager;
    private string hoveredMapPointId;
    private QuestJournalManager questJournal;
    private RectTransform questListContent;
    private Text questTitleText;
    private Text questDetailsText;
    private Button questTrackButton;
    private Image questTrackButtonBackground;
    private Text questTrackButtonText;
    private readonly List<Image> questEntryBackgrounds = new List<Image>();
    private string selectedQuestId;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private int selectedTab;

    public static bool IsOpen { get; private set; }
    public static bool BlocksInput =>
        IsOpen || Time.frameCount <= inputBlockedThroughFrame;

    private static int inputBlockedThroughFrame = -1;

    private sealed class MiniMapTransitionLabel
    {
        public int transitionIndex;
        public RectTransform rect;
        public Text text;
    }

    private sealed class MiniMapPointIcon
    {
        public string pointId;
        public RectTransform rect;
        public RuntimeMiniMapPointIconGraphic graphic;
    }

    public string AreaDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(areaNameOverride))
            {
                return areaNameOverride.Trim();
            }

            Scene scene = gameObject.scene;
            return scene.IsValid() ? scene.name : string.Empty;
        }
    }

    private void Awake()
    {
        EnsureMiniMapSnapshotCatalog();
        menuCanvas = GetComponent<Canvas>();
        EnsureEventSystem();
        BuildMenu();
        questJournal = QuestJournalManager.GetOrCreate();
        questJournal.JournalChanged += RefreshQuestJournal;
        questJournal.TrackedQuestChanged += HandleTrackedQuestChanged;
        mapPointManager = MapPointOfInterestManager.GetOrCreate();
        mapPointManager.PointsChanged += HandleMapPointsChanged;
        SetSelectedTab(0);
        SetMenuVisible(false);
    }

    private void Update()
    {
        if (equipmentSelection >= 0)
        {
            if (Input.GetMouseButtonDown(1)) EndEquipmentSelection();
            return;
        }
        bool tabPressed = Input.GetKeyDown(KeyCode.Tab);
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape);

        if (IsOpen)
        {
            if (tabPressed || escapePressed)
            {
                CloseMenu();
            }
            return;
        }

        if (tabPressed && !DocumentReader.IsInputBlocked)
        {
            OpenMenu();
        }
    }

    private void LateUpdate()
    {
        if (IsOpen)
        {
            EnsureRenderPriority();
            Scene activeScene = SceneManager.GetActiveScene();
            if (selectedTab == 0 &&
                miniMapGraphic != null &&
                selectedMapSceneName == activeScene.name &&
                ZeldaRuntimeRegistry.GetControlledMover() != null)
            {
                miniMapGraphic.SetCameraPosition(
                    ZeldaRuntimeRegistry.GetControlledMover().transform.position);
            }

            if (selectedTab == 0)
            {
                UpdateMiniMapTransitionLabels();
                UpdateMiniMapPointPositions();
            }
        }
    }

    public void OpenMenu()
    {
        if (IsOpen || DocumentReader.IsInputBlocked)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        IsOpen = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureRenderPriority();
        SetMenuVisible(true);
        selectedMapSceneName = SceneManager.GetActiveScene().name;
        RefreshMiniMap();
        RefreshQuestJournal();
    }

    public void CloseMenu()
    {
        EndEquipmentSelection();
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        inputBlockedThroughFrame = Time.frameCount;
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        SetMenuVisible(false);
    }

    private void BuildMenu()
    {
        menuRoot = CreateUiObject("Tab Journal Menu Root", transform, typeof(Image));
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image dimmer = menuRoot.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, backgroundDimOpacity);
        dimmer.raycastTarget = true;

        GameObject panelObject = CreateUiObject(
            "Empty Tab Content Panel",
            rootRect,
            typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -22f);
        panelRect.sizeDelta = new Vector2(1080f, 520f);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBlue;
        panelImage.raycastTarget = true;
        CreateBorder(panelRect, GhostBlue, 3f, out _);
        // Split the top border so the active bookmark opens into the content panel.
        panelTopLeft = panelRect.Find("Top").GetComponent<RectTransform>();
        panelTopRight = CreateBorderEdge(panelRect, "Top Right Segment", GhostBlue,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -3f), Vector2.zero).rectTransform;

        for (int index = 0; index < contentPages.Length; index++)
        {
            GameObject page = CreateUiObject(
                "Empty Content Page " + index,
                panelRect);
            RectTransform pageRect = page.GetComponent<RectTransform>();
            Stretch(pageRect);
            pageRect.offsetMin = new Vector2(20f, 20f);
            pageRect.offsetMax = new Vector2(-20f, -20f);
            contentPages[index] = page;
        }

        BuildMiniMapPage(contentPages[0].GetComponent<RectTransform>());
        BuildQuestJournalPage(contentPages[1].GetComponent<RectTransform>());
        contentPages[2].AddComponent<CharacterSkillPage>().Build(menuFont);
        BuildAbilitySelectionPage(contentPages[3].GetComponent<RectTransform>());

        string[] labels =
        {
            "地图",
            "任务日志",
            "角色能力",
            "能力选择"
        };
        float[] horizontalPositions = { -390f, -130f, 130f, 390f };
        for (int index = 0; index < labels.Length; index++)
        {
            CreateTab(rootRect, labels[index], horizontalPositions[index], index);
        }
    }

    private void BuildAbilitySelectionPage(RectTransform page)
    {
        abilityPage = page;
        // Five placeholders correspond to HUD skill slots 1-5; abilities are assigned later.
        var left = CreateUiObject("Ability Selection List", page, typeof(Image));
        var leftRect = left.GetComponent<RectTransform>();
        leftRect.anchorMin = Vector2.zero;
        leftRect.anchorMax = new Vector2(0.5f, 1f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = new Vector2(-282f, 0f);
        var background = left.GetComponent<Image>();
        background.color = new Color(0.015f, 0.045f, 0.075f, 0.72f);
        background.raycastTarget = false;
        CreateBorderEdge(page, "Ability Selection Column Divider", GhostBlue,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
            new Vector2(-271f, 0f), new Vector2(-269f, 0f));
        const float slotSize = 64f;
        const float slotPitch = 88f;
        for (int index = 0; index < 5; index++)
        {
            var slot = CreateUiObject("Ability Slot " + (index + 1), page, typeof(Image));
            var slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(-405f, (2 - index) * slotPitch);
            slotRect.sizeDelta = Vector2.one * slotSize;
            slot.GetComponent<Image>().color = new Color(0.015f, 0.04f, 0.06f, 0.85f);
            slot.GetComponent<Image>().raycastTarget = false;
            CreateBorder(slotRect, GhostBlue, 2.5f, out _);
            equipmentSlots[index] = slotRect;
            var selectButton = slot.AddComponent<Button>();
            slot.GetComponent<Image>().raycastTarget = true;
            selectButton.targetGraphic = slot.GetComponent<Image>();
            selectButton.transition = Selectable.Transition.None;
            selectButton.navigation = new Navigation { mode = Navigation.Mode.None };
            int slotIndex = index;
            selectButton.onClick.AddListener(() => BeginEquipmentSelection(slotIndex));
            var equippedIcon = CreateUiObject("Equipped Icon", slotRect, typeof(Image)).GetComponent<Image>();
            equippedIcon.rectTransform.sizeDelta = new Vector2(38f, 38f);
            equippedIcon.raycastTarget = false;
            equippedIcon.preserveAspect = true;
            equippedIcon.enabled = false;
            equipmentIcons[index] = equippedIcon;

            var number = CreateUiObject("Shortcut Number", slotRect, typeof(Text)).GetComponent<Text>();
            var numberRect = number.rectTransform;
            numberRect.anchorMin = numberRect.anchorMax = new Vector2(0f, 1f);
            numberRect.pivot = new Vector2(0f, 1f);
            numberRect.anchoredPosition = new Vector2(6f, -4f);
            numberRect.sizeDelta = new Vector2(24f, 24f);
            number.font = menuFont;
            number.fontSize = 18;
            number.alignment = TextAnchor.UpperLeft;
            number.text = (index + 1).ToString();
            number.color = GhostBlue;
            number.raycastTarget = false;
        }
        var right = CreateUiObject("Ability Selection Details", page, typeof(Image)).GetComponent<RectTransform>();
        right.anchorMin = right.anchorMax = new Vector2(0.5f, 0.5f);
        right.anchoredPosition = new Vector2(135f, 0f);
        right.sizeDelta = new Vector2(750f, 480f);
        abilityRight = right;
        right.GetComponent<Image>().color = new Color(0.015f, 0.045f, 0.075f, 0.97f);
        right.GetComponent<Image>().raycastTarget = false;
        CreateBorder(right, GhostBlue, 2f, out _);

        // Equal cells distribute the 5x5 grid evenly across the framed area.
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                var slot = CreateUiObject("Available Ability " + (row * 5 + column + 1), right, typeof(Image));
                var slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchorMin = slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2((column - 2) * 150f, (2 - row) * 92f);
                slotRect.sizeDelta = Vector2.one * slotSize;
                slot.GetComponent<Image>().color = new Color(0.015f, 0.04f, 0.06f, 0.85f);
                slot.GetComponent<Image>().raycastTarget = false;
                CreateBorder(slotRect, GhostBlue, 2.5f, out _);
                var icon = CreateUiObject("Ability Icon", slotRect, typeof(CanvasRenderer), typeof(SkillPageArt));
                var iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(44f, 44f);
                var art = icon.GetComponent<SkillPageArt>();
                art.raycastTarget = false;
                availableAbilityIcons[row * 5 + column] = art;
                icon.SetActive(false);
                int abilityIndex = row * 5 + column;
                var chooseButton = slot.AddComponent<Button>();
                slot.GetComponent<Image>().raycastTarget = true;
                chooseButton.targetGraphic = slot.GetComponent<Image>();
                chooseButton.transition = Selectable.Transition.None;
                availableAbilityBackgrounds[abilityIndex] = slot.GetComponent<Image>();
                var hover = slot.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => {
                    if (equipmentSelection >= 0 && !string.IsNullOrEmpty(availableAbilityIds[abilityIndex]))
                        availableAbilityBackgrounds[abilityIndex].color = AbilitySlotHighlight;
                });
                hover.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => availableAbilityBackgrounds[abilityIndex].color = AbilitySlotBackground);
                hover.triggers.Add(exit);
                chooseButton.navigation = new Navigation { mode = Navigation.Mode.None };
                chooseButton.onClick.AddListener(() => {
                    if (equipmentSelection < 0 || string.IsNullOrEmpty(availableAbilityIds[abilityIndex])) return;
                    if (PlayerGrowthAttributes.Instance != null && PlayerGrowthAttributes.Instance.ToggleEquippedSkill(equipmentSelection, availableAbilityIds[abilityIndex]))
                    { EndEquipmentSelection(); RefreshAvailableAbilities(); }
                });
                var name = CreateUiObject("Skill Name", slotRect, typeof(Text)).GetComponent<Text>();
                name.rectTransform.anchoredPosition = new Vector2(0f, -42f);
                name.rectTransform.sizeDelta = new Vector2(142f, 20f);
                name.font = menuFont; name.fontSize = 16;
                name.alignment = TextAnchor.MiddleCenter; name.color = GhostBlue; name.raycastTarget = false;
                availableAbilityNames[abilityIndex] = name;
            }
        }
    }

    private void RefreshAvailableAbilities()
    {
        var growth = PlayerGrowthAttributes.Instance;
        for (int i = 0; i < 5; i++)
        {
            equipmentIcons[i].sprite = SkillPageArt.GetAbilitySprite(growth != null ? growth.GetEquippedSkill(i) : null);
            equipmentIcons[i].enabled = equipmentIcons[i].sprite != null;
            equipmentIcons[i].color = GhostBlue;
        }
        int index = 0;
        foreach (var skill in PlayerGrowthAttributes.Skills)
        {
            if (skill.locked || skill.type != PlayerGrowthAttributes.SkillType.Active ||
                growth == null || !growth.HasSkill(skill.id) || growth.GetEffectiveSkillId(skill.id) != skill.id) continue;
            if (index >= availableAbilityIcons.Length) break;
            availableAbilityNames[index].text = skill.title;
            availableAbilityIds[index] = skill.id;
            var art = availableAbilityIcons[index++];
            art.soulMark = PlayerGrowthAttributes.IsSoulMarkSkill(skill.id);
            art.domino = PlayerGrowthAttributes.IsDominoSkill(skill.id);
            art.ghostForm = PlayerGrowthAttributes.IsGhostFormSkill(skill.id);
            art.combatExpertise = PlayerGrowthAttributes.IsCombatExpertiseSkill(skill.id);
            art.fearRoar = PlayerGrowthAttributes.IsFearRoarSkill(skill.id);
            art.royalCommand = PlayerGrowthAttributes.IsRoyalCommandSkill(skill.id);
            art.royalCommandLevel = skill.id == "royal_command_2" ? 2 : 1;
            art.fearRoarLevel = skill.id == "fear_roar_3" ? 3 : skill.id == "fear_roar_2" ? 2 : 1;
            art.combatExpertiseLevel = skill.id == "combat_expertise_2" ? 2 : 1;
            art.ghostFormLevel = skill.id == "ghost_form_3" ? 3 : skill.id == "ghost_form_2" ? 2 : 1;
            art.dominoLevel = skill.id == "domino_3" ? 3 : skill.id == "domino_2" ? 2 : 1;
            art.soulMarkLevel = skill.id == "soul_mark_3" ? 3 : skill.id == "soul_mark_2" ? 2 : 1;
            art.gameObject.name = skill.title;
            art.gameObject.SetActive(true);
            art.SetAllDirty();
        }
        for (; index < availableAbilityIcons.Length; index++)
        {
            availableAbilityIcons[index].gameObject.SetActive(false);
            availableAbilityNames[index].text = "";
            availableAbilityIds[index] = null;
        }
    }

    private void BeginEquipmentSelection(int slot)
    {
        if (equipmentSelection >= 0) return;
        equipmentSelection = slot;
        if (EventSystem.current != null)
        {
            previousNavigation = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
        equipmentOverlay = CreateUiObject("Equipment Selection Dimmer", menuRoot.transform, typeof(Image));
        Stretch(equipmentOverlay.GetComponent<RectTransform>());
        equipmentOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        // Only these controls render and receive clicks above the input-blocking dimmer.
        abilityRight.SetParent(equipmentOverlay.transform, true);
        equipmentSlots[slot].SetParent(equipmentOverlay.transform, true);
        equipmentOriginalScale = equipmentSlots[slot].localScale;
        equipmentSlots[slot].localScale = equipmentOriginalScale * 1.12f;
        equipmentSlots[slot].GetComponent<Image>().color = AbilitySlotHighlight;
    }

    private void EndEquipmentSelection()
    {
        if (equipmentSelection < 0) return;
        equipmentSlots[equipmentSelection].localScale = equipmentOriginalScale;
        equipmentSlots[equipmentSelection].GetComponent<Image>().color = AbilitySlotBackground;
        foreach (var background in availableAbilityBackgrounds)
            if (background != null) background.color = AbilitySlotBackground;
        abilityRight.SetParent(abilityPage, true);
        equipmentSlots[equipmentSelection].SetParent(abilityPage, true);
        equipmentSelection = -1;
        if (equipmentOverlay != null) { equipmentOverlay.SetActive(false); Destroy(equipmentOverlay); }
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.sendNavigationEvents = previousNavigation;
        }
    }

    private void BuildMiniMapPage(RectTransform page)
    {
        GameObject listPanelObject = CreateUiObject(
            "Map Scene List",
            page,
            typeof(Image),
            typeof(ScrollRect));
        RectTransform listPanelRect = listPanelObject.GetComponent<RectTransform>();
        listPanelRect.anchorMin = new Vector2(0f, 0f);
        listPanelRect.anchorMax = new Vector2(0.36f, 1f);
        listPanelRect.offsetMin = Vector2.zero;
        listPanelRect.offsetMax = new Vector2(-12f, 0f);
        Image listPanelImage = listPanelObject.GetComponent<Image>();
        listPanelImage.color = new Color(0.015f, 0.045f, 0.075f, 0.72f);
        listPanelImage.raycastTarget = true;
        CreateBorderEdge(
            page,
            "Map Column Divider",
            GhostBlue,
            new Vector2(0.36f, 0f),
            new Vector2(0.36f, 1f),
            new Vector2(-1.5f, 0f),
            new Vector2(1.5f, 0f));

        GameObject listViewportObject = CreateUiObject(
            "Map Scene List Viewport",
            listPanelRect,
            typeof(Image),
            typeof(RectMask2D));
        RectTransform listViewportRect =
            listViewportObject.GetComponent<RectTransform>();
        Stretch(listViewportRect);
        listViewportRect.offsetMin = new Vector2(12f, 12f);
        listViewportRect.offsetMax = new Vector2(-12f, -12f);
        Image listViewportImage = listViewportObject.GetComponent<Image>();
        listViewportImage.color = Color.clear;
        listViewportImage.raycastTarget = true;

        GameObject listContentObject = CreateUiObject(
            "Map Scene List Content",
            listViewportRect);
        mapSceneListContent = listContentObject.GetComponent<RectTransform>();
        mapSceneListContent.anchorMin = new Vector2(0f, 1f);
        mapSceneListContent.anchorMax = new Vector2(1f, 1f);
        mapSceneListContent.pivot = new Vector2(0.5f, 1f);
        mapSceneListContent.anchoredPosition = Vector2.zero;
        mapSceneListContent.sizeDelta = Vector2.zero;

        ScrollRect listScrollRect = listPanelObject.GetComponent<ScrollRect>();
        listScrollRect.viewport = listViewportRect;
        listScrollRect.content = mapSceneListContent;
        listScrollRect.horizontal = false;
        listScrollRect.vertical = true;
        listScrollRect.movementType = ScrollRect.MovementType.Clamped;
        listScrollRect.scrollSensitivity = 28f;

        GameObject areaNameObject = CreateUiObject(
            "Selected Map Name",
            page,
            typeof(Text));
        RectTransform areaNameRect = areaNameObject.GetComponent<RectTransform>();
        areaNameRect.anchorMin = new Vector2(0.39f, 1f);
        areaNameRect.anchorMax = new Vector2(1f, 1f);
        areaNameRect.pivot = new Vector2(0.5f, 1f);
        areaNameRect.offsetMin = new Vector2(0f, -78f);
        areaNameRect.offsetMax = Vector2.zero;
        mapAreaNameText = areaNameObject.GetComponent<Text>();
        mapAreaNameText.font = menuFont;
        mapAreaNameText.fontSize = 32;
        mapAreaNameText.fontStyle = FontStyle.Normal;
        mapAreaNameText.alignment = TextAnchor.MiddleCenter;
        mapAreaNameText.color = GhostBlue;
        mapAreaNameText.raycastTarget = false;

        GameObject mapFrameObject = CreateUiObject(
            "Mini Map Frame",
            page,
            typeof(Image));
        RectTransform mapFrameRect = mapFrameObject.GetComponent<RectTransform>();
        mapFrameRect.anchorMin = new Vector2(0.39f, 0f);
        mapFrameRect.anchorMax = Vector2.one;
        mapFrameRect.offsetMin = new Vector2(18f, 8f);
        mapFrameRect.offsetMax = new Vector2(-18f, -86f);
        Image mapFrameImage = mapFrameObject.GetComponent<Image>();
        mapFrameImage.color = new Color(0.015f, 0.045f, 0.075f, 0.97f);
        mapFrameImage.raycastTarget = false;
        CreateBorder(mapFrameRect, GhostBlue, 3f, out _);

        GameObject viewportObject = CreateUiObject(
            "Mini Map Viewport",
            mapFrameRect,
            typeof(Image),
            typeof(RuntimeMiniMapViewportInput),
            typeof(RectMask2D));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(9f, 25f);
        viewportRect.offsetMax = new Vector2(-25f, -9f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        GameObject graphicObject = CreateUiObject(
            "Blocks Wireframe Map",
            viewportRect,
            typeof(CanvasRenderer),
            typeof(RuntimeMiniMapGraphic));
        RectTransform graphicRect = graphicObject.GetComponent<RectTransform>();
        Stretch(graphicRect);
        miniMapGraphic = graphicObject.GetComponent<RuntimeMiniMapGraphic>();
        miniMapGraphic.raycastTarget = false;

        GameObject transitionLabelLayerObject = CreateUiObject(
            "Map Transition Labels",
            viewportRect);
        mapTransitionLabelLayer =
            transitionLabelLayerObject.GetComponent<RectTransform>();
        Stretch(mapTransitionLabelLayer);

        GameObject mapPointLayerObject = CreateUiObject(
            "Map Points Of Interest",
            viewportRect);
        mapPointLayer = mapPointLayerObject.GetComponent<RectTransform>();
        Stretch(mapPointLayer);

        GameObject tooltipObject = CreateUiObject(
            "Map Point Tooltip",
            viewportRect,
            typeof(Image));
        mapPointTooltipRoot = tooltipObject;
        mapPointTooltipRect = tooltipObject.GetComponent<RectTransform>();
        mapPointTooltipRect.anchorMin = mapPointTooltipRect.anchorMax =
            new Vector2(0.5f, 0.5f);
        mapPointTooltipRect.pivot = new Vector2(0.5f, 0.5f);
        mapPointTooltipRect.sizeDelta = new Vector2(230f, 84f);
        Image tooltipBackground = tooltipObject.GetComponent<Image>();
        tooltipBackground.color = new Color(0.02f, 0.08f, 0.14f, 0.98f);
        tooltipBackground.raycastTarget = false;
        CreateBorder(mapPointTooltipRect, GhostBlue, 2f, out _);

        GameObject tooltipTextObject = CreateUiObject(
            "Description",
            mapPointTooltipRect,
            typeof(Text));
        RectTransform tooltipTextRect =
            tooltipTextObject.GetComponent<RectTransform>();
        Stretch(tooltipTextRect);
        tooltipTextRect.offsetMin = new Vector2(10f, 7f);
        tooltipTextRect.offsetMax = new Vector2(-10f, -7f);
        mapPointTooltipText = tooltipTextObject.GetComponent<Text>();
        mapPointTooltipText.font = menuFont;
        mapPointTooltipText.fontSize = 13;
        mapPointTooltipText.alignment = TextAnchor.UpperLeft;
        mapPointTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        mapPointTooltipText.verticalOverflow = VerticalWrapMode.Truncate;
        mapPointTooltipText.color = Color.white;
        mapPointTooltipText.raycastTarget = false;
        mapPointTooltipRoot.SetActive(false);

        Scrollbar horizontalScrollbar = CreateMapScrollbar(
            mapFrameRect,
            "Horizontal Map Scrollbar",
            Scrollbar.Direction.LeftToRight,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(10f, 10f),
            new Vector2(-180f, 18f));
        Scrollbar verticalScrollbar = CreateMapScrollbar(
            mapFrameRect,
            "Vertical Map Scrollbar",
            Scrollbar.Direction.BottomToTop,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-19f, 25f),
            new Vector2(-11f, -10f));

        GameObject zoomPercentageObject = CreateUiObject(
            "Map Zoom Percentage",
            mapFrameRect,
            typeof(Text));
        RectTransform zoomPercentageRect =
            zoomPercentageObject.GetComponent<RectTransform>();
        zoomPercentageRect.anchorMin =
            zoomPercentageRect.anchorMax = new Vector2(1f, 0f);
        zoomPercentageRect.pivot = new Vector2(0.5f, 0.5f);
        zoomPercentageRect.anchoredPosition = new Vector2(-126f, 18f);
        zoomPercentageRect.sizeDelta = new Vector2(78f, 24f);
        Text zoomPercentageText = zoomPercentageObject.GetComponent<Text>();
        zoomPercentageText.text = "100%";
        zoomPercentageText.font = menuFont;
        zoomPercentageText.fontSize = 17;
        zoomPercentageText.alignment = TextAnchor.MiddleCenter;
        zoomPercentageText.color = GhostBlue;
        zoomPercentageText.raycastTarget = false;

        CreateMapZoomButton(
            mapFrameRect,
            "Zoom In",
            "+",
            new Vector2(-67f, 18f),
            () => miniMapGraphic.ZoomBy(0.5f));
        CreateMapZoomButton(
            mapFrameRect,
            "Zoom Out",
            "−",
            new Vector2(-37f, 18f),
            () => miniMapGraphic.ZoomBy(-0.5f));

        viewportObject.GetComponent<RuntimeMiniMapViewportInput>().Configure(
            miniMapGraphic,
            horizontalScrollbar,
            verticalScrollbar,
            zoomPercentageText);
    }

    private Scrollbar CreateMapScrollbar(
        RectTransform parent,
        string objectName,
        Scrollbar.Direction direction,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject scrollbarObject = CreateUiObject(
            objectName,
            parent,
            typeof(Image),
            typeof(Scrollbar));
        RectTransform scrollbarRect =
            scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = anchorMin;
        scrollbarRect.anchorMax = anchorMax;
        scrollbarRect.offsetMin = offsetMin;
        scrollbarRect.offsetMax = offsetMax;
        Image background = scrollbarObject.GetComponent<Image>();
        background.color = new Color(0.035f, 0.12f, 0.2f, 1f);

        GameObject handleObject = CreateUiObject(
            "Handle",
            scrollbarRect,
            typeof(Image));
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        Stretch(handleRect);
        Image handle = handleObject.GetComponent<Image>();
        handle.color = GhostBlue;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = direction;
        scrollbar.value = 0.5f;
        scrollbar.size = 1f;
        ApplySkillScrollbarStyle(scrollbar);
        return scrollbar;
    }

    internal static void ApplySkillScrollbarStyle(Scrollbar scrollbar)
    {
        var track = scrollbar.GetComponent<Image>();
        if (track != null) track.color = new Color(0.035f, 0.12f, 0.2f, 1f);
        if (scrollbar.targetGraphic != null) scrollbar.targetGraphic.color = GhostBlue;
        // Match the skill tree's standard interaction tint, without multiplying blue by blue.
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.colors = ColorBlock.defaultColorBlock;
    }

    internal static void CreateMapZoomButton(
        RectTransform parent,
        string objectName,
        string symbol,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(
            objectName,
            parent,
            typeof(Image),
            typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(26f, 26f);
        Image background = buttonObject.GetComponent<Image>();
        background.color = TabBlue;
        CreateBorder(buttonRect, GhostBlue, 1.5f, out _);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = TabBlue;
        colors.highlightedColor = Color.Lerp(TabBlue, GhostBlue, 0.32f);
        colors.pressedColor = Color.Lerp(TabBlue, GhostBlue, 0.52f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject labelObject = CreateUiObject(
            "Magnifier Icon",
            buttonRect,
            typeof(CanvasRenderer),
            typeof(RuntimeMiniMapZoomIcon));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect);
        RuntimeMiniMapZoomIcon icon =
            labelObject.GetComponent<RuntimeMiniMapZoomIcon>();
        icon.color = GhostBlue;
        icon.raycastTarget = false;
        icon.Configure(symbol == "+");
    }

    private void RefreshMiniMap()
    {
        if (miniMapGraphic == null ||
            mapAreaNameText == null ||
            mapSceneListContent == null)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        List<string> sceneNames = GetMapSceneNames(activeScene.name);
        if (string.IsNullOrEmpty(selectedMapSceneName) ||
            !sceneNames.Contains(selectedMapSceneName))
        {
            selectedMapSceneName = activeScene.name;
        }

        for (int index = mapSceneListContent.childCount - 1; index >= 0; index--)
        {
            Destroy(mapSceneListContent.GetChild(index).gameObject);
        }

        const float rowHeight = 64f;
        const float rowSpacing = 12f;
        mapSceneListContent.sizeDelta = new Vector2(
            0f,
            Mathf.Max(
                0f,
                sceneNames.Count * (rowHeight + rowSpacing) - rowSpacing));
        Color selectedColor = Color.Lerp(TabBlue, GhostBlue, 0.48f);
        for (int index = 0; index < sceneNames.Count; index++)
        {
            string sceneName = sceneNames[index];
            bool selected = sceneName == selectedMapSceneName;
            GameObject rowObject = CreateUiObject(
                "Map " + sceneName,
                mapSceneListContent,
                typeof(Image),
                typeof(Button));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition =
                new Vector2(0f, -index * (rowHeight + rowSpacing));
            rowRect.sizeDelta = new Vector2(-4f, rowHeight);

            Image background = rowObject.GetComponent<Image>();
            background.color = selected ? selectedColor : TabBlue;
            CreateBorder(rowRect, GhostBlue, 2f, out _);

            Button button = rowObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : TabBlue;
            colors.highlightedColor =
                Color.Lerp(colors.normalColor, GhostBlue, 0.30f);
            colors.pressedColor =
                Color.Lerp(colors.highlightedColor, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            string selectedScene = sceneName;
            button.onClick.AddListener(
                () => SelectMapScene(selectedScene));

            GameObject labelObject = CreateUiObject(
                "Scene Name",
                rowRect,
                typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(12f, 5f);
            labelRect.offsetMax = new Vector2(-12f, -5f);
            Text label = labelObject.GetComponent<Text>();
            label.text = GetMapSceneDisplayName(sceneName, activeScene);
            label.font = menuFont;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = GhostBlue;
            label.raycastTarget = false;
        }

        bool selectedIsCurrent = selectedMapSceneName == activeScene.name;
        mapAreaNameText.text = GetMapSceneDisplayName(
            selectedMapSceneName,
            activeScene);
        miniMapGraphic.SetCameraMarkerVisible(
            selectedIsCurrent &&
            ZeldaRuntimeRegistry.GetControlledMover() != null);
        if (selectedIsCurrent)
        {
            miniMapGraphic.RebuildFromScene(activeScene);
        }
        else
        {
            RuntimeMiniMapSceneData snapshot =
                GetMiniMapSnapshot(selectedMapSceneName);
            miniMapGraphic.RebuildFromSnapshot(snapshot);
        }
        ApplyTrackedQuestTargetToMap(selectedMapSceneName, miniMapGraphic, false);
        miniMapGraphic.ResetView();
        if (selectedIsCurrent)
        {
            ZeldaFourWayMover controlledMover =
                ZeldaRuntimeRegistry.GetControlledMover();
            if (controlledMover != null)
            {
                miniMapGraphic.SetCameraPosition(
                    controlledMover.transform.position);
                miniMapGraphic.CenterOnWorldPosition(
                    controlledMover.transform.position);
            }
        }
        RebuildMiniMapTransitionLabels(activeScene);
        UpdateMiniMapTransitionLabels();
        RebuildMiniMapPoints();
        UpdateMiniMapPointPositions();
    }

    private void ApplyTrackedQuestTargetToMap(
        string mapSceneName,
        RuntimeMiniMapGraphic mapGraphic,
        bool clampToBorder)
    {
        if (mapGraphic == null || questJournal == null ||
            string.IsNullOrWhiteSpace(questJournal.TrackedEntryId) ||
            questJournal.IsEntryCompleted(questJournal.TrackedEntryId))
        {
            mapGraphic?.SetTrackedQuestTarget(false, Vector2.zero, clampToBorder);
            return;
        }

        Vector2 target;
        bool found = mapGraphic.TryGetQuestTarget(
            questJournal.TrackedEntryId,
            out target);
        mapGraphic.SetTrackedQuestTarget(found, target, clampToBorder);
    }

    private void RebuildMiniMapTransitionLabels(Scene activeScene)
    {
        mapTransitionLabels.Clear();
        if (mapTransitionLabelLayer == null || miniMapGraphic == null)
        {
            return;
        }

        for (int childIndex = mapTransitionLabelLayer.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            Destroy(mapTransitionLabelLayer.GetChild(childIndex).gameObject);
        }

        IReadOnlyList<RuntimeMiniMapTransitionData> transitions =
            miniMapGraphic.Transitions;
        for (int index = 0; index < transitions.Count; index++)
        {
            RuntimeMiniMapTransitionData transition = transitions[index];
            if (!transition.preservesSceneState ||
                string.IsNullOrWhiteSpace(transition.targetSceneName))
            {
                continue;
            }

            string targetSceneName = transition.targetSceneName.Trim();
            GameObject labelObject = CreateUiObject(
                "Destination " + targetSceneName,
                mapTransitionLabelLayer,
                typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = labelRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(190f, 24f);
            Text label = labelObject.GetComponent<Text>();
            label.text = "前往" +
                GetMapSceneDisplayName(targetSceneName, activeScene);
            label.font = menuFont;
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = MapTransitionSkyBlue;
            label.raycastTarget = false;
            mapTransitionLabels.Add(new MiniMapTransitionLabel
            {
                transitionIndex = index,
                rect = labelRect,
                text = label
            });
        }
    }

    private void UpdateMiniMapTransitionLabels()
    {
        if (miniMapGraphic == null)
        {
            return;
        }

        bool showLabels = miniMapGraphic.ZoomPercentage >= 99.9f;
        IReadOnlyList<RuntimeMiniMapTransitionData> transitions =
            miniMapGraphic.Transitions;
        for (int index = 0; index < mapTransitionLabels.Count; index++)
        {
            MiniMapTransitionLabel label = mapTransitionLabels[index];
            bool valid = label != null &&
                         label.text != null &&
                         label.rect != null &&
                         label.transitionIndex >= 0 &&
                         label.transitionIndex < transitions.Count;
            if (!valid)
            {
                continue;
            }

            Vector2 localPosition = Vector2.zero;
            bool visible = showLabels && miniMapGraphic.TryWorldToLocal(
                transitions[label.transitionIndex].position,
                out localPosition);
            label.text.gameObject.SetActive(visible);
            if (visible)
            {
                label.rect.anchoredPosition =
                    localPosition + new Vector2(0f, -25f);
            }
        }
    }

    private void RebuildMiniMapPoints()
    {
        mapPointIcons.Clear();
        hoveredMapPointId = null;
        if (mapPointTooltipRoot != null)
        {
            mapPointTooltipRoot.SetActive(false);
        }
        if (mapPointLayer == null || miniMapGraphic == null)
        {
            return;
        }

        for (int childIndex = mapPointLayer.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            Destroy(mapPointLayer.GetChild(childIndex).gameObject);
        }

        if (mapPointManager == null)
        {
            mapPointManager = MapPointOfInterestManager.GetOrCreate();
        }

        List<MapPointOfInterestRecord> points =
            mapPointManager.GetDiscoveredPoints(selectedMapSceneName);
        for (int index = 0; index < points.Count; index++)
        {
            MapPointOfInterestRecord point = points[index];
            GameObject iconObject = CreateUiObject(
                "Point Of Interest " + point.Id,
                mapPointLayer,
                typeof(CanvasRenderer),
                typeof(RuntimeMiniMapPointIconGraphic),
                typeof(MapPointOfInterestUiInteraction));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = point.Marked
                ? new Vector2(30f, 30f)
                : new Vector2(25f, 25f);

            RuntimeMiniMapPointIconGraphic graphic =
                iconObject.GetComponent<RuntimeMiniMapPointIconGraphic>();
            graphic.color = point.IconColor;
            graphic.raycastTarget = true;
            graphic.Configure(point.IconShape, point.Marked);

            iconObject.GetComponent<MapPointOfInterestUiInteraction>().Configure(
                point.Id,
                ShowMapPointTooltip,
                HideMapPointTooltip,
                point.IconShape == MapPointIconShape.Lever);
            mapPointIcons.Add(new MiniMapPointIcon
            {
                pointId = point.Id,
                rect = iconRect,
                graphic = graphic
            });
        }
    }

    private void UpdateMiniMapPointPositions()
    {
        if (miniMapGraphic == null || mapPointManager == null)
        {
            return;
        }

        for (int index = 0; index < mapPointIcons.Count; index++)
        {
            MiniMapPointIcon icon = mapPointIcons[index];
            MapPointOfInterestRecord point = null;
            bool valid = icon != null &&
                         icon.rect != null &&
                         mapPointManager.TryGetPoint(
                             icon.pointId,
                             out point) &&
                         point.VisibleOnMap &&
                         point.SceneName == selectedMapSceneName;
            Vector2 localPosition = Vector2.zero;
            bool visible = valid && miniMapGraphic.TryWorldToLocal(
                point.WorldPosition,
                out localPosition);
            if (icon != null && icon.rect != null)
            {
                icon.rect.gameObject.SetActive(visible);
                if (visible)
                {
                    icon.rect.anchoredPosition = localPosition;
                }
            }
        }
    }

    private void ShowMapPointTooltip(string pointId, RectTransform iconRect)
    {
        if (mapPointManager == null ||
            mapPointTooltipRoot == null ||
            mapPointTooltipRect == null ||
            mapPointTooltipText == null ||
            iconRect == null ||
            !mapPointManager.TryGetPoint(
                pointId,
                out MapPointOfInterestRecord point))
        {
            return;
        }

        hoveredMapPointId = pointId;
        bool isLever = point.IconShape == MapPointIconShape.Lever;
        mapPointTooltipText.text = point.TooltipText;
        mapPointTooltipRect.sizeDelta = isLever ? new Vector2(76f, 32f) : new Vector2(230f, 84f);
        mapPointTooltipText.alignment = isLever ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
        Vector2 position = iconRect.anchoredPosition +
            (isLever ? new Vector2(55f, 25f) : new Vector2(126f, 36f));
        Rect viewport = mapPointLayer.rect;
        Vector2 half = mapPointTooltipRect.sizeDelta * 0.5f;
        position.x = Mathf.Clamp(
            position.x,
            viewport.xMin + half.x,
            viewport.xMax - half.x);
        position.y = Mathf.Clamp(
            position.y,
            viewport.yMin + half.y,
            viewport.yMax - half.y);
        mapPointTooltipRect.anchoredPosition = position;
        mapPointTooltipRoot.SetActive(true);
        mapPointTooltipRoot.transform.SetAsLastSibling();
    }

    private void HideMapPointTooltip(string pointId)
    {
        if (pointId != hoveredMapPointId)
        {
            return;
        }

        hoveredMapPointId = null;
        if (mapPointTooltipRoot != null)
        {
            mapPointTooltipRoot.SetActive(false);
        }
    }

    private void HandleMapPointsChanged()
    {
        if (!IsOpen || selectedTab != 0)
        {
            return;
        }

        RebuildMiniMapPoints();
        UpdateMiniMapPointPositions();
    }

    private void SelectMapScene(string sceneName)
    {
        selectedMapSceneName = sceneName;
        RefreshMiniMap();
    }

    private string GetMapSceneDisplayName(
        string sceneName,
        Scene activeScene)
    {
        if (sceneName == activeScene.name)
        {
            return AreaDisplayName;
        }

        RuntimeMiniMapSceneData snapshot = GetMiniMapSnapshot(sceneName);
        return snapshot != null &&
               !string.IsNullOrWhiteSpace(snapshot.DisplayName)
            ? snapshot.DisplayName
            : sceneName;
    }

    private static List<string> GetMapSceneNames(string activeSceneName)
    {
        List<string> result = new List<string>();
        if (LevelSceneGroup.TryGetLevelForScene(
                activeSceneName,
                out LevelSceneGroup.Definition configuredLevel))
        {
            for (int index = 0;
                 index < configuredLevel.SceneNames.Count;
                 index++)
            {
                string sceneName = configuredLevel.SceneNames[index];
                if (!string.IsNullOrWhiteSpace(sceneName) &&
                    !ContainsSceneName(result, sceneName))
                {
                    result.Add(sceneName);
                }
            }

            if (!ContainsSceneName(result, activeSceneName))
            {
                result.Insert(0, activeSceneName);
            }
            return result;
        }

        // Backward-compatible fallback for existing scenes that have not yet
        // been assigned a LevelSceneGroup component.
        bool multiSceneLevel = activeSceneName.StartsWith(
            "Level1",
            StringComparison.OrdinalIgnoreCase);
        if (!multiSceneLevel)
        {
            result.Add(activeSceneName);
            return result;
        }

        int buildSceneCount = SceneManager.sceneCountInBuildSettings;
        for (int index = 0; index < buildSceneCount; index++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(index);
            string sceneName = Path.GetFileNameWithoutExtension(path);
            if (sceneName.StartsWith(
                    "Level1",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(sceneName);
            }
        }

        // A scene can be reached through a persistent transition even when a
        // platform-specific build list is incomplete.  Generated snapshots
        // are the authoritative catalog for maps that can be shown while the
        // scene itself is unloaded.
        EnsureMiniMapSnapshotCatalog();
        foreach (KeyValuePair<string, RuntimeMiniMapSceneData> pair in
                 MiniMapSnapshots)
        {
            string sceneName = pair.Key;
            if (sceneName.StartsWith(
                    "Level1",
                    StringComparison.OrdinalIgnoreCase) &&
                !ContainsSceneName(result, sceneName))
            {
                result.Add(sceneName);
            }
        }

        if (!result.Contains(activeSceneName))
        {
            result.Insert(0, activeSceneName);
        }
        return result;
    }

    private static RuntimeMiniMapSceneData GetMiniMapSnapshot(
        string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        EnsureMiniMapSnapshotCatalog();
        RuntimeMiniMapSceneData snapshot;
        if (MiniMapSnapshots.TryGetValue(sceneName.Trim(), out snapshot) &&
            snapshot != null)
        {
            return snapshot;
        }

        // Retry a direct lookup for snapshots added after the catalog was
        // first initialized (useful with domain reload disabled in Editor).
        snapshot = Resources.Load<RuntimeMiniMapSceneData>(
            MiniMapResourceFolder + "/" + sceneName.Trim());
        if (snapshot != null)
        {
            RegisterMiniMapSnapshot(snapshot);
        }
        return snapshot;
    }

    private static void EnsureMiniMapSnapshotCatalog()
    {
        if (miniMapSnapshotsLoaded)
        {
            return;
        }

        miniMapSnapshotsLoaded = true;
        RuntimeMiniMapSceneData[] snapshots =
            Resources.LoadAll<RuntimeMiniMapSceneData>(
                MiniMapResourceFolder);
        for (int index = 0; index < snapshots.Length; index++)
        {
            RegisterMiniMapSnapshot(snapshots[index]);
        }
    }

    private static void RegisterMiniMapSnapshot(
        RuntimeMiniMapSceneData snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        string sceneName = string.IsNullOrWhiteSpace(snapshot.SceneName)
            ? snapshot.name
            : snapshot.SceneName.Trim();
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            MiniMapSnapshots[sceneName] = snapshot;
        }
    }

    private static bool ContainsSceneName(
        List<string> sceneNames,
        string candidate)
    {
        for (int index = 0; index < sceneNames.Count; index++)
        {
            if (string.Equals(
                    sceneNames[index],
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private void BuildQuestJournalPage(RectTransform page)
    {
        GameObject listPanelObject = CreateUiObject(
            "Quest Title List",
            page,
            typeof(Image),
            typeof(ScrollRect));
        RectTransform listPanelRect = listPanelObject.GetComponent<RectTransform>();
        listPanelRect.anchorMin = new Vector2(0f, 0f);
        listPanelRect.anchorMax = new Vector2(0.36f, 1f);
        listPanelRect.offsetMin = Vector2.zero;
        listPanelRect.offsetMax = new Vector2(-12f, 0f);
        Image listPanelImage = listPanelObject.GetComponent<Image>();
        listPanelImage.color = new Color(0.015f, 0.045f, 0.075f, 0.72f);
        listPanelImage.raycastTarget = true;
        CreateBorderEdge(
            page,
            "Quest Column Divider",
            GhostBlue,
            new Vector2(0.36f, 0f),
            new Vector2(0.36f, 1f),
            new Vector2(-1.5f, 0f),
            new Vector2(1.5f, 0f));

        GameObject viewportObject = CreateUiObject(
            "Quest List Viewport",
            listPanelRect,
            typeof(Image),
            typeof(RectMask2D));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-12f, -12f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = CreateUiObject(
            "Quest List Content",
            viewportRect);
        questListContent = contentObject.GetComponent<RectTransform>();
        questListContent.anchorMin = new Vector2(0f, 1f);
        questListContent.anchorMax = new Vector2(1f, 1f);
        questListContent.pivot = new Vector2(0.5f, 1f);
        questListContent.anchoredPosition = Vector2.zero;
        questListContent.sizeDelta = Vector2.zero;

        ScrollRect scrollRect = listPanelObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = questListContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject headerObject = CreateUiObject(
            "Selected Quest Title",
            page,
            typeof(Text));
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.39f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(0f, -78f);
        headerRect.offsetMax = Vector2.zero;
        questTitleText = headerObject.GetComponent<Text>();
        questTitleText.font = menuFont;
        questTitleText.fontSize = 32;
        questTitleText.fontStyle = FontStyle.Normal;
        questTitleText.alignment = TextAnchor.MiddleCenter;
        questTitleText.color = GhostBlue;
        questTitleText.raycastTarget = false;

        GameObject detailsFrameObject = CreateUiObject(
            "Quest Details Frame",
            page,
            typeof(Image));
        RectTransform detailsFrameRect = detailsFrameObject.GetComponent<RectTransform>();
        detailsFrameRect.anchorMin = new Vector2(0.39f, 0f);
        detailsFrameRect.anchorMax = Vector2.one;
        // Reserve a dedicated action row below the description frame.
        detailsFrameRect.offsetMin = new Vector2(18f, 82f);
        detailsFrameRect.offsetMax = new Vector2(-18f, -86f);
        Image detailsFrameImage = detailsFrameObject.GetComponent<Image>();
        detailsFrameImage.color = new Color(0.015f, 0.045f, 0.075f, 0.78f);
        detailsFrameImage.raycastTarget = false;
        CreateBorder(detailsFrameRect, GhostBlue, 3f, out _);

        GameObject detailsObject = CreateUiObject(
            "Selected Quest Details",
            detailsFrameRect,
            typeof(Text));
        RectTransform detailsRect = detailsObject.GetComponent<RectTransform>();
        Stretch(detailsRect);
        detailsRect.offsetMin = new Vector2(28f, 24f);
        detailsRect.offsetMax = new Vector2(-28f, -24f);
        questDetailsText = detailsObject.GetComponent<Text>();
        questDetailsText.font = menuFont;
        questDetailsText.fontSize = 22;
        questDetailsText.fontStyle = FontStyle.Normal;
        questDetailsText.alignment = TextAnchor.UpperLeft;
        questDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        questDetailsText.verticalOverflow = VerticalWrapMode.Truncate;
        questDetailsText.color = GhostBlue;
        questDetailsText.raycastTarget = false;

        GameObject trackButtonObject = CreateUiObject(
            "Track Selected Quest",
            page,
            typeof(Image),
            typeof(Button));
        RectTransform trackButtonRect = trackButtonObject.GetComponent<RectTransform>();
        trackButtonRect.anchorMin = new Vector2(0.57f, 0f);
        trackButtonRect.anchorMax = new Vector2(0.82f, 0f);
        trackButtonRect.pivot = new Vector2(0.5f, 0f);
        trackButtonRect.offsetMin = new Vector2(0f, 14f);
        trackButtonRect.offsetMax = new Vector2(0f, 66f);
        questTrackButtonBackground = trackButtonObject.GetComponent<Image>();
        questTrackButtonBackground.color = TabBlue;
        CreateBorder(trackButtonRect, GhostBlue, 2f, out _);

        questTrackButton = trackButtonObject.GetComponent<Button>();
        questTrackButton.targetGraphic = questTrackButtonBackground;
        questTrackButton.transition = Selectable.Transition.ColorTint;
        questTrackButton.onClick.AddListener(TrackSelectedQuest);

        ColorBlock trackColors = questTrackButton.colors;
        trackColors.normalColor = TabBlue;
        trackColors.highlightedColor = Color.Lerp(TabBlue, GhostBlue, 0.34f);
        trackColors.pressedColor = Color.Lerp(TabBlue, GhostBlue, 0.58f);
        trackColors.selectedColor = trackColors.highlightedColor;
        trackColors.disabledColor = DisabledBlue;
        trackColors.colorMultiplier = 1f;
        trackColors.fadeDuration = 0.08f;
        questTrackButton.colors = trackColors;

        GameObject trackLabelObject = CreateUiObject(
            "Track Button Label",
            trackButtonRect,
            typeof(Text));
        RectTransform trackLabelRect = trackLabelObject.GetComponent<RectTransform>();
        Stretch(trackLabelRect);
        trackLabelRect.offsetMin = new Vector2(8f, 4f);
        trackLabelRect.offsetMax = new Vector2(-8f, -4f);
        questTrackButtonText = trackLabelObject.GetComponent<Text>();
        questTrackButtonText.font = menuFont;
        questTrackButtonText.fontSize = 20;
        questTrackButtonText.alignment = TextAnchor.MiddleCenter;
        questTrackButtonText.color = GhostBlue;
        questTrackButtonText.raycastTarget = false;
    }

    private void RefreshQuestJournal()
    {
        if (questListContent == null || questTitleText == null || questDetailsText == null)
        {
            return;
        }

        if (questJournal == null)
        {
            questJournal = QuestJournalManager.GetOrCreate();
        }

        for (int index = questListContent.childCount - 1; index >= 0; index--)
        {
            Destroy(questListContent.GetChild(index).gameObject);
        }
        questEntryBackgrounds.Clear();

        IReadOnlyList<QuestJournalManager.Entry> entries = questJournal.Entries;
        QuestJournalManager.Entry selectedEntry = null;
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Id == selectedQuestId)
            {
                selectedEntry = entries[index];
                break;
            }
        }

        if (selectedEntry == null && entries.Count > 0)
        {
            selectedEntry = entries[0];
            selectedQuestId = selectedEntry.Id;
        }

        const float rowHeight = 64f;
        const float rowSpacing = 12f;
        questListContent.sizeDelta = new Vector2(
            0f,
            Mathf.Max(0f, entries.Count * (rowHeight + rowSpacing) - rowSpacing));

        Color selectedColor = Color.Lerp(TabBlue, GhostBlue, 0.48f);
        for (int index = 0; index < entries.Count; index++)
        {
            QuestJournalManager.Entry entry = entries[index];
            bool selected = entry.Id == selectedQuestId;
            GameObject rowObject = CreateUiObject(
                "Quest " + entry.Id,
                questListContent,
                typeof(Image),
                typeof(Button));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * (rowHeight + rowSpacing));
            rowRect.sizeDelta = new Vector2(-4f, rowHeight);

            Image background = rowObject.GetComponent<Image>();
            background.color = selected ? selectedColor : TabBlue;
            questEntryBackgrounds.Add(background);
            CreateBorder(rowRect, GhostBlue, 2f, out _);

            Button button = rowObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : TabBlue;
            colors.highlightedColor = Color.Lerp(colors.normalColor, GhostBlue, 0.30f);
            colors.pressedColor = Color.Lerp(colors.highlightedColor, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            string entryId = entry.Id;
            button.onClick.AddListener(() => SelectQuestEntry(entryId));

            GameObject labelObject = CreateUiObject("Title", rowRect, typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(12f, 5f);
            labelRect.offsetMax = new Vector2(-12f, -5f);
            Text label = labelObject.GetComponent<Text>();
            label.text = entry.Title;
            label.font = menuFont;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = GhostBlue;
            label.raycastTarget = false;
        }

        if (selectedEntry == null)
        {
            selectedQuestId = string.Empty;
            questTitleText.text = "暂无任务日志";
            questDetailsText.text = "当前没有可显示的任务信息。";
            UpdateQuestTrackButton(null);
            return;
        }

        questTitleText.text = selectedEntry.Title;
        questDetailsText.text = string.IsNullOrWhiteSpace(selectedEntry.Details)
            ? "暂无详细信息。"
            : selectedEntry.Details;
        UpdateQuestTrackButton(selectedEntry);
    }

    private void SelectQuestEntry(string entryId)
    {
        selectedQuestId = entryId;
        RefreshQuestJournal();
    }

    private void TrackSelectedQuest()
    {
        if (questJournal == null || string.IsNullOrWhiteSpace(selectedQuestId))
            return;

        questJournal.TrackEntry(selectedQuestId);
    }

    private void HandleTrackedQuestChanged()
    {
        if (miniMapGraphic == null)
            return;

        ApplyTrackedQuestTargetToMap(
            selectedMapSceneName,
            miniMapGraphic,
            false);
    }

    private void UpdateQuestTrackButton(QuestJournalManager.Entry selectedEntry)
    {
        if (questTrackButton == null || questTrackButtonText == null ||
            questTrackButtonBackground == null)
        {
            return;
        }

        bool hasEntry = selectedEntry != null;
        bool isCompleted = hasEntry && questJournal != null &&
            questJournal.IsEntryCompleted(selectedEntry.Id);
        bool isTracked = hasEntry && questJournal != null &&
            selectedEntry.Id == questJournal.TrackedEntryId;
        questTrackButton.interactable = hasEntry && !isTracked && !isCompleted;
        questTrackButtonText.text = isCompleted
            ? "已完成"
            : isTracked ? "正在追踪" : "追踪任务";
        questTrackButtonText.color = isTracked || isCompleted ? DisabledText : GhostBlue;
        questTrackButtonBackground.color = isTracked || isCompleted ? DisabledBlue : TabBlue;
        questTrackButton.gameObject.SetActive(hasEntry);
    }

    private void CreateTab(
        RectTransform parent,
        string label,
        float horizontalPosition,
        int index)
    {
        GameObject tabObject = CreateUiObject(
            label,
            parent,
            typeof(Image),
            typeof(Button));
        RectTransform rect = tabObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(horizontalPosition, 238f);
        rect.sizeDelta = new Vector2(240f, 76f);

        Image background = tabObject.GetComponent<Image>();
        background.color = TabBlue;
        tabBackgrounds[index] = background;

        CreateBorder(
            rect,
            GhostBlue,
            3f,
            out Image bottomEdge);
        tabBottomEdges[index] = bottomEdge;

        Button button = tabObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;
        int selectedIndex = index;
        button.onClick.AddListener(() => SetSelectedTab(selectedIndex));
        tabButtons[index] = button;

        GameObject labelObject = CreateUiObject("Label", rect, typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.font = menuFont;
        text.fontSize = 22;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = GhostBlue;
        text.raycastTarget = false;
        tabLabels[index] = text;
    }

    private void SetSelectedTab(int index)
    {
        if (index < 0 || index >= tabButtons.Length ||
            tabButtons[index] == null || !tabButtons[index].interactable)
        {
            return;
        }

        selectedTab = index;
        if (selectedTab == 3) RefreshAvailableAbilities();
        var selectedRect = tabButtons[index].GetComponent<RectTransform>();
        float halfPanelWidth = ((RectTransform)panelTopLeft.parent).rect.width * 0.5f;
        float left = selectedRect.anchoredPosition.x - selectedRect.rect.width * 0.5f;
        float right = selectedRect.anchoredPosition.x + selectedRect.rect.width * 0.5f;
        // Overlap the side strokes by their thickness to form closed corners.
        panelTopLeft.offsetMax = new Vector2(left - halfPanelWidth + 3f, 0f);
        panelTopRight.offsetMin = new Vector2(right + halfPanelWidth - 3f, -3f);
        if (selectedTab == 0 && IsOpen)
        {
            RefreshMiniMap();
        }
        else if (selectedTab == 1 && IsOpen)
        {
            RefreshQuestJournal();
        }
        Color selectedColor = Color.Lerp(TabBlue, GhostBlue, 0.5f);
        Color selectedHover = Color.Lerp(selectedColor, Color.white, 0.16f);
        Color normalHover = Color.Lerp(TabBlue, GhostBlue, 0.32f);

        for (int tabIndex = 0; tabIndex < tabButtons.Length; tabIndex++)
        {
            bool selected = tabIndex == selectedTab;
            bool enabled = true;
            tabBackgrounds[tabIndex].color = enabled
                ? selected ? selectedColor : TabBlue
                : DisabledBlue;
            tabBottomEdges[tabIndex].gameObject.SetActive(!selected);
            // The panel top stroke lies below the tab's bottom edge. Extend the
            // active tab's sides through that stroke instead of leaving a gap.
            var leftEdge = tabButtons[tabIndex].transform.Find("Left").GetComponent<RectTransform>();
            var rightEdge = tabButtons[tabIndex].transform.Find("Right").GetComponent<RectTransform>();
            leftEdge.offsetMin = new Vector2(leftEdge.offsetMin.x, selected ? -3f : 0f);
            rightEdge.offsetMin = new Vector2(rightEdge.offsetMin.x, selected ? -3f : 0f);
            contentPages[tabIndex].SetActive(selected);

            ColorBlock colors = tabButtons[tabIndex].colors;
            colors.normalColor = enabled
                ? selected ? selectedColor : TabBlue
                : DisabledBlue;
            colors.highlightedColor = enabled
                ? selected ? selectedHover : normalHover
                : DisabledBlue;
            colors.pressedColor = Color.Lerp(colors.highlightedColor, GhostBlue, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = DisabledBlue;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            tabButtons[tabIndex].colors = colors;
        }
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
            menuCanvas.planeDistance = uiCamera.nearClipPlane + 0.01f;
        }
        CRTScreenEffect.RegisterCanvas(menuCanvas);
    }

    private static void CreateBorder(
        RectTransform parent,
        Color color,
        float thickness,
        out Image bottomEdge)
    {
        CreateBorderEdge(parent, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero);
        bottomEdge = CreateBorderEdge(
            parent, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness));
        CreateBorderEdge(parent, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f));
        CreateBorderEdge(parent, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-thickness, 0f), Vector2.zero);
    }

    private static Image CreateBorderEdge(
        RectTransform parent,
        string objectName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edgeObject = CreateUiObject(objectName, parent, typeof(Image));
        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = edgeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent,
        params System.Type[] components)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] != typeof(RectTransform))
            {
                result.AddComponent(components[index]);
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

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            CloseMenu();
        }
    }

    private void OnDestroy()
    {
        if (questJournal != null)
        {
            questJournal.JournalChanged -= RefreshQuestJournal;
            questJournal.TrackedQuestChanged -= HandleTrackedQuestChanged;
        }

        if (mapPointManager != null)
        {
            mapPointManager.PointsChanged -= HandleMapPointsChanged;
        }

        if (IsOpen)
        {
            IsOpen = false;
            Time.timeScale = previousTimeScale;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }
    }

    private void OnValidate()
    {
        backgroundDimOpacity = Mathf.Clamp01(backgroundDimOpacity);
    }
}

/// <summary>Paused skill-tree UI. Nodes are driven by PlayerGrowthAttributes.Skills.</summary>
public sealed class CharacterSkillPage : MonoBehaviour
{
    private Font font;
    private Text count, title, description, upgradeText, percentage;
    private RectTransform collectibleIconRect;
    private Image upgradeFill;
    private Button upgradeButton;
    private SkillTreeScroll tree;
    private GameObject titleDivider;
    private string selectedId;
    private bool holding;
    private float holdTime;
    private PlayerGrowthAttributes observedGrowth;
    private readonly Dictionary<string, Image> nodes = new Dictionary<string, Image>();
    private static Color Ink => ZeldaUiPalette.Ghost;
    private static readonly Color Dark = new Color(0.035f, 0.12f, 0.2f, 1f);
    private static readonly Color Locked = new Color(0.055f, 0.085f, 0.12f, 1f);

    public void Build(Font menuFont)
    {
        font = menuFont;
        // Center between the menu's outer left border (-540) and divider (-270).
        RectTransform left = Rect("Skill Details", transform, new Vector2(-405, 0), new Vector2(270, 480));
        Stroke(left, new Vector2(135, 0), new Vector2(2, 480));
        var collectible = Rect("Collectible Icon", left, new Vector2(-82, 202), new Vector2(42, 42));
        collectibleIconRect = collectible;
        collectible.gameObject.AddComponent<CanvasRenderer>();
        collectible.gameObject.AddComponent<SkillPageArt>().collectibleIcon = true;
        count = Label(left, "", new Vector2(20, 202), new Vector2(156, 40), 23);
        count.alignment = TextAnchor.MiddleLeft;
        Stroke(left, new Vector2(0, 164), new Vector2(236, 2));
        title = Label(left, "", new Vector2(0, 124), new Vector2(220, 40), 26);
        var divider = Rect("Skill Title Divider", left, new Vector2(0, 92), new Vector2(206, 2));
        var dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = Ink; dividerImage.raycastTarget = false;
        titleDivider = divider.gameObject;
        description = Label(left, "", new Vector2(0, -46), new Vector2(206, 260), 20);
        description.alignment = TextAnchor.UpperCenter;
        upgradeButton = Button(left, "升级", new Vector2(0, -202), new Vector2(206, 42), null);
        upgradeText = upgradeButton.GetComponentInChildren<Text>();
        upgradeFill = Rect("Hold Progress", upgradeButton.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        upgradeFill.color = new Color(0.28f, 0.75f, 0.86f, 0.65f);
        upgradeFill.raycastTarget = false;
        upgradeFill.rectTransform.anchorMin = new Vector2(0, 0);
        upgradeFill.rectTransform.anchorMax = new Vector2(0, 1);
        upgradeFill.rectTransform.offsetMin = upgradeFill.rectTransform.offsetMax = Vector2.zero;
        upgradeFill.transform.SetSiblingIndex(0);
        var trigger = upgradeButton.gameObject.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(e => {
            if (((PointerEventData)e).button == PointerEventData.InputButton.Left && upgradeButton.interactable)
            { holding = true; holdTime = 0; }
        });
        trigger.triggers.Add(down);
        foreach (var kind in new[] { EventTriggerType.PointerUp, EventTriggerType.PointerExit })
        {
            var entry = new EventTrigger.Entry { eventID = kind };
            entry.callback.AddListener(e => CancelHold());
            trigger.triggers.Add(entry);
        }

        var frame = Rect("Skill Tree Frame", transform, new Vector2(135, 0), new Vector2(750, 480));
        frame.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.045f, 0.075f, 0.97f);
        Border(frame);
        var viewport = Rect("Tree Viewport", frame, new Vector2(-5, 14), new Vector2(730, 436));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        tree = viewport.gameObject.AddComponent<SkillTreeScroll>();
        tree.viewport = viewport;
        tree.content = Rect("Tree Content", viewport, Vector2.zero, new Vector2(1200, 600));
        tree.movementType = ScrollRect.MovementType.Clamped;
        tree.inertia = false;
        foreach (var skill in PlayerGrowthAttributes.Skills)
        {
            if (!string.IsNullOrEmpty(skill.parentId))
            {
                var parent = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == skill.parentId);
                if (parent != null)
                {
                    if (Mathf.Approximately(parent.position.x, skill.position.x))
                    {
                        Vector2 start = parent.position + Vector2.down * 54f;
                        Vector2 end = skill.position + Vector2.up * 49f;
                        Segment(tree.content, start, end);
                        Segment(tree.content, end, end + new Vector2(-6, 8));
                        Segment(tree.content, end, end + new Vector2(6, 8));
                        continue;
                    }
                    Vector2 from = parent.position + new Vector2(54, 0);
                    Vector2 to = skill.position - new Vector2(49, 0);
                    float mid = (from.x + to.x) / 2;
                    Segment(tree.content, from, new Vector2(mid, from.y));
                    Segment(tree.content, new Vector2(mid, from.y), new Vector2(mid, to.y));
                    Segment(tree.content, new Vector2(mid, to.y), to);
                    Segment(tree.content, to, to + new Vector2(-8, 6));
                    Segment(tree.content, to, to + new Vector2(-8, -6));
                }
            }
        }
        foreach (var skill in PlayerGrowthAttributes.Skills)
        {
            string id = skill.id;
            var node = Button(tree.content, "", skill.position, new Vector2(70, 70), () => { selectedId = id; CancelHold(); Refresh(); });
            node.transform.localRotation = Quaternion.Euler(0, 0, 45);
            // Thicken only skill-node borders; other page controls keep their line weight.
            foreach (Transform child in node.transform)
            {
                if (child.name != "Line") continue;
                var edge = (RectTransform)child;
                Vector2 size = edge.sizeDelta;
                edge.sizeDelta = size.x > size.y ? new Vector2(size.x, 3f) : new Vector2(3f, size.y);
            }
            node.interactable = !skill.locked;
            nodes[id] = node.GetComponent<Image>();
            var art = Rect("Icon", node.transform, Vector2.zero, new Vector2(44, 44));
            art.localRotation = Quaternion.Euler(0, 0, -45);
            if (skill.healthBonus > 0 || skill.attackBonus > 0 || skill.energyBonus > 0)
            {
                // The node is rotated 45 degrees: local +Y moves the complete
                // icon group toward the screen's upper-left corner.
                art.anchoredPosition = new Vector2(0f, 7f);
            }
            art.gameObject.AddComponent<CanvasRenderer>();
            var skillArt = art.gameObject.AddComponent<SkillPageArt>();
            skillArt.locked = skill.locked;
            skillArt.soulMark = PlayerGrowthAttributes.IsSoulMarkSkill(skill.id);
            skillArt.domino = PlayerGrowthAttributes.IsDominoSkill(skill.id);
            skillArt.ghostForm = PlayerGrowthAttributes.IsGhostFormSkill(skill.id);
            skillArt.combatExpertise = PlayerGrowthAttributes.IsCombatExpertiseSkill(skill.id);
            skillArt.fearRoar = PlayerGrowthAttributes.IsFearRoarSkill(skill.id);
            skillArt.royalCommand = PlayerGrowthAttributes.IsRoyalCommandSkill(skill.id);
            skillArt.royalCommandLevel = skill.id == "royal_command_2" ? 2 : 1;
            skillArt.fearRoarLevel = skill.id == "fear_roar_3" ? 3 : skill.id == "fear_roar_2" ? 2 : 1;
            skillArt.strengthBonus = skill.attackBonus > 0;
            skillArt.energyBonus = skill.energyBonus > 0;
            skillArt.combatExpertiseLevel = skill.id == "combat_expertise_2" ? 2 : 1;
            skillArt.ghostFormLevel = skill.id == "ghost_form_3" ? 3 : skill.id == "ghost_form_2" ? 2 : 1;
            skillArt.dominoLevel = skill.id == "domino_3" ? 3 : skill.id == "domino_2" ? 2 : 1;
            skillArt.soulMarkLevel = skill.id == "soul_mark_3" ? 3 : skill.id == "soul_mark_2" ? 2 : 1;
            if (skill.healthBonus > 0 || skill.attackBonus > 0 || skill.energyBonus > 0)
            {
                int bonusValue = skill.healthBonus > 0 ? skill.healthBonus : skill.attackBonus > 0 ? skill.attackBonus : skill.energyBonus;
                Text bonus = Label(art, "+" + bonusValue, new Vector2(20, -20), new Vector2(40, 24), 18);
                bonus.fontStyle = FontStyle.Bold;
            }
        }
        TabJournalMenuController.CreateMapZoomButton(frame, "Zoom In", "+", new Vector2(-67f, 18f), () => tree.Zoom(0.25f));
        TabJournalMenuController.CreateMapZoomButton(frame, "Zoom Out", "−", new Vector2(-37f, 18f), () => tree.Zoom(-0.25f));
        percentage = Label(frame, "100%", new Vector2(249, -222), new Vector2(78, 24), 17);
        tree.horizontalScrollbar = Scrollbar(frame, new Vector2(-85, -220), new Vector2(540, 8), false);
        tree.verticalScrollbar = Scrollbar(frame, new Vector2(366, 15), new Vector2(8, 430), true);
        tree.CenterTreeLayout();
        tree.horizontalNormalizedPosition = tree.verticalNormalizedPosition = 0.5f;
        Refresh();
    }

    private void Update()
    {
        var growth = PlayerGrowthAttributes.Instance;
        if (observedGrowth != growth)
        {
            if (observedGrowth != null) observedGrowth.AttributesChanged -= Refresh;
            observedGrowth = growth;
            if (observedGrowth != null) observedGrowth.AttributesChanged += Refresh;
            Refresh();
        }
        if (percentage != null) percentage.text = Mathf.RoundToInt(tree.Scale * 100) + "%";
        if (!holding) return;
        if (!Input.GetMouseButton(0) || !upgradeButton.interactable) { CancelHold(); return; }
        holdTime += Time.unscaledDeltaTime;
        upgradeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(holdTime / 1.2f), 1);
        if (holdTime >= 1.2f)
        {
            growth?.TryUpgradeSkill(selectedId);
            CancelHold();
            Refresh();
        }
    }
    private void OnEnable() { if (count != null) Refresh(); }
    private void OnDisable() { CancelHold(); }
    private void OnDestroy() { if (observedGrowth != null) observedGrowth.AttributesChanged -= Refresh; }
    private void CancelHold()
    {
        holding = false; holdTime = 0;
        if (upgradeFill != null) upgradeFill.rectTransform.anchorMax = new Vector2(0, 1);
    }
    private void Refresh()
    {
        if (count == null) return;
        var growth = PlayerGrowthAttributes.Instance;
        count.text = "：" + (growth != null ? growth.CollectibleCount : 0);
        // Keep icon and number centered as one compact group, including multi-digit counts.
        float countWidth = count.preferredWidth;
        const float iconWidth = 42f, gap = 3f;
        float groupWidth = iconWidth + gap + countWidth;
        collectibleIconRect.anchoredPosition = new Vector2(-groupWidth * 0.5f + iconWidth * 0.5f, 202f);
        count.rectTransform.sizeDelta = new Vector2(countWidth, 40f);
        count.rectTransform.anchoredPosition = new Vector2((iconWidth + gap) * 0.5f, 202f);
        var selected = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == selectedId);
        bool discovered = selected != null && growth != null && growth.IsSkillDiscovered(selected);
        title.text = selected == null ? "" : discovered ? selected.title : "未解锁技能";
        titleDivider.SetActive(selected != null);
        description.text = selected == null ? "" : discovered ? selected.description : "该技能需要附身特定角色才能解锁";
        bool upgraded = selected != null && growth != null && growth.HasSkill(selected.id);
        bool prerequisiteMet = selected != null && growth != null && growth.HasSkillPrerequisite(selected);
        upgradeButton.interactable = selected != null && discovered && !selected.locked && !upgraded && prerequisiteMet && growth.CollectibleCount >= selected.cost;
        upgradeText.text = selected == null ? "请选择技能" : upgraded ? "已升级" : !discovered ? "未解锁" : !prerequisiteMet ? "请先解锁前置技能" : "升级（消耗" + selected.cost + "）";
        upgradeButton.GetComponent<Image>().color = upgradeButton.interactable ? Dark : Locked;
        upgradeText.color = upgradeButton.interactable ? Ink : new Color(0.25f, 0.35f, 0.42f);
        foreach (var skill in PlayerGrowthAttributes.Skills)
        {
            if (!nodes.TryGetValue(skill.id, out var image)) continue;
            bool locked = skill.locked || growth == null || !growth.IsSkillDiscovered(skill);
            var art = image.GetComponentInChildren<SkillPageArt>(true);
            if (art != null && art.locked != locked) { art.locked = locked; art.SetAllDirty(); }
            image.color = locked ? Locked :
                growth != null && growth.HasSkill(skill.id) ? new Color(0.4f, 0.75f, 0.8f) :
                selectedId == skill.id ? new Color(0.18f, 0.4f, 0.5f) : Dark;
        }
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private Text Label(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize)
    {
        var text = Rect("Label", parent, pos, size).gameObject.AddComponent<Text>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.color = Ink;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
    }
    private Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action action)
    {
        var rect = Rect(label, parent, pos, size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Dark;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        if (action != null) button.onClick.AddListener(() => action());
        Border(rect); Label(rect, label, Vector2.zero, size, 18); return button;
    }
    private static void Stroke(Transform parent, Vector2 pos, Vector2 size)
    {
        var image = Rect("Line", parent, pos, size).gameObject.AddComponent<Image>(); image.color = Ink; image.raycastTarget = false;
    }
    private static void Border(RectTransform r)
    {
        Stroke(r, new Vector2(0, r.sizeDelta.y / 2), new Vector2(r.sizeDelta.x, 1));
        Stroke(r, new Vector2(0, -r.sizeDelta.y / 2), new Vector2(r.sizeDelta.x, 1));
        Stroke(r, new Vector2(r.sizeDelta.x / 2, 0), new Vector2(1, r.sizeDelta.y));
        Stroke(r, new Vector2(-r.sizeDelta.x / 2, 0), new Vector2(1, r.sizeDelta.y));
    }
    private static void Segment(Transform parent, Vector2 a, Vector2 b)
    {
        var line = Rect("Branch", parent, (a+b)/2, new Vector2(Vector2.Distance(a,b), 2));
        line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
        var image = line.gameObject.AddComponent<Image>(); image.color = Ink; image.raycastTarget = false;
    }
    private static Scrollbar Scrollbar(Transform parent, Vector2 pos, Vector2 size, bool vertical)
    {
        var track = Rect("Scrollbar", parent, pos, size); track.gameObject.AddComponent<Image>().color = Dark;
        var handle = Rect("Handle", track, Vector2.zero, Vector2.zero);
        var image = handle.gameObject.AddComponent<Image>(); image.color = Ink;
        var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = image;
        bar.direction = vertical ? UnityEngine.UI.Scrollbar.Direction.BottomToTop : UnityEngine.UI.Scrollbar.Direction.LeftToRight;
        TabJournalMenuController.ApplySkillScrollbarStyle(bar);
        return bar;
    }
}

public sealed class SkillTreeScroll : ScrollRect
{
    private const float MinimumScale = 0.5f;
    private Vector2 baseContentSize = new Vector2(1200, 600);
    public float Scale { get; private set; } = 1f;
    public void CenterTreeLayout()
    {
        if (content == null || content.childCount == 0) return;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var corners = new Vector3[4];
        foreach (RectTransform child in content)
        {
            child.GetLocalCorners(corners);
            var matrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale);
            foreach (var corner in corners)
            {
                Vector2 point = matrix.MultiplyPoint3x4(corner);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
        }
        // Include rotated diamond corners and branches, rather than centering on the root node.
        Vector2 center = (minimum + maximum) * 0.5f;
        foreach (RectTransform child in content) child.anchoredPosition -= center;
        baseContentSize = Vector2.Max(baseContentSize, (maximum - minimum) / Scale + Vector2.one * 80f);
        content.sizeDelta = baseContentSize * Scale;
        StopMovement();
        content.anchoredPosition = Vector2.zero;
    }
    public void Zoom(float delta)
    {
        float old = Scale;
        Scale = Mathf.Clamp(Scale + delta, MinimumScale, 3f);
        content.sizeDelta = baseContentSize * Scale;
        // Scale nodes without scaling the scroll bounds twice.
        foreach (RectTransform child in content)
        {
            child.anchoredPosition *= Scale / old;
            child.localScale = Vector3.one * Scale;
        }
        if (Scale <= MinimumScale + 0.0001f)
        {
            StopMovement();
            content.anchoredPosition = Vector2.zero;
        }
    }
    public override void OnScroll(PointerEventData data) { Zoom(data.scrollDelta.y * 0.15f); }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillPageArt : MaskableGraphic
{
    private static readonly Sprite[] soulMarkSprites = new Sprite[3];
    private static readonly Sprite[] ghostFormSprites = new Sprite[3];
    private static readonly string[][] GhostFormVariants = { BuildGhostFormPixels(1), BuildGhostFormPixels(2), BuildGhostFormPixels(3) };
    public int ghostFormLevel = 1;
    private static string[] BuildGhostFormPixels(int level)
    {
        string[] body = {
            "................", "................", "......####......", "....########....",
            "...##########...", "..############..", "..####.##.####..", "..####.##.####..",
            "..############..", "..############..", "...##########...", "....########....",
            "......####......", "................", "................", "................"
        };
        var rows = new string[21];
        for (int y = 0; y < 21; y++)
        {
            var row = new string('.', 21).ToCharArray();
            if (y < body.Length) for (int x = 0; x < 16; x++) row[x] = body[y][x];
            if (y >= 14)
                for (int x = level == 3 ? 14 : 16; x <= 20; x++)
                    if (y == 14 || y == 20 ||
                        (level == 1 ? x == 18 : level == 2 ? x == 17 || x == 19 : x == 15 || x == 17 || x == 19)) row[x] = '#';
            rows[y] = new string(row);
        }
        return rows;
    }
    private static Sprite soulMarkWorldSprite;
    private static Sprite dominoWorldSprite;
    private static readonly Sprite[] dominoSprites = new Sprite[3];
    private static readonly string[][] DominoVariants = { BuildDominoPixels(1), BuildDominoPixels(2), BuildDominoPixels(3) };
    public int dominoLevel = 1;
    private static string[] BuildDominoPixels(int level)
    {
        string[] skull = { "..#####..", ".#######.", "#########", "##..#..##", "##..#..##", ".###.###.", "..#####..", "..#.#.#.." };
        var rows = new string[21];
        for (int y = 0; y < 21; y++)
        {
            var row = new string('.', 21).ToCharArray();
            int cardShift = level == 3 ? -2 : 0; // Leave a clear gap beside the wider numeral III.
            for (int x = 3; x <= 14; x++)
                if (y >= 1 && y <= 18) row[x + cardShift] = '#';
            if (y >= 6 && y < 14)
                for (int x = 0; x < 9; x++)
                    if (skull[y - 6][x] == '#') row[x + 5 + cardShift] = '.';
            if (level > 0 && y >= 14)
                for (int x = level == 3 ? 14 : 16; x <= 20; x++)
                    if (y == 14 || y == 20 ||
                        (level == 1 ? x == 18 : level == 2 ? x == 17 || x == 19 : x == 15 || x == 17 || x == 19)) row[x] = '#';
            rows[y] = new string(row);
        }
        return rows;
    }
    public static Sprite GetDominoWorldSprite()
    {
        // Without the numeral, the card occupies columns 3..14. Center on the card,
        // not on the full canvas that reserves empty space for the numeral on the right.
        if (dominoWorldSprite == null) dominoWorldSprite = CreateSoulMarkSprite(BuildDominoPixels(0), 9f / 21f);
        return dominoWorldSprite;
    }
    private static readonly string[] SoulMarkBasePixels = {
        ".......#.......", ".......#.......", "....#######....",
        "....#..#..#....", "..###..#..###..", "..#.........#..",
        "..#.........#..", "#####..#..#####", "..#.........#..",
        "..#.........#..", "..###..#..###..", "....#..#..#....",
        "....#######....", ".......#.......", ".......#......."
    };
    private static readonly string[][] SoulMarkVariants = { BuildSoulMarkPixels(1), BuildSoulMarkPixels(2), BuildSoulMarkPixels(3) };
    public int soulMarkLevel = 1;
    private static string[] BuildSoulMarkPixels(int level)
    {
        // Shared composition for the tree and equipment slots.
        // Keep the reticle upper-left and the serif Roman I clear at bottom-right.
        var rows = new string[21];
        for (int y = 0; y < rows.Length; y++)
        {
            var row = new string('.', 21).ToCharArray();
            if (y >= 1 && y < 16)
                for (int x = 0; x < 15; x++) row[x + 1] = SoulMarkBasePixels[y - 1][x];
            if (y >= 14 && y <= 20)
                for (int x = level == 3 ? 14 : 16; x <= 20; x++)
                    if (y == 14 || y == 20 ||
                        (level == 1 ? x == 18 : level == 2 ? x == 17 || x == 19 : x == 15 || x == 17 || x == 19)) row[x] = '#';
            rows[y] = new string(row);
        }
        return rows;
    }
    public static Sprite GetAbilitySprite(string id)
    {
        if (PlayerGrowthAttributes.IsRoyalCommandSkill(id)) return ZeldaCharacterData.GetRankedCommandIcon(id == "royal_command_2" ? 2 : 1);
        if (PlayerGrowthAttributes.IsFearRoarSkill(id)) return FearRoarArea.GetRankedIcon(id == "fear_roar_3" ? 3 : id == "fear_roar_2" ? 2 : 1);
        if (PlayerGrowthAttributes.IsCombatExpertiseSkill(id)) return ZeldaHealthHeartsUI.GetRankedCombatExpertiseIcon(id == "combat_expertise_2" ? 2 : 1);
        if (PlayerGrowthAttributes.IsGhostFormSkill(id))
        {
            int rank = id == "ghost_form_3" ? 3 : id == "ghost_form_2" ? 2 : 1;
            if (ghostFormSprites[rank - 1] == null) ghostFormSprites[rank - 1] = CreateSoulMarkSprite(GhostFormVariants[rank - 1]);
            return ghostFormSprites[rank - 1];
        }
        if (PlayerGrowthAttributes.IsDominoSkill(id))
        {
            int rank = id == "domino_3" ? 3 : id == "domino_2" ? 2 : 1;
            if (dominoSprites[rank - 1] == null) dominoSprites[rank - 1] = CreateSoulMarkSprite(DominoVariants[rank - 1]);
            return dominoSprites[rank - 1];
        }
        if (!PlayerGrowthAttributes.IsSoulMarkSkill(id)) return null;
        int level = id == "soul_mark_3" ? 3 : id == "soul_mark_2" ? 2 : 1;
        if (soulMarkSprites[level - 1] != null) return soulMarkSprites[level - 1];
        soulMarkSprites[level - 1] = CreateSoulMarkSprite(SoulMarkVariants[level - 1]);
        return soulMarkSprites[level - 1];
    }
    public static Sprite GetSoulMarkWorldSprite()
    {
        if (soulMarkWorldSprite == null) soulMarkWorldSprite = CreateSoulMarkSprite(SoulMarkBasePixels);
        return soulMarkWorldSprite;
    }
    private static Sprite CreateSoulMarkSprite(string[] SoulMarkPixels, float pivotX = 0.5f)
    {
        int size = SoulMarkPixels.Length;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Soul Mark Skill Icon";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            pixels[(size - 1 - y) * size + x] = SoulMarkPixels[y][x] == '#' ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(pivotX,0.5f), size);
    }
    public bool collectibleIcon, locked, soulMark, domino, ghostForm, combatExpertise;
    public bool strengthBonus;
    public bool energyBonus;
    private static Sprite strengthBonusSprite;
    private static Sprite StrengthBonusSprite
    {
        get
        {
            if (strengthBonusSprite == null) strengthBonusSprite = ZeldaHealthHeartsUI.CreateStrengthIcon(out _);
            return strengthBonusSprite;
        }
    }
    public int combatExpertiseLevel = 1;
    public bool fearRoar;
    public int fearRoarLevel = 1;
    public bool royalCommand;
    public int royalCommandLevel = 1;
    public override Texture mainTexture => !locked && royalCommand ? ZeldaCharacterData.GetRankedCommandIcon(royalCommandLevel).texture : !locked && fearRoar ? FearRoarArea.GetRankedIcon(fearRoarLevel).texture : !locked && strengthBonus ? StrengthBonusSprite.texture : combatExpertise && !locked
        ? ZeldaHealthHeartsUI.GetRankedCombatExpertiseIcon(combatExpertiseLevel).texture : base.mainTexture;
    private RectTransform collectibleOrbit, collectibleGlow;
    private GrowthCollectible.IconAppearance collectibleAppearance;
    private const float CollectibleUiScale = 20f;
    protected override void Awake() { base.Awake(); raycastTarget = false; }
    private void Update()
    {
        if (!collectibleIcon) return;
        if (collectibleOrbit == null) BuildCollectibleIcon();
        collectibleOrbit.Rotate(0f, 0f, collectibleAppearance.rotationSpeed * Time.unscaledDeltaTime);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * collectibleAppearance.pulseSpeed) * collectibleAppearance.pulseAmount;
        collectibleGlow.localScale = Vector3.one * pulse;
    }

    private Image CollectibleImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color tint)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = obj.GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite; image.color = tint; image.raycastTarget = false;
        return image;
    }

    private void BuildCollectibleIcon()
    {
        collectibleAppearance = GrowthCollectible.GetIconAppearance();
        var appearance = collectibleAppearance;
        collectibleGlow = CollectibleImage("Breathing Glow", transform, Vector2.zero,
            Vector2.one * appearance.glowSize * CollectibleUiScale, appearance.glow,
            new Color(1, 1, 1, appearance.glowOpacity)).rectTransform;
        Vector3[] points = GrowthCollectible.IconVertices;
        float width = appearance.lineWidth * CollectibleUiScale;
        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
            {
                Vector2 a = points[i] * CollectibleUiScale, b = points[j] * CollectibleUiScale;
                var edge = CollectibleImage("Tetrahedron Edge", transform, (a + b) * 0.5f,
                    new Vector2(Vector2.Distance(a, b), width), null, Color.white);
                edge.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y, b.x-a.x) * Mathf.Rad2Deg);
                CollectibleImage("Round Cap", transform, a, Vector2.one * width, appearance.circle, Color.white);
                CollectibleImage("Round Cap", transform, b, Vector2.one * width, appearance.circle, Color.white);
            }
        }
        collectibleOrbit = new GameObject("Rotating Orbit Points", typeof(RectTransform)).GetComponent<RectTransform>();
        collectibleOrbit.SetParent(transform, false);
        for (int i = 0; i < appearance.pointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / appearance.pointCount;
            CollectibleImage("Orbit Point", collectibleOrbit,
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * appearance.radius * CollectibleUiScale,
                Vector2.one * appearance.pointSize * CollectibleUiScale, appearance.circle, Color.white);
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (energyBonus && !locked)
        {
            // Same horizontal rectangle proportions and solid fill as the E.G.O HUD.
            Quad(vh, Vector2.zero, new Vector2(30.8f, 17.6f), ZeldaUiPalette.Ghost);
            return;
        }
        if ((combatExpertise || strengthBonus || fearRoar || royalCommand) && !locked)
        {
            Color swordTint = ZeldaUiPalette.Ghost;
            Rect r = strengthBonus ? new Rect(-17f, -17f, 34f, 34f) : rectTransform.rect;
            vh.AddVert(new Vector2(r.xMin, r.yMin), swordTint, new Vector2(0, 0));
            vh.AddVert(new Vector2(r.xMin, r.yMax), swordTint, new Vector2(0, 1));
            vh.AddVert(new Vector2(r.xMax, r.yMax), swordTint, new Vector2(1, 1));
            vh.AddVert(new Vector2(r.xMax, r.yMin), swordTint, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
            return;
        }
        if (collectibleIcon)
        {
            return;
        }
        string[] pixels = locked ? new[] { "..###..", ".#...#.", ".#...#.", "#######", "###.###", "###.###", "#######" } : ghostForm ? GhostFormVariants[Mathf.Clamp(ghostFormLevel, 1, 3) - 1] : domino ? DominoVariants[Mathf.Clamp(dominoLevel, 1, 3) - 1] : soulMark ? SoulMarkVariants[Mathf.Clamp(soulMarkLevel, 1, 3) - 1] :
            new[] { "..#...#..", ".###.###.", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...." };
        float unit = (soulMark || domino || ghostForm) && !locked ? 2.1f : 3.6f;
        Color tint = locked ? new Color(0.25f,0.35f,0.42f) : ZeldaUiPalette.Ghost;
        for (int y=0;y<pixels.Length;y++) for (int x=0;x<pixels[y].Length;x++)
            if (pixels[y][x]=='#') Quad(vh,new Vector2((x-(pixels[y].Length-1)/2f)*unit,((pixels.Length-1)/2f-y)*unit),Vector2.one*unit,tint);
    }
    private static void Quad(VertexHelper vh, Vector2 center, Vector2 size, Color c)
    {
        int start=vh.currentVertCount; Vector2 half=size/2;
        vh.AddVert(center+new Vector2(-half.x,-half.y),c,Vector2.zero);
        vh.AddVert(center+new Vector2(-half.x,half.y),c,Vector2.zero);
        vh.AddVert(center+new Vector2(half.x,half.y),c,Vector2.zero);
        vh.AddVert(center+new Vector2(half.x,-half.y),c,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
    private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
    {
        int n=vh.currentVertCount; Vector2 d=(b-a).normalized; Vector2 p=new Vector2(-d.y,d.x)*width/2;
        vh.AddVert(a-p,c,Vector2.zero); vh.AddVert(a+p,c,Vector2.zero); vh.AddVert(b+p,c,Vector2.zero); vh.AddVert(b-p,c,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
}
