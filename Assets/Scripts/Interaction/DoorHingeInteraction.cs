using System.Collections.Generic;
using UnityEngine;

public class DoorHingeInteraction : MonoBehaviour
{
    private static readonly HashSet<DoorHingeInteraction> ActiveDoors = new HashSet<DoorHingeInteraction>();
    [Header("Player Interaction")]
    [SerializeField, Tooltip("Whether the controlled character can interact with this door by pressing E while nearby.")]
    private bool allowPlayerInteraction = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 1.1f;
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private bool clockwise;
    [Header("Door Motion Presentation")]
    [SerializeField, Tooltip("Opt-in easing for door prefabs. Same angle and duration; leave off on bridges and other mechanisms.")]
    private bool easeDoorRotation;
    [Header("Optional Double Door Hinges")]
    [SerializeField, Tooltip("Leave empty for the original single door. For a double door, assign its first child hinge.")]
    private Transform primaryHinge;
    [SerializeField, Tooltip("Optional second child hinge. Opens in the opposite direction with the same angle and speed.")]
    private Transform secondaryHinge;
    [SerializeField] private bool isLocked;
    [SerializeField, Tooltip("Whether a clockwork puppet can pry this door's lock. Existing doors remain pryable by default.")]
    private bool canBeLockpicked = true;
    [Header("Password Lock")]
    [SerializeField, Tooltip("When enabled, a locked door requires its four-digit password instead of an inventory key.")]
    private bool passwordLockEnabled;
    [SerializeField] private string doorPassword = "1111";
    [SerializeField] private Font passwordWindowFont;
    [Header("Unlock Keys (Any One, Maximum Three)")]
    [SerializeField] private PickupItemBase requiredUnlockItem;
    [SerializeField] private PickupItemBase requiredUnlockItem2;
    [SerializeField] private PickupItemBase requiredUnlockItem3;
    [Header("Cross-Scene Key Identities (Same Three Slots)")]
    [SerializeField, Tooltip("Assign the same identity asset used by the specific key in another scene. Overrides Required Unlock Item for slot 1.")]
    private PickupItemPersistentIdentity requiredUnlockItemIdentity;
    [SerializeField, Tooltip("Cross-scene identity for unlock-key slot 2.")]
    private PickupItemPersistentIdentity requiredUnlockItemIdentity2;
    [SerializeField, Tooltip("Cross-scene identity for unlock-key slot 3.")]
    private PickupItemPersistentIdentity requiredUnlockItemIdentity3;
    [SerializeField] private string requiredUnlockItemId;
    [SerializeField] private string requiredUnlockItemInstanceId;
    [SerializeField] private string requiredUnlockItemInstanceId2;
    [SerializeField] private string requiredUnlockItemInstanceId3;
    [SerializeField, HideInInspector] private string requiredUnlockItemName;
    [SerializeField, Min(0.1f)] private float aiAutoOpenDistance = 1.35f;
    [SerializeField, Min(0.1f)] private float aiAutoCloseDelay = 2f;
    [SerializeField] private Vector2 interactionPromptOffset = new Vector2(0f, 0.9f);
    [Header("Optional Locked Interaction Journal Entry")]
    [SerializeField] private bool addJournalEntryWhenLockedInteractionFails;
    [SerializeField] private bool addLockedJournalEntryOnlyOnce = true;
    [SerializeField] private string lockedJournalEntryId = string.Empty;
    [SerializeField] private string lockedJournalEntryTitle = string.Empty;
    [SerializeField, TextArea(4, 12)] private string lockedJournalEntryDetails = string.Empty;

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private Quaternion secondaryClosedRotation;
    private Quaternion secondaryTargetRotation;
    private HingeMotion primaryMotion;
    private HingeMotion secondaryMotion;
    private Transform PrimaryHinge => primaryHinge != null ? primaryHinge : transform;
    private bool HasSecondaryHinge => secondaryHinge != null && secondaryHinge != PrimaryHinge;
    private bool isOpen;
    private Collider2D[] doorColliders;
    private bool[] doorColliderInitialStates;
    private Renderer[] doorRenderers;
    private bool aiPassageActive;
    private bool waitForAiToLeave;
    private float aiPassageTimer;
    private GameObject interactionPromptObject;
    private TextMesh interactionPromptText;
    private Font interactionPromptFont;
    private Material interactionPromptMaterial;
    private bool lockedInteractionAcknowledged;
    private bool lockedJournalEntryWasAdded;
    private DoorPasswordPanel passwordPanel;
    private Bounds lastVisionBlockerBounds;
    private bool hasLastVisionBlockerBounds;

    public bool IsLocked => isLocked;
    public static IReadOnlyCollection<DoorHingeInteraction> WorldDoors => ActiveDoors;
    public bool IsOpen => isOpen;
    /// <summary>Read-only endpoint matrices for the map; never rotates the real door.</summary>
    public Matrix4x4 GetMapEndpointMatrix(bool open)
    {
        Transform hinge = PrimaryHinge;
        Quaternion initial = Application.isPlaying ? closedRotation : hinge.localRotation;
        Quaternion rotation = open
            ? initial * Quaternion.Euler(0f, 0f, clockwise ? -rotationAngle : rotationAngle)
            : initial;
        Matrix4x4 parent = hinge.parent != null ? hinge.parent.localToWorldMatrix : Matrix4x4.identity;
        return parent * Matrix4x4.TRS(hinge.localPosition, rotation, hinge.localScale);
    }
    public Transform MapHingeTransform => PrimaryHinge;
    public bool IsChangingOpenState =>
        Quaternion.Angle(PrimaryHinge.localRotation, targetRotation) > 0.1f ||
        (HasSecondaryHinge && Quaternion.Angle(secondaryHinge.localRotation, secondaryTargetRotation) > 0.1f);
    public bool AllowPlayerInteraction => allowPlayerInteraction;
    public bool CanBeLockpicked => canBeLockpicked;
    public bool PasswordLockEnabled => passwordLockEnabled;
    public PickupItemBase RequiredUnlockItem => requiredUnlockItem;
    public string RequiredUnlockItemId => requiredUnlockItemId;
    public string RequiredUnlockItemInstanceId => requiredUnlockItemInstanceId;
    public string RequiredUnlockItemInstanceId2 =>
        requiredUnlockItemInstanceId2;
    public string RequiredUnlockItemInstanceId3 =>
        requiredUnlockItemInstanceId3;

    public float GetSurfaceDistanceTo(
        Collider2D sourceCollider,
        Vector2 sourcePosition)
    {
        float closest = Vector2.Distance(
            sourcePosition,
            transform.position);
        if (sourceCollider == null)
        {
            return closest;
        }
        if (doorColliders == null)
        {
            CacheDoorColliders();
        }

        for (int i = 0; i < doorColliders.Length; i++)
        {
            Collider2D doorCollider = doorColliders[i];
            if (doorCollider == null || !doorCollider.enabled ||
                doorCollider == sourceCollider)
            {
                continue;
            }
            ColliderDistance2D distance =
                sourceCollider.Distance(doorCollider);
            if (!distance.isValid)
            {
                continue;
            }
            closest = Mathf.Min(
                closest,
                distance.isOverlapped ? 0f : distance.distance);
        }
        return closest;
    }

    public void SetCollisionIgnoredWith(
        Collider2D otherCollider,
        bool ignored)
    {
        if (otherCollider == null)
        {
            return;
        }
        if (doorColliders == null)
        {
            CacheDoorColliders();
        }

        for (int i = 0; i < doorColliders.Length; i++)
        {
            Collider2D doorCollider = doorColliders[i];
            if (doorCollider == null || doorCollider == otherCollider)
            {
                continue;
            }
            Physics2D.IgnoreCollision(
                otherCollider,
                doorCollider,
                ignored);
        }
    }

    private void Awake()
    {
        doorPassword = DoorPasswordPanel.NormalizePassword(doorPassword);
        CacheRequiredUnlockItemIdentity(false);
        CacheDoorColliders();
        closedRotation = PrimaryHinge.localRotation;
        targetRotation = closedRotation;
        if (HasSecondaryHinge)
        {
            secondaryClosedRotation = secondaryHinge.localRotation;
            secondaryTargetRotation = secondaryClosedRotation;
        }
        QuestJournalInteractionMarker.Configure(
            gameObject,
            addJournalEntryWhenLockedInteractionFails,
            lockedJournalEntryId,
            null);
    }

    private void Start()
    {
        // Direct same-scene pickup references receive their deterministic
        // SceneTravelStableId between Awake and Start. Cache after that step so
        // revisiting the scene cannot change the required key instance ID.
        CacheRequiredUnlockItemIdentity(true);
    }

    private void OnEnable()
    {
        ActiveDoors.Add(this);
        NotifyVisionBlockerChanged();
    }

    private void Update()
    {
        if (DocumentReader.IsInputBlocked)
        {
            return;
        }

        UpdateAiAutomaticDoor(Time.deltaTime);

        ZeldaFourWayMover nearbyCharacter = allowPlayerInteraction
            ? GetActiveControlledCharacterNearby()
            : null;
        if (allowPlayerInteraction &&
            !aiPassageActive &&
            Input.GetKeyDown(interactKey) &&
            nearbyCharacter != null)
        {
            ZeldaInteractionArbiter.Submit(
                this,
                nearbyCharacter,
                interactKey,
                transform.position,
                HandlePlayerInteraction);
        }

        bool moved = RotateHinge(PrimaryHinge, targetRotation, ref primaryMotion);
        if (HasSecondaryHinge)
            moved |= RotateHinge(secondaryHinge, secondaryTargetRotation, ref secondaryMotion);
        if (moved)
        {
            NotifyVisionBlockerChanged();
        }
    }

    private bool RotateHinge(Transform hinge, Quaternion target, ref HingeMotion motion)
    {
        Quaternion previous = hinge.localRotation;
        // The real hinge drives both sprite and collider. No visual-only offset,
        // overshoot, extra collision geometry, or change to interaction distances.
        hinge.localRotation = easeDoorRotation
            ? motion.Step(previous, target, rotationSpeed, Time.deltaTime)
            : Quaternion.RotateTowards(previous, target, rotationSpeed * Time.deltaTime);
        return Quaternion.Angle(previous, hinge.localRotation) > 0.001f;
    }

    // Per-leaf state: reversing starts at the current pose, loading snaps exactly,
    // and changing speed affects the next step without restarting the animation.
    private struct HingeMotion
    {
        private bool initialized;
        private Quaternion start;
        private Quaternion endpoint;
        private Quaternion lastPose;
        private float distance;
        private float progress;

        public Quaternion Step(Quaternion current, Quaternion target, float speed, float deltaTime)
        {
            if (!initialized || Quaternion.Angle(endpoint, target) > 0.001f ||
                Quaternion.Angle(lastPose, current) > 0.01f)
            {
                initialized = true;
                start = current;
                endpoint = target;
                distance = Quaternion.Angle(current, target);
                progress = 0f;
            }

            if (distance <= 0.001f)
                return lastPose = target;
            if (deltaTime <= 0f || speed <= 0f)
                return lastPose = current;

            progress = Mathf.Clamp01(progress + speed * deltaTime / distance);
            // SmoothStep over angular distance keeps the original angle/speed
            // duration, with no spring overshoot or asymptotic stopping.
            float eased = progress * progress * (3f - 2f * progress);
            lastPose = progress >= 1f
                ? target
                : Quaternion.Slerp(start, endpoint, eased);
            return lastPose;
        }
    }

    private void UpdateHingeTargets()
    {
        float signedAngle = clockwise ? -rotationAngle : rotationAngle;
        targetRotation = isOpen
            ? closedRotation * Quaternion.Euler(0f, 0f, signedAngle)
            : closedRotation;
        if (HasSecondaryHinge)
            secondaryTargetRotation = isOpen
                ? secondaryClosedRotation * Quaternion.Euler(0f, 0f, -signedAngle)
                : secondaryClosedRotation;
    }

    private void HandlePlayerInteraction()
    {
        if (isLocked && passwordLockEnabled)
        {
            OpenPasswordWindow();
            return;
        }

        bool wasLocked = isLocked;
        bool interactionSucceeded = TryInteract();
        if (wasLocked && !interactionSucceeded && isLocked)
        {
            lockedInteractionAcknowledged = true;
            ShowRequiredUnlockItemPopup();
            AddLockedInteractionJournalEntry();
        }
    }

    private void LateUpdate()
    {
        UpdateInteractionPrompt();
    }

    private void UpdateInteractionPrompt()
    {
        ZeldaFourWayMover controlledCharacter = !allowPlayerInteraction ||
                                                 DocumentReader.IsInputBlocked ||
                                                 aiPassageActive
            ? null
            : GetActiveControlledCharacterNearby();
        bool canInteract = controlledCharacter != null;
        if (!canInteract)
        {
            SetInteractionPromptVisible(false);
            return;
        }

        bool hasRequiredKey = HasRequiredUnlockItem();
        if (hasRequiredKey || !isLocked)
        {
            lockedInteractionAcknowledged = false;
        }
        string promptMessage = lockedInteractionAcknowledged && isLocked && !hasRequiredKey
            ? "锁住了"
            : "按[E]进行互动";

        // Interaction is gameplay; missing HUD/font must only suppress its label.
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledCharacter,
            interactKey,
            transform.position,
            SetInteractionPromptVisible);
        EnsureInteractionPrompt();
        if (interactionPromptObject == null)
            return;

        interactionPromptText.text = promptMessage;
        interactionPromptObject.transform.position =
            controlledCharacter.GetOverheadWorldPosition(interactionPromptOffset);
        interactionPromptObject.transform.rotation = Quaternion.identity;
        interactionPromptFont.RequestCharactersInTexture(
            promptMessage,
            72,
            FontStyle.Normal);
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
    }

    private bool HasRequiredUnlockItem()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        return inventory != null &&
            (ContainsConfiguredKey(
                 inventory,
                 requiredUnlockItemInstanceId) ||
             ContainsConfiguredKey(
                 inventory,
                 requiredUnlockItemInstanceId2) ||
             ContainsConfiguredKey(
                 inventory,
                 requiredUnlockItemInstanceId3));
    }

    private void ShowRequiredUnlockItemPopup()
    {
        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        if (ui == null)
            return;

        if (!HasConfiguredUnlockItem())
        {
            ui.ShowNotificationPopup("无法被打开");
            return;
        }

        string itemDisplayName = string.IsNullOrWhiteSpace(requiredUnlockItemName)
            ? "对应钥匙"
            : requiredUnlockItemName.Trim();
        ui.ShowJournalStyleNotificationPopup("需要" + itemDisplayName);
    }

    private void AddLockedInteractionJournalEntry()
    {
        if (!addJournalEntryWhenLockedInteractionFails ||
            (addLockedJournalEntryOnlyOnce && lockedJournalEntryWasAdded))
        {
            return;
        }

        QuestJournalManager.GetOrCreate().AddOrUpdateEntry(
            lockedJournalEntryId,
            lockedJournalEntryTitle,
            lockedJournalEntryDetails,
            gameObject.scene.name);
        lockedJournalEntryWasAdded = true;
    }

    private bool HasConfiguredUnlockItem()
    {
        return requiredUnlockItem != null ||
               requiredUnlockItem2 != null ||
               requiredUnlockItem3 != null ||
               requiredUnlockItemIdentity != null ||
               requiredUnlockItemIdentity2 != null ||
               requiredUnlockItemIdentity3 != null ||
               !string.IsNullOrWhiteSpace(requiredUnlockItemInstanceId) ||
               !string.IsNullOrWhiteSpace(requiredUnlockItemInstanceId2) ||
               !string.IsNullOrWhiteSpace(requiredUnlockItemInstanceId3);
    }

    private void EnsureInteractionPrompt()
    {
        if (interactionPromptObject != null)
            return;

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        interactionPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (interactionPromptFont == null)
            return;

        interactionPromptFont.RequestCharactersInTexture(
            "按[E]进行互动锁住了",
            72,
            FontStyle.Normal);

        interactionPromptObject = new GameObject(name + " Door Interaction Prompt");
        interactionPromptText = interactionPromptObject.AddComponent<TextMesh>();
        interactionPromptText.text = "按[E]进行互动";
        interactionPromptText.font = interactionPromptFont;
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
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        interactionPromptMaterial = new Material(interactionPromptFont.material)
        {
            name = name + " Door Interaction Prompt Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        interactionPromptMaterial.mainTexture = interactionPromptFont.material.mainTexture;
        promptRenderer.sharedMaterial = interactionPromptMaterial;
        interactionPromptObject.SetActive(false);
    }

    private void SetInteractionPromptVisible(bool visible)
    {
        if (interactionPromptObject != null
            && interactionPromptObject.activeSelf != visible)
        {
            interactionPromptObject.SetActive(visible);
        }
    }

    private void ToggleDoor()
    {
        // A normal player/script interaction only rotates the hinge. Collision is
        // suppressed exclusively while an AI passage is active, so recover the
        // authored collider state before applying a manual toggle. This also
        // repairs doors restored from saves made before this distinction existed.
        SetDoorCollisionEnabled(true);
        isOpen = !isOpen;
        UpdateHingeTargets();
    }

    public void ToggleFromExternal()
    {
        if (!aiPassageActive)
        {
            ToggleDoor();
        }
    }

    public bool TryInteract()
    {
        if (aiPassageActive)
        {
            return false;
        }

        if (isLocked && passwordLockEnabled)
        {
            return false;
        }

        if (isLocked && !TryUnlockWithInventoryItem())
        {
            return false;
        }

        ToggleDoor();
        return true;
    }

    /// <summary>
    /// Manual interaction used while the player is remotely controlling a
    /// clockwork puppet. It deliberately cannot consume inventory keys or
    /// open the password panel; a locked door must be pried by the puppet.
    /// </summary>
    public bool TryInteractFromClockworkPuppet()
    {
        if (!allowPlayerInteraction || aiPassageActive || isLocked)
        {
            return false;
        }

        ToggleDoor();
        return true;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (isLocked && aiPassageActive)
        {
            EndAiPassage();
        }
        if (!isLocked)
        {
            QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
        }
    }

    public void ApplyPersistentState(bool locked, bool open)
    {
        if (aiPassageActive)
        {
            EndAiPassage();
        }

        isLocked = locked;
        isOpen = open;
        UpdateHingeTargets();
        PrimaryHinge.localRotation = targetRotation;
        if (HasSecondaryHinge)
            secondaryHinge.localRotation = secondaryTargetRotation;

        // Persisted open/closed state represents the normal hinge state, not the
        // temporary AI passage state. Keeping collision disabled here made a door
        // restored as open impossible for attack hitboxes to detect or destroy.
        SetDoorCollisionEnabled(true);
        if (!isLocked)
        {
            QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
        }
    }

    private bool TryUnlockWithInventoryItem()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null)
        {
            return false;
        }

        string matchedInstanceId =
            FindMatchingUnlockItemInstanceId(inventory);
        if (string.IsNullOrEmpty(matchedInstanceId) ||
            !inventory.TryRemoveItemInstance(matchedInstanceId))
        {
            return false;
        }

        isLocked = false;
        QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
        return true;
    }

    private string FindMatchingUnlockItemInstanceId(
        PersistentInventory inventory)
    {
        if (ContainsConfiguredKey(
                inventory,
                requiredUnlockItemInstanceId))
        {
            return requiredUnlockItemInstanceId;
        }

        if (ContainsConfiguredKey(
                inventory,
                requiredUnlockItemInstanceId2))
        {
            return requiredUnlockItemInstanceId2;
        }

        if (ContainsConfiguredKey(
                inventory,
                requiredUnlockItemInstanceId3))
        {
            return requiredUnlockItemInstanceId3;
        }

        return string.Empty;
    }

    private static bool ContainsConfiguredKey(
        PersistentInventory inventory,
        string instanceId)
    {
        return inventory != null &&
            !string.IsNullOrWhiteSpace(instanceId) &&
            inventory.ContainsItemInstance(instanceId.Trim());
    }

    private void UpdateAiAutomaticDoor(float deltaTime)
    {
        if (aiPassageActive)
        {
            aiPassageTimer = Mathf.Max(0f, aiPassageTimer - Mathf.Max(0f, deltaTime));
            if (aiPassageTimer <= 0f || isLocked)
            {
                EndAiPassage();
            }

            return;
        }

        bool hasAiPathThroughDoor = HasAiPathThroughDoor();
        if (waitForAiToLeave)
        {
            if (!hasAiPathThroughDoor)
            {
                waitForAiToLeave = false;
            }

            return;
        }

        if (!isLocked && hasAiPathThroughDoor)
        {
            BeginAiPassage();
        }
    }

    private void BeginAiPassage()
    {
        aiPassageActive = true;
        aiPassageTimer = aiAutoCloseDelay;
        isOpen = true;
        UpdateHingeTargets();
        SetDoorCollisionEnabled(false);
    }

    private void EndAiPassage()
    {
        aiPassageActive = false;
        waitForAiToLeave = true;
        aiPassageTimer = 0f;
        isOpen = false;
        UpdateHingeTargets();
        SetDoorCollisionEnabled(true);
    }

    private bool HasAiPathThroughDoor()
    {
        if (doorColliders == null)
        {
            CacheDoorColliders();
        }

        foreach (ZeldaCharacterAiBase ai in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (ai == null || !ai.isActiveAndEnabled)
            {
                continue;
            }

            for (int colliderIndex = 0; colliderIndex < doorColliders.Length; colliderIndex++)
            {
                Collider2D doorCollider = doorColliders[colliderIndex];
                if (doorCollider == null || doorCollider.isTrigger ||
                    !doorColliderInitialStates[colliderIndex])
                {
                    continue;
                }

                if (ai.DoesCurrentPathIntersect(doorCollider, aiAutoOpenDistance))
                {
                    return true;
                }
            }
        }

        foreach (ClockworkPuppetRuntime puppet in
                 ClockworkPuppetRuntime.ActivePuppets)
        {
            if (puppet == null || !puppet.isActiveAndEnabled)
            {
                continue;
            }

            for (int colliderIndex = 0;
                 colliderIndex < doorColliders.Length;
                 colliderIndex++)
            {
                Collider2D doorCollider = doorColliders[colliderIndex];
                if (doorCollider == null || doorCollider.isTrigger ||
                    !doorColliderInitialStates[colliderIndex])
                {
                    continue;
                }

                if (puppet.DoesCurrentPathIntersect(
                        doorCollider,
                        aiAutoOpenDistance))
                {
                    return true;
                }
            }
        }

        foreach (ClockworkPuppetBoxDriver driver in
                 ClockworkPuppetBoxDriver.ActiveDrivers)
        {
            if (driver == null || !driver.isActiveAndEnabled)
            {
                continue;
            }

            for (int colliderIndex = 0;
                 colliderIndex < doorColliders.Length;
                 colliderIndex++)
            {
                Collider2D doorCollider = doorColliders[colliderIndex];
                if (doorCollider == null || doorCollider.isTrigger ||
                    !doorColliderInitialStates[colliderIndex])
                {
                    continue;
                }

                if (driver.DoesCurrentPathIntersect(
                        doorCollider,
                        aiAutoOpenDistance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CacheDoorColliders()
    {
        doorColliders = GetComponentsInChildren<Collider2D>(true);
        doorColliderInitialStates = new bool[doorColliders.Length];
        for (int i = 0; i < doorColliders.Length; i++)
        {
            doorColliderInitialStates[i] = doorColliders[i] != null && doorColliders[i].enabled;
        }

        // Handles protrude visually, but must not enlarge the original proximity
        // test (including its renderer fallback while AI disables colliders).
        Renderer[] candidates = GetComponentsInChildren<Renderer>(true);
        List<Renderer> interactionRenderers = new List<Renderer>(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            DoorData leaf = candidates[i].GetComponentInParent<DoorData>();
            if (leaf == null || !leaf.IsAttachedVisual(candidates[i].transform))
                interactionRenderers.Add(candidates[i]);
        }
        doorRenderers = interactionRenderers.ToArray();
    }

    private void SetDoorCollisionEnabled(bool enabled)
    {
        if (doorColliders == null)
        {
            CacheDoorColliders();
        }

        for (int i = 0; i < doorColliders.Length; i++)
        {
            Collider2D doorCollider = doorColliders[i];
            if (doorCollider == null || doorCollider.isTrigger)
            {
                continue;
            }

            doorCollider.enabled = enabled && doorColliderInitialStates[i];
        }
        NotifyVisionBlockerChanged();
    }

    private void NotifyVisionBlockerChanged()
    {
        if (!TryGetDoorBounds(out Bounds currentBounds))
        {
            CameraCircularVision.NotifyBlockersChanged();
            return;
        }

        Bounds affectedBounds = currentBounds;
        if (hasLastVisionBlockerBounds)
        {
            affectedBounds.Encapsulate(lastVisionBlockerBounds.min);
            affectedBounds.Encapsulate(lastVisionBlockerBounds.max);
        }
        lastVisionBlockerBounds = currentBounds;
        hasLastVisionBlockerBounds = true;
        CameraCircularVision.NotifyBlockersChanged(affectedBounds);
    }

    private bool TryGetDoorBounds(out Bounds bounds)
    {
        bounds = default(Bounds);
        bool found = false;
        if (doorColliders == null)
        {
            CacheDoorColliders();
        }

        for (int index = 0; index < doorColliders.Length; index++)
        {
            Collider2D doorCollider = doorColliders[index];
            if (doorCollider == null || doorCollider.isTrigger)
            {
                continue;
            }

            if (!found)
            {
                bounds = doorCollider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(doorCollider.bounds);
            }
        }
        return found;
    }

    private void CacheRequiredUnlockItemIdentity(bool generateInstanceId)
    {
        CacheUnlockItemIdentity(
            requiredUnlockItem,
            generateInstanceId,
            ref requiredUnlockItemInstanceId,
            true);
        CacheUnlockItemIdentity(
            requiredUnlockItem2,
            generateInstanceId,
            ref requiredUnlockItemInstanceId2,
            false);
        CacheUnlockItemIdentity(
            requiredUnlockItem3,
            generateInstanceId,
            ref requiredUnlockItemInstanceId3,
            false);
        CachePersistentUnlockIdentity(
            requiredUnlockItemIdentity,
            ref requiredUnlockItemInstanceId,
            true);
        CachePersistentUnlockIdentity(
            requiredUnlockItemIdentity2,
            ref requiredUnlockItemInstanceId2,
            false);
        CachePersistentUnlockIdentity(
            requiredUnlockItemIdentity3,
            ref requiredUnlockItemInstanceId3,
            false);

        requiredUnlockItemId = TrimOrEmpty(requiredUnlockItemId);
        requiredUnlockItemInstanceId =
            TrimOrEmpty(requiredUnlockItemInstanceId);
        requiredUnlockItemInstanceId2 =
            TrimOrEmpty(requiredUnlockItemInstanceId2);
        requiredUnlockItemInstanceId3 =
            TrimOrEmpty(requiredUnlockItemInstanceId3);
        requiredUnlockItemName = TrimOrEmpty(requiredUnlockItemName);
    }

    private void CachePersistentUnlockIdentity(
        PickupItemPersistentIdentity identity,
        ref string cachedInstanceId,
        bool isPrimarySlot)
    {
        if (identity == null)
        {
            return;
        }

        cachedInstanceId = identity.StableInstanceId;
        if (isPrimarySlot || string.IsNullOrWhiteSpace(requiredUnlockItemName))
        {
            requiredUnlockItemName = identity.DisplayName;
        }
    }

    private void CacheUnlockItemIdentity(
        PickupItemBase configuredItem,
        bool generateInstanceId,
        ref string cachedInstanceId,
        bool isPrimarySlot)
    {
        if (configuredItem == null ||
            string.IsNullOrWhiteSpace(configuredItem.ItemId))
        {
            cachedInstanceId = TrimOrEmpty(cachedInstanceId);
            return;
        }

        if (isPrimarySlot || string.IsNullOrWhiteSpace(requiredUnlockItemId))
        {
            requiredUnlockItemId = configuredItem.ItemId.Trim();
        }

        if (isPrimarySlot || string.IsNullOrWhiteSpace(requiredUnlockItemName))
        {
            requiredUnlockItemName =
                string.IsNullOrWhiteSpace(configuredItem.ItemName)
                    ? configuredItem.gameObject.name
                    : configuredItem.ItemName.Trim();
        }

            string instanceId = generateInstanceId
                ? configuredItem.UniqueInstanceId
                : configuredItem.ConfiguredUniqueInstanceId;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            cachedInstanceId = instanceId.Trim();
        }
    }

    private static string TrimOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private void OpenPasswordWindow()
    {
        if (!passwordLockEnabled || !isLocked || DoorPasswordPanel.IsOpen)
        {
            return;
        }

        if (passwordPanel == null)
        {
            passwordPanel = GetComponent<DoorPasswordPanel>();
            if (passwordPanel == null)
            {
                passwordPanel = gameObject.AddComponent<DoorPasswordPanel>();
            }
        }

        Font resolvedFont = passwordWindowFont;
        if (resolvedFont == null)
        {
            ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
            resolvedFont = ui != null ? ui.PermissionLabelFont : null;
        }
        passwordPanel.Open(
            resolvedFont,
            doorPassword,
            HandlePasswordResult);
        SetInteractionPromptVisible(false);
    }

    private void HandlePasswordResult(bool passwordCorrect)
    {
        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        if (!passwordCorrect)
        {
            if (ui != null)
            {
                ui.ShowNotificationPopup("密码错误");
            }
            return;
        }

        if (isLocked)
        {
            isLocked = false;
            lockedInteractionAcknowledged = false;
            ToggleDoor();
            QuestJournalManager.GetOrCreate().EvaluateKnownCompletionConditions();
        }
        if (ui != null)
        {
            ui.ShowNotificationPopup("已开启");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheRequiredUnlockItemIdentity(false);
        doorPassword = DoorPasswordPanel.NormalizePassword(doorPassword);
        aiAutoOpenDistance = Mathf.Max(0.1f, aiAutoOpenDistance);
        aiAutoCloseDelay = Mathf.Max(0.1f, aiAutoCloseDelay);
    }
#endif

    private ZeldaFourWayMover GetActiveControlledCharacterNearby()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null)
            return null;

        ZeldaCharacterData characterData = mover.GetComponent<ZeldaCharacterData>();
        if (characterData != null &&
            (characterData.IsDead || !characterData.CanInteractWithDoors))
            return null;

        return IsPointNearDoor(mover.transform.position, interactionDistance)
            ? mover
            : null;
    }

    private void OnDisable()
    {
        ActiveDoors.Remove(this);
        NotifyVisionBlockerChanged();
        SetInteractionPromptVisible(false);
        if (passwordPanel != null)
        {
            passwordPanel.CloseWithoutSubmission();
        }
    }

    private void OnDestroy()
    {
        ActiveDoors.Remove(this);
        NotifyVisionBlockerChanged();
        if (interactionPromptObject != null)
        {
            Destroy(interactionPromptObject);
        }

        if (interactionPromptMaterial != null)
        {
            Destroy(interactionPromptMaterial);
        }
    }

    private bool IsPointNearDoor(Vector3 point, float maximumDistance)
    {
        if (doorColliders == null || doorRenderers == null)
        {
            CacheDoorColliders();
        }

        float safeDistance = Mathf.Max(0f, maximumDistance);
        float maximumDistanceSquared = safeDistance * safeDistance;
        bool testedUsableGeometry = false;

        for (int i = 0; i < doorColliders.Length; i++)
        {
            Collider2D doorCollider = doorColliders[i];
            // ClosestPoint on a disabled Collider2D returns the query point.
            // Treating that result as geometry makes every world position
            // appear to be at distance zero from an opened door.
            if (doorCollider == null ||
                !doorCollider.enabled ||
                doorCollider.isTrigger ||
                !doorCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            testedUsableGeometry = true;
            Vector2 closestPoint = doorCollider.ClosestPoint(point);
            if (((Vector2)point - closestPoint).sqrMagnitude <=
                maximumDistanceSquared)
            {
                return true;
            }
        }

        // Open doors intentionally disable their solid colliders. Their
        // visible bounds remain a reliable proximity target so the player can
        // still close them, without reintroducing the disabled-collider bug.
        for (int i = 0; i < doorRenderers.Length; i++)
        {
            Renderer doorRenderer = doorRenderers[i];
            if (doorRenderer == null ||
                !doorRenderer.enabled ||
                !doorRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            testedUsableGeometry = true;
            Vector3 closestPoint = doorRenderer.bounds.ClosestPoint(point);
            if (((Vector2)(point - closestPoint)).sqrMagnitude <=
                maximumDistanceSquared)
            {
                return true;
            }
        }

        if (testedUsableGeometry)
        {
            return false;
        }

        // Editor-only/invisible doors may have no currently usable geometry.
        // In that case the hinge position is the only safe finite fallback.
        return ((Vector2)(point - transform.position)).sqrMagnitude <=
               maximumDistanceSquared;
    }
}
