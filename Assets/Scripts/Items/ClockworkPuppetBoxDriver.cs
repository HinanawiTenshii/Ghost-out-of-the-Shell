using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remote-control state for a clockwork puppet concealed inside a ground
/// cardboard box. The puppet drives the box directly; the box only supplies
/// its AI attraction, physical shell, and two-hit durability.
/// </summary>
public sealed class ClockworkPuppetBoxDriver : MonoBehaviour
{
    private static readonly HashSet<ClockworkPuppetBoxDriver> ActiveSet =
        new HashSet<ClockworkPuppetBoxDriver>();
    private static ClockworkPuppetBoxDriver remoteControlledDriver;
    private static int characterInputBlockedThroughFrame = -1;

    private readonly HashSet<DoorHingeInteraction> ignoredMovingDoors =
        new HashSet<DoorHingeInteraction>();
    private readonly List<DoorHingeInteraction> movingDoorCleanup =
        new List<DoorHingeInteraction>();

    private CardboardBoxPickupItem box;
    private ClockworkPuppetPickupItem puppet;
    private ZeldaFourWayMover remoteControlOwner;
    private ZeldaReusableGridNavigator navigator;
    private Rigidbody2D physicsBody;
    private Collider2D drivingCollider;
    private bool originalTriggerState;
    private bool addedPhysicsBody;
    private bool addedNavigator;
    private float magic;
    private bool disembarking;
    private string itemInstanceId;
    private string itemName;
    private string itemDescription;
    private bool hasVisualColor;
    private Color visualColor;
    private Vector2 previousPhysicsPosition;
    private float lastMovementTime = float.NegativeInfinity;
    private CameraVisionRevealSource revealSource;
    private CameraVisionStreamingExempt streamingExemption;
    private bool addedStreamingExemption;
    private CameraVisionStreamingExempt ownerStreamingExemption;
    private bool addedOwnerStreamingExemption;

    private Vector2 manualMoveDirection;
    private int manualInputSequence;
    private int leftInputOrder;
    private int rightInputOrder;
    private int downInputOrder;
    private int upInputOrder;
    private Vector2 previousManualRawInput;
    private Vector2 lastAnalogMoveDirection;

    private DoorHingeInteraction pryTargetDoor;
    private float pryHoldTimer;
    private ZeldaPossessionProgressBar pryProgressBar;
    private GameObject promptObject;
    private TextMesh promptText;
    private Font promptFont;
    private Material promptMaterial;

    public bool IsMoving => !disembarking &&
        Time.time - lastMovementTime <= 0.15f;
    public float RemainingMagic => magic;
    public bool IsUnderRemoteControl => remoteControlledDriver == this;
    public static bool IsRemoteControlActive => remoteControlledDriver != null;
    public static bool BlocksCharacterInputThroughFrame =>
        Time.frameCount <= characterInputBlockedThroughFrame;
    public static Transform RemoteControlTargetTransform =>
        remoteControlledDriver != null ? remoteControlledDriver.transform : null;
    public static IReadOnlyCollection<ClockworkPuppetBoxDriver> ActiveDrivers =>
        ActiveSet;

    public static void EndActiveRemoteControl()
    {
        if (remoteControlledDriver != null)
        {
            remoteControlledDriver.EndRemoteControl();
        }
    }

    public bool DoesCurrentPathIntersect(
        Collider2D targetCollider,
        float lookAheadDistance)
    {
        // Concealed puppets are manually driven, so doors must wait for E.
        return false;
    }

    private void OnEnable()
    {
        ActiveSet.Add(this);
        if (revealSource != null)
        {
            revealSource.enabled = true;
        }
    }

    private void OnDisable()
    {
        ActiveSet.Remove(this);
        EndRemoteControl();
        RestoreMovingDoorCollisions();
        if (revealSource != null)
        {
            revealSource.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveSet.Clear();
        remoteControlledDriver = null;
        characterInputBlockedThroughFrame = -1;
    }

    public void Configure(
        CardboardBoxPickupItem owner,
        ClockworkPuppetPickupItem source,
        float initialMagic,
        string configuredInstanceId,
        string configuredItemName,
        string configuredItemDescription,
        bool configuredHasVisualColor,
        Color configuredVisualColor)
    {
        box = owner;
        puppet = source;
        magic = source != null
            ? Mathf.Clamp(initialMagic, 0f, source.MaximumMagic)
            : 0f;
        itemInstanceId = configuredInstanceId;
        itemName = configuredItemName;
        itemDescription = configuredItemDescription;
        hasVisualColor = configuredHasVisualColor;
        visualColor = configuredVisualColor;
        remoteControlOwner = ZeldaRuntimeRegistry.GetControlledMover();

        drivingCollider = GetComponent<Collider2D>();
        if (drivingCollider != null)
        {
            originalTriggerState = drivingCollider.isTrigger;
            drivingCollider.isTrigger = false;
        }

        physicsBody = GetComponent<Rigidbody2D>();
        if (physicsBody == null)
        {
            physicsBody = gameObject.AddComponent<Rigidbody2D>();
            addedPhysicsBody = true;
        }
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        physicsBody.gravityScale = 0f;
        physicsBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        physicsBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        physicsBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        navigator = GetComponent<ZeldaReusableGridNavigator>();
        if (navigator == null)
        {
            navigator = gameObject.AddComponent<ZeldaReusableGridNavigator>();
            addedNavigator = true;
        }
        navigator.Configure(physicsBody, drivingCollider);
        navigator.ConfigureNavigationDebug(
            source != null && source.ShowNavigationDebug,
            source != null
                ? source.NavigationPathColor
                : new Color(0.15f, 0.85f, 1f, 0.9f),
            source != null
                ? source.NavigationTargetColor
                : new Color(1f, 0.25f, 0.75f, 1f),
            source != null ? source.NavigationPathWidth : 0.035f,
            source != null ? source.NavigationTargetMarkerSize : 0.18f,
            source != null ? source.NavigationDebugSortingOrder : 200);

        revealSource = GetComponent<CameraVisionRevealSource>();
        if (revealSource == null)
        {
            revealSource = gameObject.AddComponent<CameraVisionRevealSource>();
        }
        revealSource.Configure(source != null ? source.VisionRevealRadius : 3f);

        streamingExemption = GetComponent<CameraVisionStreamingExempt>();
        if (streamingExemption == null)
        {
            streamingExemption =
                gameObject.AddComponent<CameraVisionStreamingExempt>();
            addedStreamingExemption = true;
        }

        previousPhysicsPosition = physicsBody.position;
        if (magic <= 0f)
        {
            box.DestroyStoredClockworkPuppet();
            return;
        }
        BeginRemoteControl();
    }

    private void Update()
    {
        if (disembarking || box == null || puppet == null ||
            box.IsDestroyed || !box.HasStoredItem)
        {
            EndRemoteControl();
            return;
        }
        if (magic <= 0f)
        {
            EndRemoteControl();
            box.DestroyStoredClockworkPuppet();
            return;
        }
        if (!IsUnderRemoteControl)
        {
            manualMoveDirection = Vector2.zero;
            ResetPryProgress();
            SetPromptVisible(false);
            return;
        }
        if (DocumentReader.IsInputBlocked)
        {
            manualMoveDirection = Vector2.zero;
            ResetPryProgress();
            SetPromptVisible(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            EndRemoteControl();
            return;
        }

        manualMoveDirection = ReadLastInputFourWayDirection();
        DoorHingeInteraction nearbyDoor = FindNearestManualDoor(
            out float doorDistance);
        LeverData nearbyLever = FindNearestManualLever(
            out float leverDistance);
        bool useDoor = nearbyDoor != null &&
            (nearbyLever == null || doorDistance <= leverDistance);

        if (useDoor)
        {
            if (nearbyDoor.IsLocked)
            {
                ShowPrompt(nearbyDoor.CanBeLockpicked
                    ? "按住[E]撬锁"
                    : "按[E]尝试撬锁");
                UpdateDoorPry(nearbyDoor);
            }
            else
            {
                ResetPryProgress();
                ShowPrompt("按[E]进行互动");
                if (Input.GetKeyDown(KeyCode.E))
                {
                    nearbyDoor.TryInteractFromClockworkPuppet();
                }
            }
            return;
        }

        ResetPryProgress();
        if (nearbyLever != null)
        {
            ShowPrompt("按[E]脱离纸箱并附着机关");
            if (Input.GetKeyDown(KeyCode.E))
            {
                DisembarkPuppet(nearbyLever);
            }
            return;
        }
        SetPromptVisible(false);
    }

    private void FixedUpdate()
    {
        RefreshMovingDoorCollisionIgnores();
        UpdateMovementObservation();
        if (disembarking || box == null || puppet == null ||
            box.IsDestroyed || !box.HasStoredItem ||
            !IsUnderRemoteControl || DocumentReader.IsInputBlocked)
        {
            if (physicsBody != null)
            {
                physicsBody.velocity = Vector2.zero;
            }
            return;
        }
        if (manualMoveDirection.sqrMagnitude <= 0.0001f)
        {
            physicsBody.velocity = Vector2.zero;
            return;
        }

        Vector2 displacement = navigator != null
            ? navigator.GetSafeDisplacement(
                manualMoveDirection,
                puppet.MoveSpeed * Time.fixedDeltaTime)
            : manualMoveDirection * puppet.MoveSpeed * Time.fixedDeltaTime;
        if (displacement.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        box.MoveGroundBoxWithNavigation(physicsBody, displacement);
        lastMovementTime = Time.time;
        SpendMagic(puppet.MovementMagicPerSecond * Time.fixedDeltaTime);
    }

    private void BeginRemoteControl()
    {
        if (disembarking || box == null || box.IsDestroyed)
        {
            return;
        }
        if (remoteControlledDriver != null && remoteControlledDriver != this)
        {
            remoteControlledDriver.EndRemoteControl();
        }

        remoteControlledDriver = this;
        characterInputBlockedThroughFrame = Time.frameCount;
        EnsureOwnerStreamingExemption();
        manualMoveDirection = Vector2.zero;
        ResetManualDirectionInput();
        ResetPryProgress();
        SetPromptVisible(false);
        if (navigator != null)
        {
            navigator.InvalidatePath();
        }
    }

    private void EndRemoteControl()
    {
        if (remoteControlledDriver != this)
        {
            return;
        }

        remoteControlledDriver = null;
        characterInputBlockedThroughFrame = Time.frameCount;
        ReleaseOwnerStreamingExemption();
        manualMoveDirection = Vector2.zero;
        ResetManualDirectionInput();
        ResetPryProgress();
        SetPromptVisible(false);
        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
        }
    }

    private void UpdateMovementObservation()
    {
        if (physicsBody == null)
        {
            return;
        }
        Vector2 currentPosition = physicsBody.position;
        if ((currentPosition - previousPhysicsPosition).sqrMagnitude >
            0.000001f)
        {
            lastMovementTime = Time.time;
        }
        previousPhysicsPosition = currentPosition;
    }

    private LeverData FindNearestManualLever(out float closestDistance)
    {
        LeverData closest = null;
        closestDistance = float.MaxValue;
        float allowedDistance = puppet != null
            ? puppet.ManualInteractionDistance
            : 0.85f;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled)
            {
                continue;
            }
            float distance = lever.GetSurfaceDistanceTo(
                drivingCollider,
                transform.position);
            if (distance <= allowedDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closest = lever;
            }
        }
        return closest;
    }

    private DoorHingeInteraction FindNearestManualDoor(
        out float closestDistance)
    {
        DoorHingeInteraction closest = null;
        closestDistance = float.MaxValue;
        foreach (DoorHingeInteraction door in DoorHingeInteraction.WorldDoors)
        {
            if (door == null || !door.isActiveAndEnabled ||
                !door.AllowPlayerInteraction)
            {
                continue;
            }
            float distance = door.GetSurfaceDistanceTo(
                drivingCollider,
                transform.position);
            float allowedDistance = door.IsLocked
                ? Mathf.Max(
                    puppet.LockedDoorPryDistance,
                    puppet.ManualInteractionDistance)
                : puppet.ManualInteractionDistance;
            if (distance <= allowedDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closest = door;
            }
        }
        return closest;
    }

    private void UpdateDoorPry(DoorHingeInteraction door)
    {
        if (door == null || !door.CanBeLockpicked)
        {
            ResetPryProgress();
            if (door != null && Input.GetKeyDown(KeyCode.E) &&
                ZeldaHealthHeartsUI.Instance != null)
            {
                ZeldaHealthHeartsUI.Instance.ShowNotificationPopup(
                    "这个锁太复杂了");
            }
            return;
        }

        float pryCost = puppet.MaximumMagic * Mathf.Clamp01(
            puppet.LockedDoorPryMagicFraction);
        if (!Input.GetKey(KeyCode.E))
        {
            ResetPryProgress();
            return;
        }
        if (magic + 0.0001f < pryCost)
        {
            ResetPryProgress();
            if (Input.GetKeyDown(KeyCode.E) &&
                ZeldaHealthHeartsUI.Instance != null)
            {
                ZeldaHealthHeartsUI.Instance.ShowNotificationPopup(
                    "魔像能量不足");
            }
            return;
        }

        if (pryTargetDoor != door)
        {
            ResetPryProgress();
            pryTargetDoor = door;
        }
        pryHoldTimer += Time.deltaTime;
        EnsurePryProgressBar();
        if (pryProgressBar != null)
        {
            pryProgressBar.transform.rotation = Quaternion.identity;
            pryProgressBar.SetProgress(
                pryHoldTimer /
                    Mathf.Max(0.1f, puppet.LockedDoorPryHoldDuration),
                new Color(0.92f, 0.12f, 0.1f, 1f));
        }
        if (pryHoldTimer < puppet.LockedDoorPryHoldDuration)
        {
            return;
        }

        door.SetLocked(false);
        ResetPryProgress();
        SpendMagic(pryCost);
    }

    private void ResetPryProgress()
    {
        pryTargetDoor = null;
        pryHoldTimer = 0f;
        if (pryProgressBar != null)
        {
            pryProgressBar.Hide();
        }
    }

    private void EnsurePryProgressBar()
    {
        Vector3 localPosition = new Vector3(0f, -0.82f, 0f);
        if (pryProgressBar != null)
        {
            pryProgressBar.transform.localPosition = localPosition;
            return;
        }

        GameObject progressObject = new GameObject(
            "Boxed Puppet Lock Pry Progress");
        progressObject.transform.SetParent(transform, false);
        progressObject.transform.localPosition = localPosition;
        progressObject.transform.localScale = Vector3.one * 0.8f;
        pryProgressBar =
            progressObject.AddComponent<ZeldaPossessionProgressBar>();
        pryProgressBar.Hide();
    }

    private void SpendMagic(float amount)
    {
        magic = Mathf.Max(0f, magic - Mathf.Max(0f, amount));
        if (magic > 0f || box == null)
        {
            return;
        }

        EndRemoteControl();
        box.DestroyStoredClockworkPuppet();
    }

    private void DisembarkPuppet(LeverData lever)
    {
        if (disembarking || box == null || puppet == null || lever == null ||
            !lever.isActiveAndEnabled)
        {
            return;
        }

        disembarking = true;
        Vector3 spawnPosition = drivingCollider != null
            ? drivingCollider.bounds.center
            : (Vector3)box.WorldCenter;
        ClockworkPuppetPickupItem source = puppet;
        ZeldaFourWayMover ownerMover = remoteControlOwner != null &&
            remoteControlOwner.isActiveAndEnabled
            ? remoteControlOwner
            : ZeldaRuntimeRegistry.GetControlledMover();
        ZeldaCharacterData ownerData = ownerMover != null
            ? ownerMover.GetComponent<ZeldaCharacterData>()
            : null;
        float transferredMagic = magic;

        EndRemoteControl();
        if (!box.TryDetachStoredClockworkPuppet(source))
        {
            disembarking = false;
            BeginRemoteControl();
            return;
        }
        if (drivingCollider != null)
        {
            drivingCollider.isTrigger = originalTriggerState;
        }
        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();
        source.DeployControlledFromCardboardBox(
            spawnPosition,
            lever,
            ownerData,
            transferredMagic,
            itemInstanceId,
            itemName,
            itemDescription,
            hasVisualColor,
            visualColor);
        enabled = false;
    }

    private Vector2 ReadLastInputFourWayDirection()
    {
        RecordDirectionalKeyPresses();
        Vector2 rawInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));
        Vector2 keyboardDirection = GetMostRecentHeldKeyboardDirection(
            out bool hasHeldKeyboardDirection);
        if (hasHeldKeyboardDirection)
        {
            previousManualRawInput = rawInput;
            return keyboardDirection;
        }

        Vector2 analogDirection = ResolveAnalogFourWayDirection(rawInput);
        previousManualRawInput = rawInput;
        return analogDirection;
    }

    private void RecordDirectionalKeyPresses()
    {
        if (Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.LeftArrow))
            leftInputOrder = ++manualInputSequence;
        if (Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.RightArrow))
            rightInputOrder = ++manualInputSequence;
        if (Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.DownArrow))
            downInputOrder = ++manualInputSequence;
        if (Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow))
            upInputOrder = ++manualInputSequence;
    }

    private Vector2 GetMostRecentHeldKeyboardDirection(
        out bool hasHeldDirection)
    {
        hasHeldDirection = false;
        int newestOrder = int.MinValue;
        Vector2 direction = Vector2.zero;
        ConsiderHeldDirection(
            Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
            leftInputOrder,
            Vector2.left,
            ref hasHeldDirection,
            ref newestOrder,
            ref direction);
        ConsiderHeldDirection(
            Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow),
            rightInputOrder,
            Vector2.right,
            ref hasHeldDirection,
            ref newestOrder,
            ref direction);
        ConsiderHeldDirection(
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
            downInputOrder,
            Vector2.down,
            ref hasHeldDirection,
            ref newestOrder,
            ref direction);
        ConsiderHeldDirection(
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
            upInputOrder,
            Vector2.up,
            ref hasHeldDirection,
            ref newestOrder,
            ref direction);
        return direction;
    }

    private static void ConsiderHeldDirection(
        bool isHeld,
        int inputOrder,
        Vector2 candidate,
        ref bool hasHeldDirection,
        ref int newestOrder,
        ref Vector2 direction)
    {
        if (!isHeld || (hasHeldDirection && inputOrder <= newestOrder))
        {
            return;
        }
        hasHeldDirection = true;
        newestOrder = inputOrder;
        direction = candidate;
    }

    private Vector2 ResolveAnalogFourWayDirection(Vector2 input)
    {
        const float threshold = 0.01f;
        bool horizontalActive = Mathf.Abs(input.x) > threshold;
        bool verticalActive = Mathf.Abs(input.y) > threshold;
        if (!horizontalActive && !verticalActive)
        {
            lastAnalogMoveDirection = Vector2.zero;
            return Vector2.zero;
        }

        Vector2 horizontalDirection = horizontalActive
            ? new Vector2(Mathf.Sign(input.x), 0f)
            : Vector2.zero;
        Vector2 verticalDirection = verticalActive
            ? new Vector2(0f, Mathf.Sign(input.y))
            : Vector2.zero;
        if (!horizontalActive)
        {
            lastAnalogMoveDirection = verticalDirection;
            return verticalDirection;
        }
        if (!verticalActive)
        {
            lastAnalogMoveDirection = horizontalDirection;
            return horizontalDirection;
        }

        bool horizontalChanged =
            Mathf.Abs(previousManualRawInput.x) <= threshold ||
            Mathf.Sign(previousManualRawInput.x) != Mathf.Sign(input.x);
        bool verticalChanged =
            Mathf.Abs(previousManualRawInput.y) <= threshold ||
            Mathf.Sign(previousManualRawInput.y) != Mathf.Sign(input.y);
        if (horizontalChanged != verticalChanged)
        {
            lastAnalogMoveDirection = horizontalChanged
                ? horizontalDirection
                : verticalDirection;
            return lastAnalogMoveDirection;
        }

        bool lastDirectionStillActive =
            (lastAnalogMoveDirection.x != 0f &&
             Mathf.Sign(lastAnalogMoveDirection.x) == Mathf.Sign(input.x)) ||
            (lastAnalogMoveDirection.y != 0f &&
             Mathf.Sign(lastAnalogMoveDirection.y) == Mathf.Sign(input.y));
        if (lastDirectionStillActive)
        {
            return lastAnalogMoveDirection;
        }
        lastAnalogMoveDirection = Mathf.Abs(input.x) > Mathf.Abs(input.y)
            ? horizontalDirection
            : verticalDirection;
        return lastAnalogMoveDirection;
    }

    private void ResetManualDirectionInput()
    {
        manualInputSequence = 0;
        leftInputOrder = 0;
        rightInputOrder = 0;
        downInputOrder = 0;
        upInputOrder = 0;
        previousManualRawInput = Vector2.zero;
        lastAnalogMoveDirection = Vector2.zero;
    }

    private void ShowPrompt(string message)
    {
        EnsurePrompt();
        if (promptObject == null)
        {
            return;
        }
        promptText.text = message;
        promptFont.RequestCharactersInTexture(message, 72, FontStyle.Normal);
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        promptObject.transform.position =
            transform.position + Vector3.up * 1.02f;
        promptObject.transform.rotation = Quaternion.identity;
        SetPromptVisible(true);
    }

    private void EnsurePrompt()
    {
        if (promptObject != null)
        {
            return;
        }
        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        promptFont = ui != null ? ui.PermissionLabelFont : null;
        if (promptFont == null)
        {
            return;
        }

        promptObject = new GameObject("Boxed Clockwork Puppet Prompt");
        promptText = promptObject.AddComponent<TextMesh>();
        promptText.font = promptFont;
        promptText.fontSize = 72;
        promptText.characterSize = 0.035f;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.color = ZeldaUiPalette.Primary;
        MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        promptMaterial = new Material(promptFont.material)
        {
            name = "Boxed Clockwork Puppet Prompt Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        renderer.sharedMaterial = promptMaterial;
        promptObject.SetActive(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptObject != null && promptObject.activeSelf != visible)
        {
            promptObject.SetActive(visible);
        }
    }

    private void EnsureOwnerStreamingExemption()
    {
        if (ownerStreamingExemption != null)
        {
            return;
        }
        ZeldaFourWayMover owner = remoteControlOwner != null &&
            remoteControlOwner.isActiveAndEnabled
            ? remoteControlOwner
            : ZeldaRuntimeRegistry.GetControlledMover();
        if (owner == null)
        {
            return;
        }
        remoteControlOwner = owner;
        ownerStreamingExemption = owner.GetComponentInParent<
            CameraVisionStreamingExempt>(true);
        if (ownerStreamingExemption == null)
        {
            ownerStreamingExemption = owner.gameObject.AddComponent<
                CameraVisionStreamingExempt>();
            addedOwnerStreamingExemption = true;
        }
    }

    private void ReleaseOwnerStreamingExemption()
    {
        if (addedOwnerStreamingExemption && ownerStreamingExemption != null)
        {
            Destroy(ownerStreamingExemption);
        }
        ownerStreamingExemption = null;
        addedOwnerStreamingExemption = false;
    }

    private void RefreshMovingDoorCollisionIgnores()
    {
        if (drivingCollider == null)
        {
            return;
        }
        movingDoorCleanup.Clear();
        foreach (DoorHingeInteraction ignoredDoor in ignoredMovingDoors)
        {
            if (ignoredDoor == null || !ignoredDoor.isActiveAndEnabled ||
                !ignoredDoor.IsChangingOpenState)
            {
                movingDoorCleanup.Add(ignoredDoor);
            }
        }
        for (int i = 0; i < movingDoorCleanup.Count; i++)
        {
            DoorHingeInteraction door = movingDoorCleanup[i];
            if (door != null)
            {
                door.SetCollisionIgnoredWith(drivingCollider, false);
            }
            ignoredMovingDoors.Remove(door);
        }
        foreach (DoorHingeInteraction door in DoorHingeInteraction.WorldDoors)
        {
            if (door == null || !door.isActiveAndEnabled ||
                !door.IsChangingOpenState || ignoredMovingDoors.Contains(door))
            {
                continue;
            }
            door.SetCollisionIgnoredWith(drivingCollider, true);
            ignoredMovingDoors.Add(door);
        }
    }

    private void RestoreMovingDoorCollisions()
    {
        if (drivingCollider != null)
        {
            foreach (DoorHingeInteraction door in ignoredMovingDoors)
            {
                if (door != null)
                {
                    door.SetCollisionIgnoredWith(drivingCollider, false);
                }
            }
        }
        ignoredMovingDoors.Clear();
        movingDoorCleanup.Clear();
    }

    private void OnDestroy()
    {
        ActiveSet.Remove(this);
        EndRemoteControl();
        RestoreMovingDoorCollisions();
        if (revealSource != null)
        {
            Destroy(revealSource);
        }
        if (addedStreamingExemption && streamingExemption != null)
        {
            Destroy(streamingExemption);
        }
        if (drivingCollider != null)
        {
            drivingCollider.isTrigger = originalTriggerState;
        }
        if (addedNavigator && navigator != null)
        {
            Destroy(navigator);
        }
        if (addedPhysicsBody && physicsBody != null)
        {
            Destroy(physicsBody);
        }
        if (promptObject != null)
        {
            Destroy(promptObject);
        }
        if (promptMaterial != null)
        {
            Destroy(promptMaterial);
        }
    }
}
