using System.Collections.Generic;
using UnityEngine;

/// <summary>World behaviour for a deployed clockwork puppet.</summary>
public sealed class ClockworkPuppetRuntime : MonoBehaviour
{
    private static readonly HashSet<ClockworkPuppetRuntime> ActiveSet =
        new HashSet<ClockworkPuppetRuntime>();
    private static readonly Vector3 PryProgressLocalPosition =
        new Vector3(0f, -0.7f, 0f);
    private static ClockworkPuppetRuntime remoteControlledPuppet;
    private static int characterInputBlockedThroughFrame = -1;

    private LeverData targetLever;
    private FortressBallista targetBallista;
    private readonly List<Collider2D> ignoredBallistaColliders = new List<Collider2D>();
    private Rigidbody2D physicsBody;
    private Collider2D bodyCollider;
    private ZeldaReusableGridNavigator navigator;
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
    private readonly List<Collider2D> ignoredLeverColliders = new List<Collider2D>();
    private readonly HashSet<DoorHingeInteraction> ignoredMovingDoors =
        new HashSet<DoorHingeInteraction>();
    private readonly List<DoorHingeInteraction> movingDoorCleanup =
        new List<DoorHingeInteraction>();
    private Transform initialOwner;
    private ZeldaFourWayMover remoteControlOwner;
    private SpriteRenderer bodyRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;
    private int lastVisibleEnergySegments = -1;
    private int lastVisibleWalkPose = -1;
    private int walkPose;
    private float walkElapsed;
    private float walkMotionRemaining;
    private Vector2 previousWalkPosition;
    private bool hasWalkPosition;
    private float magic;
    private float operationTimer;
    private bool attached;
    private bool broken;
    private string itemId;
    private string itemInstanceId;
    private string itemName;
    private string itemDescription;
    private bool hasVisualColor;
    private Color visualColor;
    private int maxStackSize;
    private float maximumMagic;
    private float moveSpeed;
    private float AreaMoveSpeed => moveSpeed * SpeedBoostArea.GetSpeedMultiplier(this);
    private float movementMagicPerSecond;
    private float operationMagicCost;
    private float operationInterval;
    private float leverContactDistance;
    private float leverSearchRadius;
    private float reclaimDistance;
    private float lockedDoorPryDistance;
    private float manualInteractionDistance;
    private float lockedDoorPryHoldDuration;
    private float lockedDoorPryMagicFraction;
    private bool manualControlMode;
    private int remoteControlEnteredFrame = -1;
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
    private GameObject reclaimPromptObject;
    private TextMesh reclaimPromptText;
    private Font reclaimPromptFont;
    private Material reclaimPromptMaterial;
    private CameraVisionStreamingExempt controlledCharacterStreamingExemption;
    private bool addedControlledCharacterStreamingExemption;

    public LeverData TargetLever => targetLever;
    public float RemainingMagic => magic;
    public bool IsBroken => broken;
    public string PuppetInstanceId => itemInstanceId;
    public bool TryResumeRemoteControl(ZeldaCharacterData user)
    {
        var mover = user != null ? user.GetComponent<ZeldaFourWayMover>() : null;
        if (!isActiveAndEnabled || broken || magic <= 0f || mover == null || !mover.isActiveAndEnabled ||
            user.IsDead || user.IsGhostLike || ZeldaRuntimeRegistry.GetControlledMover() != mover ||
            ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject) != gameObject.scene) return false;
        // Release any previous owner's streaming lease before changing the controlling character.
        ReleaseControlledCharacterStreamingExemption();
        remoteControlOwner = mover;
        BeginRemoteControl();return IsUnderRemoteControl;
    }
    public static void EndActiveRemoteControl()
    {
        if (remoteControlledPuppet != null) remoteControlledPuppet.EndRemoteControl();
    }
    public bool IsUnderRemoteControl => remoteControlledPuppet == this;
    public string SaveSourceItemId => itemId;
    public LeverData SaveTargetLever => targetLever;
    public FortressBallista SaveTargetBallista => targetBallista;
    public bool IsAttachedToBallista(FortressBallista ballista) => attached && targetBallista != null && targetBallista == ballista;
    public bool SaveAttached => attached;
    public void RestoreSavedControl(LeverData lever, bool wasAttached, bool wasRemote, FortressBallista ballista = null)
    {
        remoteControlOwner = ZeldaRuntimeRegistry.GetControlledMover();
        initialOwner = remoteControlOwner != null ? remoteControlOwner.transform : null;
        manualControlMode = true;
        targetLever = lever;
        attached = false;
        AssignTargetBallista(null);
        if (wasAttached && ballista != null) AttachToBallista(ballista);
        else if (wasAttached && lever != null) AttachToLever(lever);
        if (wasRemote) BeginRemoteControl(); else EndRemoteControl();
    }
    public static ClockworkPuppetRuntime RemoteControlledPuppet =>
        remoteControlledPuppet;
    public static Transform RemoteControlTargetTransform =>
        remoteControlledPuppet != null
            ? remoteControlledPuppet.transform
            : ClockworkPuppetBoxDriver.RemoteControlTargetTransform;
    public static bool IsRemoteControlActive =>
        remoteControlledPuppet != null ||
        ClockworkPuppetBoxDriver.IsRemoteControlActive;
    public static bool BlocksCharacterInput =>
        IsRemoteControlActive ||
        Time.frameCount <= characterInputBlockedThroughFrame ||
        ClockworkPuppetBoxDriver.BlocksCharacterInputThroughFrame;
    public static IReadOnlyCollection<ClockworkPuppetRuntime> ActivePuppets =>
        ActiveSet;

    public bool DoesCurrentPathIntersect(
        Collider2D targetCollider,
        float lookAheadDistance)
    {
        return !broken && navigator != null &&
            navigator.DoesCurrentPathIntersect(
                targetCollider,
                lookAheadDistance);
    }

    private void OnEnable()
    {
        ResetWalkAnimation();
        ActiveSet.Add(this);
    }

    private void OnDisable()
    {
        ResetWalkAnimation();
        ActiveSet.Remove(this);
        EndRemoteControl();
        RestoreMovingDoorCollisions();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveSet.Clear();
        remoteControlledPuppet = null;
        characterInputBlockedThroughFrame = -1;
    }

    public void SetInventoryIdentity(
        string configuredInstanceId,
        string configuredItemName,
        string configuredDescription,
        bool configuredHasVisualColor,
        Color configuredVisualColor)
    {
        if (!string.IsNullOrWhiteSpace(configuredInstanceId))
        {
            itemInstanceId = configuredInstanceId.Trim();
        }
        itemName = configuredItemName ?? string.Empty;
        itemDescription = configuredDescription ?? string.Empty;
        hasVisualColor = configuredHasVisualColor;
        visualColor = configuredHasVisualColor
            ? configuredVisualColor
            : Color.white;
    }

    public void Configure(
        ClockworkPuppetPickupItem configuredSource,
        ZeldaCharacterData deployingCharacter,
        LeverData preferredLever = null,
        float initialMagic = -1f)
    {
        itemId = configuredSource != null
            ? configuredSource.ItemId
            : ClockworkPuppetPickupItem.DefaultItemId;
        itemInstanceId = configuredSource != null
            ? configuredSource.UniqueInstanceId
            : System.Guid.NewGuid().ToString("N");
        itemName = configuredSource != null
            ? configuredSource.ItemName
            : string.Empty;
        itemDescription = configuredSource != null
            ? configuredSource.ItemDescription
            : string.Empty;
        hasVisualColor = configuredSource != null &&
            configuredSource.ItemVisual != null;
        visualColor = hasVisualColor
            ? configuredSource.ItemVisual.DisplayColor
            : Color.white;
        maxStackSize = configuredSource != null ? configuredSource.MaxStackSize : 1;
        maximumMagic = configuredSource != null ? configuredSource.MaximumMagic : 1f;
        moveSpeed = configuredSource != null ? configuredSource.MoveSpeed : 1.35f;
        movementMagicPerSecond = configuredSource != null ? configuredSource.MovementMagicPerSecond : 0.45f;
        operationMagicCost = configuredSource != null ? configuredSource.OperationMagicCost : 1f;
        operationInterval = configuredSource != null ? configuredSource.OperationInterval : 3f;
        leverContactDistance = configuredSource != null ? configuredSource.LeverContactDistance : 0.18f;
        leverSearchRadius = configuredSource != null
            ? configuredSource.LeverSearchRadius
            : 0f;
        reclaimDistance = configuredSource != null ? configuredSource.ReclaimDistance : 1.15f;
        lockedDoorPryDistance = configuredSource != null ? configuredSource.LockedDoorPryDistance : 0.7f;
        manualInteractionDistance = configuredSource != null
            ? configuredSource.ManualInteractionDistance
            : 0.85f;
        lockedDoorPryHoldDuration = configuredSource != null
            ? configuredSource.LockedDoorPryHoldDuration
            : 3f;
        lockedDoorPryMagicFraction = configuredSource != null
            ? configuredSource.LockedDoorPryMagicFraction
            : 0.5f;
        manualControlMode = deployingCharacter != null;
        remoteControlOwner = deployingCharacter != null
            ? deployingCharacter.GetComponent<ZeldaFourWayMover>()
            : null;
        magic = initialMagic >= 0f
            ? Mathf.Clamp(initialMagic, 0f, maximumMagic)
            : maximumMagic;
        transform.localScale = Vector3.one * (configuredSource != null ? configuredSource.DeployedScale : 0.58f);
        physicsBody = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        navigator = GetComponent<ZeldaReusableGridNavigator>();
        if (navigator == null) navigator = gameObject.AddComponent<ZeldaReusableGridNavigator>();
        navigator.Configure(physicsBody, bodyCollider);
        navigator.ConfigureNavigationDebug(
            configuredSource != null && configuredSource.ShowNavigationDebug,
            configuredSource != null
                ? configuredSource.NavigationPathColor
                : new Color(0.15f, 0.85f, 1f, 0.9f),
            configuredSource != null
                ? configuredSource.NavigationTargetColor
                : new Color(1f, 0.25f, 0.75f, 1f),
            configuredSource != null
                ? configuredSource.NavigationPathWidth
                : 0.035f,
            configuredSource != null
                ? configuredSource.NavigationTargetMarkerSize
                : 0.18f,
            configuredSource != null
                ? configuredSource.NavigationDebugSortingOrder
                : 200);
        CameraVisionRevealSource revealSource =
            GetComponent<CameraVisionRevealSource>();
        if (revealSource == null)
        {
            revealSource = gameObject.AddComponent<CameraVisionRevealSource>();
        }
        revealSource.Configure(
            configuredSource != null
                ? configuredSource.VisionRevealRadius
                : 3f);
        if (GetComponent<CameraVisionStreamingExempt>() == null)
        {
            gameObject.AddComponent<CameraVisionStreamingExempt>();
        }
        IgnoreDeployingCharacterTemporarily(deployingCharacter);
        BuildVisual();
        if (magic <= 0f)
        {
            BreakPuppet();
            return;
        }
        AssignTargetLever(manualControlMode
            ? null
            : (preferredLever != null
                ? preferredLever
                : FindNearestLever(transform.position, leverSearchRadius)));
        operationTimer = 0f;
        if (manualControlMode)
        {
            BeginRemoteControl();
        }
    }

    private void Update()
    {
        if (broken || DocumentReader.IsInputBlocked)
        {
            manualMoveDirection = Vector2.zero;
            ResetPryProgress();
            SetReclaimPromptVisible(false);
            return;
        }

        if (IsUnderRemoteControl)
        {
            UpdateRemoteControl();
            return;
        }

        manualMoveDirection = Vector2.zero;
        TryOfferReclaim();
    }

    private void FixedUpdate()
    {
        RefreshMovingDoorCollisionIgnores();
        if (broken || DocumentReader.IsInputBlocked)
        {
            if (physicsBody != null) physicsBody.velocity = Vector2.zero;
            return;
        }

        RestoreOwnerCollisionWhenClear();
        if (manualControlMode)
        {
            TickRemoteMovement();
            return;
        }

        if (targetLever == null || !targetLever.isActiveAndEnabled)
        {
            AssignTargetLever(FindNearestLever(
                transform.position,
                leverSearchRadius));
            attached = false;
        }
        if (targetLever == null)
        {
            return;
        }

        Vector2 mountPoint = targetLever.PuppetMountPoint;
        float distance = Vector2.Distance(transform.position, mountPoint);
        if (distance > leverContactDistance)
        {
            attached = false;
            if (physicsBody != null) physicsBody.SetRotation(0f);
            Vector2 direction = navigator != null
                ? navigator.GetDirection(mountPoint, leverContactDistance)
                : (mountPoint - (Vector2)transform.position).normalized;
            Vector2 displacement = navigator != null
                ? navigator.GetSafeDisplacement(direction, AreaMoveSpeed * Time.fixedDeltaTime)
                : direction * AreaMoveSpeed * Time.fixedDeltaTime;
            if (physicsBody != null)
                physicsBody.MovePosition(physicsBody.position + displacement);
            else
                transform.position += (Vector3)displacement;
            if (displacement.sqrMagnitude > 0.000001f)
                SpendMagic(movementMagicPerSecond * Time.fixedDeltaTime);
            return;
        }

        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
            physicsBody.position = mountPoint;
        }
        else transform.position = mountPoint;
        Vector2 stem = targetLever.StemDirection;
        float stemAngle = Vector2.SignedAngle(Vector2.up, stem);
        if (physicsBody != null) physicsBody.SetRotation(stemAngle);
        else transform.rotation = Quaternion.Euler(0f, 0f, stemAngle);
        if (!attached)
        {
            attached = true;
            operationTimer = 0f;
        }

        operationTimer -= Time.fixedDeltaTime;
        if (operationTimer <= 0f)
        {
            targetLever.ActivateFromClockworkPuppet();
            operationTimer = operationInterval;
            SpendMagic(operationMagicCost);
            if (!broken && physicsBody != null)
                physicsBody.position = targetLever.PuppetMountPoint;
        }
    }

    private void LateUpdate()
    {
        if (runtimeTexture == null) return;
        Vector2 position = physicsBody != null ? physicsBody.position : (Vector2)transform.position;
        float travelled = hasWalkPosition ? (position - previousWalkPosition).magnitude : 0f;
        previousWalkPosition = position;
        hasWalkPosition = true;
        bool mayWalk = !broken && !attached && !DocumentReader.IsInputBlocked && Time.deltaTime > 0f &&
            (manualControlMode
                ? IsUnderRemoteControl && manualMoveDirection.sqrMagnitude > 0.0001f
                : targetLever != null && targetLever.isActiveAndEnabled);
        // Sample actual displacement, not input: a puppet pushing a wall must stop stepping.
        // A short grace bridges render frames between physics steps; ignore teleport-sized jumps.
        float maximumStep = Mathf.Max(0.1f, AreaMoveSpeed * Time.deltaTime * 3f);
        if (mayWalk && travelled > 0.0001f && travelled <= maximumStep)
            walkMotionRemaining = 0.06f;
        else
            walkMotionRemaining = Mathf.Max(0f, walkMotionRemaining - Time.deltaTime);
        if (mayWalk && walkMotionRemaining > 0f)
        {
            walkElapsed = Mathf.Repeat(walkElapsed + Time.deltaTime, 0.24f);
            walkPose = walkElapsed < 0.12f ? 1 : 2;
        }
        else
        {
            walkElapsed = 0f;
            walkMotionRemaining = 0f;
            walkPose = 0;
        }
        UpdateMagicVisual();
    }

    private void ResetWalkAnimation()
    {
        hasWalkPosition = false;
        walkElapsed = 0f;
        walkMotionRemaining = 0f;
        walkPose = 0;
        UpdateMagicVisual();
    }

    private void BeginRemoteControl()
    {
        if (broken)
        {
            return;
        }

        if (remoteControlledPuppet != null &&
            remoteControlledPuppet != this)
        {
            remoteControlledPuppet.EndRemoteControl();
        }
        ClockworkPuppetBoxDriver.EndActiveRemoteControl();

        manualControlMode = true;
        remoteControlledPuppet = this;
        remoteControlEnteredFrame = Time.frameCount;
        characterInputBlockedThroughFrame = Time.frameCount;
        EnsureControlledCharacterStreamingExemption();
        manualMoveDirection = Vector2.zero;
        ResetManualDirectionInput();
        ResetPryProgress();
        SetReclaimPromptVisible(false);
        if (navigator != null)
        {
            navigator.InvalidatePath();
        }
    }

    private void EndRemoteControl()
    {
        if (remoteControlledPuppet != this)
        {
            return;
        }

        remoteControlledPuppet = null;
        characterInputBlockedThroughFrame = Time.frameCount;
        ReleaseControlledCharacterStreamingExemption();
        manualMoveDirection = Vector2.zero;
        ResetManualDirectionInput();
        ResetPryProgress();
        SetReclaimPromptVisible(false);
        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
        }
    }

    private void UpdateRemoteControl()
    {
        // R that resumes control must not also exit in this puppet's later Update.
        if (Time.frameCount == remoteControlEnteredFrame) return;
        if (Input.GetKeyDown(KeyCode.R))
        {
            EndRemoteControl();
            return;
        }

        if (attached)
        {
            manualMoveDirection = Vector2.zero;
            ResetPryProgress();
            if (targetBallista != null)
            {
                UpdateAttachedPose();
                if (!attached) return;
                ShowRemotePrompt(targetBallista.IsCoolingDown ? "弩炮冷却中" : "按[E]发射弩炮");
                if (Input.GetKeyDown(KeyCode.E)) TryFireMountedBallista();
                return;
            }
            ShowRemotePrompt("按[E]控制拉杆");
            if (Input.GetKeyDown(KeyCode.E) && targetLever != null &&
                targetLever.isActiveAndEnabled)
            {
                targetLever.ActivateFromClockworkPuppet();
                UpdateAttachedPose();
                SpendMagic(operationMagicCost);
            }
            return;
        }

        manualMoveDirection = ReadLastInputFourWayDirection();

        DoorHingeInteraction nearbyDoor = FindNearestManualDoor(
            out float doorDistance);
        LeverData nearbyLever = FindNearestManualLever(
            out float leverDistance);
        FortressBallista nearbyBallista = FortressBallista.FindNearestPuppetMount(
            bodyCollider, transform.position, gameObject.scene, manualInteractionDistance, out float ballistaDistance);
        if (nearbyBallista != null && ballistaDistance < doorDistance && ballistaDistance < leverDistance)
        {
            ResetPryProgress();
            ShowRemotePrompt("按[E]附着弩炮尾部");
            if (Input.GetKeyDown(KeyCode.E)) TryAttachToBallista(nearbyBallista);
            return;
        }
        bool useDoor = nearbyDoor != null &&
            (nearbyLever == null || doorDistance <= leverDistance);

        if (useDoor)
        {
            if (nearbyDoor.IsLocked)
            {
                ShowRemotePrompt(nearbyDoor.CanBeLockpicked
                    ? "按住[E]撬锁"
                    : "按[E]尝试撬锁");
                UpdateDoorPry(nearbyDoor);
            }
            else
            {
                ResetPryProgress();
                ShowRemotePrompt("按[E]进行互动");
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
            ShowRemotePrompt("按[E]附着机关");
            if (Input.GetKeyDown(KeyCode.E))
            {
                AttachToLever(nearbyLever);
            }
            return;
        }

        SetReclaimPromptVisible(false);
    }

    private void TickRemoteMovement()
    {
        if (attached)
        {
            UpdateAttachedPose();
            return;
        }

        if (!IsUnderRemoteControl ||
            manualMoveDirection.sqrMagnitude <= 0.0001f)
        {
            if (physicsBody != null)
            {
                physicsBody.velocity = Vector2.zero;
            }
            return;
        }

        if (physicsBody != null)
        {
            physicsBody.SetRotation(0f);
        }
        transform.rotation = Quaternion.identity;
        Vector2 displacement = navigator != null
            ? navigator.GetSafeDisplacement(
                manualMoveDirection,
                AreaMoveSpeed * Time.fixedDeltaTime)
            : manualMoveDirection * AreaMoveSpeed * Time.fixedDeltaTime;
        if (displacement.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (physicsBody != null)
        {
            physicsBody.MovePosition(physicsBody.position + displacement);
        }
        else
        {
            transform.position += (Vector3)displacement;
        }
        SpendMagic(movementMagicPerSecond * Time.fixedDeltaTime);
    }

    private void AttachToLever(LeverData lever)
    {
        if (lever == null || !lever.isActiveAndEnabled)
        {
            return;
        }

        AssignTargetBallista(null);
        AssignTargetLever(lever);
        attached = true;
        manualMoveDirection = Vector2.zero;
        UpdateAttachedPose();
    }

    public void AttachToLeverFromCardboardBox(LeverData lever)
    {
        AttachToLever(lever);
    }

    public bool TryAttachToBallista(FortressBallista ballista)
    {
        if (!IsUnderRemoteControl || broken || magic <= 0f || DocumentReader.IsInputBlocked ||
            Time.timeScale <= 0f || ballista == null || !ballista.CanAcceptPuppet(this) ||
            ballista.GetPuppetMountDistance(bodyCollider, transform.position, gameObject.scene) > manualInteractionDistance ||
            !ballista.HasClearPuppetApproach(bodyCollider, transform.position)) return false;
        return AttachToBallista(ballista);
    }

    private bool AttachToBallista(FortressBallista ballista)
    {
        if (ballista == null || !ballista.CanAcceptPuppet(this) || ballista.gameObject.scene != gameObject.scene) return false;
        AssignTargetLever(null);
        AssignTargetBallista(ballista);
        attached = true;
        manualControlMode = true;
        manualMoveDirection = Vector2.zero;
        UpdateAttachedPose();
        return true;
    }

    public bool TryFireMountedBallista()
    {
        if (targetBallista == null || !attached || broken || magic <= 0f || !IsUnderRemoteControl) return false;
        var owner = remoteControlOwner != null ? remoteControlOwner.CharacterData : null;
        if (owner == null || ZeldaRuntimeRegistry.GetControlledMover() != remoteControlOwner ||
            !targetBallista.TryFireFromPuppet(this, owner)) return false;
        // Match other mechanism operations; blocked/cooling-down shots cost nothing.
        SpendMagic(operationMagicCost);
        return true;
    }

    private void AssignTargetBallista(FortressBallista next)
    {
        foreach (var collider in ignoredBallistaColliders)
            if (bodyCollider != null && collider != null) Physics2D.IgnoreCollision(bodyCollider, collider, false);
        ignoredBallistaColliders.Clear();
        targetBallista = next;
        if (next == null || bodyCollider == null) return;
        foreach (var collider in next.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider == bodyCollider) continue;
            Physics2D.IgnoreCollision(bodyCollider, collider, true);
            ignoredBallistaColliders.Add(collider);
        }
    }

    private void UpdateAttachedPose()
    {
        if (attached && targetBallista != null && targetBallista.isActiveAndEnabled)
        {
            float angle = Vector2.SignedAngle(Vector2.up, targetBallista.ShotDirection);
            if (physicsBody != null)
            {
                physicsBody.velocity = Vector2.zero;
                physicsBody.position = targetBallista.RearWorldPosition;
                physicsBody.SetRotation(angle);
            }
            else
            {
                transform.position = targetBallista.RearWorldPosition;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            return;
        }
        if (!attached || targetLever == null ||
            !targetLever.isActiveAndEnabled)
        {
            attached = false;
            AssignTargetBallista(null);
            AssignTargetLever(null);
            if (physicsBody != null)
            {
                physicsBody.velocity = Vector2.zero;
                physicsBody.SetRotation(0f);
            }
            transform.rotation = Quaternion.identity;
            return;
        }

        Vector2 mountPoint = targetLever.PuppetMountPoint;
        Vector2 stem = targetLever.StemDirection;
        float stemAngle = Vector2.SignedAngle(Vector2.up, stem);
        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
            physicsBody.position = mountPoint;
            physicsBody.SetRotation(stemAngle);
        }
        else
        {
            transform.position = mountPoint;
            transform.rotation = Quaternion.Euler(0f, 0f, stemAngle);
        }
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

        if (!Input.GetKey(KeyCode.E))
        {
            ResetPryProgress();
            return;
        }

        var actor = remoteControlOwner != null ? remoteControlOwner.CharacterData : null;
        if (!CharacterSkillRequirements.Require(actor, CharacterSkillRequirements.PuppetPry, Input.GetKeyDown(KeyCode.E)))
        {
            ResetPryProgress();
            return;
        }
        int pryCost = GetPryMagicCost(actor);
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
                pryHoldTimer / Mathf.Max(0.1f, lockedDoorPryHoldDuration),
                new Color(0.92f, 0.12f, 0.1f, 1f));
        }

        if (pryHoldTimer < lockedDoorPryHoldDuration)
        {
            return;
        }

        TryCompleteDoorPry(door);
        ResetPryProgress();
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
        if (pryProgressBar != null)
        {
            // Reapply the anchor while prying so existing runtime instances
            // also pick up positioning changes after script reloads.
            pryProgressBar.transform.localPosition = PryProgressLocalPosition;
            return;
        }

        GameObject progressObject = new GameObject("Puppet Lock Pry Progress");
        progressObject.transform.SetParent(transform, false);
        progressObject.transform.localPosition = PryProgressLocalPosition;
        progressObject.transform.localScale = Vector3.one * 0.8f;
        pryProgressBar = progressObject.AddComponent<ZeldaPossessionProgressBar>();
        pryProgressBar.Hide();
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
                bodyCollider,
                transform.position);
            float allowedDistance = door.IsLocked
                ? Mathf.Max(lockedDoorPryDistance, manualInteractionDistance)
                : manualInteractionDistance;
            if (distance <= allowedDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closest = door;
            }
        }
        return closest;
    }

    private LeverData FindNearestManualLever(out float closestDistance)
    {
        LeverData closest = null;
        closestDistance = float.MaxValue;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled)
            {
                continue;
            }

            float distance = lever.GetSurfaceDistanceTo(
                bodyCollider,
                transform.position);
            if (distance <= manualInteractionDistance &&
                distance < closestDistance)
            {
                closestDistance = distance;
                closest = lever;
            }
        }
        return closest;
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

        bool horizontalChanged = Mathf.Abs(previousManualRawInput.x) <= threshold ||
            Mathf.Sign(previousManualRawInput.x) != Mathf.Sign(input.x);
        bool verticalChanged = Mathf.Abs(previousManualRawInput.y) <= threshold ||
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

        // With no input history, prefer the stronger axis. An exact tie uses
        // vertical instead of restoring the former permanent horizontal bias.
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

    private void EnsureControlledCharacterStreamingExemption()
    {
        if (controlledCharacterStreamingExemption != null)
        {
            return;
        }

        ZeldaFourWayMover controlledMover = remoteControlOwner != null &&
            remoteControlOwner.isActiveAndEnabled
            ? remoteControlOwner
            : ZeldaRuntimeRegistry.GetControlledMover();
        if (controlledMover == null)
        {
            return;
        }

        controlledCharacterStreamingExemption =
            controlledMover.GetComponentInParent<
                CameraVisionStreamingExempt>(true);
        if (controlledCharacterStreamingExemption == null)
        {
            controlledCharacterStreamingExemption =
                controlledMover.gameObject.AddComponent<
                    CameraVisionStreamingExempt>();
            addedControlledCharacterStreamingExemption = true;
        }
    }

    private void ReleaseControlledCharacterStreamingExemption()
    {
        if (addedControlledCharacterStreamingExemption &&
            controlledCharacterStreamingExemption != null)
        {
            Destroy(controlledCharacterStreamingExemption);
        }

        controlledCharacterStreamingExemption = null;
        addedControlledCharacterStreamingExemption = false;
    }

    private void ShowRemotePrompt(string message)
    {
        EnsureReclaimPrompt();
        if (reclaimPromptObject == null)
        {
            return;
        }

        reclaimPromptText.text = message;
        reclaimPromptFont.RequestCharactersInTexture(
            message,
            72,
            FontStyle.Normal);
        reclaimPromptMaterial.mainTexture =
            reclaimPromptFont.material.mainTexture;
        reclaimPromptObject.transform.position =
            transform.position + Vector3.up * 0.72f;
        reclaimPromptObject.transform.rotation = Quaternion.identity;
        SetReclaimPromptVisible(true);
    }

    private void AssignTargetLever(LeverData nextTarget)
    {
        SetLeverCollisionIgnored(false);
        targetLever = nextTarget;
        if (navigator != null)
        {
            navigator.SetIgnoredTarget(targetLever);
            navigator.InvalidatePath();
        }
        SetLeverCollisionIgnored(true);
    }

    private void RefreshMovingDoorCollisionIgnores()
    {
        if (bodyCollider == null)
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
                door.SetCollisionIgnoredWith(bodyCollider, false);
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
            door.SetCollisionIgnoredWith(bodyCollider, true);
            ignoredMovingDoors.Add(door);
        }
    }

    private void RestoreMovingDoorCollisions()
    {
        if (bodyCollider != null)
        {
            foreach (DoorHingeInteraction door in ignoredMovingDoors)
            {
                if (door != null)
                {
                    door.SetCollisionIgnoredWith(bodyCollider, false);
                }
            }
        }
        ignoredMovingDoors.Clear();
        movingDoorCleanup.Clear();
    }

    private void SetLeverCollisionIgnored(bool ignored)
    {
        if (!ignored)
        {
            for (int i = 0; i < ignoredLeverColliders.Count; i++)
                if (bodyCollider != null && ignoredLeverColliders[i] != null)
                    Physics2D.IgnoreCollision(bodyCollider, ignoredLeverColliders[i], false);
            ignoredLeverColliders.Clear();
            return;
        }
        if (targetLever == null || bodyCollider == null) return;
        Collider2D[] colliders = targetLever.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == bodyCollider) continue;
            Physics2D.IgnoreCollision(bodyCollider, colliders[i], true);
            ignoredLeverColliders.Add(colliders[i]);
        }
    }

    private void IgnoreDeployingCharacterTemporarily(ZeldaCharacterData character)
    {
        if (character == null || bodyCollider == null) return;
        initialOwner = character.transform;
        Collider2D[] colliders = character.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == bodyCollider) continue;
            Physics2D.IgnoreCollision(bodyCollider, colliders[i], true);
            ignoredOwnerColliders.Add(colliders[i]);
        }
    }

    private void RestoreOwnerCollisionWhenClear()
    {
        if (initialOwner == null || ignoredOwnerColliders.Count == 0 ||
            Vector2.Distance(transform.position, initialOwner.position) < 0.8f) return;
        for (int i = 0; i < ignoredOwnerColliders.Count; i++)
            if (bodyCollider != null && ignoredOwnerColliders[i] != null)
                Physics2D.IgnoreCollision(bodyCollider, ignoredOwnerColliders[i], false);
        ignoredOwnerColliders.Clear();
        initialOwner = null;
        if (navigator != null) navigator.InvalidatePath();
    }

    private void TryOfferReclaim()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || Vector2.Distance(mover.transform.position, transform.position) > reclaimDistance)
        {
            SetReclaimPromptVisible(false);
            return;
        }
        // Reclaim selection must work even when its optional prompt cannot exist.
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            mover,
            KeyCode.E,
            mover.transform.position,
            SetReclaimPromptVisible);
        EnsureReclaimPrompt();
        if (reclaimPromptObject != null)
        {
            const string reclaimMessage = "按[E]收回魔像";
            reclaimPromptText.text = reclaimMessage;
            reclaimPromptFont.RequestCharactersInTexture(
                reclaimMessage,
                72,
                FontStyle.Normal);
            reclaimPromptMaterial.mainTexture =
                reclaimPromptFont.material.mainTexture;
            reclaimPromptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, 1.05f));
            reclaimPromptObject.transform.rotation = Quaternion.identity;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            ZeldaInteractionArbiter.Submit(
                this,
                mover,
                KeyCode.E,
                mover.transform.position,
                Reclaim);
        }
    }

    private void EnsureReclaimPrompt()
    {
        if (reclaimPromptObject != null) return;
        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        reclaimPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (reclaimPromptFont == null) return;
        const string message = "按[E]收回魔像";
        reclaimPromptFont.RequestCharactersInTexture(message, 72, FontStyle.Normal);
        reclaimPromptObject = new GameObject("Clockwork Puppet Reclaim Prompt");
        reclaimPromptText = reclaimPromptObject.AddComponent<TextMesh>();
        reclaimPromptText.text = message;
        reclaimPromptText.font = reclaimPromptFont;
        reclaimPromptText.fontSize = 72;
        reclaimPromptText.characterSize = 0.035f;
        reclaimPromptText.anchor = TextAnchor.MiddleCenter;
        reclaimPromptText.alignment = TextAlignment.Center;
        reclaimPromptText.color = ZeldaUiPalette.Primary;
        MeshRenderer renderer = reclaimPromptObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        reclaimPromptMaterial = new Material(reclaimPromptFont.material)
        {
            name = "Clockwork Puppet Reclaim Prompt Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        reclaimPromptMaterial.mainTexture = reclaimPromptFont.material.mainTexture;
        renderer.sharedMaterial = reclaimPromptMaterial;
        reclaimPromptObject.SetActive(false);
    }

    private void SetReclaimPromptVisible(bool visible)
    {
        if (reclaimPromptObject != null && reclaimPromptObject.activeSelf != visible)
        {
            reclaimPromptObject.SetActive(visible);
        }
    }

    private void Reclaim()
    {
        if (broken || PersistentInventory.Instance == null)
        {
            return;
        }
        if (PersistentInventory.Instance.TryAddUniqueItem(
                itemId,
                itemInstanceId,
                1,
                maxStackSize,
                itemName,
                itemDescription,
                hasVisualColor,
                visualColor,
                true,
                magic))
        {
            ZeldaRuntimeRegistry.GetControlledMover()?.TryBeginItemAttack(.24f);
            Destroy(gameObject);
        }
    }

    public int GetPryMagicCost(ZeldaCharacterData actor)
    {
        return CharacterSkillRequirements.PryEnergyCost(maximumMagic * Mathf.Clamp01(lockedDoorPryMagicFraction),
            actor != null ? actor.FinalSkillValue : 0);
    }

    private bool TryCompleteDoorPry(DoorHingeInteraction door)
    {
        var actor = remoteControlOwner != null ? remoteControlOwner.CharacterData : null;
        if (broken || !IsUnderRemoteControl || ZeldaRuntimeRegistry.GetControlledMover() != remoteControlOwner ||
            door == null || !door.isActiveAndEnabled || !door.IsLocked || !door.CanBeLockpicked ||
            !CharacterSkillRequirements.Require(actor, CharacterSkillRequirements.PuppetPry)) return false;
        int cost = GetPryMagicCost(actor);
        if (magic + .0001f < cost) return false;
        door.SetLocked(false);
        SpendMagic(cost);
        return true;
    }

    private void SpendMagic(float amount)
    {
        magic = Mathf.Max(0f, magic - Mathf.Max(0f, amount));
        UpdateMagicVisual();
        if (magic <= 0f)
        {
            BreakPuppet();
        }
    }

    private void BreakPuppet()
    {
        if (broken)
        {
            return;
        }
        broken = true;
        PersistentInventory.Instance?.RemoveClockworkPuppetRemote(itemInstanceId);
        EndRemoteControl();
        if (physicsBody != null) physicsBody.velocity = Vector2.zero;
        SetReclaimPromptVisible(false);
        attached = false;
        transform.rotation = Quaternion.identity;
        UpdateMagicVisual();
        // An exhausted mechanism is no longer a reclaimable world object.
        // Destroying the runtime root also removes its collider, navigator,
        // prompt and generated visual resources through OnDestroy.
        Destroy(gameObject);
    }

    private static LeverData FindNearestLever(
        Vector2 position,
        float searchRadius)
    {
        LeverData closest = null;
        float best = searchRadius > 0f
            ? searchRadius * searchRadius
            : float.MaxValue;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled)
            {
                continue;
            }
            float sqr = (lever.PuppetMountPoint - position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                closest = lever;
            }
        }
        return closest;
    }

    private void BuildVisual()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();
        runtimeSprite = ClockworkPuppetPickupItemVisual.CreateRuntimeSprite(out runtimeTexture);
        bodyRenderer.sprite = runtimeSprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 20;
        lastVisibleEnergySegments = -1;
        lastVisibleWalkPose = -1;
        ResetWalkAnimation();
        UpdateMagicVisual();
    }

    private void UpdateMagicVisual()
    {
        if (runtimeTexture == null)
        {
            return;
        }
        float normalized = Mathf.Clamp01(magic / Mathf.Max(0.0001f, maximumMagic));
        int visibleSegments = Mathf.Clamp(Mathf.CeilToInt(normalized * 6f), 0, 6);
        if (visibleSegments == lastVisibleEnergySegments && walkPose == lastVisibleWalkPose) return;
        if (walkPose != lastVisibleWalkPose)
            ClockworkPuppetPickupItemVisual.ApplyRuntimePose(runtimeTexture, normalized, walkPose);
        else
            ClockworkPuppetPickupItemVisual.ApplyRuntimeEnergy(runtimeTexture, normalized);
        lastVisibleEnergySegments = visibleSegments;
        lastVisibleWalkPose = walkPose;
    }

    private void OnDestroy()
    {
        AssignTargetBallista(null);
        EndRemoteControl();
        RestoreMovingDoorCollisions();
        ActiveSet.Remove(this);
        SetLeverCollisionIgnored(false);
        for (int i = 0; i < ignoredOwnerColliders.Count; i++)
            if (bodyCollider != null && ignoredOwnerColliders[i] != null)
                Physics2D.IgnoreCollision(bodyCollider, ignoredOwnerColliders[i], false);
        if (reclaimPromptObject != null) Destroy(reclaimPromptObject);
        if (reclaimPromptMaterial != null) Destroy(reclaimPromptMaterial);
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (runtimeTexture != null) Destroy(runtimeTexture);
    }
}
