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
    private readonly Image[] tabBackgrounds = new Image[3];
    private readonly Image[] tabBottomEdges = new Image[3];
    private readonly Button[] tabButtons = new Button[3];
    private readonly Text[] tabLabels = new Text[3];
    private readonly GameObject[] contentPages = new GameObject[3];
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
                Camera.main != null &&
                selectedMapSceneName == activeScene.name &&
                ZeldaRuntimeRegistry.GetControlledMover() != null)
            {
                miniMapGraphic.SetCameraPosition(Camera.main.transform.position);
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

        string[] labels =
        {
            "小地图",
            "任务日志",
            "角色能力（尚未开放）"
        };
        float[] horizontalPositions = { -360f, 0f, 360f };
        for (int index = 0; index < labels.Length; index++)
        {
            CreateTab(rootRect, labels[index], horizontalPositions[index], index);
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
        background.color = DisabledBlue;

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
        ColorBlock colors = scrollbar.colors;
        colors.normalColor = GhostBlue;
        colors.highlightedColor = Color.Lerp(GhostBlue, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(GhostBlue, Color.white, 0.38f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        scrollbar.colors = colors;
        return scrollbar;
    }

    private void CreateMapZoomButton(
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
                HideMapPointTooltip);
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
        mapPointTooltipText.text = string.IsNullOrWhiteSpace(point.Description)
            ? point.Title
            : point.Title + "\n" + point.Description;
        Vector2 position = iconRect.anchoredPosition + new Vector2(126f, 36f);
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
        rect.sizeDelta = new Vector2(300f, 76f);

        Image background = tabObject.GetComponent<Image>();
        background.color = index == 2 ? DisabledBlue : TabBlue;
        tabBackgrounds[index] = background;

        CreateBorder(
            rect,
            index == 2 ? DisabledText : GhostBlue,
            3f,
            out Image bottomEdge);
        tabBottomEdges[index] = bottomEdge;

        Button button = tabObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.interactable = index != 2;
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
        text.fontSize = index == 2 ? 18 : 22;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = index == 2 ? DisabledText : GhostBlue;
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
            bool enabled = tabIndex != 2;
            tabBackgrounds[tabIndex].color = enabled
                ? selected ? selectedColor : TabBlue
                : DisabledBlue;
            tabBottomEdges[tabIndex].gameObject.SetActive(!selected);
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
