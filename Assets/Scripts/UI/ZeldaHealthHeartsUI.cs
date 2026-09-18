using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Displays the controlled character's health as pixel hearts.</summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[DefaultExecutionOrder(200)]
public sealed class ZeldaHealthHeartsUI : MonoBehaviour
{
    [System.Serializable]
    public struct TaskPointerDestination
    {
        public Vector2 worldPosition;
        public string label;
    }

    private sealed class SceneTaskState
    {
        public Vector2 worldPosition;
        public string label;
        public bool isVisible;
        public TaskPointerDestination[] destinations;
        public bool hasSavedTarget;
        public Vector2 savedWorldPosition;
        public string savedLabel;
    }

    public enum JournalStyleNotificationKind
    {
        QuestJournalUpdate = 0,
        RequiredKey = 1,
        CannotOpen = 2,
        PasswordError = 3,
        Opened = 4,
        General = 5
    }

    private sealed class JournalStylePopupEntry
    {
        public JournalStyleNotificationKind kind;
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup canvasGroup;
        public Text text;
        public float timer;
    }

    private sealed class QuestObjectiveRow
    {
        public GameObject root;
        public Text text;
        public Image strike;
        public RectTransform strikeRect;
    }

    private const int MaximumHeartCount = 10;
    private const int HeartPixelWidth = 9;
    private const int HeartPixelHeight = 8;
    private const int SwordPixelSize = 9;
    private const int WrenchPixelSize = 9;
    private const int PermissionPixelSize = 11;
    private const int InventorySlotCount = 5;
    private const float TaskContainerScale = 2f;
    private const float EnforcedTaskIndicatorSize = 104f;
    private const int HudSortingOrder = 32760;

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private Vector2 screenOffset = new Vector2(12f, 12f);
    [SerializeField] private Vector2 permissionScreenOffset = new Vector2(12f, 12f);
    [SerializeField] private Vector2 possessionEnergyBarSize = new Vector2(8.8f, 15.4f);
    private const float PossessionEnergyDisplayScale = 1.2f;
    public Vector2 PossessionEnergyDisplaySize =>
        new Vector2(possessionEnergyBarSize.y, possessionEnergyBarSize.x) * PossessionEnergyDisplayScale;
    [SerializeField, Min(0f)] private float possessionEnergySpacing = 3.3f;
    [SerializeField, Min(0f)] private float possessionEnergyPermissionGap = 9f;
    [SerializeField] private Vector2 areaPermissionTopOffset = new Vector2(0f, 12f);
    [SerializeField, Min(1f)] private float areaPermissionDigitScale = 13.2f;
    [SerializeField] private Vector2 inventoryScreenOffset = new Vector2(12f, 12f);
    [SerializeField, Min(1f)] private float inventorySlotSize = 32f;
    [SerializeField, Min(0f)] private float inventorySlotSpacing = 10f;
    [SerializeField] private Vector2 taskScreenOffset = new Vector2(12f, 12f);
    [SerializeField, Min(1f)] private float taskIndicatorSize = 60f;
    [SerializeField, Min(1f)] private float taskTextWidth = 110f;
    [SerializeField, Min(0f)] private float taskSectionSpacing = 6f;
    [SerializeField] private bool showTaskPointer = true;
    [SerializeField] private Vector2 taskPointerWorldPosition = Vector2.zero;
    [SerializeField] private string taskPointerLabel = "目标";
    [SerializeField] private TaskPointerDestination[] taskPointerDestinations;
    [SerializeField, Min(0.1f)] private float newTaskPopupDuration = 2.5f;
    [SerializeField, Min(0.01f)] private float newTaskPopupFadeDuration = 0.5f;
    [SerializeField] private Vector2 newTaskPopupScreenOffset = new Vector2(12f, 12f);
    [Header("Quest Journal Update Popup")]
    [SerializeField, Min(0.1f)] private float journalUpdatePopupDuration = 2.8f;
    [SerializeField, Min(0.01f)] private float journalUpdatePopupFadeDuration = 0.65f;
    [SerializeField] private Vector2 journalUpdatePopupSize = new Vector2(340f, 72f);
    [SerializeField, Min(0f)] private float journalUpdatePopupTopGap = 16f;
    [SerializeField, Min(1f)] private float pixelScale = 2.2f;
    [SerializeField, Min(1f)] private float permissionScaleMultiplier = 4f;
    [SerializeField, Min(0f)] private float heartSpacing = 3.3f;
    [SerializeField] private Color normalHeartColor = new Color(0.9f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color ghostHeartColor = new Color32(40, 130, 210, 255);
    [SerializeField] private Color overHealthColor = new Color(1f, 0.78f, 0.05f, 1f);
    [SerializeField] private Color attackSwordColor = new Color(0.48f, 0.27f, 0.12f, 1f);
    [SerializeField] private Color skillWrenchColor = new Color(0.48f, 0.5f, 0.54f, 1f);
    [SerializeField] private Color permissionColor = new Color32(95, 248, 233, 255);
    [SerializeField] private Color areaPermissionAllowedColor = new Color32(95, 248, 233, 255);
    [SerializeField] private Color areaPermissionWarningColor = new Color(1f, 0.78f, 0.05f, 1f);
    [SerializeField] private Color areaPermissionDeniedColor = new Color(0.9f, 0.08f, 0.08f, 1f);
    [SerializeField] private Font permissionLabelFont;
    [SerializeField, Min(0.01f)] private float healthShakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float healthShakeAmount = 3f;
    [SerializeField, Min(0f)] private float healthShakeSpeed = 70f;
    [SerializeField, Min(0.01f)] private float healthFlashDuration = 0.3f;
    [SerializeField, Min(0f)] private float healthFlashSpeed = 35f;
    [SerializeField] private Color healthFlashColor = new Color32(95, 248, 233, 255);

    private static ZeldaHealthHeartsUI instance;
    private readonly Dictionary<string, SceneTaskState> sceneTaskStates =
        new Dictionary<string, SceneTaskState>();

    private readonly Image[] hearts = new Image[MaximumHeartCount];
    private readonly List<Image> swords = new List<Image>();
    private readonly List<Image> wrenches = new List<Image>();
    private readonly List<Image> possessionEnergyBars = new List<Image>();
    private static Sprite spentPossessionEnergySprite;
    private static Sprite solidEnergySprite;
    public static void ConfigureEnergyIcon(Image icon, int index, ZeldaCharacterData data)
    {
        if (solidEnergySprite == null)
            solidEnergySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
        bool extra = data != null && index >= data.BaseMaxPossessionEnergy && data.CrystalEnergyThirds > 0;
        int normalCurrent = data != null ? data.FinalPossessionEnergy - data.CrystalEnergyThirds / 3 : 0;
        icon.sprite = extra || index < normalCurrent ? solidEnergySprite : GetSpentPossessionEnergyIcon();
        icon.type = extra ? Image.Type.Filled : Image.Type.Simple;
        icon.fillMethod = Image.FillMethod.Horizontal;
        icon.fillOrigin = 0;
        icon.fillAmount = extra ? data.CrystalEnergyThirds / 3f : 1f;
        var particles = icon.GetComponent<TemporaryEnergyParticles>();
        if (extra && particles == null) particles = icon.gameObject.AddComponent<TemporaryEnergyParticles>();
        if (particles != null) particles.SetVisible(extra, icon.color);
    }
    public static Sprite GetSpentPossessionEnergyIcon()
    {
        if (spentPossessionEnergySprite != null) return spentPossessionEnergySprite;
        var texture = new Texture2D(18, 10, TextureFormat.RGBA32, false);
        texture.name = "Spent Possession Energy";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[180];
        for (int y = 0; y < 10; y++) for (int x = 0; x < 18; x++)
            pixels[y * 18 + x] = x == 0 || x == 17 || y == 0 || y == 9
                ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        spentPossessionEnergySprite = Sprite.Create(texture, new Rect(0, 0, 18, 10), new Vector2(0.5f, 0.5f), 18f);
        return spentPossessionEnergySprite;
    }
    private readonly Image[] inventorySlots = new Image[InventorySlotCount];
    private readonly Image[] inventoryItemIcons = new Image[InventorySlotCount];
    private readonly Text[] inventoryQuantityTexts = new Text[InventorySlotCount];
    private readonly Sprite[] permissionSprites = new Sprite[10];
    private readonly Texture2D[] permissionTextures = new Texture2D[10];
    private readonly Sprite[] negativePermissionSprites = new Sprite[2];
    private readonly Texture2D[] negativePermissionTextures = new Texture2D[2];
    private ActiveZeldaCharacterDataSource dataSource;
    private PersistentInventory inventory;
    private QuestJournalManager questJournal;
    private bool questJournalEventsConnected;
    private ZeldaCharacterData displayedCharacter;
    private RectTransform heartContainer;
    private RectTransform swordContainer;
    private RectTransform wrenchContainer;
    private RectTransform possessionEnergyContainer;
    private RectTransform inventoryContainer;
    private Image inventoryFrameImage;
    private readonly List<Image> inventoryFrameEdges = new List<Image>();
    private RectTransform inventorySelectedNameRect;
    private Text inventorySelectedNameText;
    private Text inventoryControlHintText;
    private float inventoryLayoutSpacing;
    private float inventoryLayoutHorizontalPadding;
    private RectTransform taskContainer;
    private RectTransform taskIndicatorRect;
    private RectTransform taskTextAreaRect;
    private Image taskIndicatorImage;
    private Image taskTextAreaImage;
    private RectTransform taskPointerRect;
    private RuntimeMiniMapGraphic taskMiniMapGraphic;
    private RectTransform taskPointerLabelRect;
    private Image taskPointerLabelFrameImage;
    private Text taskPointerLabelText;
    private RectTransform questObjectivePanel;
    private Text questObjectiveTitleText;
    private Image questObjectiveSeparator;
    private readonly List<QuestObjectiveRow> questObjectiveRows =
        new List<QuestObjectiveRow>();
    private GameObject newTaskPopupObject;
    private Text newTaskPopupText;
    private CanvasGroup newTaskPopupCanvasGroup;
    private GameObject journalUpdatePopupObject;
    private CanvasGroup journalUpdatePopupCanvasGroup;
    private Text journalUpdatePopupText;
    private readonly List<JournalStylePopupEntry> journalStylePopupEntries =
        new List<JournalStylePopupEntry>();
    private Vector2 journalStylePopupBasePosition;
    private Image permissionImage;
    private Image permissionSymbolImage;
    private Image areaPermissionImage;
    private Image areaPermissionSeparatorImage;
    private Text permissionLabelText;
    private Text areaPermissionLabelText;
    private Text areaPermissionStatusText;
    private Sprite permissionFrameSprite;
    private Texture2D permissionFrameTexture;
    private Sprite taskTextFrameSprite;
    private Texture2D taskTextFrameTexture;
    private Sprite taskIndicatorFrameSprite;
    private Texture2D taskIndicatorFrameTexture;
    private Sprite taskPointerSprite;
    private Texture2D taskPointerTexture;
    private Sprite heartSprite;
    private Sprite swordSprite;
    private Sprite wrenchSprite;
    private Texture2D heartTexture;
    private Texture2D swordTexture;
    private Texture2D wrenchTexture;
    private bool overHealthDisplay;
    private Vector2 heartContainerBasePosition;
    private int lastObservedHealth;
    private float healthShakeTimer;
    private float healthFlashTimer;
    private float newTaskPopupTimer;
    private bool hasSavedTaskPointerTarget;
    private Vector2 savedTaskPointerWorldPosition;
    private string savedTaskPointerLabel;
    private Color currentHeartColor;
    private Canvas hudCanvas;
    private string currentTaskSceneKey;
    private string taskMiniMapSceneName;
    private string taskMiniMapAreaDisplayName;
    private GameObject trackedQuestWorldMarker;
    private Texture2D trackedQuestWorldMarkerTexture;
    private Sprite trackedQuestWorldMarkerSprite;
    private bool trackedQuestTargetInCurrentScene;
    private Vector2 trackedQuestTargetWorldPosition;

    public Font PermissionLabelFont => permissionLabelFont;
    public static ZeldaHealthHeartsUI Instance => instance;
    public Vector2 TaskPointerWorldPosition => taskPointerWorldPosition;
    public string TaskPointerLabel => taskPointerLabel;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            instance.RegisterSceneTaskConfiguration(gameObject.scene, this);
            Destroy(gameObject);
            return;
        }

        instance = this;
        Scene initialScene = gameObject.scene;
        RegisterSceneTaskConfiguration(initialScene, this);
        currentTaskSceneKey = GetSceneTaskKey(initialScene);
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        hudCanvas = GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ConfigureResponsiveScaling();
        // Attribute windows occupy 31000; the persistent HUD remains above
        // them while pause/document overlays retain the maximum priority.
        EnforceHudRenderPriority();
        SceneManager.sceneLoaded += OnSceneLoaded;

        CreateHeartSprite();
        CreateSwordSprite();
        CreateWrenchSprite();
        CreatePermissionSprites();
        CreateHeartObjects();
        CreateSwordContainer();
        CreateWrenchContainer();
        CreatePossessionEnergyContainer();
        var skillSlots = new GameObject("Skill Slots", typeof(RectTransform), typeof(RuntimeSkillSlots));
        skillSlots.transform.SetParent(transform, false);
        skillSlots.GetComponent<RuntimeSkillSlots>().Build(permissionLabelFont,
            wrenchContainer.anchoredPosition.x);
        CreatePermissionObject();
        CreateAreaPermissionObject();
        CreateInventoryPlaceholder();
        CreateTaskPlaceholder();
        CreateQuestObjectivePanel();
        CreateNewTaskPopup();
        CreateJournalUpdatePopup();
        TryConnectDataSource();
        TryConnectInventory();
        TryConnectQuestJournal();
        ApplyTaskPlaceholderLayout();
        RefreshDisplay();
        RefreshInventoryDisplay();
        UpdateTaskPointer();
        RefreshQuestObjectivePanel();
    }

    private void ConfigureResponsiveScaling()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    private void Start()
    {
        // A final event hookup after every scene object's Awake has run. From
        // this point onward the HUD is updated only by its data events.
        TryConnectDataSource();
        TryConnectInventory();
        TryConnectQuestJournal();
        RefreshQuestObjectivePanel();
        RefreshDisplay();
        RefreshInventoryDisplay();
        UpdateTaskPointer();
        enabled = HasActiveUiAnimation();
    }

    private void Update()
    {
        UpdateNewTaskPopup();
        UpdateJournalUpdatePopup();
        UpdateHealthShake(Time.unscaledDeltaTime);
        UpdateHealthFlash(Time.unscaledDeltaTime);
        enabled = HasActiveUiAnimation();
    }

    private void LateUpdate()
    {
        // The camera continues its smooth follow briefly after movement input
        // ends. Re-project the fixed world target after that camera movement.
        if (trackedQuestTargetInCurrentScene)
        {
            UpdateTrackedQuestWorldMarker(
                true,
                trackedQuestTargetWorldPosition);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DisconnectDataSource();
        DisconnectInventory();
        DisconnectQuestJournal();
        if (instance == this)
        {
            instance = null;
        }

        if (heartSprite != null)
        {
            Destroy(heartSprite);
        }

        if (heartTexture != null)
        {
            Destroy(heartTexture);
        }

        if (swordSprite != null)
        {
            Destroy(swordSprite);
        }

        if (swordTexture != null)
        {
            Destroy(swordTexture);
        }

        if (wrenchSprite != null)
        {
            Destroy(wrenchSprite);
        }

        if (wrenchTexture != null)
        {
            Destroy(wrenchTexture);
        }

        if (trackedQuestWorldMarker != null)
        {
            Destroy(trackedQuestWorldMarker);
        }

        if (trackedQuestWorldMarkerSprite != null)
        {
            Destroy(trackedQuestWorldMarkerSprite);
        }

        if (trackedQuestWorldMarkerTexture != null)
        {
            Destroy(trackedQuestWorldMarkerTexture);
        }

        for (int i = 0; i < permissionSprites.Length; i++)
        {
            if (permissionSprites[i] != null)
            {
                Destroy(permissionSprites[i]);
            }

            if (permissionTextures[i] != null)
            {
                Destroy(permissionTextures[i]);
            }
        }

        for (int i = 0; i < negativePermissionSprites.Length; i++)
        {
            if (negativePermissionSprites[i] != null)
            {
                Destroy(negativePermissionSprites[i]);
            }

            if (negativePermissionTextures[i] != null)
            {
                Destroy(negativePermissionTextures[i]);
            }
        }

        DestroyRuntimeSpriteAndTexture(permissionFrameSprite, permissionFrameTexture);
        DestroyRuntimeSpriteAndTexture(taskTextFrameSprite, taskTextFrameTexture);
        DestroyRuntimeSpriteAndTexture(taskIndicatorFrameSprite, taskIndicatorFrameTexture);
        DestroyRuntimeSpriteAndTexture(taskPointerSprite, taskPointerTexture);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyTaskStateForScene(scene);
        EnforceHudRenderPriority();
        TryConnectDataSource();
        TryConnectInventory();
        TryConnectQuestJournal();
        RefreshDisplay();
        RefreshInventoryDisplay();
        UpdateTaskPointer();
        RefreshQuestObjectivePanel();
    }

    private void EnforceHudRenderPriority()
    {
        if (hudCanvas == null)
        {
            hudCanvas = GetComponent<Canvas>();
        }
        if (hudCanvas == null)
        {
            return;
        }

        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value <= highestSortingLayerValue)
            {
                continue;
            }

            highestSortingLayerValue = sortingLayer.value;
            highestSortingLayerId = sortingLayer.id;
        }

        hudCanvas.overrideSorting = true;
        hudCanvas.sortingLayerID = highestSortingLayerId;
        // Reserve 32767 for modal document/pause canvases. The HUD otherwise
        // stays above vision masks and every world/Blocks renderer even after
        // CRT converts it to ScreenSpaceCamera.
        hudCanvas.sortingOrder = HudSortingOrder;

        Camera uiCamera = CRTScreenEffect.FindActiveUiCamera();
        if (uiCamera != null)
        {
            // Never fall back to a true Overlay canvas after scene travel.
            // Overlay would avoid Blocks, but it is submitted after all camera
            // effects and therefore bypasses CRT. The dedicated UI camera is
            // already deeper than the Blocks camera and solves both concerns.
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = uiCamera;
            hudCanvas.planeDistance =
                Mathf.Max(uiCamera.nearClipPlane + 0.1f, 1f);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            SetLayerRecursively(gameObject, uiLayer);
        }
        CRTScreenEffect.RegisterCanvas(hudCanvas);
    }

    public void RefreshSceneCameraBinding()
    {
        EnforceHudRenderPriority();
        UpdateTaskPointer();
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int i = 0; i < targetTransform.childCount; i++)
        {
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
        }
    }

    public void SetTaskPointerTarget(Vector2 worldPosition)
    {
        taskPointerWorldPosition = worldPosition;
        if (taskPointerDestinations != null)
        {
            for (int i = 0; i < taskPointerDestinations.Length; i++)
            {
                if ((taskPointerDestinations[i].worldPosition - worldPosition).sqrMagnitude <= 0.0001f)
                {
                    taskPointerLabel = taskPointerDestinations[i].label;
                    break;
                }
            }
        }
        showTaskPointer = true;
        SaveCurrentSceneTaskState();
        UpdateTaskPointer();
    }

    public void SetTaskPointerTarget(Vector2 worldPosition, string label)
    {
        taskPointerWorldPosition = worldPosition;
        taskPointerLabel = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        showTaskPointer = true;
        SaveCurrentSceneTaskState();
        UpdateTaskPointer();
    }

    public void SetTaskPointerDestination(int destinationIndex)
    {
        if (taskPointerDestinations == null
            || destinationIndex < 0
            || destinationIndex >= taskPointerDestinations.Length)
        {
            return;
        }

        TaskPointerDestination destination = taskPointerDestinations[destinationIndex];
        SetTaskPointerTarget(destination.worldPosition, destination.label);
    }

    public void SetTaskPointerLabel(string label)
    {
        string nextLabel = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        taskPointerLabel = nextLabel;
        SaveCurrentSceneTaskState();
        UpdateTaskPointer();
    }

    public void PushTaskPointerTarget(Vector2 worldPosition, string label)
    {
        savedTaskPointerWorldPosition = taskPointerWorldPosition;
        savedTaskPointerLabel = taskPointerLabel;
        hasSavedTaskPointerTarget = true;
        SetTaskPointerTarget(worldPosition, label);
    }

    public bool RestorePreviousTaskPointerTarget()
    {
        if (!hasSavedTaskPointerTarget)
            return false;

        Vector2 previousPosition = savedTaskPointerWorldPosition;
        string previousLabel = savedTaskPointerLabel;
        hasSavedTaskPointerTarget = false;
        SetTaskPointerTarget(previousPosition, previousLabel);
        return true;
    }

    public void SetTaskPointerTarget(Transform target)
    {
        if (target != null)
        {
            SetTaskPointerTarget(target.position);
        }
    }

    public void HideTaskPointer()
    {
        showTaskPointer = false;
        SaveCurrentSceneTaskState();
        UpdateTaskPointer();
    }

    public void ShowTaskPointer()
    {
        showTaskPointer = true;
        SaveCurrentSceneTaskState();
        UpdateTaskPointer();
    }

    private void RegisterSceneTaskConfiguration(Scene scene, ZeldaHealthHeartsUI source)
    {
        string sceneKey = GetSceneTaskKey(scene);
        if (string.IsNullOrEmpty(sceneKey) || source == null ||
            sceneTaskStates.ContainsKey(sceneKey))
        {
            return;
        }

        sceneTaskStates.Add(sceneKey, new SceneTaskState
        {
            worldPosition = source.taskPointerWorldPosition,
            label = source.taskPointerLabel,
            isVisible = source.showTaskPointer,
            destinations = CloneTaskDestinations(source.taskPointerDestinations),
            hasSavedTarget = false,
            savedWorldPosition = Vector2.zero,
            savedLabel = string.Empty
        });
    }

    private void ApplyTaskStateForScene(Scene scene)
    {
        string sceneKey = GetSceneTaskKey(scene);
        currentTaskSceneKey = sceneKey;
        if (!sceneTaskStates.TryGetValue(sceneKey, out SceneTaskState state))
        {
            // A scene without its own HUD configuration must not inherit the
            // objective from the previously active scene.
            state = new SceneTaskState
            {
                worldPosition = Vector2.zero,
                label = string.Empty,
                isVisible = false,
                destinations = null,
                hasSavedTarget = false,
                savedWorldPosition = Vector2.zero,
                savedLabel = string.Empty
            };
            sceneTaskStates[sceneKey] = state;
        }

        taskPointerWorldPosition = state.worldPosition;
        taskPointerLabel = state.label;
        showTaskPointer = state.isVisible;
        taskPointerDestinations = CloneTaskDestinations(state.destinations);
        hasSavedTaskPointerTarget = state.hasSavedTarget;
        savedTaskPointerWorldPosition = state.savedWorldPosition;
        savedTaskPointerLabel = state.savedLabel;
    }

    private void SaveCurrentSceneTaskState()
    {
        if (string.IsNullOrEmpty(currentTaskSceneKey))
        {
            currentTaskSceneKey = GetSceneTaskKey(SceneManager.GetActiveScene());
        }

        if (string.IsNullOrEmpty(currentTaskSceneKey))
        {
            return;
        }

        if (!sceneTaskStates.TryGetValue(currentTaskSceneKey, out SceneTaskState state))
        {
            state = new SceneTaskState();
            sceneTaskStates[currentTaskSceneKey] = state;
        }

        state.worldPosition = taskPointerWorldPosition;
        state.label = taskPointerLabel;
        state.isVisible = showTaskPointer;
        state.destinations = CloneTaskDestinations(taskPointerDestinations);
        state.hasSavedTarget = hasSavedTaskPointerTarget;
        state.savedWorldPosition = savedTaskPointerWorldPosition;
        state.savedLabel = savedTaskPointerLabel;
    }

    private static string GetSceneTaskKey(Scene scene)
    {
        if (!scene.IsValid())
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
    }

    private static TaskPointerDestination[] CloneTaskDestinations(
        TaskPointerDestination[] source)
    {
        if (source == null || source.Length == 0)
        {
            return null;
        }

        TaskPointerDestination[] copy =
            new TaskPointerDestination[source.Length];
        System.Array.Copy(source, copy, source.Length);
        return copy;
    }

    private void TryConnectDataSource()
    {
        ActiveZeldaCharacterDataSource nextSource = ActiveZeldaCharacterDataSource.Instance;
        if (nextSource == dataSource)
        {
            return;
        }

        DisconnectDataSource();
        dataSource = nextSource;
        if (dataSource != null)
        {
            dataSource.DataChanged += RefreshDisplay;
            dataSource.ActiveCharacterMoved += UpdateTaskPointer;
        }
    }

    private void DisconnectDataSource()
    {
        if (dataSource != null)
        {
            dataSource.DataChanged -= RefreshDisplay;
            dataSource.ActiveCharacterMoved -= UpdateTaskPointer;
            dataSource = null;
        }
    }

    private void TryConnectInventory()
    {
        PersistentInventory nextInventory = PersistentInventory.Instance;
        if (nextInventory == inventory)
        {
            return;
        }

        DisconnectInventory();
        inventory = nextInventory;
        if (inventory != null)
        {
            inventory.InventoryChanged += RefreshInventoryDisplay;
            inventory.SelectedSlotChanged += OnSelectedInventorySlotChanged;
        }

        RefreshInventoryDisplay();
    }

    private void DisconnectInventory()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= RefreshInventoryDisplay;
            inventory.SelectedSlotChanged -= OnSelectedInventorySlotChanged;
            inventory = null;
        }
    }

    private void TryConnectQuestJournal()
    {
        QuestJournalManager nextJournal = QuestJournalManager.GetOrCreate();
        if (nextJournal == questJournal && questJournalEventsConnected)
        {
            return;
        }

        DisconnectQuestJournal();
        questJournal = nextJournal;
        if (questJournal != null)
        {
            questJournal.JournalEntriesChanged += OnQuestJournalEntriesChanged;
            questJournal.TrackedQuestChanged += OnTrackedQuestChanged;
            questJournalEventsConnected = true;
        }
    }

    private void DisconnectQuestJournal()
    {
        if (questJournal == null)
        {
            return;
        }

        questJournal.JournalEntriesChanged -= OnQuestJournalEntriesChanged;
        questJournal.TrackedQuestChanged -= OnTrackedQuestChanged;
        questJournalEventsConnected = false;
        questJournal = null;
    }

    private void OnQuestJournalEntriesChanged(QuestJournalChangeKind changeKind)
    {
        UpdateTaskPointer();
        RefreshQuestObjectivePanel();
        if (changeKind == QuestJournalChangeKind.Completed)
        {
            ShowJournalStyleNotificationPopup(
                "任务已完成",
                JournalStyleNotificationKind.QuestJournalUpdate);
            return;
        }

        if (changeKind != QuestJournalChangeKind.Added &&
            changeKind != QuestJournalChangeKind.Removed &&
            changeKind != QuestJournalChangeKind.Updated)
        {
            return;
        }

        ShowJournalUpdatePopup();
    }

    private void OnTrackedQuestChanged()
    {
        UpdateTaskPointer();
        RefreshQuestObjectivePanel();
    }

    private void OnSelectedInventorySlotChanged(int selectedIndex)
    {
        RefreshInventoryDisplay();
    }

    private void RefreshInventoryDisplay()
    {
        int selectedIndex = inventory != null ? inventory.SelectedSlotIndex : -1;
        string selectedItemName = string.Empty;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            bool selected = i == selectedIndex;
            inventorySlots[i].rectTransform.localScale = selected
                ? Vector3.one * 1.18f
                : Vector3.one;

            PersistentInventory.Slot slot = inventory != null ? inventory.GetSlot(i) : null;
            bool hasItem = slot != null && !slot.IsEmpty;
            inventoryItemIcons[i].gameObject.SetActive(hasItem);
            inventoryQuantityTexts[i].gameObject.SetActive(hasItem);
            if (!hasItem)
            {
                inventoryItemIcons[i].sprite = null;
                inventoryQuantityTexts[i].text = string.Empty;
                continue;
            }

            PickupItemBase itemPrefab = inventory.ResolveItemPrefab(slot.ItemId);
            inventoryQuantityTexts[i].text = slot.Quantity.ToString();
            if (selected)
            {
                selectedItemName = !string.IsNullOrWhiteSpace(slot.ItemName)
                    ? slot.ItemName
                    : itemPrefab != null ? itemPrefab.ItemName : string.Empty;
            }
            PickupItemVisualBase itemVisual = itemPrefab != null
                ? itemPrefab.ItemVisual
                : null;
            inventoryItemIcons[i].sprite = itemVisual != null
                ? itemVisual.InventoryIcon
                : null;
            inventoryItemIcons[i].color = itemVisual == null
                ? Color.white
                : slot.HasVisualColor
                    ? itemVisual.GetInventoryTint(slot.VisualColor)
                    : itemVisual.InventoryTint;
            inventoryItemIcons[i].gameObject.SetActive(inventoryItemIcons[i].sprite != null);
        }

        RefreshInventoryQuantityColors(GetCurrentInventoryUiColor());

        if (inventorySelectedNameText != null)
        {
            bool showName = selectedIndex >= 0 && !string.IsNullOrWhiteSpace(selectedItemName);
            inventorySelectedNameText.gameObject.SetActive(showName);
            if (showName)
            {
                inventorySelectedNameText.text = selectedItemName;
                float selectedSlotCenter =
                    inventoryLayoutHorizontalPadding +
                    inventorySlotSize * 0.5f +
                    selectedIndex *
                    (inventorySlotSize + inventoryLayoutSpacing);
                inventorySelectedNameRect.anchoredPosition = new Vector2(
                    selectedSlotCenter,
                    inventoryContainer.sizeDelta.y + 5f);
                if (permissionLabelFont != null
                    && permissionLabelFont.material != null
                    && permissionLabelFont.material.mainTexture != null)
                {
                    permissionLabelFont.material.mainTexture.filterMode = FilterMode.Point;
                }
            }
        }
    }

    private Color GetCurrentInventoryUiColor()
    {
        return displayedCharacter != null && displayedCharacter.IsGhostLike
            ? ghostHeartColor
            : ZeldaUiPalette.Primary;
    }

    private void RefreshInventoryQuantityColors(Color inventoryColor)
    {
        for (int i = 0; i < inventoryQuantityTexts.Length; i++)
        {
            Text quantityText = inventoryQuantityTexts[i];
            if (quantityText == null)
            {
                continue;
            }

            PersistentInventory.Slot slot = inventory != null
                ? inventory.GetSlot(i)
                : null;
            if (slot == null || slot.IsEmpty)
            {
                quantityText.text = string.Empty;
                quantityText.gameObject.SetActive(false);
                continue;
            }

            PickupItemBase itemPrefab = inventory.ResolveItemPrefab(slot.ItemId);
            bool reachedStackLimit = itemPrefab != null &&
                slot.Quantity >= Mathf.Max(1, itemPrefab.MaxStackSize);
            quantityText.color = reachedStackLimit
                ? areaPermissionWarningColor
                : inventoryColor;
        }
    }

    private void RefreshDisplay()
    {
        ZeldaCharacterData currentCharacter = dataSource != null
            ? dataSource.ActiveCharacterData
            : null;
        int currentHealth = dataSource != null ? dataSource.CurrentHealth : 0;
        int attackPower = dataSource != null ? dataSource.AttackPower : 0;
        int skillValue = dataSource != null ? dataSource.SkillValue : 0;
        int permissionLevel = dataSource != null ? dataSource.PermissionLevel : 1;
        int areaPermissionLevel = dataSource != null ? dataSource.CurrentAreaPermissionLevel : 0;
        int possessionEnergy = dataSource != null ? dataSource.PossessionEnergy : 0;

        bool characterChanged = currentCharacter != displayedCharacter;
        if (characterChanged)
        {
            displayedCharacter = currentCharacter;
            overHealthDisplay = currentHealth > MaximumHeartCount;
            lastObservedHealth = currentHealth;
        }
        else
        {
            if (currentCharacter != null && currentHealth < lastObservedHealth)
            {
                healthShakeTimer = healthShakeDuration;
                healthFlashTimer = healthFlashDuration;
            }

            lastObservedHealth = currentHealth;
            if (currentHealth > MaximumHeartCount)
            {
                overHealthDisplay = true;
            }
            else if (currentHealth < MaximumHeartCount)
            {
                overHealthDisplay = false;
            }
        }

        int visibleHeartCount = overHealthDisplay
            ? MaximumHeartCount
            : Mathf.Clamp(currentHealth, 0, MaximumHeartCount);
        bool isActualGhost = displayedCharacter is GhostZeldaCharacterData;
        bool isGhostCharacter = displayedCharacter != null && displayedCharacter.IsGhostLike;
        Color heartColor = isGhostCharacter
            ? ghostHeartColor
            : overHealthDisplay ? overHealthColor : normalHeartColor;
        currentHeartColor = heartColor;

        for (int i = 0; i < hearts.Length; i++)
        {
            bool visible = displayedCharacter != null && i < visibleHeartCount;
            if (hearts[i].gameObject.activeSelf != visible)
            {
                hearts[i].gameObject.SetActive(visible);
            }

            hearts[i].color = heartColor;
        }

        int visibleSwordCount = displayedCharacter == null
            ? 0
            : isActualGhost ? 1 : Mathf.Max(0, attackPower);
        EnsureSwordCount(visibleSwordCount);

        for (int i = 0; i < swords.Count; i++)
        {
            bool visible = i < visibleSwordCount;
            if (swords[i].gameObject.activeSelf != visible)
            {
                swords[i].gameObject.SetActive(visible);
            }

            swords[i].color = isGhostCharacter ? ghostHeartColor : attackSwordColor;
        }

        int visibleWrenchCount = displayedCharacter == null
            ? 0
            : isActualGhost ? 1 : Mathf.Max(0, skillValue);
        EnsureWrenchCount(visibleWrenchCount);

        for (int i = 0; i < wrenches.Count; i++)
        {
            bool visible = i < visibleWrenchCount;
            if (wrenches[i].gameObject.activeSelf != visible)
            {
                wrenches[i].gameObject.SetActive(visible);
            }

            wrenches[i].color = isGhostCharacter ? ghostHeartColor : skillWrenchColor;
        }

        bool showPermission = displayedCharacter != null;
        if (permissionImage.gameObject.activeSelf != showPermission)
        {
            permissionImage.gameObject.SetActive(showPermission);
            permissionSymbolImage.gameObject.SetActive(showPermission);
            permissionLabelText.gameObject.SetActive(showPermission);
        }

        permissionSymbolImage.sprite = isActualGhost
            ? permissionSprites[0]
            : GetPermissionSprite(permissionLevel);
        Color currentPermissionColor = isGhostCharacter ? ghostHeartColor : permissionColor;
        permissionImage.color = currentPermissionColor;
        permissionSymbolImage.color = currentPermissionColor;
        permissionLabelText.color = currentPermissionColor;

        if (areaPermissionImage.gameObject.activeSelf != showPermission)
        {
            areaPermissionImage.gameObject.SetActive(showPermission);
            areaPermissionLabelText.gameObject.SetActive(showPermission);
            areaPermissionSeparatorImage.gameObject.SetActive(showPermission);
            areaPermissionStatusText.gameObject.SetActive(showPermission);
        }

        areaPermissionImage.sprite = permissionSprites[Mathf.Clamp(areaPermissionLevel, 0, 9)];
        int permissionDifference = areaPermissionLevel - permissionLevel;
        Color areaStateColor = isGhostCharacter
            ? ghostHeartColor
            : permissionDifference <= 0
                ? areaPermissionAllowedColor
                : permissionDifference == 1
                    ? areaPermissionWarningColor
                    : areaPermissionDeniedColor;
        areaPermissionImage.color = areaStateColor;
        areaPermissionStatusText.text = isGhostCharacter
            ? "不可察觉"
            : permissionDifference <= 0
                ? "合法区域"
                : permissionDifference == 1
                    ? "非法入侵"
                    : "敌对区域";
        areaPermissionStatusText.color = areaStateColor;
        areaPermissionLabelText.color = isGhostCharacter ? ghostHeartColor : permissionColor;
        areaPermissionSeparatorImage.color = isGhostCharacter ? ghostHeartColor : ZeldaUiPalette.Primary;

        Color inventoryColor = isGhostCharacter ? ghostHeartColor : ZeldaUiPalette.Primary;
        ApplyQuestObjectiveColor(inventoryColor);
        if (taskIndicatorImage != null)
        {
            taskIndicatorImage.color = inventoryColor;
        }

        if (taskPointerLabelFrameImage != null)
        {
            taskPointerLabelFrameImage.color = inventoryColor;
        }

        if (taskPointerLabelText != null)
        {
            taskPointerLabelText.color = inventoryColor;
        }

        if (inventorySelectedNameText != null)
        {
            inventorySelectedNameText.color = inventoryColor;
        }
        if (inventoryControlHintText != null)
        {
            inventoryControlHintText.color = inventoryColor;
        }

        for (int edgeIndex = 0;
             edgeIndex < inventoryFrameEdges.Count;
             edgeIndex++)
        {
            inventoryFrameEdges[edgeIndex].color = inventoryColor;
        }

        if (taskTextAreaImage != null)
        {
            taskTextAreaImage.color = inventoryColor;
        }

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i].gameObject.activeSelf != showPermission)
            {
                inventorySlots[i].gameObject.SetActive(showPermission);
            }

            inventorySlots[i].color = inventoryColor;
        }
        RefreshInventoryQuantityColors(inventoryColor);

        int visibleEnergyCount = displayedCharacter != null
            ? Mathf.Max(0, displayedCharacter.BaseMaxPossessionEnergy + (displayedCharacter.CrystalEnergyThirds > 0 ? 1 : 0))
            : 0;
        EnsurePossessionEnergyCount(visibleEnergyCount);
        for (int i = 0; i < possessionEnergyBars.Count; i++)
        {
            bool visible = i < visibleEnergyCount;
            if (possessionEnergyBars[i].gameObject.activeSelf != visible)
            {
                possessionEnergyBars[i].gameObject.SetActive(visible);
            }

            possessionEnergyBars[i].color = isGhostCharacter ? ghostHeartColor : ZeldaUiPalette.Primary;
            ConfigureEnergyIcon(possessionEnergyBars[i], i, displayedCharacter);
        }

        UpdateTaskPointer();
        if (healthShakeTimer > 0f || healthFlashTimer > 0f)
        {
            enabled = true;
        }
    }

    private void CreateHeartObjects()
    {
        GameObject containerObject = new GameObject("Hearts", typeof(RectTransform));
        containerObject.transform.SetParent(transform, false);
        heartContainer = containerObject.GetComponent<RectTransform>();
        heartContainer.anchorMin = new Vector2(0f, 1f);
        heartContainer.anchorMax = new Vector2(0f, 1f);
        heartContainer.pivot = new Vector2(0f, 1f);
        heartContainerBasePosition = new Vector2(screenOffset.x, -screenOffset.y);
        heartContainer.anchoredPosition = heartContainerBasePosition;

        float heartWidth = HeartPixelWidth * pixelScale;
        float heartHeight = HeartPixelHeight * pixelScale;
        heartContainer.sizeDelta = new Vector2(
            MaximumHeartCount * heartWidth + (MaximumHeartCount - 1) * heartSpacing,
            heartHeight);

        for (int i = 0; i < hearts.Length; i++)
        {
            GameObject heartObject = new GameObject(
                $"Heart {i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            heartObject.transform.SetParent(heartContainer, false);

            RectTransform heartRect = heartObject.GetComponent<RectTransform>();
            heartRect.anchorMin = new Vector2(0f, 1f);
            heartRect.anchorMax = new Vector2(0f, 1f);
            heartRect.pivot = new Vector2(0f, 1f);
            heartRect.anchoredPosition = new Vector2(i * (heartWidth + heartSpacing), 0f);
            heartRect.sizeDelta = new Vector2(heartWidth, heartHeight);

            Image image = heartObject.GetComponent<Image>();
            image.sprite = heartSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            hearts[i] = image;
        }
    }

    private void UpdateHealthShake(float deltaTime)
    {
        if (healthShakeTimer <= 0f)
        {
            heartContainer.anchoredPosition = heartContainerBasePosition;
            return;
        }

        healthShakeTimer = Mathf.Max(0f, healthShakeTimer - Mathf.Max(0f, deltaTime));
        float strength = healthShakeDuration > 0f
            ? healthShakeTimer / healthShakeDuration
            : 0f;
        float phase = Time.unscaledTime * healthShakeSpeed;
        Vector2 offset = new Vector2(
            Mathf.Sin(phase),
            Mathf.Cos(phase * 1.37f)) * healthShakeAmount * strength;

        // Whole-pixel movement preserves the crisp pixel-art appearance.
        offset.x = Mathf.Round(offset.x);
        offset.y = Mathf.Round(offset.y);
        heartContainer.anchoredPosition = heartContainerBasePosition + offset;
    }

    private void UpdateHealthFlash(float deltaTime)
    {
        if (healthFlashTimer <= 0f)
        {
            SetHeartColor(currentHeartColor);
            return;
        }

        healthFlashTimer = Mathf.Max(0f, healthFlashTimer - Mathf.Max(0f, deltaTime));
        if (healthFlashTimer <= 0f)
        {
            // The event-driven HUD disables Update as soon as no animation is
            // active, so restore the base color on this final animation frame.
            SetHeartColor(currentHeartColor);
            return;
        }

        bool showHighlight = Mathf.Sin(Time.unscaledTime * healthFlashSpeed) >= 0f;
        SetHeartColor(showHighlight ? healthFlashColor : currentHeartColor);
    }

    private void SetHeartColor(Color color)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].color = color;
        }
    }

    private void CreateSwordContainer()
    {
        GameObject containerObject = new GameObject("Attack Power", typeof(RectTransform));
        containerObject.transform.SetParent(transform, false);
        swordContainer = containerObject.GetComponent<RectTransform>();
        swordContainer.anchorMin = new Vector2(0f, 1f);
        swordContainer.anchorMax = new Vector2(0f, 1f);
        swordContainer.pivot = new Vector2(0f, 1f);

        float heartHeight = HeartPixelHeight * pixelScale;
        swordContainer.anchoredPosition = new Vector2(
            screenOffset.x,
            -screenOffset.y - heartHeight - PossessionEnergyDisplaySize.y - heartSpacing * 2f - 8f);
        swordContainer.sizeDelta = new Vector2(0f, SwordPixelSize * pixelScale);
    }

    private void EnsureSwordCount(int requiredCount)
    {
        float swordSize = SwordPixelSize * pixelScale;
        while (swords.Count < requiredCount)
        {
            int index = swords.Count;
            GameObject swordObject = new GameObject(
                $"Attack Sword {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            swordObject.transform.SetParent(swordContainer, false);

            RectTransform swordRect = swordObject.GetComponent<RectTransform>();
            swordRect.anchorMin = new Vector2(0f, 1f);
            swordRect.anchorMax = new Vector2(0f, 1f);
            swordRect.pivot = new Vector2(0f, 1f);
            swordRect.anchoredPosition = new Vector2(index * (swordSize + heartSpacing), 0f);
            swordRect.sizeDelta = new Vector2(swordSize, swordSize);

            Image image = swordObject.GetComponent<Image>();
            image.sprite = swordSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            swords.Add(image);
        }

        float width = requiredCount > 0
            ? requiredCount * swordSize + (requiredCount - 1) * heartSpacing
            : 0f;
        swordContainer.sizeDelta = new Vector2(width, swordSize);
    }

    private void CreateWrenchContainer()
    {
        GameObject containerObject = new GameObject("Skill Value", typeof(RectTransform));
        containerObject.transform.SetParent(transform, false);
        wrenchContainer = containerObject.GetComponent<RectTransform>();
        wrenchContainer.anchorMin = new Vector2(0f, 1f);
        wrenchContainer.anchorMax = new Vector2(0f, 1f);
        wrenchContainer.pivot = new Vector2(0f, 1f);

        float heartHeight = HeartPixelHeight * pixelScale;
        float swordHeight = SwordPixelSize * pixelScale;
        wrenchContainer.anchoredPosition = new Vector2(
            screenOffset.x,
            -screenOffset.y - heartHeight - PossessionEnergyDisplaySize.y - swordHeight - heartSpacing * 3f - 8f);
        wrenchContainer.sizeDelta = new Vector2(0f, WrenchPixelSize * pixelScale);
    }

    private void EnsureWrenchCount(int requiredCount)
    {
        float wrenchSize = WrenchPixelSize * pixelScale;
        while (wrenches.Count < requiredCount)
        {
            int index = wrenches.Count;
            GameObject wrenchObject = new GameObject(
                $"Skill Wrench {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            wrenchObject.transform.SetParent(wrenchContainer, false);

            RectTransform wrenchRect = wrenchObject.GetComponent<RectTransform>();
            wrenchRect.anchorMin = new Vector2(0f, 1f);
            wrenchRect.anchorMax = new Vector2(0f, 1f);
            wrenchRect.pivot = new Vector2(0f, 1f);
            wrenchRect.anchoredPosition = new Vector2(index * (wrenchSize + heartSpacing), 0f);
            wrenchRect.sizeDelta = new Vector2(wrenchSize, wrenchSize);

            Image image = wrenchObject.GetComponent<Image>();
            image.sprite = wrenchSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            wrenches.Add(image);
        }

        float width = requiredCount > 0
            ? requiredCount * wrenchSize + (requiredCount - 1) * heartSpacing
            : 0f;
        wrenchContainer.sizeDelta = new Vector2(width, wrenchSize);
    }

    private void CreatePermissionObject()
    {
        float frameSize = PermissionPixelSize * pixelScale * permissionScaleMultiplier;
        float symbolPixelScale = pixelScale * Mathf.Max(1f, permissionScaleMultiplier - 1f);
        Vector2 permissionPosition = permissionScreenOffset;

        GameObject permissionObject = new GameObject(
            "Permission Frame",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        permissionObject.transform.SetParent(transform, false);

        RectTransform permissionRect = permissionObject.GetComponent<RectTransform>();
        permissionRect.anchorMin = Vector2.zero;
        permissionRect.anchorMax = Vector2.zero;
        permissionRect.pivot = Vector2.zero;
        permissionRect.anchoredPosition = permissionPosition;
        permissionRect.sizeDelta = Vector2.one * frameSize;

        permissionImage = permissionObject.GetComponent<Image>();
        permissionImage.sprite = permissionFrameSprite;
        permissionImage.raycastTarget = false;
        permissionImage.preserveAspect = true;
        permissionImage.color = permissionColor;

        GameObject symbolObject = new GameObject(
            "Permission Symbol",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        symbolObject.transform.SetParent(transform, false);
        RectTransform symbolRect = symbolObject.GetComponent<RectTransform>();
        symbolRect.anchorMin = Vector2.zero;
        symbolRect.anchorMax = Vector2.zero;
        symbolRect.pivot = new Vector2(0.5f, 0.5f);
        symbolRect.anchoredPosition = permissionPosition + new Vector2(
            frameSize * 0.5f,
            frameSize * 0.66f);
        symbolRect.sizeDelta = new Vector2(9f, 5f) * symbolPixelScale;

        permissionSymbolImage = symbolObject.GetComponent<Image>();
        permissionSymbolImage.raycastTarget = false;
        permissionSymbolImage.preserveAspect = true;
        permissionSymbolImage.color = permissionColor;

        GameObject labelObject = new GameObject(
            "Permission Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.zero;
        labelRect.pivot = Vector2.zero;
        labelRect.anchoredPosition = permissionPosition + new Vector2(0f, frameSize * 0.08f);
        labelRect.sizeDelta = new Vector2(frameSize, frameSize * 0.3f);

        permissionLabelText = labelObject.GetComponent<Text>();
        permissionLabelText.text = "权限等级";
        permissionLabelText.font = permissionLabelFont;
        permissionLabelText.fontSize = 13;
        permissionLabelText.fontStyle = FontStyle.Normal;
        permissionLabelText.alignment = TextAnchor.MiddleCenter;
        permissionLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        permissionLabelText.verticalOverflow = VerticalWrapMode.Overflow;
        permissionLabelText.raycastTarget = false;
        permissionLabelText.color = permissionColor;
    }

    private void CreatePossessionEnergyContainer()
    {
        GameObject containerObject = new GameObject("Possession Energy", typeof(RectTransform));
        containerObject.transform.SetParent(transform, false);
        possessionEnergyContainer = containerObject.GetComponent<RectTransform>();
        possessionEnergyContainer.anchorMin = new Vector2(0f, 1f);
        possessionEnergyContainer.anchorMax = new Vector2(0f, 1f);
        possessionEnergyContainer.pivot = new Vector2(0f, 1f);
        possessionEnergyContainer.anchoredPosition = new Vector2(
            screenOffset.x, -screenOffset.y - HeartPixelHeight * pixelScale - heartSpacing - 4f);
        possessionEnergyContainer.sizeDelta = Vector2.zero;
    }

    private void CreateAreaPermissionObject()
    {
        const float labelHeight = 20f;
        const float labelGap = 4f;
        const float labelVerticalLift = 4f;
        const float statusHeight = 20f;
        const float statusGap = 4f;

        GameObject labelObject = new GameObject(
            "Area Permission Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(
            areaPermissionTopOffset.x,
            -areaPermissionTopOffset.y + labelVerticalLift);
        labelRect.sizeDelta = new Vector2(100f, labelHeight);

        areaPermissionLabelText = labelObject.GetComponent<Text>();
        areaPermissionLabelText.text = "区域等级";
        areaPermissionLabelText.font = permissionLabelFont;
        areaPermissionLabelText.fontSize = 13;
        areaPermissionLabelText.fontStyle = FontStyle.Normal;
        areaPermissionLabelText.alignment = TextAnchor.MiddleCenter;
        areaPermissionLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        areaPermissionLabelText.verticalOverflow = VerticalWrapMode.Overflow;
        areaPermissionLabelText.raycastTarget = false;
        areaPermissionLabelText.color = permissionColor;

        GameObject separatorObject = new GameObject(
            "Area Permission Separator",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        separatorObject.transform.SetParent(transform, false);

        RectTransform separatorRect = separatorObject.GetComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0.5f, 1f);
        separatorRect.anchorMax = new Vector2(0.5f, 1f);
        separatorRect.pivot = new Vector2(0.5f, 0.5f);
        separatorRect.anchoredPosition = new Vector2(
            areaPermissionTopOffset.x,
            -areaPermissionTopOffset.y - labelHeight);
        separatorRect.sizeDelta = new Vector2(80f, 2.2f);

        areaPermissionSeparatorImage = separatorObject.GetComponent<Image>();
        areaPermissionSeparatorImage.raycastTarget = false;
        areaPermissionSeparatorImage.color = ZeldaUiPalette.Primary;

        GameObject areaPermissionObject = new GameObject(
            "Area Permission Level",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        areaPermissionObject.transform.SetParent(transform, false);

        RectTransform areaPermissionRect = areaPermissionObject.GetComponent<RectTransform>();
        areaPermissionRect.anchorMin = new Vector2(0.5f, 1f);
        areaPermissionRect.anchorMax = new Vector2(0.5f, 1f);
        areaPermissionRect.pivot = new Vector2(0.5f, 1f);
        areaPermissionRect.anchoredPosition = new Vector2(
            areaPermissionTopOffset.x,
            -areaPermissionTopOffset.y - labelHeight - labelGap);
        areaPermissionRect.sizeDelta = new Vector2(3f, 5f) * areaPermissionDigitScale;

        areaPermissionImage = areaPermissionObject.GetComponent<Image>();
        areaPermissionImage.raycastTarget = false;
        areaPermissionImage.preserveAspect = true;
        areaPermissionImage.color = areaPermissionAllowedColor;

        GameObject statusObject = new GameObject(
            "Area Permission Status",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        statusObject.transform.SetParent(transform, false);

        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(
            areaPermissionTopOffset.x,
            -areaPermissionTopOffset.y - labelHeight - labelGap -
            5f * areaPermissionDigitScale - statusGap);
        statusRect.sizeDelta = new Vector2(100f, statusHeight);

        areaPermissionStatusText = statusObject.GetComponent<Text>();
        areaPermissionStatusText.text = "合法区域";
        areaPermissionStatusText.font = permissionLabelFont;
        areaPermissionStatusText.fontSize = 13;
        areaPermissionStatusText.fontStyle = FontStyle.Normal;
        areaPermissionStatusText.alignment = TextAnchor.MiddleCenter;
        areaPermissionStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        areaPermissionStatusText.verticalOverflow = VerticalWrapMode.Overflow;
        areaPermissionStatusText.raycastTarget = false;
        areaPermissionStatusText.color = areaPermissionAllowedColor;
    }

    private void CreateInventoryPlaceholder()
    {
        GameObject containerObject = new GameObject(
            "Inventory Placeholder",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        containerObject.transform.SetParent(transform, false);
        inventoryContainer = containerObject.GetComponent<RectTransform>();
        inventoryContainer.anchorMin = new Vector2(0.5f, 0f);
        inventoryContainer.anchorMax = new Vector2(0.5f, 0f);
        inventoryContainer.pivot = new Vector2(0.5f, 0f);
        inventoryContainer.anchoredPosition = new Vector2(
            0f,
            inventoryScreenOffset.y + 26f);
        inventoryLayoutSpacing = Mathf.Max(18f, inventorySlotSpacing);
        inventoryLayoutHorizontalPadding = 18f;
        const float verticalPadding = 12f;
        float contentWidth =
            InventorySlotCount * inventorySlotSize +
            (InventorySlotCount - 1) * inventoryLayoutSpacing;
        inventoryContainer.sizeDelta = new Vector2(
            contentWidth + inventoryLayoutHorizontalPadding * 2f,
            inventorySlotSize + verticalPadding * 2f);
        inventoryFrameImage = containerObject.GetComponent<Image>();
        inventoryFrameImage.sprite = null;
        inventoryFrameImage.color = new Color(0f, 0f, 0f, 0.72f);
        inventoryFrameImage.raycastTarget = false;
        CreateInventoryOuterFrameEdge(
            "Top",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -2f),
            Vector2.zero);
        CreateInventoryOuterFrameEdge(
            "Bottom",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            Vector2.zero,
            new Vector2(0f, 2f));
        CreateInventoryOuterFrameEdge(
            "Left",
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(2f, 0f));
        CreateInventoryOuterFrameEdge(
            "Right",
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-2f, 0f),
            Vector2.zero);

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            GameObject slotObject = new GameObject(
                $"Inventory Slot {i + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            slotObject.transform.SetParent(inventoryContainer, false);

            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0f, 0f);
            slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2(
                inventoryLayoutHorizontalPadding +
                inventorySlotSize * 0.5f +
                i * (inventorySlotSize + inventoryLayoutSpacing),
                verticalPadding + inventorySlotSize * 0.5f);
            slotRect.sizeDelta = Vector2.one * inventorySlotSize;

            Image image = slotObject.GetComponent<Image>();
            image.sprite = permissionFrameSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = ZeldaUiPalette.Primary;
            inventorySlots[i] = image;

            GameObject iconObject = new GameObject(
                "Item Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(slotObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = Vector2.one * inventorySlotSize * 0.68f;

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            inventoryItemIcons[i] = iconImage;

            GameObject quantityObject = new GameObject(
                "Item Quantity",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            quantityObject.transform.SetParent(slotObject.transform, false);
            RectTransform quantityRect =
                quantityObject.GetComponent<RectTransform>();
            quantityRect.anchorMin = Vector2.zero;
            quantityRect.anchorMax = Vector2.one;
            quantityRect.pivot = new Vector2(1f, 0f);
            quantityRect.offsetMin = new Vector2(2f, 1f);
            quantityRect.offsetMax = new Vector2(-3f, -2f);

            Text quantityText = quantityObject.GetComponent<Text>();
            quantityText.font = permissionLabelFont;
            quantityText.fontSize = 13;
            quantityText.fontStyle = FontStyle.Normal;
            quantityText.alignment = TextAnchor.LowerRight;
            quantityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            quantityText.verticalOverflow = VerticalWrapMode.Overflow;
            quantityText.color = ZeldaUiPalette.Primary;
            quantityText.raycastTarget = false;
            inventoryQuantityTexts[i] = quantityText;
            quantityObject.SetActive(false);
        }

        GameObject selectedNameObject = new GameObject(
            "Selected Item Name",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        selectedNameObject.transform.SetParent(inventoryContainer, false);
        inventorySelectedNameRect = selectedNameObject.GetComponent<RectTransform>();
        inventorySelectedNameRect.anchorMin = new Vector2(0f, 0f);
        inventorySelectedNameRect.anchorMax = new Vector2(0f, 0f);
        inventorySelectedNameRect.pivot = new Vector2(0.5f, 0f);
        inventorySelectedNameRect.sizeDelta = new Vector2(150f, 24f);

        inventorySelectedNameText = selectedNameObject.GetComponent<Text>();
        inventorySelectedNameText.font = permissionLabelFont;
        inventorySelectedNameText.fontSize = 12;
        inventorySelectedNameText.fontStyle = FontStyle.Normal;
        inventorySelectedNameText.alignment = TextAnchor.MiddleCenter;
        inventorySelectedNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        inventorySelectedNameText.verticalOverflow = VerticalWrapMode.Truncate;
        inventorySelectedNameText.color = ZeldaUiPalette.Primary;
        inventorySelectedNameText.raycastTarget = false;
        selectedNameObject.SetActive(false);

        GameObject hintObject = new GameObject(
            "Inventory Control Hint",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        hintObject.transform.SetParent(inventoryContainer, false);
        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 1f);
        hintRect.anchoredPosition = new Vector2(0f, -5f);
        hintRect.sizeDelta = new Vector2(330f, 18f);
        inventoryControlHintText = hintObject.GetComponent<Text>();
        inventoryControlHintText.text =
            "[Q]丢弃道具，[R]使用道具，[C]道具说明";
        inventoryControlHintText.font = permissionLabelFont;
        inventoryControlHintText.fontSize = 11;
        inventoryControlHintText.fontStyle = FontStyle.Normal;
        inventoryControlHintText.alignment = TextAnchor.MiddleCenter;
        inventoryControlHintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        inventoryControlHintText.verticalOverflow = VerticalWrapMode.Truncate;
        inventoryControlHintText.color = ZeldaUiPalette.Primary;
        inventoryControlHintText.raycastTarget = false;
    }

    private void CreateInventoryOuterFrameEdge(
        string edgeName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject edgeObject = new GameObject(
            "Inventory Outer Frame " + edgeName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        edgeObject.transform.SetParent(inventoryContainer, false);
        RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
        edgeRect.anchorMin = anchorMin;
        edgeRect.anchorMax = anchorMax;
        edgeRect.offsetMin = offsetMin;
        edgeRect.offsetMax = offsetMax;
        Image edgeImage = edgeObject.GetComponent<Image>();
        edgeImage.color = ZeldaUiPalette.Primary;
        edgeImage.raycastTarget = false;
        inventoryFrameEdges.Add(edgeImage);
    }

    private void CreateTaskPlaceholder()
    {
        GameObject containerObject = new GameObject("Task Placeholder", typeof(RectTransform));
        containerObject.transform.SetParent(transform, false);
        taskContainer = containerObject.GetComponent<RectTransform>();

        GameObject indicatorObject = new GameObject(
            "Task Indicator Slot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        indicatorObject.transform.SetParent(taskContainer, false);
        taskIndicatorRect = indicatorObject.GetComponent<RectTransform>();
        taskIndicatorImage = indicatorObject.GetComponent<Image>();
        taskIndicatorImage.sprite = taskIndicatorFrameSprite;
        taskIndicatorImage.color = ZeldaUiPalette.Primary;
        taskIndicatorImage.preserveAspect = true;
        taskIndicatorImage.raycastTarget = false;

        GameObject miniMapViewportObject = new GameObject(
            "Task Mini Map Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));
        miniMapViewportObject.transform.SetParent(taskIndicatorRect, false);
        RectTransform miniMapViewportRect =
            miniMapViewportObject.GetComponent<RectTransform>();
        miniMapViewportRect.anchorMin = Vector2.zero;
        miniMapViewportRect.anchorMax = Vector2.one;
        miniMapViewportRect.offsetMin = new Vector2(4f, 4f);
        miniMapViewportRect.offsetMax = new Vector2(-4f, -4f);
        Image miniMapViewportImage =
            miniMapViewportObject.GetComponent<Image>();
        miniMapViewportImage.color = new Color(0f, 0f, 0f, 0.64f);
        miniMapViewportImage.raycastTarget = false;

        GameObject pointerObject = new GameObject(
            "Task Real-Time Mini Map",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RuntimeMiniMapGraphic));
        pointerObject.transform.SetParent(miniMapViewportRect, false);
        taskPointerRect = pointerObject.GetComponent<RectTransform>();
        taskPointerRect.anchorMin = Vector2.zero;
        taskPointerRect.anchorMax = Vector2.one;
        taskPointerRect.pivot = new Vector2(0.5f, 0.5f);
        taskPointerRect.offsetMin = Vector2.zero;
        taskPointerRect.offsetMax = Vector2.zero;
        taskMiniMapGraphic =
            pointerObject.GetComponent<RuntimeMiniMapGraphic>();
        taskMiniMapGraphic.raycastTarget = false;
        taskMiniMapGraphic.SetIconScale(0.45f);
        taskMiniMapGraphic.SetZoomToMaximum();

        GameObject labelFrameObject = new GameObject(
            "Task Pointer Label Frame",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        labelFrameObject.transform.SetParent(taskContainer, false);
        taskPointerLabelRect = labelFrameObject.GetComponent<RectTransform>();
        taskPointerLabelFrameImage = labelFrameObject.GetComponent<Image>();
        taskPointerLabelFrameImage.sprite = taskTextFrameSprite;
        taskPointerLabelFrameImage.color = ZeldaUiPalette.Primary;
        taskPointerLabelFrameImage.raycastTarget = false;

        GameObject labelTextObject = new GameObject(
            "Task Pointer Label Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelTextObject.transform.SetParent(taskPointerLabelRect, false);
        RectTransform labelTextRect = labelTextObject.GetComponent<RectTransform>();
        labelTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelTextRect.pivot = new Vector2(0.5f, 0.5f);
        labelTextRect.anchoredPosition = Vector2.zero;
        labelTextRect.sizeDelta = new Vector2(112f, 28f);
        labelTextRect.localScale = Vector3.one * 0.5f;
        taskPointerLabelText = labelTextObject.GetComponent<Text>();
        taskPointerLabelText.font = permissionLabelFont;
        taskPointerLabelText.fontSize = 16;
        taskPointerLabelText.fontStyle = FontStyle.Normal;
        taskPointerLabelText.alignment = TextAnchor.MiddleCenter;
        taskPointerLabelText.alignByGeometry = true;
        taskPointerLabelText.color = ZeldaUiPalette.Primary;
        taskPointerLabelText.horizontalOverflow = HorizontalWrapMode.Wrap;
        taskPointerLabelText.verticalOverflow = VerticalWrapMode.Truncate;
        taskPointerLabelText.resizeTextForBestFit = false;
        taskPointerLabelText.raycastTarget = false;

        ApplyTaskPlaceholderLayout();
    }

    private void CreateQuestObjectivePanel()
    {
        GameObject panelObject = new GameObject(
            "Tracked Quest Objectives",
            typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);
        questObjectivePanel = panelObject.GetComponent<RectTransform>();
        questObjectivePanel.anchorMin = new Vector2(1f, 1f);
        questObjectivePanel.anchorMax = new Vector2(1f, 1f);
        questObjectivePanel.pivot = new Vector2(1f, 1f);
        // The real-time mini-map is 104 units at a 2x container scale, giving
        // it a 208-pixel screen width. Keep this panel slightly narrower.
        questObjectivePanel.anchoredPosition = new Vector2(-12f, -264f);
        questObjectivePanel.sizeDelta = new Vector2(200f, 142f);

        GameObject titleObject = new GameObject(
            "Quest Name",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        titleObject.transform.SetParent(questObjectivePanel, false);
        questObjectiveTitleText = titleObject.GetComponent<Text>();
        questObjectiveTitleText.font = permissionLabelFont;
        questObjectiveTitleText.fontSize = 17;
        questObjectiveTitleText.alignment = TextAnchor.MiddleRight;
        questObjectiveTitleText.color = ZeldaUiPalette.Primary;
        questObjectiveTitleText.raycastTarget = false;
        RectTransform titleRect = questObjectiveTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0f, 27f);

        GameObject separatorObject = new GameObject(
            "Quest Name Separator",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        separatorObject.transform.SetParent(questObjectivePanel, false);
        questObjectiveSeparator = separatorObject.GetComponent<Image>();
        questObjectiveSeparator.color = ZeldaUiPalette.Primary;
        questObjectiveSeparator.raycastTarget = false;
        RectTransform separatorRect = separatorObject.GetComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0f, 1f);
        separatorRect.anchorMax = new Vector2(1f, 1f);
        separatorRect.pivot = new Vector2(1f, 1f);
        separatorRect.anchoredPosition = new Vector2(0f, -30f);
        separatorRect.sizeDelta = new Vector2(0f, 2f);

        RefreshQuestObjectivePanel();
    }

    private void RefreshQuestObjectivePanel()
    {
        if (questObjectivePanel == null)
            return;

        if (questJournal == null)
        {
            questJournal = QuestJournalManager.GetOrCreate();
        }

        string trackedId = questJournal != null
            ? questJournal.TrackedEntryId
            : string.Empty;
        QuestJournalManager.Entry entry = null;
        bool hasEntry = questJournal != null &&
            questJournal.TryGetEntry(trackedId, out entry);
        questObjectivePanel.gameObject.SetActive(hasEntry);
        if (!hasEntry)
            return;

        questObjectiveTitleText.text = entry.Title;
        Color activeColor = displayedCharacter != null && displayedCharacter.IsGhostLike
            ? ghostHeartColor
            : ZeldaUiPalette.Primary;
        ApplyQuestObjectiveColor(activeColor);
        List<string> objectiveTexts = new List<string>();
        List<bool> objectiveCompletion = new List<bool>();
        BuildQuestObjectiveState(
            trackedId,
            questJournal,
            objectiveTexts,
            objectiveCompletion);

        EnsureQuestObjectiveRows(objectiveTexts.Count);
        for (int index = 0; index < questObjectiveRows.Count; index++)
        {
            QuestObjectiveRow row = questObjectiveRows[index];
            bool visible = index < objectiveTexts.Count;
            row.root.SetActive(visible);
            if (!visible)
                continue;

            bool completed = objectiveCompletion[index];
            row.text.text = objectiveTexts[index];
            row.text.color = completed
                ? GetCompletedQuestObjectiveColor(activeColor)
                : activeColor;
            row.strike.gameObject.SetActive(completed);
            row.strike.color = row.text.color;
            row.strikeRect.sizeDelta = new Vector2(
                Mathf.Min(192f, row.text.preferredWidth),
                1.5f);
        }

        if (permissionLabelFont != null)
        {
            string characters = entry.Title;
            for (int index = 0; index < objectiveTexts.Count; index++)
            {
                characters += objectiveTexts[index];
            }
            permissionLabelFont.RequestCharactersInTexture(
                characters,
                17,
                FontStyle.Normal);
        }
    }

    private void ApplyQuestObjectiveColor(Color activeColor)
    {
        if (questObjectiveTitleText != null)
        {
            questObjectiveTitleText.color = activeColor;
        }
        if (questObjectiveSeparator != null)
        {
            questObjectiveSeparator.color = activeColor;
        }

        Color completedColor = GetCompletedQuestObjectiveColor(activeColor);
        for (int index = 0; index < questObjectiveRows.Count; index++)
        {
            QuestObjectiveRow row = questObjectiveRows[index];
            bool completed = row.strike != null && row.strike.gameObject.activeSelf;
            row.text.color = completed ? completedColor : activeColor;
            row.strike.color = completedColor;
        }
    }

    private static Color GetCompletedQuestObjectiveColor(Color activeColor)
    {
        return new Color(
            activeColor.r * 0.42f,
            activeColor.g * 0.42f,
            activeColor.b * 0.42f,
            0.78f);
    }

    private static void BuildQuestObjectiveState(
        string questId,
        QuestJournalManager journal,
        List<string> texts,
        List<bool> completion)
    {
        bool questComplete = journal.IsEntryCompleted(questId);
        switch (questId)
        {
            case "level0.escape_prison":
                AddObjective(texts, completion, "设法从大门逃出监狱", questComplete);
                break;
            case "level0.obtain_gate_key":
                AddObjective(texts, completion, "前往典狱长住所获取监狱大门钥匙", questComplete);
                break;
            case "level1.escape_castle":
                AddObjective(texts, completion, "设法逃出城堡", questComplete);
                break;
            case "level1.craft_super_bomb":
                AddObjective(texts, completion, "找到炸弹引信",
                    journal.EntryDetailsContain(questId, "已找到引信"));
                AddObjective(texts, completion, "找到超级炸弹外壳",
                    journal.EntryDetailsContain(questId, "已找到超级炸弹外壳"));
                AddObjective(texts, completion, "找到魔力火药",
                    journal.EntryDetailsContain(questId, "已找到魔力火药"));
                AddObjective(texts, completion, "在工作台合成超级炸弹", questComplete);
                break;
            case "level1.open_monster_cage":
                AddObjective(texts, completion, "找到牢笼密码",
                    journal.EntryDetailsContain(questId, "0451"));
                AddObjective(texts, completion, "找到怪物牢笼钥匙",
                    journal.EntryDetailsContain(questId, "已找到怪物牢笼钥匙"));
                AddObjective(texts, completion, "打开怪物牢笼", questComplete);
                break;
            case "level1.find_castle_gate_key":
                AddObjective(texts, completion, "设法找到城堡大门钥匙", questComplete);
                break;
        }
    }

    private static void AddObjective(
        List<string> texts,
        List<bool> completion,
        string text,
        bool isComplete)
    {
        texts.Add(text);
        completion.Add(isComplete);
    }

    private void EnsureQuestObjectiveRows(int count)
    {
        while (questObjectiveRows.Count < count)
        {
            int index = questObjectiveRows.Count;
            GameObject rowObject = new GameObject(
                "Quest Objective " + (index + 1),
                typeof(RectTransform));
            rowObject.transform.SetParent(questObjectivePanel, false);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -38f - index * 23f);
            rowRect.sizeDelta = new Vector2(0f, 20f);

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(rowRect, false);
            Text text = textObject.GetComponent<Text>();
            text.font = permissionLabelFont;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleRight;
            text.color = ZeldaUiPalette.Primary;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            GameObject strikeObject = new GameObject(
                "Completed Strike",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            strikeObject.transform.SetParent(textRect, false);
            Image strike = strikeObject.GetComponent<Image>();
            strike.raycastTarget = false;
            RectTransform strikeRect = strikeObject.GetComponent<RectTransform>();
            strikeRect.anchorMin = new Vector2(1f, 0.5f);
            strikeRect.anchorMax = new Vector2(1f, 0.5f);
            strikeRect.pivot = new Vector2(1f, 0.5f);
            strikeRect.anchoredPosition = Vector2.zero;
            strikeRect.sizeDelta = new Vector2(0f, 1.5f);

            questObjectiveRows.Add(new QuestObjectiveRow
            {
                root = rowObject,
                text = text,
                strike = strike,
                strikeRect = strikeRect
            });
        }
    }

    private void CreateJournalUpdatePopup()
    {
        journalUpdatePopupObject = new GameObject(
            "Quest Journal Update Popup",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        journalUpdatePopupObject.transform.SetParent(transform, false);

        journalUpdatePopupCanvasGroup =
            journalUpdatePopupObject.GetComponent<CanvasGroup>();
        journalUpdatePopupCanvasGroup.alpha = 1f;
        journalUpdatePopupCanvasGroup.interactable = false;
        journalUpdatePopupCanvasGroup.blocksRaycasts = false;

        RectTransform popupRect =
            journalUpdatePopupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 1f);
        popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 1f);
        float areaPermissionBottomOffset =
            areaPermissionTopOffset.y +
            20f + 4f + 5f * areaPermissionDigitScale + 4f + 20f;
        popupRect.anchoredPosition = new Vector2(
            areaPermissionTopOffset.x,
            -areaPermissionBottomOffset - journalUpdatePopupTopGap);
        journalStylePopupBasePosition = popupRect.anchoredPosition;
        popupRect.sizeDelta = new Vector2(
            Mathf.Max(240f, journalUpdatePopupSize.x),
            Mathf.Max(56f, journalUpdatePopupSize.y));

        Image popupBackground = journalUpdatePopupObject.GetComponent<Image>();
        popupBackground.color = new Color(0f, 0f, 0f, 0.96f);
        popupBackground.raycastTarget = false;

        GameObject borderObject = new GameObject(
            "Journal Popup Border",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        borderObject.transform.SetParent(popupRect, false);
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImage = borderObject.GetComponent<Image>();
        borderImage.sprite = taskTextFrameSprite;
        borderImage.color = ZeldaUiPalette.Primary;
        borderImage.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Journal Popup Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(popupRect, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 10f);
        textRect.offsetMax = new Vector2(-18f, -10f);
        journalUpdatePopupText = textObject.GetComponent<Text>();
        journalUpdatePopupText.text = "任务日志已更新";
        journalUpdatePopupText.font = permissionLabelFont;
        journalUpdatePopupText.fontSize = 20;
        journalUpdatePopupText.fontStyle = FontStyle.Normal;
        journalUpdatePopupText.alignment = TextAnchor.MiddleCenter;
        journalUpdatePopupText.color = ZeldaUiPalette.Primary;
        journalUpdatePopupText.horizontalOverflow = HorizontalWrapMode.Overflow;
        journalUpdatePopupText.verticalOverflow = VerticalWrapMode.Truncate;
        journalUpdatePopupText.raycastTarget = false;

        journalUpdatePopupObject.SetActive(false);
        journalStylePopupEntries.Add(new JournalStylePopupEntry
        {
            kind = JournalStyleNotificationKind.QuestJournalUpdate,
            root = journalUpdatePopupObject,
            rect = popupRect,
            canvasGroup = journalUpdatePopupCanvasGroup,
            text = journalUpdatePopupText,
            timer = 0f
        });
    }

    private void ShowJournalUpdatePopup()
    {
        ShowJournalStyleNotificationPopup(
            "任务日志已更新",
            JournalStyleNotificationKind.QuestJournalUpdate);
    }

    public void ShowJournalStyleNotificationPopup(string message)
    {
        ShowJournalStyleNotificationPopup(
            message,
            ClassifyJournalStyleNotification(message));
    }

    public void ShowJournalStyleNotificationPopup(
        string message,
        JournalStyleNotificationKind kind)
    {
        if (journalUpdatePopupObject == null)
        {
            return;
        }

        JournalStylePopupEntry entry = GetOrCreateJournalStylePopup(kind);
        if (entry == null)
        {
            return;
        }

        if (entry.text != null)
        {
            entry.text.text = string.IsNullOrWhiteSpace(message)
                ? "任务日志已更新"
                : message.Trim();
        }

        entry.timer = Mathf.Max(0.1f, journalUpdatePopupDuration);
        if (entry.canvasGroup != null)
        {
            entry.canvasGroup.alpha = 1f;
        }

        entry.root.SetActive(true);
        LayoutJournalStyleNotifications();
        enabled = true;
    }

    private JournalStylePopupEntry GetOrCreateJournalStylePopup(
        JournalStyleNotificationKind kind)
    {
        for (int index = 0; index < journalStylePopupEntries.Count; index++)
        {
            if (journalStylePopupEntries[index].kind == kind)
            {
                return journalStylePopupEntries[index];
            }
        }

        GameObject popupObject = Instantiate(
            journalUpdatePopupObject,
            transform,
            false);
        popupObject.name = "Journal Style Notification " + kind;
        RectTransform popupRect = popupObject.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = popupObject.GetComponent<CanvasGroup>();
        Transform textTransform = popupObject.transform.Find("Journal Popup Text");
        Text popupText = textTransform != null
            ? textTransform.GetComponent<Text>()
            : null;
        JournalStylePopupEntry entry = new JournalStylePopupEntry
        {
            kind = kind,
            root = popupObject,
            rect = popupRect,
            canvasGroup = canvasGroup,
            text = popupText,
            timer = 0f
        };
        popupObject.SetActive(false);
        journalStylePopupEntries.Add(entry);
        return entry;
    }

    private static JournalStyleNotificationKind ClassifyJournalStyleNotification(
        string message)
    {
        string value = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : message.Trim();
        if (value == "任务日志已更新")
            return JournalStyleNotificationKind.QuestJournalUpdate;
        if (value.StartsWith("需要"))
            return JournalStyleNotificationKind.RequiredKey;
        if (value == "无法被打开")
            return JournalStyleNotificationKind.CannotOpen;
        if (value == "密码错误")
            return JournalStyleNotificationKind.PasswordError;
        if (value == "已开启")
            return JournalStyleNotificationKind.Opened;
        return JournalStyleNotificationKind.General;
    }

    private void LayoutJournalStyleNotifications()
    {
        List<JournalStylePopupEntry> active =
            new List<JournalStylePopupEntry>();
        for (int index = 0; index < journalStylePopupEntries.Count; index++)
        {
            JournalStylePopupEntry entry = journalStylePopupEntries[index];
            if (entry.root != null && entry.root.activeSelf)
            {
                active.Add(entry);
            }
        }
        active.Sort((first, second) => first.kind.CompareTo(second.kind));

        float verticalOffset = 0f;
        const float spacing = 8f;
        for (int index = 0; index < active.Count; index++)
        {
            JournalStylePopupEntry entry = active[index];
            entry.rect.anchoredPosition = journalStylePopupBasePosition +
                new Vector2(0f, -verticalOffset);
            verticalOffset += entry.rect.sizeDelta.y + spacing;
        }
    }

    private void CreateNewTaskPopup()
    {
        newTaskPopupObject = new GameObject(
            "New Task Objective Popup",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        newTaskPopupObject.transform.SetParent(transform, false);
        newTaskPopupCanvasGroup = newTaskPopupObject.GetComponent<CanvasGroup>();
        newTaskPopupCanvasGroup.alpha = 1f;
        newTaskPopupCanvasGroup.interactable = false;
        newTaskPopupCanvasGroup.blocksRaycasts = false;
        RectTransform popupRect = newTaskPopupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(1f, 0f);
        popupRect.anchorMax = new Vector2(1f, 0f);
        popupRect.pivot = new Vector2(1f, 0f);
        popupRect.anchoredPosition = new Vector2(
            -newTaskPopupScreenOffset.x,
            newTaskPopupScreenOffset.y);
        popupRect.sizeDelta = new Vector2(220f, 56f);
        Image popupBackground = newTaskPopupObject.GetComponent<Image>();
        popupBackground.color = Color.black;
        popupBackground.raycastTarget = false;

        GameObject borderObject = new GameObject(
            "Popup Border",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        borderObject.transform.SetParent(popupRect, false);
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImage = borderObject.GetComponent<Image>();
        borderImage.sprite = taskTextFrameSprite;
        borderImage.color = ZeldaUiPalette.Primary;
        borderImage.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Popup Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(popupRect, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);
        newTaskPopupText = textObject.GetComponent<Text>();
        newTaskPopupText.text = "有新任务目标";
        newTaskPopupText.font = permissionLabelFont;
        newTaskPopupText.fontSize = 16;
        newTaskPopupText.fontStyle = FontStyle.Normal;
        newTaskPopupText.alignment = TextAnchor.MiddleCenter;
        newTaskPopupText.color = ZeldaUiPalette.Primary;
        newTaskPopupText.horizontalOverflow = HorizontalWrapMode.Overflow;
        newTaskPopupText.verticalOverflow = VerticalWrapMode.Truncate;
        newTaskPopupText.raycastTarget = false;

        newTaskPopupObject.SetActive(false);
    }

    public void ShowNotificationPopup(string message)
    {
        ShowJournalStyleNotificationPopup(message);
    }

    private bool HasActiveUiAnimation()
    {
        return healthShakeTimer > 0f ||
               healthFlashTimer > 0f ||
               trackedQuestTargetInCurrentScene ||
               (newTaskPopupObject != null && newTaskPopupObject.activeSelf) ||
               HasActiveJournalStyleNotification();
    }

    private bool HasActiveJournalStyleNotification()
    {
        for (int index = 0; index < journalStylePopupEntries.Count; index++)
        {
            GameObject root = journalStylePopupEntries[index].root;
            if (root != null && root.activeSelf)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateNewTaskPopup()
    {
        if (newTaskPopupObject == null || !newTaskPopupObject.activeSelf)
            return;

        newTaskPopupTimer = Mathf.Max(
            0f,
            newTaskPopupTimer - Time.unscaledDeltaTime);

        if (newTaskPopupCanvasGroup != null)
        {
            float fadeDuration = Mathf.Clamp(
                newTaskPopupFadeDuration,
                0.01f,
                Mathf.Max(0.01f, newTaskPopupDuration));
            newTaskPopupCanvasGroup.alpha = Mathf.Clamp01(
                newTaskPopupTimer / fadeDuration);
        }

        if (newTaskPopupTimer <= 0f)
        {
            newTaskPopupObject.SetActive(false);
        }
    }

    private void UpdateJournalUpdatePopup()
    {
        bool layoutChanged = false;
        float fadeDuration = Mathf.Clamp(
            journalUpdatePopupFadeDuration,
            0.01f,
            Mathf.Max(0.01f, journalUpdatePopupDuration));
        for (int index = 0; index < journalStylePopupEntries.Count; index++)
        {
            JournalStylePopupEntry entry = journalStylePopupEntries[index];
            if (entry.root == null || !entry.root.activeSelf)
            {
                continue;
            }

            // These notifications pause together with document reading and
            // other gameplay pauses, preserving their readable lifetime.
            entry.timer = Mathf.Max(0f, entry.timer - Time.deltaTime);
            if (entry.canvasGroup != null)
            {
                entry.canvasGroup.alpha = Mathf.Clamp01(
                    entry.timer / fadeDuration);
            }

            if (entry.timer <= 0f)
            {
                entry.root.SetActive(false);
                layoutChanged = true;
            }
        }

        if (layoutChanged)
        {
            LayoutJournalStyleNotifications();
        }
    }

    private void ApplyTaskPlaceholderLayout()
    {
        if (taskContainer == null)
        {
            Transform existingContainer = transform.Find("Task Placeholder");
            if (existingContainer == null)
            {
                return;
            }

            taskContainer = existingContainer.GetComponent<RectTransform>();
            Transform indicator = existingContainer.Find("Task Indicator Slot");
            Transform textArea = existingContainer.Find("Task Text Slot");
            taskIndicatorRect = indicator != null ? indicator.GetComponent<RectTransform>() : null;
            taskTextAreaRect = textArea != null ? textArea.GetComponent<RectTransform>() : null;
            taskIndicatorImage = indicator != null ? indicator.GetComponent<Image>() : null;
            taskTextAreaImage = textArea != null ? textArea.GetComponent<Image>() : null;
        }

        if (taskIndicatorRect == null)
        {
            return;
        }

        // The former text rectangle is no longer part of the task placeholder.
        if (taskTextAreaRect != null)
        {
            taskTextAreaRect.gameObject.SetActive(false);
        }

        taskIndicatorSize = EnforcedTaskIndicatorSize;
        taskContainer.anchorMin = new Vector2(1f, 1f);
        taskContainer.anchorMax = new Vector2(1f, 1f);
        taskContainer.pivot = new Vector2(1f, 1f);
        taskContainer.anchoredPosition = new Vector2(
            -taskScreenOffset.x / TaskContainerScale,
            -taskScreenOffset.y / TaskContainerScale);
        taskContainer.localScale = Vector3.one * TaskContainerScale;
        taskContainer.sizeDelta = new Vector2(
            taskIndicatorSize,
            taskIndicatorSize + 21f);

        taskIndicatorRect.anchorMin = new Vector2(1f, 1f);
        taskIndicatorRect.anchorMax = new Vector2(1f, 1f);
        taskIndicatorRect.pivot = new Vector2(1f, 1f);
        taskIndicatorRect.anchoredPosition = Vector2.zero;
        taskIndicatorRect.sizeDelta = Vector2.one * taskIndicatorSize;

        if (taskPointerLabelRect != null)
        {
            const float labelHeight = 18f;
            const float labelGap = 0f;
            taskPointerLabelRect.anchorMin = new Vector2(1f, 1f);
            taskPointerLabelRect.anchorMax = new Vector2(1f, 1f);
            taskPointerLabelRect.pivot = new Vector2(1f, 1f);
            taskPointerLabelRect.anchoredPosition =
                new Vector2(0f, -taskIndicatorSize - labelGap);
            taskPointerLabelRect.sizeDelta =
                new Vector2(taskIndicatorSize, labelHeight);
        }
    }

    private void UpdateTaskPointer()
    {
        if (taskMiniMapGraphic == null)
            return;

        ZeldaCharacterData character = dataSource != null
            ? dataSource.ActiveCharacterData
            : displayedCharacter;
        bool visible = showTaskPointer && character != null && !character.IsDead;
        taskMiniMapGraphic.gameObject.SetActive(visible);
        if (taskPointerLabelRect != null)
        {
            taskPointerLabelRect.gameObject.SetActive(visible);
        }
        if (!visible)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (taskMiniMapSceneName != activeScene.name)
        {
            taskMiniMapGraphic.RebuildFromScene(activeScene);
            taskMiniMapSceneName = activeScene.name;
            taskMiniMapAreaDisplayName =
                ResolveSceneAreaDisplayName(activeScene);
        }

        if (taskPointerLabelText != null)
        {
            taskPointerLabelText.text = taskMiniMapAreaDisplayName;
            if (permissionLabelFont != null
                && permissionLabelFont.material != null
                && permissionLabelFont.material.mainTexture != null)
            {
                permissionLabelFont.material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        Transform remoteControlTarget =
            ClockworkPuppetRuntime.RemoteControlTargetTransform;
        Vector2 characterPosition = remoteControlTarget != null
            ? (Vector2)remoteControlTarget.position
            : (Vector2)character.transform.position;
        taskMiniMapGraphic.SetZoomToMaximum();
        taskMiniMapGraphic.SetCameraMarkerVisible(true);
        taskMiniMapGraphic.SetCameraPosition(characterPosition);
        taskMiniMapGraphic.CenterOnWorldPosition(characterPosition);
        ApplyTrackedQuestTarget(activeScene, characterPosition);
        taskPointerRect.localRotation = Quaternion.identity;
    }

    private void ApplyTrackedQuestTarget(Scene activeScene, Vector2 characterPosition)
    {
        if (questJournal == null)
        {
            questJournal = QuestJournalManager.GetOrCreate();
        }

        Vector2 targetPosition = Vector2.zero;
        bool targetInScene = questJournal != null &&
            !string.IsNullOrWhiteSpace(questJournal.TrackedEntryId) &&
            !questJournal.IsEntryCompleted(questJournal.TrackedEntryId) &&
            taskMiniMapGraphic.TryGetQuestTarget(
                questJournal.TrackedEntryId,
                out targetPosition);
        taskMiniMapGraphic.SetTrackedQuestTarget(
            targetInScene,
            targetInScene ? targetPosition : Vector2.zero,
            true);
        trackedQuestTargetInCurrentScene = targetInScene;
        trackedQuestTargetWorldPosition = targetPosition;
        UpdateTrackedQuestWorldMarker(targetInScene, targetPosition);
    }

    private void UpdateTrackedQuestWorldMarker(bool hasTarget, Vector2 targetPosition)
    {
        Camera camera = Camera.main;
        bool visible = false;
        if (hasTarget && camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(targetPosition);
            visible = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
        }

        EnsureTrackedQuestWorldMarker();
        if (trackedQuestWorldMarker == null)
            return;

        trackedQuestWorldMarker.SetActive(visible);
        if (visible)
        {
            // This marker belongs to the scene, not the HUD canvas. Keeping it
            // at the target's fixed world position also prevents camera follow
            // smoothing from dragging the marker after movement input ends.
            trackedQuestWorldMarker.transform.position =
                new Vector3(targetPosition.x, targetPosition.y, 0f);
        }
    }

    private void EnsureTrackedQuestWorldMarker()
    {
        if (trackedQuestWorldMarker != null)
            return;

        trackedQuestWorldMarkerTexture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false);
        trackedQuestWorldMarkerTexture.name = "Tracked Quest World Marker Texture";
        trackedQuestWorldMarkerTexture.filterMode = FilterMode.Point;
        trackedQuestWorldMarkerTexture.wrapMode = TextureWrapMode.Clamp;
        trackedQuestWorldMarkerTexture.SetPixel(0, 0, Color.white);
        trackedQuestWorldMarkerTexture.Apply(false, false);
        trackedQuestWorldMarkerSprite = Sprite.Create(
            trackedQuestWorldMarkerTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        trackedQuestWorldMarkerSprite.name = "Tracked Quest World Marker Sprite";

        trackedQuestWorldMarker = new GameObject("Tracked Quest World Marker");
        DontDestroyOnLoad(trackedQuestWorldMarker);
        trackedQuestWorldMarker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

        CreateTrackedQuestWorldMarkerLayer(
            trackedQuestWorldMarker.transform,
            "White Border",
            0.46f,
            Color.white,
            29997);
        CreateTrackedQuestWorldMarkerLayer(
            trackedQuestWorldMarker.transform,
            "Dark Cyan Outer",
            0.36f,
            new Color32(18, 92, 108, 255),
            29998);
        CreateTrackedQuestWorldMarkerLayer(
            trackedQuestWorldMarker.transform,
            "Sky Blue Inner",
            0.18f,
            new Color32(86, 190, 245, 255),
            29999);
        trackedQuestWorldMarker.SetActive(false);
    }

    private void CreateTrackedQuestWorldMarkerLayer(
        Transform parent,
        string layerName,
        float worldSize,
        Color color,
        int sortingOrder)
    {
        GameObject layerObject = new GameObject(layerName, typeof(SpriteRenderer));
        layerObject.transform.SetParent(parent, false);
        layerObject.transform.localPosition = Vector3.zero;
        layerObject.transform.localRotation = Quaternion.identity;
        layerObject.transform.localScale = new Vector3(worldSize, worldSize, 1f);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        renderer.sprite = trackedQuestWorldMarkerSprite;
        renderer.color = color;
        renderer.sortingLayerID = 0;
        // CameraCircularVision uses order 30000. These layers deliberately
        // remain immediately below it, while still staying above ordinary
        // world sprites. UI is composited by its later camera and stays above.
        renderer.sortingOrder = sortingOrder;
    }

    private static string ResolveSceneAreaDisplayName(Scene scene)
    {
        if (!scene.IsValid())
        {
            return string.Empty;
        }

        TabJournalMenuController[] menus =
            FindObjectsOfType<TabJournalMenuController>(true);
        for (int index = 0; index < menus.Length; index++)
        {
            TabJournalMenuController menu = menus[index];
            if (menu != null && menu.gameObject.scene == scene)
            {
                string displayName = menu.AreaDisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Trim();
                }
            }
        }

        RuntimeMiniMapSceneData snapshot =
            Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + scene.name);
        return snapshot != null &&
               !string.IsNullOrWhiteSpace(snapshot.DisplayName)
            ? snapshot.DisplayName
            : scene.name;
    }

    private void EnsurePossessionEnergyCount(int requiredCount)
    {
        while (possessionEnergyBars.Count < requiredCount)
        {
            int index = possessionEnergyBars.Count;
            GameObject barObject = new GameObject(
                $"Energy {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            barObject.transform.SetParent(possessionEnergyContainer, false);

            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(
                index * (PossessionEnergyDisplaySize.x + possessionEnergySpacing), 0f);
            barRect.sizeDelta = PossessionEnergyDisplaySize;

            Image image = barObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = ZeldaUiPalette.Primary;
            possessionEnergyBars.Add(image);
        }

        float width = requiredCount > 0
            ? requiredCount * PossessionEnergyDisplaySize.x + (requiredCount - 1) * possessionEnergySpacing
            : 0f;
        possessionEnergyContainer.sizeDelta = new Vector2(width, PossessionEnergyDisplaySize.y);
    }

    private void CreateHeartSprite()
    {
        // Nine columns by eight rows, authored bottom-to-top.
        string[] rows =
        {
            "....#....",
            "...###...",
            "..#####..",
            ".#######.",
            "#########",
            "#########",
            ".###.###.",
            "..#...#.."
        };

        heartTexture = new Texture2D(HeartPixelWidth, HeartPixelHeight, TextureFormat.RGBA32, false);
        heartTexture.name = "Runtime Pixel Heart";
        heartTexture.filterMode = FilterMode.Point;
        heartTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < HeartPixelHeight; y++)
        {
            for (int x = 0; x < HeartPixelWidth; x++)
            {
                heartTexture.SetPixel(x, y, rows[y][x] == '#' ? Color.white : Color.clear);
            }
        }

        heartTexture.Apply(false, true);
        heartSprite = Sprite.Create(
            heartTexture,
            new Rect(0f, 0f, HeartPixelWidth, HeartPixelHeight),
            new Vector2(0.5f, 0.5f),
            HeartPixelHeight);
        heartSprite.name = "Runtime Pixel Heart";
    }

    private void CreateSwordSprite()
    {
        swordSprite = CreateStrengthIcon(out swordTexture);
    }

    private static Sprite combatExpertiseIcon;
    private static readonly Sprite[] rankedCombatIcons = new Sprite[2];
    public static Sprite GetRankedCombatExpertiseIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 1, 2);
        if (rankedCombatIcons[rank - 1] == null) rankedCombatIcons[rank - 1] = CreateStrengthIcon(out _, true, rank);
        return rankedCombatIcons[rank - 1];
    }
    public static Sprite GetCombatExpertiseIcon()
    {
        if (combatExpertiseIcon == null) combatExpertiseIcon = CreateStrengthIcon(out _, true);
        return combatExpertiseIcon;
    }

    public static Sprite CreateStrengthIcon(out Texture2D swordTexture, bool crossed = false, int rank = 0)
    {
        // Reference silhouette, authored top-to-bottom: broad diagonal blade,
        // opposing crossguard ends and a short, thick grip. Texture resolution
        // is independent of SwordPixelSize so the HUD layout stays the same.
        string[] rows =
        {
            ".............####",
            "............#####",
            "...........######",
            "..........######.",
            ".........######..",
            "........######...",
            ".##....######....",
            ".###..######.....",
            "...########......",
            "...#######.......",
            "....#####........",
            "...#####.........",
            "..###.###........",
            "####....##.......",
            "###.....##.......",
            "###..............",
            "................."
        };

        if (crossed)
        {
            // Keep the slender silhouette, then add half a source pixel of weight below.
            // The ordinary strength icon above retains its original silhouette.
            rows = new[]
            {
                "...............#.",
                "..............##.",
                ".............##..",
                "............##...",
                "...........##....",
                "..........##.....",
                ".........##......",
                "........##.......",
                "...#...##........",
                "....#.##.........",
                ".....##..........",
                "....#.#..........",
                "...#...#.........",
                "..#..............",
                ".##..............",
                ".#...............",
                "................."
            };
            // Double the resolution for a small thickness adjustment instead of a full
            // original-pixel expansion, which would merge the guards again.
            var thickerRows = new string[rows.Length * 2];
            for (int y = 0; y < thickerRows.Length; y++)
            {
                var row = new char[thickerRows.Length];
                for (int x = 0; x < row.Length; x++)
                    row[x] = rows[y / 2][x / 2] == '#' ||
                        (x > 0 && rows[y / 2][(x - 1) / 2] == '#') ? '#' : '.';
                thickerRows[y] = new string(row);
            }
            rows = thickerRows;
        }

        int textureSize = rank > 0 ? 44 : rows.Length;
        swordTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        swordTexture.name = "Runtime Pixel Sword";
        swordTexture.filterMode = FilterMode.Point;
        swordTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool filled = y < rows.Length && x < rows.Length &&
                    (rows[y][x] == '#' || (crossed && rows[y][rows.Length - 1 - x] == '#'));
                // Keep the swords in the upper-left and the numeral clear of the guards.
                if (rank > 0 && y >= 32 && y <= 41 && x >= 32 && x <= 41)
                    filled |= y <= 33 || y >= 40 || (rank == 1 ? x >= 36 && x <= 37 :
                        (x >= 34 && x <= 35) || (x >= 38 && x <= 39));
                swordTexture.SetPixel(x, textureSize - 1 - y, filled ? Color.white : Color.clear);
            }
        }

        swordTexture.Apply(false, true);
        Sprite swordSprite = Sprite.Create(
            swordTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        swordSprite.name = "Runtime Pixel Sword";
        return swordSprite;
    }

    private void CreateWrenchSprite()
    {
        wrenchSprite = CreateSkillIcon(out wrenchTexture);
    }

    public static Sprite CreateSkillIcon(out Texture2D wrenchTexture)
    {
        // Double open-ended wrench, authored top-to-bottom from the reference.
        // Keep texture resolution independent of the existing HUD icon size.
        string[] rows =
        {
            ".......###..",
            "......###...",
            "......##...#",
            ".......##.##",
            "......######",
            ".....###.##.",
            ".##.###.....",
            "######......",
            "##.##.......",
            "#...##......",
            "...###......",
            "..###......."
        };

        int textureSize = rows.Length;
        wrenchTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        wrenchTexture.name = "Runtime Pixel Wrench";
        wrenchTexture.filterMode = FilterMode.Point;
        wrenchTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                wrenchTexture.SetPixel(x, textureSize - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            }
        }

        wrenchTexture.Apply(false, true);
        Sprite wrenchSprite = Sprite.Create(
            wrenchTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        wrenchSprite.name = "Runtime Pixel Wrench";
        return wrenchSprite;
    }

    private void CreatePermissionSprites()
    {
        permissionFrameTexture = new Texture2D(
            PermissionPixelSize,
            PermissionPixelSize,
            TextureFormat.RGBA32,
            false);
        permissionFrameTexture.name = "Runtime Permission Frame";
        permissionFrameTexture.filterMode = FilterMode.Point;
        permissionFrameTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < PermissionPixelSize; y++)
        {
            for (int x = 0; x < PermissionPixelSize; x++)
            {
                bool isBorder = x == 0 || x == PermissionPixelSize - 1 ||
                    y == 0 || y == PermissionPixelSize - 1;
                permissionFrameTexture.SetPixel(x, y, isBorder ? Color.white : Color.clear);
            }
        }

        permissionFrameTexture.Apply(false, true);
        permissionFrameSprite = Sprite.Create(
            permissionFrameTexture,
            new Rect(0f, 0f, PermissionPixelSize, PermissionPixelSize),
            new Vector2(0.5f, 0.5f),
            PermissionPixelSize);
        permissionFrameSprite.name = "Runtime Permission Frame";

        for (int level = 0; level <= 9; level++)
        {
            Texture2D texture;
            Sprite sprite = CreateSpriteFromRows(
                GetPermissionDigitRows(level),
                $"Runtime Permission Digit {level}",
                out texture);
            permissionTextures[level] = texture;
            permissionSprites[level] = sprite;
        }

        for (int absoluteValue = 1; absoluteValue <= 2; absoluteValue++)
        {
            Texture2D texture;
            Sprite sprite = CreateSpriteFromRows(
                GetSignedPermissionRows(-absoluteValue),
                $"Runtime Permission Digit -{absoluteValue}",
                out texture);
            int index = absoluteValue - 1;
            negativePermissionTextures[index] = texture;
            negativePermissionSprites[index] = sprite;
        }

        CreateTaskTextFrameSprite();

    }

    private void CreateTaskTextFrameSprite()
    {
        // These higher-resolution one-pixel outlines render at about four
        // screen pixels after the task container's 2x scale.
        const int indicatorSize = 45;
        taskIndicatorFrameTexture = CreateOutlineTexture(
            indicatorSize,
            indicatorSize,
            "Runtime Task Indicator Frame");
        taskIndicatorFrameSprite = Sprite.Create(
            taskIndicatorFrameTexture,
            new Rect(0f, 0f, indicatorSize, indicatorSize),
            new Vector2(0.5f, 0.5f),
            indicatorSize);
        taskIndicatorFrameSprite.name = "Runtime Task Indicator Frame";

        const int pointerWidth = 25;
        const int pointerHeight = 51;
        taskPointerTexture = new Texture2D(
            pointerWidth,
            pointerHeight,
            TextureFormat.RGBA32,
            false);
        taskPointerTexture.name = "Runtime Task Direction Pointer";
        taskPointerTexture.filterMode = FilterMode.Point;
        taskPointerTexture.wrapMode = TextureWrapMode.Clamp;
        const int pointerCenterX = 12;
        const int pointerCenterY = 25;
        for (int y = 0; y < pointerHeight; y++)
        {
            for (int x = 0; x < pointerWidth; x++)
            {
                int dx = x - pointerCenterX;
                int dy = y - pointerCenterY;
                bool northNeedle =
                    (dy >= 0 && dy <= 21 && Mathf.Abs(dx) <= 1) ||
                    (dy >= 22 && dy <= 24 && dx == 0);
                bool southNeedle = dy <= 0 && dy >= -9 && Mathf.Abs(dx) <= 1;
                bool horizontalArms =
                    Mathf.Abs(dy) <= 1 &&
                    Mathf.Abs(dx) >= 2 &&
                    Mathf.Abs(dx) <= 7;
                int hubRadiusSquared = dx * dx + dy * dy;
                bool centerHub = hubRadiusSquared >= 3 && hubRadiusSquared <= 10;
                bool centerHole = hubRadiusSquared <= 1;
                taskPointerTexture.SetPixel(
                    x,
                    y,
                    (northNeedle || southNeedle || horizontalArms || centerHub) && !centerHole
                        ? Color.white
                        : Color.clear);
            }
        }
        taskPointerTexture.Apply(false, true);
        taskPointerSprite = Sprite.Create(
            taskPointerTexture,
            new Rect(0f, 0f, pointerWidth, pointerHeight),
            new Vector2(0.5f, 0.5f),
            pointerHeight);
        taskPointerSprite.name = "Runtime Task Direction Pointer";

        const int labelFrameWidth = 30;
        const int labelFrameHeight = 9;
        taskTextFrameTexture = CreateOutlineTexture(
            labelFrameWidth,
            labelFrameHeight,
            "Runtime Task Pointer Label Frame");
        taskTextFrameSprite = Sprite.Create(
            taskTextFrameTexture,
            new Rect(0f, 0f, labelFrameWidth, labelFrameHeight),
            new Vector2(0.5f, 0.5f),
            labelFrameHeight);
        taskTextFrameSprite.name = "Runtime Task Pointer Label Frame";

    }

    private Texture2D CreateOutlineTexture(int width, int height, string textureName)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                texture.SetPixel(x, y, isBorder ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private Sprite CreateSpriteFromRows(string[] rows, string spriteName, out Texture2D texture)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, rows[row][x] == '#' ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            height);
        sprite.name = spriteName;
        return sprite;
    }

    private void DestroyRuntimeSpriteAndTexture(Sprite sprite, Texture2D texture)
    {
        if (sprite != null)
        {
            Destroy(sprite);
        }

        if (texture != null)
        {
            Destroy(texture);
        }
    }

    private string[] GetPermissionDigitRows(int level)
    {
        switch (level)
        {
            case 0: return new[] { "###", "#.#", "#.#", "#.#", "###" };
            case 1: return new[] { ".#.", "##.", ".#.", ".#.", "###" };
            case 2: return new[] { "###", "..#", "###", "#..", "###" };
            case 3: return new[] { "###", "..#", "###", "..#", "###" };
            case 4: return new[] { "#.#", "#.#", "###", "..#", "..#" };
            case 5: return new[] { "###", "#..", "###", "..#", "###" };
            case 6: return new[] { "###", "#..", "###", "#.#", "###" };
            case 7: return new[] { "###", "..#", ".#.", ".#.", ".#." };
            case 8: return new[] { "###", "#.#", "###", "#.#", "###" };
            default: return new[] { "###", "#.#", "###", "..#", "###" };
        }
    }

    private Sprite GetPermissionSprite(int level)
    {
        int clampedLevel = Mathf.Clamp(
            level,
            ZeldaCharacterData.MinimumPermissionLevel,
            ZeldaCharacterData.MaximumPermissionLevel);
        if (clampedLevel >= 0)
        {
            return permissionSprites[clampedLevel];
        }

        return negativePermissionSprites[-clampedLevel - 1];
    }

    private string[] GetSignedPermissionRows(int level)
    {
        string[] digitRows = GetPermissionDigitRows(Mathf.Abs(level));
        string[] signedRows = new string[digitRows.Length];
        for (int row = 0; row < digitRows.Length; row++)
        {
            signedRows[row] = (row == 2 ? "###." : "....") + digitRows[row];
        }

        return signedRows;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pixelScale = Mathf.Max(1f, pixelScale);
        permissionScaleMultiplier = Mathf.Max(1f, permissionScaleMultiplier);
        heartSpacing = Mathf.Max(0f, heartSpacing);
        possessionEnergyBarSize.x = Mathf.Max(1f, possessionEnergyBarSize.x);
        possessionEnergyBarSize.y = Mathf.Max(1f, possessionEnergyBarSize.y);
        possessionEnergySpacing = Mathf.Max(0f, possessionEnergySpacing);
        possessionEnergyPermissionGap = Mathf.Max(0f, possessionEnergyPermissionGap);
        areaPermissionDigitScale = Mathf.Max(1f, areaPermissionDigitScale);
        inventorySlotSize = Mathf.Max(1f, inventorySlotSize);
        inventorySlotSpacing = Mathf.Max(0f, inventorySlotSpacing);
        taskIndicatorSize = Mathf.Max(1f, taskIndicatorSize);
        taskTextWidth = Mathf.Max(1f, taskTextWidth);
        taskSectionSpacing = Mathf.Max(0f, taskSectionSpacing);
        newTaskPopupDuration = Mathf.Max(0.1f, newTaskPopupDuration);
        newTaskPopupFadeDuration = Mathf.Clamp(
            newTaskPopupFadeDuration,
            0.01f,
            newTaskPopupDuration);
        journalUpdatePopupDuration = Mathf.Max(0.1f, journalUpdatePopupDuration);
        journalUpdatePopupFadeDuration = Mathf.Clamp(
            journalUpdatePopupFadeDuration,
            0.01f,
            journalUpdatePopupDuration);
        journalUpdatePopupSize.x = Mathf.Max(240f, journalUpdatePopupSize.x);
        journalUpdatePopupSize.y = Mathf.Max(56f, journalUpdatePopupSize.y);
        journalUpdatePopupTopGap = Mathf.Max(0f, journalUpdatePopupTopGap);
        healthShakeDuration = Mathf.Max(0.01f, healthShakeDuration);
        healthShakeAmount = Mathf.Max(0f, healthShakeAmount);
        healthShakeSpeed = Mathf.Max(0f, healthShakeSpeed);
        healthFlashDuration = Mathf.Max(0.01f, healthFlashDuration);
        healthFlashSpeed = Mathf.Max(0f, healthFlashSpeed);
    }
#endif
}

/// <summary>Five equipped skills plus a separate body-owned slot selected with key 0.</summary>
public sealed class RuntimeSkillSlots : MonoBehaviour
{
    // Preserve indices 0..4 for the existing equipment/save data. Index 5 is displayed as key 0.
    public const int CharacterSlotIndex = 5;
    private ZeldaCharacterData observedCharacter;
    private DominoSkillPreview dominoPreview;
    private void LateUpdate()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        bool show = mover != null && !mover.GetComponent<ZeldaCharacterData>().IsDead && !mover.GetComponent<ZeldaCharacterData>().IsGhostForm &&
            !DocumentReader.IsInputBlocked && !ClockworkPuppetRuntime.BlocksCharacterInput &&
            observedEquipment != null && PlayerGrowthAttributes.IsDominoSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("domino");
        if (!show) { if (dominoPreview != null) dominoPreview.Hide(); return; }
        if (dominoPreview == null) dominoPreview = new GameObject("Domino Skill Preview").AddComponent<DominoSkillPreview>();
        dominoPreview.Show(mover);
    }
    private void OnDisable() { if (dominoPreview != null) dominoPreview.Hide(); }
    private PlayerGrowthAttributes observedEquipment;
    private void RefreshEquipment()
    {
        for (int i = 0; i < 5; i++)
        {
            string id = observedEquipment != null ? observedEquipment.GetEquippedSkill(i) : null;
            var skill = System.Array.Find(PlayerGrowthAttributes.Skills, entry => entry.id == id);
            AssignSlot(i, skill != null ? skill.title : "", SkillPageArt.GetAbilitySprite(id));
        }
    }
    private void OnDestroy()
    {
        if (dominoPreview != null) Destroy(dominoPreview.gameObject);
        if (observedEquipment != null) observedEquipment.AttributesChanged -= RefreshEquipment;
    }
    public int SelectedIndex { get; private set; } = 0;
    public event System.Action<int> SelectionChanged;
    private readonly string[] skillNames = new string[6];
    private readonly Image[] backgrounds = new Image[6];
    private readonly Image[] icons = new Image[6];
    private readonly List<Graphic> tinted = new List<Graphic>();
    private Text selectedName;
    private bool ghost;
    private const float SlotSize = 40f;
    private const float Pitch = 48f;
    private static float SlotY(int index) => index == CharacterSlotIndex ? Pitch + 16f : -index * Pitch;

    public void Build(Font font, float leftOffset)
    {
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0, 0.5f);
        root.pivot = new Vector2(0, 1);
        // Place the third slot's center at the screen's vertical midpoint.
        root.anchoredPosition = new Vector2(leftOffset, Pitch * 2f + SlotSize * 0.5f);
        root.sizeDelta = new Vector2(SlotSize, Pitch * 4 + SlotSize);
        for (int i = 0; i < backgrounds.Length; i++)
        {
            bool characterSlot = i == CharacterSlotIndex;
            var slot = Rect(characterSlot ? "Character Skill 0" : "Skill " + (i + 1), root, new Vector2(0, SlotY(i)), Vector2.one * SlotSize);
            backgrounds[i] = slot.gameObject.AddComponent<Image>();
            backgrounds[i].raycastTarget = false;
            Edge(slot, new Vector2(0, 0), new Vector2(SlotSize, 2.5f));
            Edge(slot, new Vector2(0, -SlotSize + 2.5f), new Vector2(SlotSize, 2.5f));
            Edge(slot, Vector2.zero, new Vector2(2.5f, SlotSize));
            Edge(slot, new Vector2(SlotSize - 2.5f, 0), new Vector2(2.5f, SlotSize));
            icons[i] = Rect("Skill Icon", slot, new Vector2(10, -10), new Vector2(26, 26)).gameObject.AddComponent<Image>();
            icons[i].preserveAspect = true; icons[i].raycastTarget = false; icons[i].enabled = false;
            var number = Rect("Shortcut", slot, new Vector2(4f, -2), new Vector2(14, 16)).gameObject.AddComponent<Text>();
            number.font = font; number.fontSize = 14; number.text = characterSlot ? "0" : (i + 1).ToString();
            number.alignment = TextAnchor.UpperLeft;
            number.raycastTarget = false; tinted.Add(number);
        }
        selectedName = Rect("Selected Skill Name", root, new Vector2(SlotSize + 10, 0), new Vector2(230, SlotSize)).gameObject.AddComponent<Text>();
        selectedName.font = font; selectedName.fontSize = 18;
        selectedName.alignment = TextAnchor.MiddleLeft; selectedName.raycastTarget = false; tinted.Add(selectedName);
        Refresh();
    }

    public void AssignSlot(int index, string skillName, Sprite icon = null)
    {
        if (index < 0 || index >= icons.Length) return;
        skillNames[index] = skillName ?? "";
        icons[index].sprite = icon; icons[index].enabled = icon != null;
        Refresh();
    }
    public void SelectSlot(int index)
    {
        if (index < 0 || index >= backgrounds.Length || SelectedIndex == index) return;
        SelectedIndex = index; Refresh(); SelectionChanged?.Invoke(index);
    }
    private void Update()
    {
        if (observedEquipment != PlayerGrowthAttributes.Instance)
        {
            if (observedEquipment != null) observedEquipment.AttributesChanged -= RefreshEquipment;
            observedEquipment = PlayerGrowthAttributes.Instance;
            if (observedEquipment != null) observedEquipment.AttributesChanged += RefreshEquipment;
            RefreshEquipment();
        }
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        var character = mover != null ? mover.GetComponent<ZeldaCharacterData>() : null;
        if (observedCharacter != character)
        {
            observedCharacter = character;
            AssignSlot(CharacterSlotIndex, character != null ? character.CharacterSkillName : "", character != null ? character.CharacterSkillIcon : null);
        }
        bool isGhost = mover != null && mover.GetComponent<ZeldaCharacterData>() != null && mover.GetComponent<ZeldaCharacterData>().IsGhostLike;
        if (ghost != isGhost) { ghost = isGhost; Refresh(); }
        if (mover == null || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return;
        // Resolve selection first so key 0 + skill-use in the same frame cannot cast the previous slot.
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) SelectSlot(CharacterSlotIndex);
        else for (int i = 0; i < 5; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i))) { SelectSlot(i); break; }
        if (Input.GetKeyDown(KeyCode.LeftControl) && SelectedIndex == CharacterSlotIndex && character != null)
            character.TryUseCharacterSkill();
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null && character != null &&
            PlayerGrowthAttributes.IsCombatExpertiseSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("combat_expertise"))
            character.TryUseCombatExpertise();
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null && character != null &&
            PlayerGrowthAttributes.IsFearRoarSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("fear_roar"))
            character.TryUseFearRoar();
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null && character != null &&
            PlayerGrowthAttributes.IsRoyalCommandSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("royal_command"))
            character.TryUseRoyalCommand();
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null &&
            PlayerGrowthAttributes.IsSoulMarkSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("soul_mark"))
            SoulMarkRuntime.Use(mover);
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null &&
            PlayerGrowthAttributes.IsDominoSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("domino"))
            DominoSkillRuntime.Use(mover);
        if (Input.GetKeyDown(KeyCode.LeftControl) && observedEquipment != null &&
            PlayerGrowthAttributes.IsGhostFormSkill(observedEquipment.GetEquippedSkill(SelectedIndex)) && observedEquipment.HasSkill("ghost_form"))
            GhostFormRuntime.Use(mover);
    }
    private void Refresh()
    {
        if (selectedName == null) return;
        Color tint = ghost ? ZeldaUiPalette.Ghost : ZeldaUiPalette.Primary;
        foreach (Graphic graphic in tinted) graphic.color = tint;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            icons[i].color = tint;
            backgrounds[i].color = new Color(0.015f, 0.04f, 0.06f, 0.85f);
            float scale = i == SelectedIndex ? 1.18f : 1f;
            RectTransform slot = backgrounds[i].rectTransform;
            slot.localScale = Vector3.one * scale;
            // Compensate the top-left pivot to enlarge around the slot center.
            float expansion = SlotSize * (scale - 1f) * 0.5f;
            slot.anchoredPosition = new Vector2(-expansion, SlotY(i) + expansion);
        }
        selectedName.text = skillNames[SelectedIndex] ?? "";
        selectedName.rectTransform.anchoredPosition = new Vector2(SlotSize + 10, SlotY(SelectedIndex));
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    private void Edge(Transform parent, Vector2 position, Vector2 size)
    {
        var image = Rect("Border", parent, position, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false; tinted.Add(image);
    }
}

/// <summary>Small pooled UI sparks for temporary crystal energy; no particle-system cameras.</summary>
public sealed class TemporaryEnergyParticles : MonoBehaviour
{
    private readonly Image[] sparks = new Image[6];
    private Color tint;
    private float clock;
    public void SetVisible(bool visible, Color color)
    {
        tint = color;
        if (visible && sparks[0] == null)
        {
            for (int i = 0; i < sparks.Length; i++)
            {
                var obj = new GameObject("Energy Spark " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(transform, false);
                sparks[i] = obj.GetComponent<Image>();
                sparks[i].raycastTarget = false;
                sparks[i].rectTransform.sizeDelta = Vector2.one * 1.4f;
            }
        }
        foreach (var spark in sparks) if (spark != null) spark.gameObject.SetActive(visible);
        enabled = visible;
    }
    private void Update()
    {
        clock += Time.deltaTime;
        var rect = ((RectTransform)transform).rect;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null) continue;
            float phase = Mathf.Repeat(clock * 1.2f + i / 6f, 1f);
            float x = Mathf.Lerp(-rect.width * 0.6f, rect.width * 0.6f, (i * 0.618034f) % 1f);
            sparks[i].rectTransform.anchoredPosition = new Vector2(x + Mathf.Sin(phase * 5f + i) * 1.2f,
                Mathf.Lerp(-rect.height * 0.5f, rect.height * 0.5f + 7f, phase));
            sparks[i].color = new Color(tint.r, tint.g, tint.b, Mathf.Sin(phase * Mathf.PI) * 0.85f);
        }
    }
}
