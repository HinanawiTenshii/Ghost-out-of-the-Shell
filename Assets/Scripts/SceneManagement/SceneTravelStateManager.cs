using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps runtime scene snapshots in memory so revisiting a loaded scene
/// restores its gameplay objects instead of resetting the level.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed partial class SceneTravelStateManager : MonoBehaviour
{
    [Serializable]
    private sealed class ObjectState
    {
        public string path;
        public bool activeSelf;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool hasCharacter;
        public ZeldaCharacterData.RuntimeState characterState;
        public bool hasMover;
        public bool moverEnabled;
        public bool hasAi;
        public bool aiEnabled;
        public ZeldaAiState aiState;
        public ZeldaCharacterAiBase.SaveState savedAi;
        public bool hasDoor;
        public bool doorLocked;
        public bool doorOpen;
        public bool hasLever;
        public bool leverIsOn;
        public bool hasBatterySocket;
        public BatterySocket.RuntimeState batterySocketState;
        public bool hasItemSubmissionStation;
        public SpecificItemSubmissionStation.RuntimeState itemSubmissionState;
        public string pickupId, pickupIdentity;
        public string pickupRuntimeState;
        public List<SavedScalarFields.Value> pickupFields;
        public bool hasPickupColor;
        public Color pickupColor;
        public List<SavedScalarFields.Value> documentFields;
        public List<SavedScalarFields.Value> doorDataFields;
        public string deviceKind, deviceItemId, leverPath;
        public bool deviceRemote, deviceAttached;
        public List<SavedScalarFields.Value> deviceFields;
        public TimedLeverObjectToggle.SaveState timedToggle;
        public string deviceOwnerPath;
        public bool deviceOwnedByPlayer;
    }

    private sealed class SceneState
    {
        public string sceneName;
        public readonly Dictionary<string, ObjectState> objects =
            new Dictionary<string, ObjectState>();
    }

    private readonly Dictionary<string, SceneState> sceneStates =
        new Dictionary<string, SceneState>();
    private ZeldaCharacterData.RuntimeState travelingCharacterState;
    private bool hasTravelingCharacterState;
    private GameObject travelingCharacterObject;
    private bool restorePending;
    private Vector2 pendingArrivalPosition;
    private CameraVisionObjectStreaming[] captureStreamingManagers;
    private SceneState quickRestartSceneState;
    private string quickRestartSceneName;
    private string quickRestartCharacterPath;
    private string quickRestartCharacterName;
    private GameObject quickRestartPersistentCharacterObject;
    private GameObject quickRestartPersistentCharacterTemplate;
    private ZeldaCharacterData.RuntimeState quickRestartCharacterState;
    private Vector3 quickRestartCharacterPosition;
    private bool hasQuickRestartCharacter;
    private PersistentInventory.RuntimeState quickRestartInventoryState;
    private bool hasQuickRestartInventory;
    private PlayerGrowthAttributes.RuntimeState quickRestartGrowthState;
    private bool hasQuickRestartGrowth;
    private bool quickRestartPending;
    private bool sceneTravelRestoreInProgress;

    public static SceneTravelStateManager Instance { get; private set; }
    public bool HasTravelingControlledCharacter =>
        travelingCharacterObject != null &&
        travelingCharacterObject.GetComponent<ZeldaFourWayMover>() != null;
    public bool HasQuickRestartCheckpoint =>
        quickRestartSceneState != null &&
        !string.IsNullOrEmpty(quickRestartSceneName);
    public bool IsQuickRestartPending => quickRestartPending;
    public bool LastQuickRestartRestoreSucceeded { get; private set; }
    public bool IsSceneTravelRestoreInProgress =>
        sceneTravelRestoreInProgress;
    public event Action QuickRestartCompleted;

    public bool TryGetMapDoorState(string sceneName, string objectId, out bool open)
    {
        open = false;
        if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(objectId) ||
            !sceneStates.TryGetValue(sceneName, out SceneState scene) ||
            !scene.objects.TryGetValue(objectId, out ObjectState state) || !state.hasDoor) return false;
        open = state.doorOpen;
        return true;
    }

    // Match the named-hierarchy IDs without adding components to an editor scene.
    public static string GetMapObjectId(Transform target)
    {
        SceneTravelStableId existing = target.GetComponent<SceneTravelStableId>();
        if (existing != null && !string.IsNullOrEmpty(existing.StableId)) return existing.StableId;
        int ordinal = 0;
        if (target.parent != null)
        {
            for (int i = 0; i < target.GetSiblingIndex(); i++)
            {
                Transform sibling = target.parent.GetChild(i);
                if (sibling.name == target.name && sibling.gameObject.hideFlags == HideFlags.None) ordinal++;
            }
        }
        else
        {
            foreach (GameObject root in target.gameObject.scene.GetRootGameObjects())
            {
                if (root == target.gameObject) break;
                if (root.name == target.name && root.hideFlags == HideFlags.None) ordinal++;
            }
        }
        string segment = BuildStableSegment(target.name, ordinal);
        return target.parent != null ? GetMapObjectId(target.parent) + "/" + segment : segment;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCreatedBeforeFirstSceneLoads()
    {
        // Scene identities must be assigned before gameplay can collect or
        // destroy an object. Do not rely on a transition point being active.
        GetOrCreate();
    }

    public static SceneTravelStateManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("Scene Travel State Manager");
        return managerObject.AddComponent<SceneTravelStateManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TravelToScene(string targetSceneName, Vector2 arrivalPosition)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName)) return;
        RetroSceneLoadReveal.BeginTransition(targetSceneName.Trim(),
            () => TravelToSceneCovered(targetSceneName, arrivalPosition));
    }

    private void TravelToSceneCovered(string targetSceneName, Vector2 arrivalPosition)
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid() || string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        CaptureTravelingCharacter();
        CaptureScene(currentScene);
        PreserveTravelingCharacterObject();

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory != null)
        {
            inventory.PreserveForNextSceneLoad();
        }

        pendingArrivalPosition = arrivalPosition;
        restorePending = true;
        sceneTravelRestoreInProgress = true;
        SceneManager.LoadScene(targetSceneName.Trim());
    }

    /// <summary>
    /// Starts a fresh non-persistent scene session. Ordinary scene changes
    /// and main-menu navigation use this so no previously captured door,
    /// pickup, character or other scene-object state can be restored later.
    /// </summary>
    public void ResetAllSceneStates()
    {
        if (!GameSaveSystem.IsLoading && GameSaveSystem.Instance != null)
            GameSaveSystem.Instance.Save(0, false);
        StopAllCoroutines();
        sceneStates.Clear();

        QuestJournalManager.GetOrCreate().ResetAllEntries();
        MapPointOfInterestManager.GetOrCreate().ResetAllPoints();
        ItemDescriptionWindow.ResetDiscoveryHistoryIfPresent();

        restorePending = false;
        pendingArrivalPosition = Vector2.zero;
        sceneTravelRestoreInProgress = false;

        quickRestartPending = false;
        quickRestartSceneState = null;
        quickRestartSceneName = string.Empty;
        quickRestartCharacterPath = string.Empty;
        quickRestartCharacterName = string.Empty;
        quickRestartPersistentCharacterObject = null;
        DestroyQuickRestartCharacterTemplate();
        hasQuickRestartCharacter = false;
        hasQuickRestartInventory = false;
        hasQuickRestartGrowth = false;

        if (travelingCharacterObject != null)
        {
            Destroy(travelingCharacterObject);
        }

        travelingCharacterObject = null;
        hasTravelingCharacterState = false;
        captureStreamingManagers = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignStableObjectIds(scene);

        if (quickRestartPending)
        {
            StartCoroutine(RestoreQuickRestartAfterSceneLoad(scene));
            return;
        }

        if (!restorePending)
        {
            sceneTravelRestoreInProgress = false;
            if (travelingCharacterObject != null)
            {
                Destroy(travelingCharacterObject);
                travelingCharacterObject = null;
                hasTravelingCharacterState = false;
            }
            return;
        }

        restorePending = false;
        StartCoroutine(RestoreAfterDeferredSceneObjects(scene));
    }

    private IEnumerator RestoreAfterDeferredSceneObjects(Scene scene)
    {
        // Persistent singleton prefabs from the revisited scene destroy their
        // duplicate instances in Awake. Destroy is deferred until the end of
        // the frame, so restoring directly from sceneLoaded sees those pending
        // roots and every later sibling index produces a different saved path.
        // Wait until they are actually removed before indexing the hierarchy.
        yield return null;

        RestoreScene(scene);
        ReclaimTravelingCharacterControl();
        yield return ApplyArrivalAfterSceneInitialization();
        if (sceneStates.TryGetValue(scene.name, out var restoredState)) FinalizeRestoredDevices(scene, restoredState);
        sceneTravelRestoreInProgress = false;
    }

    private IEnumerator ApplyArrivalAfterSceneInitialization()
    {
        // GhostPlayerSpawnPoint creates its player in Start, which runs after
        // SceneManager.sceneLoaded. Wait until that initialization completes
        // so its spawn position cannot overwrite the configured destination.
        ZeldaFourWayMover mover = null;
        const int maximumWaitFrames = 10;
        for (int frame = 0; frame < maximumWaitFrames && mover == null; frame++)
        {
            yield return null;
            mover = ZeldaRuntimeRegistry.GetControlledMover();
        }

        if (mover == null)
        {
            Debug.LogWarning(
                "Scene travel could not find a controlled character to place at the configured arrival position.",
                this);
            yield break;
        }

        ApplyTravelingCharacterState(mover);
        Rigidbody2D body = mover.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = pendingArrivalPosition;
        }
        mover.transform.position = pendingArrivalPosition;
        Physics2D.SyncTransforms();

        ActiveZeldaCharacterDataSource source = ActiveZeldaCharacterDataSource.Instance;
        if (source != null)
        {
            source.Refresh();
        }
    }

    private void CaptureTravelingCharacter()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        ZeldaCharacterData data = mover != null
            ? mover.GetComponent<ZeldaCharacterData>()
            : null;
        hasTravelingCharacterState = data != null && !data.IsDead;
        if (hasTravelingCharacterState)
        {
            travelingCharacterState = data.CaptureRuntimeState();
            travelingCharacterObject = mover.gameObject;
        }
        else
        {
            travelingCharacterObject = null;
        }
    }

    private void PreserveTravelingCharacterObject()
    {
        if (travelingCharacterObject == null)
        {
            return;
        }

        Transform characterTransform = travelingCharacterObject.transform;
        if (characterTransform.parent != null)
        {
            characterTransform.SetParent(null, true);
        }
        DontDestroyOnLoad(travelingCharacterObject);
    }

    public void ReclaimTravelingCharacterControl()
    {
        if (travelingCharacterObject == null)
        {
            return;
        }

        ZeldaFourWayMover travelingMover =
            travelingCharacterObject.GetComponent<ZeldaFourWayMover>();
        if (travelingMover == null)
        {
            return;
        }

        foreach (ZeldaFourWayMover mover in ZeldaRuntimeRegistry.Movers)
        {
            if (mover != null && mover != travelingMover && mover.isActiveAndEnabled)
            {
                mover.enabled = false;
            }
        }

        if (!travelingMover.enabled)
        {
            travelingMover.enabled = true;
        }
        ZeldaRuntimeRegistry.ClaimControlledMover(travelingMover);
    }

    private void ApplyTravelingCharacterState(ZeldaFourWayMover mover)
    {
        if (!hasTravelingCharacterState)
        {
            return;
        }

        ZeldaCharacterData data = mover != null
            ? mover.GetComponent<ZeldaCharacterData>()
            : null;
        if (data != null)
        {
            data.ApplyRuntimeState(travelingCharacterState);
        }
    }

    private void CaptureScene(Scene scene)
    {
        sceneStates[scene.name] = CreateSceneSnapshot(scene);
    }

    private SceneState CreateSceneSnapshot(Scene scene)
    {
        AssignStableObjectIds(scene);
        SceneState snapshot = new SceneState
        {
            sceneName = scene.name
        };
        captureStreamingManagers =
            FindObjectsOfType<CameraVisionObjectStreaming>(true);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            CaptureHierarchy(roots[i].transform, snapshot);
        }

        captureStreamingManagers = null;
        return snapshot;
    }

    private void CaptureHierarchy(Transform target, SceneState snapshot)
    {
        if (target == null || target.gameObject == gameObject ||
            (travelingCharacterObject != null &&
             (target.gameObject == travelingCharacterObject ||
              target.IsChildOf(travelingCharacterObject.transform))) ||
            target.gameObject.hideFlags != HideFlags.None)
        {
            return;
        }

        if (ShouldTrack(target.gameObject))
        {
            ZeldaCharacterData character = target.GetComponent<ZeldaCharacterData>();
            ZeldaFourWayMover mover = target.GetComponent<ZeldaFourWayMover>();
            ZeldaCharacterAiBase ai = target.GetComponent<ZeldaCharacterAiBase>();
            DoorHingeInteraction door = target.GetComponent<DoorHingeInteraction>();
            LeverData lever = target.GetComponent<LeverData>();
            BatterySocket batterySocket = target.GetComponent<BatterySocket>();
            SpecificItemSubmissionStation itemSubmissionStation =
                target.GetComponent<SpecificItemSubmissionStation>();
            ObjectState state = new ObjectState
            {
                path = BuildPath(target),
                activeSelf = target.gameObject.activeSelf ||
                             IsDirectlySuspendedByVisionStreaming(target.gameObject),
                localPosition = target.localPosition,
                localRotation = target.localRotation,
                localScale = target.localScale,
                hasCharacter = character != null,
                characterState = character != null
                    ? character.CaptureRuntimeState()
                    : default(ZeldaCharacterData.RuntimeState),
                hasMover = mover != null,
                moverEnabled = mover != null && mover.enabled,
                hasAi = ai != null,
                aiEnabled = ai != null && ai.enabled,
                aiState = ai != null ? ai.CurrentState : ZeldaAiState.Idle,
                savedAi = ai != null ? ai.CaptureSaveState() : null,
                hasDoor = door != null,
                doorLocked = door != null && door.IsLocked,
                doorOpen = door != null && door.IsOpen,
                hasLever = lever != null,
                leverIsOn = lever != null && lever.IsOn,
                hasBatterySocket = batterySocket != null,
                batterySocketState = batterySocket != null ? batterySocket.CaptureRuntimeState() : default,
                hasItemSubmissionStation = itemSubmissionStation != null,
                itemSubmissionState = itemSubmissionStation != null
                    ? itemSubmissionStation.CaptureRuntimeState()
                    : default(SpecificItemSubmissionStation.RuntimeState)
            };
            snapshot.objects[state.path] = state;
            var pickup = target.GetComponent<PickupItemBase>();
            if (pickup != null)
            {
                var box = pickup as CardboardBoxPickupItem;
                if (box != null)
                {
                    box.PrepareContentsForSave();
                    var driver = box.GetComponent<ClockworkPuppetBoxDriver>();
                    state.deviceRemote = driver != null && driver.IsUnderRemoteControl;
                }
                state.pickupId = pickup.ItemId;
                state.pickupIdentity = pickup.UniqueInstanceId;
                state.pickupRuntimeState = pickup.InventoryState;
                state.pickupFields = SavedScalarFields.Capture(pickup, true);
                state.hasPickupColor = pickup.ItemVisual != null;
                if (state.hasPickupColor) state.pickupColor = pickup.ItemVisual.DisplayColor;
            }
            var document = target.GetComponent<DocumentReader>();
            if (document != null) state.documentFields = SavedScalarFields.Capture(document, true);
            var doorData = target.GetComponent<DoorData>();
            if (doorData != null) state.doorDataFields = SavedScalarFields.Capture(doorData);
            var bomb = target.GetComponent<PlacedBomb>();
            var timedToggle = target.GetComponent<TimedLeverObjectToggle>();
            if (timedToggle != null) state.timedToggle = timedToggle.CaptureSaveState();
            if (bomb != null)
            {
                state.deviceKind = "bomb"; state.deviceItemId = bomb.SaveSourceItemId;
                if (bomb.SaveOwner != null)
                {
                    state.deviceOwnerPath = BuildPath(bomb.SaveOwner.transform);
                    state.deviceOwnedByPlayer = bomb.SaveOwner.GetComponent<ZeldaFourWayMover>() == ZeldaRuntimeRegistry.GetControlledMover();
                }
                state.deviceFields = SavedScalarFields.Capture(bomb, true);
            }
            var puppet = target.GetComponent<ClockworkPuppetRuntime>();
            if (puppet != null)
            {
                state.deviceKind = "puppet"; state.deviceItemId = puppet.SaveSourceItemId;
                state.deviceRemote = puppet.IsUnderRemoteControl; state.deviceAttached = puppet.SaveAttached;
                state.leverPath = puppet.SaveTargetLever != null ? BuildPath(puppet.SaveTargetLever.transform) : "";
                state.deviceFields = SavedScalarFields.Capture(puppet, true);
            }
        }

        for (int i = 0; i < target.childCount; i++)
        {
            CaptureHierarchy(target.GetChild(i), snapshot);
        }
    }

    private bool IsDirectlySuspendedByVisionStreaming(GameObject candidate)
    {
        if (captureStreamingManagers == null)
        {
            return false;
        }

        for (int i = 0; i < captureStreamingManagers.Length; i++)
        {
            CameraVisionObjectStreaming manager = captureStreamingManagers[i];
            if (manager != null &&
                manager.IsSuspendedByStreaming(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreScene(Scene scene)
    {
        SceneState snapshot;
        if (!sceneStates.TryGetValue(scene.name, out snapshot))
        {
            return;
        }

        RestoreSceneSnapshot(scene, snapshot);
    }

    private bool RestoreSceneSnapshot(
        Scene scene,
        SceneState snapshot,
        bool requireStrongIdentityMatch = false)
    {
        if (snapshot == null ||
            !scene.IsValid() ||
            !scene.isLoaded ||
            snapshot.sceneName != scene.name)
        {
            Debug.LogWarning(
                "Refused to restore a scene snapshot because it does not " +
                "belong to the currently loaded scene.",
                this);
            return false;
        }

        // sceneLoaded runs before Start. Assign again here so objects created
        // by spawn points and runtime visual builders receive the same stable
        // identities they had when the checkpoint was captured.
        AssignStableObjectIds(scene);
        Dictionary<string, Transform> currentObjects = new Dictionary<string, Transform>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            IndexHierarchy(roots[i].transform, currentObjects);
        }

        // Dropped/produced pickups are runtime roots and do not exist in the authored scene.
        PickupItemBase[] pickupTemplates = null;
        foreach (var saved in snapshot.objects.Values)
        {
            if (!currentObjects.ContainsKey(saved.path) && !string.IsNullOrEmpty(saved.deviceKind))
            {
                if (pickupTemplates == null) pickupTemplates = SaveGameCatalog.LoadPickupTemplates();
                foreach (var template in pickupTemplates)
                {
                    if (template.ItemId != saved.deviceItemId) continue;
                    Component device = null;
                    if (saved.deviceKind == "bomb" && template is BombPickupItem bombTemplate)
                        device = bombTemplate.CreateConfiguredPlacedBomb(saved.localPosition, null);
                    if (saved.deviceKind == "puppet" && template is ClockworkPuppetPickupItem puppetTemplate)
                        device = puppetTemplate.DeployPuppet(saved.localPosition, null, null, -1);
                    if (device == null) break;
                    device.gameObject.AddComponent<SceneTravelStableId>().Initialize(saved.path);
                    currentObjects[saved.path] = device.transform;
                    SavedScalarFields.Apply(device, saved.deviceFields);
                    break;
                }
            }
            if (currentObjects.ContainsKey(saved.path) || string.IsNullOrEmpty(saved.pickupId)) continue;
            if (pickupTemplates == null) pickupTemplates = SaveGameCatalog.LoadPickupTemplates();
            foreach (var template in pickupTemplates)
            {
                if (template.ItemId != saved.pickupId) continue;
                var item = Instantiate(template, saved.localPosition, saved.localRotation);
                var identity = item.GetComponent<SceneTravelStableId>();
                if (identity == null) identity = item.gameObject.AddComponent<SceneTravelStableId>();
                identity.Initialize(saved.path);
                currentObjects[saved.path] = item.transform;
                break;
            }
        }

        int matchingObjectCount = 0;
        foreach (string savedPath in snapshot.objects.Keys)
        {
            if (currentObjects.ContainsKey(savedPath))
            {
                matchingObjectCount++;
            }
        }

        // Missing-object restoration is intentionally destructive. An empty
        // or largely mismatched snapshot indicates stale/early identity data,
        // not that every object in the scene was destroyed by the player.
        int savedObjectCount = snapshot.objects.Count;
        float matchRatio = savedObjectCount > 0
            ? (float)matchingObjectCount / savedObjectCount
            : 0f;
        if (requireStrongIdentityMatch &&
            (savedObjectCount == 0 || matchRatio < 0.5f))
        {
            Debug.LogWarning(
                "Refused to apply an incomplete scene snapshot (" +
                matchingObjectCount + "/" + savedObjectCount +
                " objects matched). The loaded scene was left intact.",
                this);
            return false;
        }

        foreach (KeyValuePair<string, ObjectState> entry in snapshot.objects)
        {
            Transform target;
            if (!currentObjects.TryGetValue(entry.Key, out target) || target == null)
            {
                continue;
            }

            ObjectState state = entry.Value;
            var pickup = target.GetComponent<PickupItemBase>();
            if (pickup != null && !string.IsNullOrEmpty(state.pickupId))
            {
                SavedScalarFields.Apply(pickup, state.pickupFields);
                pickup.SetUniqueInstanceId(state.pickupIdentity);
                pickup.ApplyInventoryState(state.pickupRuntimeState);
                if (state.hasPickupColor && pickup.ItemVisual != null) pickup.ItemVisual.SetDisplayColor(state.pickupColor);
            }
            var document = target.GetComponent<DocumentReader>();
            if (document != null) SavedScalarFields.Apply(document, state.documentFields);
            var doorData = target.GetComponent<DoorData>();
            if (doorData != null) SavedScalarFields.Apply(doorData, state.doorDataFields);
            target.localPosition = state.localPosition;
            target.localRotation = state.localRotation;
            target.localScale = state.localScale;
            if (target.gameObject.activeSelf != state.activeSelf)
            {
                target.gameObject.SetActive(state.activeSelf);
            }

            ZeldaCharacterData character = target.GetComponent<ZeldaCharacterData>();
            if (state.hasCharacter && character != null)
            {
                character.ApplyRuntimeState(state.characterState);
            }

            DoorHingeInteraction door = target.GetComponent<DoorHingeInteraction>();
            if (state.hasDoor && door != null)
            {
                door.ApplyPersistentState(state.doorLocked, state.doorOpen);
            }

            LeverData lever = target.GetComponent<LeverData>();
            if (state.hasLever && lever != null)
            {
                lever.ApplyPersistentState(state.leverIsOn);
            }

            BatterySocket batterySocket = target.GetComponent<BatterySocket>();
            if (state.hasBatterySocket && batterySocket != null)
                batterySocket.ApplyRuntimeState(state.batterySocketState);

            SpecificItemSubmissionStation itemSubmissionStation =
                target.GetComponent<SpecificItemSubmissionStation>();
            if (state.hasItemSubmissionStation && itemSubmissionStation != null)
            {
                itemSubmissionStation.ApplyRuntimeState(
                    state.itemSubmissionState);
            }

            ZeldaFourWayMover mover = target.GetComponent<ZeldaFourWayMover>();
            if (state.hasMover && mover != null)
            {
                mover.enabled = state.moverEnabled;
            }

            ZeldaCharacterAiBase ai = target.GetComponent<ZeldaCharacterAiBase>();
            if (state.hasAi && ai != null)
            {
                ai.enabled = state.aiEnabled;
                // A machine's activation is a level condition, not a guard's
                // transient pursuit. Restore it before the normal return policy.
                if (ai is AutomatonCharacterAi && state.savedAi != null)
                    ai.ApplySaveState(state.savedAi);
                ai.RecoverAfterPersistentSceneReturn(state.aiState);
            }
        }

        // A scene-authored gameplay object that no longer existed when the
        // snapshot was taken had been collected or destroyed. Keep it absent.
        foreach (KeyValuePair<string, Transform> entry in currentObjects)
        {
            if (!snapshot.objects.ContainsKey(entry.Key) &&
                IsMissingStateRelevant(entry.Value.gameObject))
            {
                entry.Value.gameObject.SetActive(false);
            }
        }

        return true;
    }

    public void ClearQuickRestartCheckpoint()
    {
        if (quickRestartPending)
        {
            return;
        }

        quickRestartSceneState = null;
        quickRestartSceneName = string.Empty;
        quickRestartCharacterPath = string.Empty;
        quickRestartCharacterName = string.Empty;
        quickRestartPersistentCharacterObject = null;
        DestroyQuickRestartCharacterTemplate();
        hasQuickRestartCharacter = false;
        hasQuickRestartInventory = false;
        hasQuickRestartGrowth = false;
    }

    public void CaptureQuickRestartCheckpoint(Scene scene)
    {
        if (!scene.IsValid() ||
            !scene.isLoaded ||
            scene != SceneManager.GetActiveScene() ||
            sceneTravelRestoreInProgress ||
            quickRestartPending)
        {
            return;
        }

        quickRestartSceneName = scene.name;
        quickRestartSceneState = CreateSceneSnapshot(scene);

        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        ZeldaCharacterData data = mover != null
            ? mover.GetComponent<ZeldaCharacterData>()
            : null;
        hasQuickRestartCharacter = mover != null && data != null && !data.IsDead;
        quickRestartCharacterPath = string.Empty;
        quickRestartCharacterName = string.Empty;
        quickRestartPersistentCharacterObject = null;
        DestroyQuickRestartCharacterTemplate();
        if (hasQuickRestartCharacter)
        {
            quickRestartCharacterState = data.CaptureRuntimeState();
            quickRestartCharacterPosition = mover.transform.position;
            quickRestartCharacterName = mover.gameObject.name;
            if (mover.gameObject.scene == scene)
            {
                quickRestartCharacterPath = BuildPath(mover.transform);
            }
            else
            {
                // A character brought through PersistentSceneTransitionPoint
                // lives in Unity's DontDestroyOnLoad scene. Keep its exact
                // identity; name/type matching is ambiguous for civilians and
                // other prefabs with configurable visuals.
                quickRestartPersistentCharacterObject = mover.gameObject;
                CreateQuickRestartCharacterTemplate(mover);
            }
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        hasQuickRestartInventory = inventory != null;
        if (hasQuickRestartInventory)
        {
            quickRestartInventoryState = inventory.CaptureRuntimeState();
        }

        PlayerGrowthAttributes growth = PlayerGrowthAttributes.Instance;
        hasQuickRestartGrowth = growth != null;
        if (hasQuickRestartGrowth)
        {
            quickRestartGrowthState = growth.CaptureRuntimeState();
        }
    }

    public bool RestartFromQuickCheckpoint()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!HasQuickRestartCheckpoint ||
            quickRestartPending ||
            sceneTravelRestoreInProgress ||
            !activeScene.IsValid() ||
            activeScene.name != quickRestartSceneName ||
            quickRestartSceneState.sceneName != activeScene.name)
        {
            return false;
        }

        return RetroSceneLoadReveal.BeginTransition(quickRestartSceneName, () =>
        {
            PersistentInventory inventory = PersistentInventory.Instance;
            if (inventory != null) inventory.PreserveForNextSceneLoad();
            quickRestartPending = true;
            sceneTravelRestoreInProgress = false;
            restorePending = false;
            SceneManager.LoadScene(quickRestartSceneName);
        });
    }

    private IEnumerator RestoreQuickRestartAfterSceneLoad(Scene scene)
    {
        LastQuickRestartRestoreSucceeded = false;
        // Allow scene Awake/Start, singleton duplicate destruction and dynamic
        // Ghost creation to complete before restoring the entry snapshot.
        yield return null;
        yield return null;

        bool restoredSnapshot =
            RestoreSceneSnapshot(
                scene,
                quickRestartSceneState,
                true);
        if (!restoredSnapshot)
        {
            quickRestartPending = false;
            QuickRestartCompleted?.Invoke();
            yield break;
        }

        ZeldaFourWayMover restartMover = FindQuickRestartCharacter(scene);
        const int maximumCharacterWaitFrames = 10;
        for (int frame = 0;
             restartMover == null && frame < maximumCharacterWaitFrames;
             frame++)
        {
            yield return null;
            restartMover = FindQuickRestartCharacter(scene);
        }

        if (restartMover != null && hasQuickRestartCharacter)
        {
            ZeldaFourWayMover[] movers =
                FindObjectsOfType<ZeldaFourWayMover>(true);
            for (int i = 0; i < movers.Length; i++)
            {
                ZeldaFourWayMover candidate = movers[i];
                if (candidate != null &&
                    candidate != restartMover &&
                    candidate.enabled)
                {
                    candidate.enabled = false;
                }
            }

            ZeldaCharacterData data =
                restartMover.GetComponent<ZeldaCharacterData>();
            if (data != null)
            {
                data.ApplyRuntimeState(quickRestartCharacterState);
            }

            restartMover.transform.position = quickRestartCharacterPosition;
            Rigidbody2D body = restartMover.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = quickRestartCharacterPosition;
            }

            if (!restartMover.enabled)
            {
                restartMover.enabled = true;
            }
            ZeldaRuntimeRegistry.ClaimControlledMover(restartMover);
            ReconcilePersistentTravelingCharacter(restartMover);
        }

        if (hasQuickRestartInventory &&
            PersistentInventory.Instance != null)
        {
            PersistentInventory.Instance.ApplyRuntimeState(
                quickRestartInventoryState);
        }

        if (hasQuickRestartGrowth &&
            PlayerGrowthAttributes.Instance != null)
        {
            PlayerGrowthAttributes.Instance.ApplyRuntimeState(
                quickRestartGrowthState);
        }

        Physics2D.SyncTransforms();
        ActiveZeldaCharacterDataSource source =
            ActiveZeldaCharacterDataSource.Instance;
        if (source != null)
        {
            source.Refresh();
        }

        quickRestartPending = false;
        FinalizeRestoredDevices(scene, quickRestartSceneState);
        LastQuickRestartRestoreSucceeded = restartMover != null;
        QuickRestartCompleted?.Invoke();
    }

    private ZeldaFourWayMover FindQuickRestartCharacter(Scene scene)
    {
        if (!hasQuickRestartCharacter)
        {
            return null;
        }

        if (quickRestartPersistentCharacterObject != null)
        {
            ZeldaFourWayMover persistentMover =
                quickRestartPersistentCharacterObject
                    .GetComponent<ZeldaFourWayMover>();
            if (persistentMover != null)
            {
                return persistentMover;
            }
        }

        ZeldaFourWayMover recreatedPersistentMover =
            RecreateQuickRestartPersistentCharacter();
        if (recreatedPersistentMover != null)
        {
            return recreatedPersistentMover;
        }

        if (!string.IsNullOrEmpty(quickRestartCharacterPath))
        {
            Dictionary<string, Transform> currentObjects =
                new Dictionary<string, Transform>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                IndexHierarchy(roots[i].transform, currentObjects);
            }

            Transform exactTransform;
            if (currentObjects.TryGetValue(
                    quickRestartCharacterPath,
                    out exactTransform) &&
                exactTransform != null)
            {
                ZeldaFourWayMover exactMover =
                    exactTransform.GetComponent<ZeldaFourWayMover>();
                if (exactMover != null)
                {
                    return exactMover;
                }
            }
        }

        ZeldaFourWayMover[] movers =
            FindObjectsOfType<ZeldaFourWayMover>(true);
        for (int i = 0; i < movers.Length; i++)
        {
            ZeldaFourWayMover candidate = movers[i];
            if (candidate == null ||
                candidate.gameObject.name != quickRestartCharacterName)
            {
                continue;
            }

            ZeldaCharacterData data =
                candidate.GetComponent<ZeldaCharacterData>();
            if (data != null &&
                data.GetType().AssemblyQualifiedName ==
                quickRestartCharacterState.characterType)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CreateQuickRestartCharacterTemplate(
        ZeldaFourWayMover sourceMover)
    {
        if (sourceMover == null)
        {
            return;
        }

        GameObject sourceObject = sourceMover.gameObject;
        quickRestartPersistentCharacterTemplate = Instantiate(sourceObject);
        quickRestartPersistentCharacterTemplate.name =
            sourceObject.name + " [Quick Restart Template]";
        quickRestartPersistentCharacterTemplate.SetActive(false);
        DontDestroyOnLoad(quickRestartPersistentCharacterTemplate);

        // Instantiating an active controlled character briefly invokes the
        // clone's OnEnable. Explicitly return registry ownership to the real
        // character after the template has been disabled.
        ZeldaRuntimeRegistry.ClaimControlledMover(sourceMover);
    }

    private ZeldaFourWayMover RecreateQuickRestartPersistentCharacter()
    {
        if (quickRestartPersistentCharacterTemplate == null)
        {
            return null;
        }

        GameObject recreatedObject =
            Instantiate(quickRestartPersistentCharacterTemplate);
        recreatedObject.name = string.IsNullOrEmpty(quickRestartCharacterName)
            ? "Restored Traveling Character"
            : quickRestartCharacterName;
        recreatedObject.transform.position = quickRestartCharacterPosition;
        DontDestroyOnLoad(recreatedObject);
        recreatedObject.SetActive(true);

        ZeldaFourWayMover recreatedMover =
            recreatedObject.GetComponent<ZeldaFourWayMover>();
        if (recreatedMover == null)
        {
            Destroy(recreatedObject);
            return null;
        }

        quickRestartPersistentCharacterObject = recreatedObject;
        return recreatedMover;
    }

    private void DestroyQuickRestartCharacterTemplate()
    {
        if (quickRestartPersistentCharacterTemplate == null)
        {
            return;
        }

        Destroy(quickRestartPersistentCharacterTemplate);
        quickRestartPersistentCharacterTemplate = null;
    }

    private void ReconcilePersistentTravelingCharacter(
        ZeldaFourWayMover restartMover)
    {
        GameObject restartObject =
            restartMover != null ? restartMover.gameObject : null;

        if (travelingCharacterObject != null &&
            travelingCharacterObject != restartObject)
        {
            // This is the stale DontDestroyOnLoad character retained by a
            // previous persistent transition. Leaving it merely disabled
            // creates the visible, AI-less duplicate reported after restart.
            Destroy(travelingCharacterObject);
            travelingCharacterObject = null;
            hasTravelingCharacterState = false;
        }

        if (restartObject != null &&
            quickRestartPersistentCharacterObject == restartObject)
        {
            travelingCharacterObject = restartObject;
            travelingCharacterState = quickRestartCharacterState;
            hasTravelingCharacterState = true;
        }
    }

    private static void IndexHierarchy(
        Transform target,
        Dictionary<string, Transform> destination)
    {
        if (target == null || target.gameObject.hideFlags != HideFlags.None)
        {
            return;
        }

        destination[BuildPath(target)] = target;
        for (int i = 0; i < target.childCount; i++)
        {
            IndexHierarchy(target.GetChild(i), destination);
        }
    }

    private static void AssignStableObjectIds(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        Dictionary<string, int> rootNameCounts =
            new Dictionary<string, int>();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i] != null ? roots[i].transform : null;
            if (root == null || root.gameObject.hideFlags != HideFlags.None)
            {
                continue;
            }

            string rootSegment = BuildStableSegment(
                root.name,
                GetAndIncrementNameOrdinal(rootNameCounts, root.name));
            AssignStableObjectIdsRecursive(root, rootSegment);
        }
    }

    private static void AssignStableObjectIdsRecursive(
        Transform target,
        string stablePath)
    {
        if (target == null || target.gameObject.hideFlags != HideFlags.None)
        {
            return;
        }

        if (ShouldTrack(target.gameObject))
        {
            SceneTravelStableId stableId =
                target.GetComponent<SceneTravelStableId>();
            if (stableId == null)
            {
                stableId = target.gameObject.AddComponent<SceneTravelStableId>();
            }
            stableId.Initialize(stablePath);
        }

        Dictionary<string, int> childNameCounts =
            new Dictionary<string, int>();
        for (int i = 0; i < target.childCount; i++)
        {
            Transform child = target.GetChild(i);
            if (child == null || child.gameObject.hideFlags != HideFlags.None)
            {
                continue;
            }

            int ordinal =
                GetAndIncrementNameOrdinal(childNameCounts, child.name);
            AssignStableObjectIdsRecursive(
                child,
                stablePath + "/" + BuildStableSegment(child.name, ordinal));
        }
    }

    private static int GetAndIncrementNameOrdinal(
        Dictionary<string, int> nameCounts,
        string objectName)
    {
        string safeName = objectName ?? string.Empty;
        int ordinal;
        if (!nameCounts.TryGetValue(safeName, out ordinal))
        {
            ordinal = 0;
        }

        nameCounts[safeName] = ordinal + 1;
        return ordinal;
    }

    private static string BuildStableSegment(string objectName, int ordinal)
    {
        string escapedName = (objectName ?? string.Empty)
            .Replace("%", "%25")
            .Replace("/", "%2F")
            .Replace("@", "%40");
        return escapedName + "@" + ordinal;
    }

    private static bool ShouldTrack(GameObject candidate)
    {
        return candidate.GetComponent<Renderer>() != null ||
               candidate.GetComponent<TimedLeverObjectToggle>() != null ||
               candidate.GetComponent<Collider2D>() != null ||
               candidate.GetComponent<PickupItemBase>() != null ||
               candidate.GetComponent<GrowthCollectible>() != null ||
               candidate.GetComponent<DoorData>() != null ||
               candidate.GetComponent<ZeldaCharacterData>() != null ||
               candidate.GetComponent<DoorHingeInteraction>() != null ||
               candidate.GetComponent<LeverData>() != null ||
               candidate.GetComponent<SpecificItemSubmissionStation>() != null;
    }

    private static bool IsMissingStateRelevant(GameObject candidate)
    {
        return candidate.GetComponent<PickupItemBase>() != null ||
               candidate.GetComponent<GrowthCollectible>() != null ||
               candidate.GetComponent<DoorData>() != null ||
               candidate.GetComponent<ZeldaCharacterData>() != null ||
               candidate.GetComponent<SpecificItemSubmissionStation>() != null;
    }

    private static string BuildPath(Transform target)
    {
        SceneTravelStableId stableId =
            target != null ? target.GetComponent<SceneTravelStableId>() : null;
        if (stableId != null && !string.IsNullOrEmpty(stableId.StableId))
        {
            return stableId.StableId;
        }

        // Fallback for a dynamically created object that appeared after the
        // scene identity pass. Scene-authored stateful objects use StableId.
        string path = target.name + "#" + target.GetSiblingIndex();
        Transform parent = target.parent;
        while (parent != null && parent.gameObject.scene == target.gameObject.scene)
        {
            path = parent.name + "#" + parent.GetSiblingIndex() + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
