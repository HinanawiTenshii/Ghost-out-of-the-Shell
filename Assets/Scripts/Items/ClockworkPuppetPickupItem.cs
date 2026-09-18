using UnityEngine;

/// <summary>A magic-powered puppet that autonomously seeks and operates levers.</summary>
public sealed class ClockworkPuppetPickupItem : PickupItemBase
{
    public const string DefaultItemId = "clockwork_puppet";

    [Header("Clockwork Puppet")]
    [SerializeField, Min(0.1f)] private float maximumMagic = 12f;
    [SerializeField, Min(0.05f)] private float moveSpeed = 1.35f;
    [SerializeField, Min(0f)] private float movementMagicPerSecond = 0.45f;
    [SerializeField, Min(0f)] private float operationMagicCost = 1f;
    [SerializeField, Min(0.1f)] private float operationInterval = 3f;
    [SerializeField, Min(0.02f)] private float leverContactDistance = 0.18f;
    [SerializeField, Min(0f), Tooltip("Maximum world-space distance used when searching for a lever. Set to 0 for unlimited range.")]
    private float leverSearchRadius;
    [SerializeField, Min(0.1f)] private float reclaimDistance = 1.15f;
    [SerializeField, Min(0.1f)] private float lockedDoorPryDistance = 0.7f;
    [SerializeField, Min(0.1f)] private float manualInteractionDistance = 0.85f;
    [SerializeField, Min(0.1f)] private float lockedDoorPryHoldDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float lockedDoorPryMagicFraction = 0.5f;
    [SerializeField, Min(0.05f)] private float deployedScale = 0.58f;
    [SerializeField, Min(0.1f), Tooltip("Radius of the additional camera-vision reveal centred on a deployed puppet, including while it drives a cardboard box.")]
    private float visionRevealRadius = 3f;

    [Header("Navigation Debug")]
    [SerializeField] private bool showNavigationDebug = true;
    [SerializeField] private Color navigationPathColor =
        new Color(0.15f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color navigationTargetColor =
        new Color(1f, 0.25f, 0.75f, 1f);
    [SerializeField, Min(0.005f)] private float navigationPathWidth = 0.035f;
    [SerializeField, Min(0.05f)] private float navigationTargetMarkerSize = 0.18f;
    [SerializeField] private int navigationDebugSortingOrder = 200;
    private float storedMagic = -1f;

    public float MaximumMagic => maximumMagic;
    public float MoveSpeed => moveSpeed;
    public float MovementMagicPerSecond => movementMagicPerSecond;
    public float OperationMagicCost => operationMagicCost;
    public float OperationInterval => operationInterval;
    public float LeverContactDistance => leverContactDistance;
    public float LeverSearchRadius => Mathf.Max(0f, leverSearchRadius);
    public float ReclaimDistance => reclaimDistance;
    public float LockedDoorPryDistance => lockedDoorPryDistance;
    public float ManualInteractionDistance => manualInteractionDistance;
    public float LockedDoorPryHoldDuration => lockedDoorPryHoldDuration;
    public float LockedDoorPryMagicFraction => lockedDoorPryMagicFraction;
    public float DeployedScale => deployedScale;
    public float VisionRevealRadius => visionRevealRadius;
    public bool ShowNavigationDebug => showNavigationDebug;
    public Color NavigationPathColor => navigationPathColor;
    public Color NavigationTargetColor => navigationTargetColor;
    public float NavigationPathWidth => navigationPathWidth;
    public float NavigationTargetMarkerSize => navigationTargetMarkerSize;
    public int NavigationDebugSortingOrder => navigationDebugSortingOrder;
    public override bool HasInventoryCharge => storedMagic >= 0f;
    public override float InventoryCharge => storedMagic >= 0f
        ? storedMagic
        : maximumMagic;

    public override void ApplyInventoryCharge(bool hasCharge, float charge)
    {
        storedMagic = hasCharge
            ? Mathf.Clamp(charge, 0f, maximumMagic)
            : -1f;
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        DeployPuppet(
            user != null ? user.transform.position : transform.position,
            user,
            null,
            storedMagic >= 0f ? storedMagic : maximumMagic);
        return true;
    }

    public ClockworkPuppetRuntime DeployFromCardboardBox(
        Vector3 worldPosition,
        LeverData preferredLever,
        float remainingMagic,
        string itemInstanceId,
        string itemName,
        string itemDescription,
        bool hasVisualColor,
        Color visualColor)
    {
        ClockworkPuppetRuntime runtime = DeployPuppet(
            worldPosition,
            null,
            preferredLever,
            remainingMagic);
        if (runtime != null)
        {
            runtime.SetInventoryIdentity(
                itemInstanceId,
                itemName,
                itemDescription,
                hasVisualColor,
                visualColor);
        }
        return runtime;
    }

    public ClockworkPuppetRuntime DeployControlledFromCardboardBox(
        Vector3 worldPosition,
        LeverData targetLever,
        ZeldaCharacterData controllingCharacter,
        float remainingMagic,
        string itemInstanceId,
        string itemName,
        string itemDescription,
        bool hasVisualColor,
        Color visualColor)
    {
        ClockworkPuppetRuntime runtime = DeployPuppet(
            worldPosition,
            controllingCharacter,
            targetLever,
            remainingMagic);
        if (runtime == null)
        {
            return null;
        }

        runtime.SetInventoryIdentity(
            itemInstanceId,
            itemName,
            itemDescription,
            hasVisualColor,
            visualColor);
        runtime.AttachToLeverFromCardboardBox(targetLever);
        return runtime;
    }

    public ClockworkPuppetRuntime DeployPuppet(
        Vector3 worldPosition,
        ZeldaCharacterData deployingCharacter,
        LeverData preferredLever,
        float initialMagic)
    {
        GameObject deployed = new GameObject("Deployed Clockwork Puppet");
        deployed.transform.position = worldPosition;
        deployed.AddComponent<SpriteRenderer>();
        Rigidbody2D body = deployed.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D collider = deployed.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;
        // Match the pickup prefab's authored collider before the shared
        // deployedScale is applied to both its visual and physical footprint.
        collider.size = new Vector2(0.72f, 0.88f);
        collider.offset = new Vector2(0f, 0.03f);
        ClockworkPuppetRuntime runtime = deployed.AddComponent<ClockworkPuppetRuntime>();
        runtime.Configure(this, deployingCharacter, preferredLever, initialMagic);
        return runtime;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        maximumMagic = Mathf.Max(0.1f, maximumMagic);
        moveSpeed = Mathf.Max(0.05f, moveSpeed);
        movementMagicPerSecond = Mathf.Max(0f, movementMagicPerSecond);
        operationMagicCost = Mathf.Max(0f, operationMagicCost);
        operationInterval = Mathf.Max(0.1f, operationInterval);
        leverContactDistance = Mathf.Max(0.02f, leverContactDistance);
        leverSearchRadius = Mathf.Max(0f, leverSearchRadius);
        reclaimDistance = Mathf.Max(0.1f, reclaimDistance);
        lockedDoorPryDistance = Mathf.Max(0.1f, lockedDoorPryDistance);
        manualInteractionDistance = Mathf.Max(0.1f, manualInteractionDistance);
        lockedDoorPryHoldDuration = Mathf.Max(0.1f, lockedDoorPryHoldDuration);
        lockedDoorPryMagicFraction = Mathf.Clamp01(lockedDoorPryMagicFraction);
        deployedScale = Mathf.Max(0.05f, deployedScale);
        visionRevealRadius = Mathf.Max(0.1f, visionRevealRadius);
        navigationPathWidth = Mathf.Max(0.005f, navigationPathWidth);
        navigationTargetMarkerSize =
            Mathf.Max(0.05f, navigationTargetMarkerSize);
    }
#endif
}
