using System.Collections.Generic;
using UnityEngine;

public enum ZeldaAiState
{
    Idle,
    Suspicious,
    Alert,
    Search,
    Recovery,
    Hostile,
    Stunned
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(ZeldaCharacterData))]
[RequireComponent(typeof(ZeldaFourWayMover))]
public class ZeldaCharacterAiBase : MonoBehaviour
{
    private const string VisionObjectName = "AI Vision Cone";
    private static readonly Vector2Int[] GridDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    [Header("Vision")]
    [SerializeField, Min(0.1f)] private float visionRadius = 4f;
    [SerializeField, Range(1f, 360f)] private float visionAngle = 90f;
    [SerializeField, Range(1f, 360f)] private float peripheralVisionAngle = 180f;
    [SerializeField, Min(0.1f)] private float peripheralVisionRadius = 1.4f;
    [SerializeField, Range(4, 180)] private int visionRayCount = 48;
    [SerializeField] private LayerMask visionObstacleLayers = ~0;
    [SerializeField] private Color visionColor = new Color(0.45f, 0.8f, 1f, 0.22f);
    [Tooltip("Visual-only vision mesh refresh interval. Perception checks remain fully responsive.")]
    [SerializeField, Min(0.02f)] private float visionVisualRefreshInterval = 0.08f;

    [Header("Suspicion")]
    [SerializeField, Min(0f)] private float immediateResponseDistance = 2f;
    [SerializeField, Min(0.01f)] private float maximumSuspicion = 100f;
    [SerializeField, Min(0f)] private float suspicionIncreasePerSecond = 35f;
    [SerializeField, Min(0f)] private float suspicionDecreasePerSecond = 25f;

    [Header("Cardboard Box Perception")]
    [Tooltip("Distance an AI keeps while approaching a moving cardboard box.")]
    [SerializeField, Min(0.1f)] private float cardboardBoxApproachDistance = 1.05f;
    [Tooltip("How long a suspicious box remains locked after a brief vision interruption.")]
    [SerializeField, Min(0f)] private float cardboardBoxSightMemoryDuration = 1.5f;

    [Header("Hostile Behaviour")]
    [SerializeField, Min(0f)] private float attackDistance = 1.1f;
    [SerializeField, Range(0f, 180f)] private float attackFacingTolerance = 20f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.35f;
    [SerializeField, Min(0f)] private float regularHostileLoseSightDelay = 7f;
    [SerializeField] private LayerMask solidCollisionLayers = ~0;
    [SerializeField, Min(0f)] private float collisionSkinWidth = 0.01f;

    [Header("Search Behaviour")]
    [SerializeField, Min(0f)] private float searchDuration = 4f;
    [SerializeField, Min(0.1f)] private float searchRadius = 1.8f;
    [SerializeField, Min(0.01f)] private float searchWaypointTolerance = 0.15f;
    [Tooltip("Continuous time without meaningful progress before a search waypoint is replaced.")]
    [SerializeField, Min(0.1f)] private float searchWaypointChangeInterval = 1.2f;
    [Tooltip("Minimum empty space kept between a generated search waypoint and solid colliders.")]
    [SerializeField, Min(0.05f)] private float searchWaypointObstacleClearance = 0.45f;
    [Tooltip("Candidate points tested before the AI waits and tries again.")]
    [SerializeField, Range(1, 32)] private int searchWaypointSamplingAttempts = 12;

    [Header("Investigation Behaviour")]
    [Tooltip("How long an AI remains in the investigation state after reaching a pulse source.")]
    [SerializeField, Min(0f)] private float investigationDuration = 3f;
    [Tooltip("How long an AI pauses after each movement during a pulse investigation.")]
    [SerializeField, Min(0f)] private float investigationWaypointPauseDuration = 0.65f;
    [Tooltip("Maximum distance used to relocate an investigation target away from solid geometry.")]
    [SerializeField, Min(0.1f)] private float investigationTargetRelocationRadius = 1.5f;
    [Tooltip("Directions tested on each relocation ring around an unsafe investigation point.")]
    [SerializeField, Range(4, 32)] private int investigationRelocationSamplesPerRing = 12;
    [Tooltip("Closest distance an attracted NPC will choose around the actual incident.")]
    [SerializeField, Min(0.1f)] private float investigationApproachMinimumRadius = 0.75f;
    [Tooltip("Furthest preferred observation distance around the actual incident.")]
    [SerializeField, Min(0.1f)] private float investigationApproachMaximumRadius = 1.45f;
    [Tooltip("Minimum separation between observation targets assigned to NPCs attracted by the same incident.")]
    [SerializeField, Min(0f)] private float investigationApproachTargetSpacing = 0.45f;

    [Header("Recovery Behaviour")]
    [SerializeField, Min(0.01f)] private float recoveryPositionTolerance = 0.15f;

    [Header("Permission Warning")]
    [SerializeField, Min(0.1f)] private float warningFollowDistance = 1.4f;
    [SerializeField, Min(0f)] private float warningEscalationTime = 4f;
    [SerializeField, Min(0f)] private float warningDistanceTolerance = 0.15f;

    [Header("State Indicator")]
    [SerializeField] private Vector2 stateIndicatorOffset = new Vector2(0f, 1.15f);
    [SerializeField, Min(0.1f)] private float stateIndicatorScale = 0.65f;
    [SerializeField, Min(0f)] private float hostileIndicatorDuration = 0.7f;

    [Header("Possession Lock")]
    [SerializeField, Min(0f)] private float possessionShakeAmount = 0.025f;
    [SerializeField, Min(0f)] private float possessionShakeSpeed = 45f;

    [Header("Obstacle Avoidance")]
    [SerializeField, Min(0.05f)] private float obstacleProbeDistance = 0.75f;
    [SerializeField, Range(5f, 90f)] private float avoidanceAngleStep = 30f;
    [SerializeField, Range(1, 6)] private int avoidanceDirectionSteps = 3;
    [SerializeField, Min(0f)] private float avoidanceDirectionHoldTime = 0.3f;
    [Tooltip("How long the direct route must remain clear before the AI leaves wall-following mode.")]
    [SerializeField, Min(0f)] private float wallFollowExitDelay = 0.35f;
    [Tooltip("Time without physical movement before a stable corner escape is selected.")]
    [SerializeField, Min(0.05f)] private float cornerStuckDetectionDuration = 0.18f;
    [Tooltip("How long the selected corner escape direction is retained.")]
    [SerializeField, Min(0.05f)] private float cornerEscapeHoldDuration = 0.4f;
    [SerializeField, Range(8, 24)] private int cornerEscapeDirectionSamples = 16;
    [Tooltip("Time window used to detect short alternating movements near corners.")]
    [SerializeField, Min(0.1f)] private float cornerOscillationWindowDuration = 0.3f;
    [Tooltip("Net distance divided by total traveled distance below which movement is considered oscillation.")]
    [SerializeField, Range(0.1f, 0.9f)] private float cornerMinimumProgressRatio = 0.45f;

    [Header("Navigation Update Frequency")]
    [Tooltip("Seconds between full obstacle-route recalculations in normal states. AI instances are staggered automatically.")]
    [SerializeField, Min(0.01f)] private float navigationRefreshInterval = 0.1f;
    [Tooltip("A shorter refresh interval used while hostile so pursuit remains responsive.")]
    [SerializeField, Min(0.01f)] private float hostileNavigationRefreshInterval = 0.05f;
    [Tooltip("Immediately recalculate when the desired travel direction changes by at least this angle.")]
    [SerializeField, Range(1f, 90f)] private float navigationDirectionChangeAngle = 12f;

    [Header("Runtime Grid Pathfinding")]
    [Tooltip("Build a local physics grid at runtime and use A* without requiring scene navigation components.")]
    [SerializeField] private bool useGridPathfinding = true;
    [SerializeField, Min(0.2f)] private float gridCellSize = 0.5f;
    [SerializeField, Range(2, 12)] private int gridSearchPadding = 5;
    [SerializeField, Range(15, 81)] private int maximumGridDimension = 45;
    [Tooltip("Maximum grid dimension used only when the normal A* search cannot find a route.")]
    [SerializeField, Range(31, 161)] private int expandedGridDimension = 121;
    [Tooltip("Smaller cell size used by the fallback A* pass in narrow spaces.")]
    [SerializeField, Min(0.1f)] private float minimumGridCellSize = 0.25f;
    [SerializeField, Min(0.05f)] private float gridPathRefreshInterval = 0.35f;
    [SerializeField, Min(0.05f)] private float hostileGridPathRefreshInterval = 0.18f;
    [Tooltip("Minimum delay before rebuilding A* for a target that keeps moving. Existing valid paths are reused during this delay.")]
    [SerializeField, Min(0.05f)] private float movingTargetGridRepathInterval = 0.3f;
    [Tooltip("How often an otherwise valid path may be rebuilt to account for changed scene obstacles.")]
    [SerializeField, Min(0.2f)] private float gridPathMaintenanceInterval = 1.25f;
    [Tooltip("Maximum number of complete A* builds shared by all AI characters in one frame. This spreads expensive physics-grid scans across frames.")]
    [SerializeField, Range(1, 8)] private int maximumAStarBuildsPerFrame = 1;
    [Tooltip("Minimum real-time spacing between complete A* builds shared by every AI. The spacing grows automatically when many NPCs are hostile.")]
    [SerializeField, Min(0.01f)] private float globalAStarBuildInterval = 0.04f;
    [Tooltip("Minimum spacing between the large/fine fallback A* passes shared by every AI.")]
    [SerializeField, Min(0.05f)] private float expensiveGridFallbackInterval = 0.5f;
    [Tooltip("How often an existing A* path checks and simplifies its upcoming physics segments.")]
    [SerializeField, Min(0.02f)] private float gridPathValidationInterval = 0.12f;
    [SerializeField, Min(0.02f)] private float gridWaypointTolerance = 0.14f;
    [Tooltip("Extra space kept between AI grid paths and solid walls.")]
    [SerializeField, Min(0f)] private float gridWallClearance = 0.16f;
    [Tooltip("Maximum wall-separation correction applied in one physics step.")]
    [SerializeField, Min(0.01f)] private float maximumWallSeparationStep = 0.12f;
    [Tooltip("Preferred additional distance from solid objects when the grid has room for a wider route.")]
    [SerializeField, Min(0f)] private float preferredGridObstacleDistance = 0.7f;
    [Tooltip("Extra A* cost assigned to nodes close to solid objects.")]
    [SerializeField, Min(0f)] private float gridObstacleProximityCost = 3.5f;
    [Tooltip("Small A* cost for changing direction, producing stable paths with fewer unnecessary turns.")]
    [SerializeField, Min(0f)] private float gridTurnCost = 0.08f;

    [Header("Character Separation")]
    [SerializeField, Min(0.05f)] private float characterSeparationDistance = 0.75f;
    [SerializeField, Min(0f)] private float characterSeparationStrength = 1.25f;
    [Tooltip("Distance at which an AI begins predicting a collision with another character.")]
    [SerializeField, Min(0.1f)] private float characterAvoidanceLookAheadDistance = 1.35f;
    [Tooltip("Additional lateral clearance kept between character colliders while passing.")]
    [SerializeField, Min(0f)] private float characterAvoidanceClearancePadding = 0.12f;
    [Tooltip("Speed retained by the lower-priority AI while it gives way.")]
    [SerializeField, Range(0.1f, 1f)] private float characterYieldSpeedMultiplier = 0.62f;
    [Tooltip("Seconds between nearby-character separation scans.")]
    [SerializeField, Min(0.01f)] private float characterSeparationRefreshInterval = 0.12f;
    [Tooltip("Minimum delay before a sharp direction change may force another nearby-character scan.")]
    [SerializeField, Min(0.01f)] private float characterSeparationForcedRefreshInterval = 0.05f;

    [Header("Navigation Debug")]
    [SerializeField] private bool showNavigationDebug = true;
    [SerializeField] private Color navigationPathColor =
        new Color(0.15f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color navigationTargetColor =
        new Color(1f, 0.25f, 0.75f, 1f);
    [SerializeField, Min(0.005f)] private float navigationPathWidth = 0.035f;
    [SerializeField, Min(0.05f)] private float navigationTargetMarkerSize = 0.18f;
    [SerializeField] private int navigationDebugSortingOrder = 200;

    // Dense castle scenes can place many adjacent wall colliders inside one
    // cast. Truncating a NonAlloc query silently made a later wall disappear
    // from the navigation graph, so keep enough reusable capacity without
    // allocating during searches.
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[128];
    private readonly RaycastHit2D[] visionHits = new RaycastHit2D[64];
    private readonly Collider2D[] gridOverlapHits = new Collider2D[96];
    private readonly List<Vector2> gridPath = new List<Vector2>(48);
    private readonly List<Vector2> simplifiedGridPath =
        new List<Vector2>(48);
    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;
    private ZeldaCharacterData characterData;
    private ZeldaFourWayMover mover;
    private SpriteRenderer characterRenderer;
    private Mesh visionMesh;
    private Vector3[] visionVertices;
    private int[] visionTriangles;
    private int cachedVisionRayCount = -1;
    private MeshRenderer visionRenderer;
    private Material visionMaterial;
    private float nextVisionVisualRefreshTime;
    private bool hasBuiltVisionMesh;
    private ZeldaAiStateIndicator stateIndicator;
    private ZeldaAiState currentState = ZeldaAiState.Idle;
    private ZeldaFourWayMover targetMover;
    private Vector2 facingDirection = Vector2.down;
    private Vector2 moveDirection;
    private Vector2 lastKnownTargetPosition;
    private float attackCooldownTimer;
    private Vector2 avoidanceDirection;
    private float avoidanceDirectionTimer;
    private bool isFollowingObstacle;
    private int obstacleFollowSide = 1;
    private float directRouteClearTimer;
    private Vector2 previousFixedPosition;
    private float cornerStuckTimer;
    private Vector2 cornerEscapeDirection;
    private float cornerEscapeTimer;
    private Vector2 cornerOscillationStartPosition;
    private float cornerOscillationElapsed;
    private float cornerOscillationTravelDistance;
    private PermissionArea activePermissionArea;
    private ZeldaFourWayMover recognizedPermissionTarget;
    private float warningTimer;
    private bool hostileUntilTargetDeath;
    private bool retaliationHostile;
    private bool regularHostile;
    private CardboardBoxWearState suspiciousCardboardBox;
    private ZeldaFourWayMover suspiciousCardboardBoxWearer;
    private CardboardBoxPickupItem suspiciousGroundCardboardBox;
    private float lastSuspiciousCardboardBoxSeenTime = float.NegativeInfinity;
    private bool hasCardboardBoxAttractionTarget;
    private bool isInspectingCardboardBox;
    private ZeldaFourWayMover inspectedCardboardBoxWearer;
    private CardboardBoxPickupItem hostileCardboardBox;
    private float regularHostileLostSightTimer;
    private float searchTimer;
    private Vector2 searchCenter;
    private Vector2 searchWaypoint;
    private float searchWaypointTimer;
    private float previousSearchWaypointDistance = float.PositiveInfinity;
    private bool isTravelingToInvestigation;
    private bool isDoorIncidentInvestigation;
    private bool isPausingDuringInvestigation;
    private float investigationPauseTimer;
    private Vector2 investigationIncidentPosition;
    private Vector2 investigationApproachPosition;
    private Vector2 initialScenePosition;
    private Vector2 initialSceneFacingDirection = Vector2.down;
    private float suspicionValue;
    private float hostileIndicatorTimer;
    private bool possessionLocked;
    private bool hasPossessionVisualBase;
    private SpriteRenderer possessionVisualRenderer;
    private Vector3 possessionVisualBasePosition;
    private Vector2 cachedPlannedDirection;
    private Vector2 lastRequestedNavigationDirection;
    private float nextNavigationRefreshTime;
    private Vector2 cachedSeparation;
    private Vector2 cachedSeparatedDirection;
    private float cachedCharacterYieldWeight;
    private Vector2 lastSeparationDesiredDirection;
    private float nextSeparationRefreshTime;
    private float lastSeparationRefreshTime;
    private float[] gridCosts;
    private int[] gridParents;
    private byte[] gridStates;
    private byte[] gridWalkability;
    private float[] gridObstaclePenalties;
    private int[] gridHeap;
    private int[] gridHeapPositions;
    private int gridHeapCount;
    private int gridWidth;
    private int gridHeight;
    private Vector2Int gridOriginCell;
    private float activeGridCellSize;
    private float activeGridClearance;
    private int activeGridGoalRadius;
    private Vector2 gridPathTarget;
    private float gridPathStoppingDistance;
    private Vector2 gridPathPlannedTarget;
    private float gridPathPlannedStoppingDistance;
    private bool hasGridPathPlan;
    private int gridPathIndex;
    private float nextGridPathRefreshTime;
    private float nextGridPathMaintenanceTime;
    private float nextGridPathValidationTime;
    private bool retainLoadedUntilIdle;
    private bool hasConsumedUnawareFirstHit;
    private LineRenderer navigationDebugLine;
    private SpriteRenderer navigationDebugTarget;
    private Material navigationDebugMaterial;
    private bool hasNavigationDebugTarget;
    private static Sprite navigationTargetSprite;
    private static readonly List<Vector2> IncidentApproachAssignments =
        new List<Vector2>(16);
    private static int incidentAssignmentFrame = -1;
    private static Vector2 incidentAssignmentPosition;
    private static int nextIncidentAssignmentSlot;
    private static int aStarBudgetFrame = -1;
    private static int aStarBuildsThisFrame;
    private static float nextGlobalAStarBuildTime;
    private static float nextExpensiveGridFallbackTime;
    private static int hostileCountCacheFrame = -1;
    private static int hostileCountCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetNavigationPerformanceState()
    {
        aStarBudgetFrame = -1;
        aStarBuildsThisFrame = 0;
        nextGlobalAStarBuildTime = 0f;
        nextExpensiveGridFallbackTime = 0f;
        hostileCountCacheFrame = -1;
        hostileCountCache = 0;
    }

    public ZeldaAiState CurrentState => currentState;
    private ZeldaAiState stateBeforeStun;
    private float stunRemaining;
    public bool IsStunned => currentState == ZeldaAiState.Stunned;
    private float royalCommandRemaining;
    private ZeldaPossessionProgressBar royalCommandBar;
    public bool IsIgnoringPlayer => royalCommandRemaining > 0f;
    public bool CanReceiveRoyalCommand(ZeldaFourWayMover player)
    {
        return isActiveAndEnabled && characterData != null && !characterData.IsDead &&
            !mover.isActiveAndEnabled && targetMover == player &&
            (currentState == ZeldaAiState.Alert || currentState == ZeldaAiState.Hostile);
    }
    public void ReceiveRoyalCommand()
    {
        mover.CancelAttackForStun();
        rb.velocity = Vector2.zero;
        BeginRecovery();
        royalCommandRemaining = 5f;
        UpdateRoyalCommandBar();
    }
    private void UpdateRoyalCommandBar()
    {
        if (!IsIgnoringPlayer || characterData.IsDead || mover.isActiveAndEnabled)
        {
            if (royalCommandBar != null) royalCommandBar.Hide();
            return;
        }
        if (royalCommandBar == null)
        {
            royalCommandBar = new GameObject("Royal Command Timer").AddComponent<ZeldaPossessionProgressBar>();
            royalCommandBar.transform.SetParent(transform, false);
        }
        royalCommandBar.transform.localPosition = (Vector3)stateIndicatorOffset + Vector3.up * 0.25f;
        royalCommandBar.SetProgress(royalCommandRemaining / 5f, new Color(1f, 0.85f, 0.08f));
    }
    public float StunRemaining => IsStunned ? stunRemaining : 0f;

    /// <summary>UnityEvent-friendly entry point. Reapplying refreshes the three-second duration.</summary>
    [ContextMenu("Stun (3 seconds)")]
    public void Stun()
    {
        StunForDuration(3f);
    }

    public void StunForDuration(float duration)
    {
        if (duration <= 0f) return;
        if (!isActiveAndEnabled || characterData == null || characterData.IsDead || mover.isActiveAndEnabled) return;
        if (!IsStunned)
        {
            stateBeforeStun = currentState;
            // Suspend, rather than exit/re-enter, so existing state timers and patrol progress survive.
            currentState = ZeldaAiState.Stunned;
        }
        stunRemaining = Mathf.Max(stunRemaining, duration);
        moveDirection = Vector2.zero;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        mover.CancelAttackForStun();
        UpdateCharacterVisual();
        UpdateStateIndicator();
    }

    private void FinishStun()
    {
        currentState = stateBeforeStun;
        stunRemaining = 0f;
        InvalidateNavigationPlan();
        bool reactive = currentState == ZeldaAiState.Suspicious || currentState == ZeldaAiState.Alert || currentState == ZeldaAiState.Hostile;
        if (reactive)
        {
            bool visible = targetMover != null && CanSeePlayer(targetMover);
            if (suspiciousCardboardBox != null) visible |= CanSeeWornCardboardBox(suspiciousCardboardBox, false);
            if (suspiciousGroundCardboardBox != null) visible |= CanSeeDrivenGroundCardboardBox(suspiciousGroundCardboardBox, false);
            if (hostileCardboardBox != null && !hostileCardboardBox.IsDestroyed && hostileCardboardBox.isActiveAndEnabled)
                visible |= CanSeeWorldPoint(hostileCardboardBox.WorldCenter, hostileCardboardBox.transform);
            if (!visible)
            {
                if (currentState == ZeldaAiState.Hostile) BeginSearch();
                else BeginRecovery();
            }
        }
        UpdateStateIndicator();
    }
    [System.Serializable]
    public sealed class SaveState
    {
        public ZeldaAiState state;
        public ZeldaAiState stateBeforeStun;
        public float stunRemaining;
        public float royalCommandRemaining;
        public Vector2 facing, home, homeFacing, lastKnownTarget, searchCenter, searchWaypoint;
        public float suspicion, warning, lostSight, searchTime, attackCooldown;
        public bool targetsPlayer, retaliation, regular, untilDeath, retained;
    }
    public SaveState CaptureSaveState()
    {
        return new SaveState {
            state = currentState, facing = facingDirection, home = initialScenePosition,
            stateBeforeStun = stateBeforeStun, stunRemaining = stunRemaining,
            royalCommandRemaining = royalCommandRemaining,
            homeFacing = initialSceneFacingDirection, lastKnownTarget = lastKnownTargetPosition,
            searchCenter = searchCenter, searchWaypoint = searchWaypoint,
            suspicion = suspicionValue, warning = warningTimer, lostSight = regularHostileLostSightTimer,
            searchTime = searchTimer, attackCooldown = attackCooldownTimer,
            targetsPlayer = targetMover != null && targetMover == ZeldaRuntimeRegistry.GetControlledMover(),
            retaliation = retaliationHostile, regular = regularHostile,
            untilDeath = hostileUntilTargetDeath, retained = retainLoadedUntilIdle
        };
    }
    public void ApplySaveState(SaveState state)
    {
        if (state == null) return;
        royalCommandRemaining = Mathf.Clamp(state.royalCommandRemaining, 0f, 5f);
        if (IsStunned) currentState = stateBeforeStun;
        ChangeState(state.state);
        stateBeforeStun = state.stateBeforeStun == ZeldaAiState.Stunned ? ZeldaAiState.Idle : state.stateBeforeStun;
        stunRemaining = Mathf.Max(0f, state.stunRemaining);
        facingDirection = state.facing; initialScenePosition = state.home;
        initialSceneFacingDirection = state.homeFacing; lastKnownTargetPosition = state.lastKnownTarget;
        searchCenter = state.searchCenter; searchWaypoint = state.searchWaypoint;
        suspicionValue = state.suspicion; warningTimer = state.warning;
        regularHostileLostSightTimer = state.lostSight; searchTimer = state.searchTime;
        attackCooldownTimer = state.attackCooldown; retaliationHostile = state.retaliation;
        regularHostile = state.regular; hostileUntilTargetDeath = state.untilDeath;
        retainLoadedUntilIdle = state.retained;
        targetMover = state.targetsPlayer ? ZeldaRuntimeRegistry.GetControlledMover() : null;
        moveDirection = Vector2.zero;
        InvalidateNavigationPlan();
        UpdateStateIndicator();
    }
    // Read the live state rather than the legacy hostility latch, so suspicion,
    // alerts, searches and recovery also finish while outside camera range.
    // Do not gate on isActiveAndEnabled: streaming may need to reactivate a
    // suspended/restored character that already has a non-idle state.
    public bool RequiresVisionStreamingRetention => currentState != ZeldaAiState.Idle || IsIgnoringPlayer;
    public Vector2 FacingDirection => facingDirection;
    public ZeldaFourWayMover CurrentTarget => targetMover;
    public float SuspicionValue => suspicionValue;
    public float SuspicionProgress => maximumSuspicion > 0f ? suspicionValue / maximumSuspicion : 0f;
    public bool IsPossessionLocked => possessionLocked;
    public bool IsInPossessableState =>
        currentState != ZeldaAiState.Suspicious &&
        currentState != ZeldaAiState.Alert &&
        currentState != ZeldaAiState.Hostile;
    protected Vector2 AiPosition => rb != null ? rb.position : (Vector2)transform.position;
    protected Vector2 InitialScenePosition => initialScenePosition;
    protected virtual float AiMovementSpeedMultiplier => 1f;
    protected virtual bool EnforcesPermissionAreas => true;
    protected virtual int MinimumPermissionViolationDifference => 1;

    /// <summary>
    /// Applies the surprise-hit bonus before this AI has become suspicious,
    /// alerted or hostile. The bonus can only be consumed once during the
    /// current awareness cycle and is restored after the AI fully returns to
    /// Idle.
    /// </summary>
    public int ResolveIncomingAttackDamage(int baseDamage)
    {
        ZeldaAiState awarenessState = IsStunned ? stateBeforeStun : currentState;
        if (baseDamage <= 0 || !isActiveAndEnabled ||
            mover == null || mover.isActiveAndEnabled ||
            hasConsumedUnawareFirstHit ||
            awarenessState == ZeldaAiState.Suspicious ||
            awarenessState == ZeldaAiState.Alert ||
            awarenessState == ZeldaAiState.Hostile)
        {
            return baseDamage;
        }

        hasConsumedUnawareFirstHit = true;
        return baseDamage > int.MaxValue / 3
            ? int.MaxValue
            : baseDamage * 3;
    }
    protected Vector2 AiMoveDirection
    {
        get => moveDirection;
        set => moveDirection = value;
    }

    protected Vector2 PlanAiPathTo(Vector2 worldTarget)
    {
        return PlanDirectionTo(worldTarget);
    }

    public virtual void InvestigatePosition(Vector2 investigationPosition)
    {
        if (IsStunned) return;
        if (!isActiveAndEnabled || characterData == null || characterData.IsDead || possessionLocked)
        {
            return;
        }

        targetMover = null;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        warningTimer = 0f;
        suspicionValue = 0f;
        searchTimer = 0f;
        searchWaypointTimer = 0f;
        previousSearchWaypointDistance = float.PositiveInfinity;
        investigationIncidentPosition = investigationPosition;
        investigationApproachPosition =
            ResolveInvestigationApproachPosition(investigationPosition);
        searchCenter = investigationApproachPosition;
        searchWaypoint = investigationApproachPosition;
        isTravelingToInvestigation = true;
        isDoorIncidentInvestigation = true;
        isPausingDuringInvestigation = false;
        investigationPauseTimer = 0f;
        previousFixedPosition = rb.position;
        cornerStuckTimer = 0f;
        cornerEscapeDirection = Vector2.zero;
        cornerEscapeTimer = 0f;
        cornerOscillationStartPosition = rb.position;
        cornerOscillationElapsed = 0f;
        cornerOscillationTravelDistance = 0f;
        moveDirection = Vector2.zero;
        SetFacingDirection(investigationPosition - rb.position);
        ChangeState(ZeldaAiState.Search);
    }

    private Vector2 ResolveInvestigationApproachPosition(
        Vector2 incidentPosition)
    {
        PrepareIncidentAssignmentBatch(incidentPosition);
        int slot = nextIncidentAssignmentSlot++;
        float minimumRadius = Mathf.Max(
            0.1f,
            investigationApproachMinimumRadius);
        float maximumRadius = Mathf.Max(
            minimumRadius,
            investigationApproachMaximumRadius);
        float radialStep = Mathf.Max(
            0.2f,
            Mathf.Min(gridCellSize, maximumRadius - minimumRadius));
        int ringCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                (maximumRadius - minimumRadius) / radialStep) + 1);
        int samples = Mathf.Clamp(
            investigationRelocationSamplesPerRing,
            4,
            32);

        // The golden angle distributes sequentially notified NPCs around the
        // event without placing successive actors into adjacent sectors.
        const float GoldenAngleRadians = 2.39996323f;
        float preferredAngle = slot * GoldenAngleRadians;
        int preferredRing = slot % ringCount;
        for (int ringOffset = 0; ringOffset < ringCount; ringOffset++)
        {
            int ring = (preferredRing + ringOffset) % ringCount;
            float radius = ringCount <= 1
                ? minimumRadius
                : Mathf.Lerp(
                    minimumRadius,
                    maximumRadius,
                    ring / (float)(ringCount - 1));
            for (int angleAttempt = 0;
                 angleAttempt < samples;
                 angleAttempt++)
            {
                int alternatingStep = (angleAttempt + 1) / 2;
                float angleSign = (angleAttempt & 1) == 0 ? 1f : -1f;
                float angle = preferredAngle + angleSign * alternatingStep *
                    Mathf.PI * 2f / samples;
                Vector2 candidate = incidentPosition +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsSearchWaypointClear(candidate) ||
                    IsInvestigationApproachReserved(candidate))
                {
                    continue;
                }

                IncidentApproachAssignments.Add(candidate);
                return candidate;
            }
        }

        // If the preferred observation annulus is crowded or obstructed,
        // continue expanding outward. The center itself is intentionally
        // never selected.
        float relocationRadius = Mathf.Max(
            maximumRadius,
            investigationTargetRelocationRadius);
        float ringStep = Mathf.Max(
            0.2f,
            Mathf.Min(gridCellSize, searchWaypointObstacleClearance));
        int extraRingCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                Mathf.Max(0f, relocationRadius - maximumRadius) /
                ringStep) + 1);
        for (int ring = 1; ring <= extraRingCount; ring++)
        {
            float radius = maximumRadius + ring * ringStep;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = preferredAngle +
                    Mathf.PI * 2f * sample / samples;
                Vector2 candidate = incidentPosition +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsSearchWaypointClear(candidate) ||
                    IsInvestigationApproachReserved(candidate))
                {
                    continue;
                }

                IncidentApproachAssignments.Add(candidate);
                return candidate;
            }
        }

        // No legal observation point exists in the configured area. Waiting
        // at the current valid position is safer than walking into the event
        // collider itself.
        return rb.position;
    }

    private static void PrepareIncidentAssignmentBatch(
        Vector2 incidentPosition)
    {
        bool sameBatch = incidentAssignmentFrame == Time.frameCount &&
                         (incidentAssignmentPosition - incidentPosition)
                         .sqrMagnitude <= 0.0025f;
        if (sameBatch)
        {
            return;
        }

        incidentAssignmentFrame = Time.frameCount;
        incidentAssignmentPosition = incidentPosition;
        nextIncidentAssignmentSlot = 0;
        IncidentApproachAssignments.Clear();
    }

    private bool IsInvestigationApproachReserved(Vector2 candidate)
    {
        float spacing = Mathf.Max(0f, investigationApproachTargetSpacing);
        float spacingSquared = spacing * spacing;
        for (int index = 0;
             index < IncidentApproachAssignments.Count;
             index++)
        {
            if ((IncidentApproachAssignments[index] - candidate).sqrMagnitude <
                spacingSquared)
            {
                return true;
            }
        }
        return false;
    }

    public virtual void OnPossessionWitnessed(
        ZeldaFourWayMover possessingMover,
        ZeldaFourWayMover newControlledMover)
    {
        if (IsStunned) return;
        if (!isActiveAndEnabled || possessionLocked || possessingMover == null ||
            newControlledMover == null || possessingMover == mover)
        {
            return;
        }

        ZeldaCharacterData possessingData = possessingMover.GetComponent<ZeldaCharacterData>();
        ZeldaCharacterData controlledData = newControlledMover.GetComponent<ZeldaCharacterData>();
        if (possessingData == null || possessingData.IsGhostLike ||
            controlledData == null || controlledData.IsGhostLike)
        {
            return;
        }

        bool wasHostileWitness = targetMover == possessingMover && regularHostile &&
                                 currentState == ZeldaAiState.Hostile;
        if (wasHostileWitness || CanSeePossessingPlayer(possessingMover))
        {
            BeginTemporaryHostility(newControlledMover, false);
        }
    }

    public bool IsActivelyHostileTo(ZeldaFourWayMover candidate)
    {
        return candidate != null && targetMover == candidate && currentState == ZeldaAiState.Hostile &&
               (regularHostile || hostileUntilTargetDeath);
    }

    /// <summary>
    /// Persistent scene snapshots deliberately do not resume a pursuit after
    /// the player has left and revisited the scene. Return any AI that was in
    /// a transient response state to the position and facing captured by its
    /// scene-authored instance in Awake.
    /// </summary>
    public void RecoverAfterPersistentSceneReturn(ZeldaAiState capturedState)
    {
        if (!isActiveAndEnabled ||
            mover == null ||
            mover.enabled ||
            characterData == null ||
            characterData.IsDead ||
            capturedState == ZeldaAiState.Idle)
        {
            return;
        }

        retainLoadedUntilIdle = capturedState == ZeldaAiState.Hostile ||
            capturedState == ZeldaAiState.Search ||
            capturedState == ZeldaAiState.Recovery;
        BeginRecovery();
    }

    public bool DoesCurrentPathIntersect(Collider2D targetCollider, float lookAheadDistance)
    {
        if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger ||
            bodyCollider == null || !bodyCollider.enabled || moveDirection.sqrMagnitude <= 0.0001f ||
            lookAheadDistance <= 0f)
        {
            return false;
        }

        ContactFilter2D pathFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };
        int hitCount = bodyCollider.Cast(
            moveDirection.normalized,
            pathFilter,
            movementHits,
            lookAheadDistance + collisionSkinWidth);

        for (int i = 0; i < hitCount; i++)
        {
            if (movementHits[i].collider == targetCollider)
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void Awake()
    {
        ZeldaRuntimeRegistry.Register(this);
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        characterData = GetComponent<ZeldaCharacterData>();
        mover = GetComponent<ZeldaFourWayMover>();
        initialScenePosition = rb.position;
        initialSceneFacingDirection = mover.InitialFacingVector.sqrMagnitude > 0.0001f
            ? mover.InitialFacingVector.normalized
            : Vector2.down;
        facingDirection = initialSceneFacingDirection;
        characterRenderer = FindCharacterRenderer();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        bodyCollider.isTrigger = false;
        CreateVisionVisual();
        CreateStateIndicator();
        CreateNavigationDebugVisual();
    }

    protected virtual void OnEnable()
    {
        // Normal activation initializes AI afresh; save restoration reapplies
        // the suspended state and remaining duration through ApplySaveState.
        if (IsStunned) currentState = stateBeforeStun;
        stunRemaining = 0f;
        retainLoadedUntilIdle = false;
        hasConsumedUnawareFirstHit = false;
        targetMover = null;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        warningTimer = 0f;
        suspicionValue = 0f;
        hostileIndicatorTimer = 0f;
        moveDirection = Vector2.zero;
        isTravelingToInvestigation = false;
        isDoorIncidentInvestigation = false;
        isPausingDuringInvestigation = false;
        investigationPauseTimer = 0f;
        InvalidateNavigationPlan();
        SetVisionVisible(true);
        SetStateIndicatorVisible(true);
        ChangeState(ZeldaAiState.Idle);
    }

    protected virtual void OnDisable()
    {
        if (royalCommandBar != null) royalCommandBar.Hide();
        moveDirection = Vector2.zero;
        SetPossessionLock(false);
        SetNavigationDebugVisible(false);
        SetVisionVisible(false);
        SetStateIndicatorVisible(false);
    }

    protected virtual void Update()
    {
        royalCommandRemaining = Mathf.Max(0f, royalCommandRemaining - Time.deltaTime);
        UpdateRoyalCommandBar();
        if (IsStunned)
        {
            moveDirection = Vector2.zero;
            if (characterData.IsDead) return;
            float elapsed = Mathf.Min(stunRemaining, Time.deltaTime);
            // This memory uses an absolute timestamp rather than a delta-time counter.
            lastSuspiciousCardboardBoxSeenTime += elapsed;
            stunRemaining -= elapsed;
            if (stunRemaining <= 0f) FinishStun();
            return;
        }
        if (characterData.IsDead)
        {
            moveDirection = Vector2.zero;
            return;
        }

        if (possessionLocked)
        {
            moveDirection = Vector2.zero;
            UpdateCharacterVisual();
            UpdatePossessionLockVisual();
            UpdateStateIndicator();
            return;
        }

        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        avoidanceDirectionTimer = Mathf.Max(0f, avoidanceDirectionTimer - Time.deltaTime);
        hostileIndicatorTimer = Mathf.Max(0f, hostileIndicatorTimer - Time.deltaTime);
        mover.AdvanceExternalAttackTimer(Time.deltaTime);
        UpdateTargetAndState(Time.deltaTime);
        TickCurrentState(Time.deltaTime);
        moveDirection = ApplyCharacterSeparation(moveDirection);
        UpdateFacingFromMovement();
        UpdateCharacterVisual();
        UpdateStateIndicator();
        UpdateNavigationDebugVisual();
    }

    protected virtual void FixedUpdate()
    {
        if (IsStunned)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            previousFixedPosition = rb.position;
            return;
        }
        if (characterData.IsDead || mover.IsAttacking || possessionLocked)
        {
            previousFixedPosition = rb.position;
            return;
        }

        Vector2 currentPosition = rb.position;
        Vector2 actualMovement = currentPosition - previousFixedPosition;
        float movedSquared = actualMovement.sqrMagnitude;
        previousFixedPosition = currentPosition;

        Vector2 wallCorrection = GetWallSeparationCorrection();
        if (wallCorrection.sqrMagnitude > 0.000001f)
        {
            rb.MovePosition(rb.position + wallCorrection);
            InvalidateNavigationPlan();
            cornerStuckTimer = 0f;
            return;
        }

        Vector2 effectiveMoveDirection = moveDirection;
        bool isYieldingToCharacter = cachedCharacterYieldWeight > 0.001f;
        if (isYieldingToCharacter && cornerEscapeTimer > 0f)
        {
            // Character yielding owns the local steering decision. A corner
            // escape selected before the encounter must not pull the actor
            // back toward a wall or repeatedly invalidate its grid route.
            cornerEscapeTimer = 0f;
            cornerEscapeDirection = Vector2.zero;
        }
        bool detectedOscillation =
            UpdateCornerOscillation(currentPosition, actualMovement);
        bool hasActiveAStarPath = useGridPathfinding &&
                                  gridPathIndex < gridPath.Count;
        if (detectedOscillation && hasActiveAStarPath &&
            !isYieldingToCharacter)
        {
            // Do not override a valid A* route with an arbitrary local escape
            // direction. Rebuild the graph from the real physics position so
            // a corner/contact mismatch is represented in the next route.
            ClearGridPath();
            nextGridPathRefreshTime = 0f;
            ResetObstacleFollowing();
            effectiveMoveDirection = Vector2.zero;
            cornerStuckTimer = 0f;
        }
        else if (detectedOscillation && !isYieldingToCharacter &&
                 cornerEscapeTimer <= 0f &&
                 TryChooseCornerEscapeDirection(
                     moveDirection,
                     out Vector2 oscillationEscapeDirection))
        {
            cornerEscapeDirection = oscillationEscapeDirection;
            cornerEscapeTimer = cornerEscapeHoldDuration;
            cornerStuckTimer = 0f;
        }

        if (cornerEscapeTimer > 0f)
        {
            cornerEscapeTimer = Mathf.Max(
                0f,
                cornerEscapeTimer - Time.fixedDeltaTime);
            effectiveMoveDirection = cornerEscapeDirection;
            if (cornerEscapeTimer <= 0f)
            {
                cornerEscapeDirection = Vector2.zero;
                InvalidateNavigationPlan();
            }
        }
        else if (moveDirection.sqrMagnitude > 0.0001f)
        {
            if (isYieldingToCharacter)
            {
                // A character is a temporary dynamic obstruction, not a
                // reason to discard the valid wall-avoiding A* route.
                cornerStuckTimer = 0f;
            }
            else if (movedSquared <= 0.000001f)
            {
                cornerStuckTimer += Time.fixedDeltaTime;
                if (cornerStuckTimer >= cornerStuckDetectionDuration &&
                    useGridPathfinding && gridPathIndex < gridPath.Count)
                {
                    ClearGridPath();
                    nextGridPathRefreshTime = 0f;
                    ResetObstacleFollowing();
                    effectiveMoveDirection = Vector2.zero;
                    cornerStuckTimer = 0f;
                }
                else if (cornerStuckTimer >= cornerStuckDetectionDuration &&
                    TryChooseCornerEscapeDirection(
                        moveDirection,
                        out Vector2 escapeDirection))
                {
                    cornerEscapeDirection = escapeDirection;
                    cornerEscapeTimer = cornerEscapeHoldDuration;
                    effectiveMoveDirection = cornerEscapeDirection;
                    cornerStuckTimer = 0f;
                }
            }
            else
            {
                cornerStuckTimer = 0f;
            }
        }
        else
        {
            cornerStuckTimer = 0f;
        }

        Vector2 displacement = effectiveMoveDirection * characterData.MoveSpeed *
                               Mathf.Max(0f, AiMovementSpeedMultiplier) * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + GetBlockedDisplacement(displacement));
    }

    private bool UpdateCornerOscillation(
        Vector2 currentPosition,
        Vector2 actualMovement)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            ResetCornerOscillationWindow(currentPosition);
            return false;
        }

        cornerOscillationElapsed += Time.fixedDeltaTime;
        cornerOscillationTravelDistance += actualMovement.magnitude;
        if (cornerOscillationElapsed < cornerOscillationWindowDuration)
        {
            return false;
        }

        float netDistance =
            Vector2.Distance(currentPosition, cornerOscillationStartPosition);
        float progressRatio = cornerOscillationTravelDistance > 0.0001f
            ? netDistance / cornerOscillationTravelDistance
            : 1f;
        bool nearObstacle =
            GetObstacleClearance(
                moveDirection.normalized,
                obstacleProbeDistance) <
            obstacleProbeDistance - collisionSkinWidth;
        bool oscillating =
            cornerOscillationTravelDistance > collisionSkinWidth &&
            progressRatio < cornerMinimumProgressRatio &&
            nearObstacle;

        ResetCornerOscillationWindow(currentPosition);
        return oscillating;
    }

    private void ResetCornerOscillationWindow(Vector2 position)
    {
        cornerOscillationStartPosition = position;
        cornerOscillationElapsed = 0f;
        cornerOscillationTravelDistance = 0f;
    }

    private bool TryChooseCornerEscapeDirection(
        Vector2 desiredDirection,
        out Vector2 escapeDirection)
    {
        escapeDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        int samples = Mathf.Clamp(cornerEscapeDirectionSamples, 8, 24);
        Vector2 normalizedDesired = desiredDirection.normalized;
        for (int i = 0; i < samples; i++)
        {
            float angle = Mathf.PI * 2f * i / samples;
            Vector2 candidate =
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float clearance =
                GetObstacleClearance(candidate, obstacleProbeDistance);
            if (clearance <= collisionSkinWidth)
            {
                continue;
            }

            float alignment = Vector2.Dot(candidate, normalizedDesired);
            // Clearance dominates the choice; alignment prevents an
            // unnecessary reversal when both sides of a corner are open.
            float score = clearance * 3f + alignment * 0.5f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            escapeDirection = candidate;
        }

        return escapeDirection.sqrMagnitude > 0.0001f;
    }

    private Vector2 GetWallSeparationCorrection()
    {
        Vector3 lossyScale = bodyCollider.transform.lossyScale;
        Vector2 colliderSize = new Vector2(
            bodyCollider.size.x * Mathf.Abs(lossyScale.x),
            bodyCollider.size.y * Mathf.Abs(lossyScale.y));
        Vector2 center = bodyCollider.transform.TransformPoint(bodyCollider.offset);
        // Only query the actual body footprint here. Planning clearance is
        // handled by the grid; treating a merely nearby wall as penetration
        // caused alternating correction and path-following every frame.
        Vector2 querySize = colliderSize;
        int overlapCount = Physics2D.OverlapBoxNonAlloc(
            center,
            querySize,
            bodyCollider.transform.eulerAngles.z,
            gridOverlapHits,
            solidCollisionLayers);

        Vector2 correction = Vector2.zero;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = gridOverlapHits[i];
            gridOverlapHits[i] = null;
            if (overlap == null || overlap == bodyCollider || overlap.isTrigger ||
                overlap.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(overlap) ||
                IsNonParticipatingCharacterCollider(overlap) ||
                overlap.GetComponentInParent<ZeldaCharacterData>() != null)
            {
                continue;
            }

            ColliderDistance2D distance = bodyCollider.Distance(overlap);
            if (!distance.isValid || !distance.isOverlapped)
            {
                continue;
            }

            correction += distance.normal *
                          (distance.distance - collisionSkinWidth);
        }

        float maximumStep = Mathf.Max(0.01f, maximumWallSeparationStep);
        return correction.sqrMagnitude > maximumStep * maximumStep
            ? correction.normalized * maximumStep
            : correction;
    }

    protected virtual void LateUpdate()
    {
        if (IsStunned) return;
        if (Time.time < nextVisionVisualRefreshTime)
        {
            return;
        }

        float stagger = Mathf.Lerp(
            0.85f,
            1.15f,
            Mathf.Abs(GetInstanceID() % 19) / 19f);
        nextVisionVisualRefreshTime = Time.time +
            Mathf.Max(0.02f, visionVisualRefreshInterval) * stagger;

        if (hasBuiltVisionMesh && visionRenderer != null &&
            !visionRenderer.isVisible)
        {
            // Retained hostile/recovering NPCs can be far outside every
            // camera. Their visual cone has no consumer there; perception is
            // evaluated separately and is intentionally not skipped.
            return;
        }

        UpdateVisionMesh();
        hasBuiltVisionMesh = true;
    }

    protected virtual void TickIdle(float deltaTime)
    {
        moveDirection = Vector2.zero;
        hasNavigationDebugTarget = false;
    }

    protected virtual void TickSuspicious(float deltaTime)
    {
        if (suspiciousGroundCardboardBox != null)
        {
            Vector2 toBox = suspiciousGroundCardboardBox.WorldCenter - rb.position;
            SetFacingDirection(toBox);
            if (isInspectingCardboardBox &&
                toBox.magnitude <= attackDistance &&
                Vector2.Angle(facingDirection, toBox) <= attackFacingTolerance)
            {
                moveDirection = Vector2.zero;
                TryAttackTarget(toBox);
                return;
            }

            float approachRadius = isInspectingCardboardBox
                ? Mathf.Max(
                    gridWaypointTolerance,
                    attackDistance - gridWaypointTolerance)
                : cardboardBoxApproachDistance;
            Vector2 followPosition = ResolveSharedFollowPosition(
                suspiciousGroundCardboardBox,
                suspiciousGroundCardboardBox.WorldCenter,
                approachRadius);
            float followTolerance = Mathf.Max(
                gridWaypointTolerance,
                warningDistanceTolerance);
            moveDirection = Vector2.Distance(rb.position, followPosition) >
                    followTolerance
                ? PlanDirectionTo(followPosition, followTolerance)
                : Vector2.zero;
            return;
        }

        if (suspiciousCardboardBox != null &&
            suspiciousCardboardBox.WearerMover != null)
        {
            Vector2 toBox = suspiciousCardboardBox.WorldCenter - rb.position;
            SetFacingDirection(toBox);
            if (isInspectingCardboardBox &&
                toBox.magnitude <= attackDistance &&
                Vector2.Angle(facingDirection, toBox) <= attackFacingTolerance)
            {
                moveDirection = Vector2.zero;
                TryAttackTarget(toBox);
                return;
            }

            float approachRadius = isInspectingCardboardBox
                ? Mathf.Max(
                    gridWaypointTolerance,
                    attackDistance - gridWaypointTolerance)
                : cardboardBoxApproachDistance;
            Vector2 followPosition = ResolveSharedFollowPosition(
                suspiciousCardboardBox,
                suspiciousCardboardBox.WorldCenter,
                approachRadius);
            float followTolerance = Mathf.Max(
                gridWaypointTolerance,
                warningDistanceTolerance);
            moveDirection = Vector2.Distance(rb.position, followPosition) >
                    followTolerance
                ? PlanDirectionTo(followPosition, followTolerance)
                : Vector2.zero;
            return;
        }

        moveDirection = Vector2.zero;
        if (targetMover != null)
        {
            SetFacingDirection((Vector2)targetMover.transform.position - rb.position);
        }
    }

    protected virtual void TickAlert(float deltaTime)
    {
        if (activePermissionArea == null || targetMover == null)
        {
            moveDirection = Vector2.zero;
            SetFacingDirection(lastKnownTargetPosition - rb.position);
            return;
        }

        Vector2 targetPosition = targetMover.transform.position;
        Vector2 toTarget = targetPosition - rb.position;
        SetFacingDirection(toTarget);
        Vector2 followPosition = ResolveSharedFollowPosition(
            targetMover,
            targetPosition,
            warningFollowDistance);
        float followTolerance = Mathf.Max(
            gridWaypointTolerance,
            warningDistanceTolerance);
        moveDirection = Vector2.Distance(rb.position, followPosition) >
                followTolerance
            ? PlanDirectionTo(followPosition, followTolerance)
            : Vector2.zero;
    }

    protected virtual void TickSearch(float deltaTime)
    {
        if (isTravelingToInvestigation)
        {
            Vector2 toInvestigation =
                investigationApproachPosition - rb.position;
            if (toInvestigation.sqrMagnitude > searchWaypointTolerance * searchWaypointTolerance)
            {
                moveDirection = PlanDirectionTo(
                    investigationApproachPosition);
                return;
            }

            isTravelingToInvestigation = false;
            searchTimer = 0f;
            searchWaypointTimer = 0f;
            if (isDoorIncidentInvestigation)
            {
                BeginInvestigationPause();
                return;
            }

            ChooseNextSearchWaypoint();
        }

        if (isDoorIncidentInvestigation)
        {
            if (isPausingDuringInvestigation)
            {
                moveDirection = Vector2.zero;
                SetFacingDirection(
                    investigationIncidentPosition - rb.position);
                investigationPauseTimer += deltaTime;
                if (investigationPauseTimer < investigationWaypointPauseDuration)
                {
                    return;
                }

                isPausingDuringInvestigation = false;
                investigationPauseTimer = 0f;
                ChooseNextSearchWaypoint();
            }

            Vector2 investigationWaypointOffset = searchWaypoint - rb.position;
            UpdateSearchWaypointProgress(investigationWaypointOffset, deltaTime);
            if (investigationWaypointOffset.sqrMagnitude <=
                    searchWaypointTolerance * searchWaypointTolerance ||
                searchWaypointTimer >= searchWaypointChangeInterval)
            {
                BeginInvestigationPause();
                return;
            }

            moveDirection = PlanDirectionTo(searchWaypoint);
            return;
        }

        Vector2 toWaypoint = searchWaypoint - rb.position;
        UpdateSearchWaypointProgress(toWaypoint, deltaTime);
        if (toWaypoint.sqrMagnitude <= searchWaypointTolerance * searchWaypointTolerance ||
            searchWaypointTimer >= searchWaypointChangeInterval)
        {
            ChooseNextSearchWaypoint();
            toWaypoint = searchWaypoint - rb.position;
        }

        moveDirection = toWaypoint.sqrMagnitude > 0f
            ? PlanDirectionTo(searchWaypoint)
            : Vector2.zero;
    }

    protected virtual void TickRecovery(float deltaTime)
    {
        Vector2 toInitialPosition = initialScenePosition - rb.position;
        if (toInitialPosition.sqrMagnitude <= recoveryPositionTolerance * recoveryPositionTolerance)
        {
            moveDirection = Vector2.zero;
            SetFacingDirection(initialSceneFacingDirection);
            UpdateCharacterVisual();
            CompleteRecovery();
            return;
        }

        moveDirection = PlanDirectionTo(initialScenePosition);
    }

    protected virtual void TickHostile(float deltaTime)
    {
        if (hostileCardboardBox != null)
        {
            if (hostileCardboardBox.IsDestroyed ||
                !hostileCardboardBox.isActiveAndEnabled)
            {
                hostileCardboardBox = null;
                moveDirection = Vector2.zero;
                return;
            }

            Vector2 toBox = hostileCardboardBox.WorldCenter - rb.position;
            float boxDistance = toBox.magnitude;
            SetFacingDirection(toBox);
            if (boxDistance <= attackDistance &&
                Vector2.Angle(facingDirection, toBox) <= attackFacingTolerance)
            {
                moveDirection = Vector2.zero;
                TryAttackTarget(toBox);
                return;
            }

            float approachRadius = Mathf.Max(
                gridWaypointTolerance,
                attackDistance - gridWaypointTolerance);
            Vector2 followPosition = ResolveSharedFollowPosition(
                hostileCardboardBox,
                hostileCardboardBox.WorldCenter,
                approachRadius);
            moveDirection = PlanDirectionTo(
                followPosition,
                gridWaypointTolerance);
            return;
        }

        if (targetMover == null)
        {
            moveDirection = Vector2.zero;
            return;
        }

        Vector2 toTarget = (Vector2)targetMover.transform.position - rb.position;
        float distance = toTarget.magnitude;
        SetFacingDirection(toTarget);

        if (distance <= attackDistance &&
            Vector2.Angle(facingDirection, toTarget) <= attackFacingTolerance)
        {
            moveDirection = Vector2.zero;
            TryAttackTarget(toTarget);
            return;
        }

        float hostileApproachRadius = Mathf.Max(
            gridWaypointTolerance,
            attackDistance - gridWaypointTolerance);
        Vector2 hostileFollowPosition = ResolveSharedFollowPosition(
            targetMover,
            targetMover.transform.position,
            hostileApproachRadius);
        moveDirection = PlanDirectionTo(
            hostileFollowPosition,
            gridWaypointTolerance);
    }

    private Vector2 ResolveSharedFollowPosition(
        Object followTarget,
        Vector2 targetCenter,
        float baseRadius)
    {
        if (followTarget == null)
        {
            return targetCenter;
        }

        int ownInstanceId = GetInstanceID();
        int followerCount = 0;
        int followerRank = 0;
        foreach (ZeldaCharacterAiBase otherAi in
                 ZeldaRuntimeRegistry.AiCharacters)
        {
            if (otherAi == null || !otherAi.isActiveAndEnabled ||
                otherAi.GetSharedFollowTarget() != followTarget)
            {
                continue;
            }

            followerCount++;
            if (otherAi.GetInstanceID() < ownInstanceId)
            {
                followerRank++;
            }
        }

        float radius = Mathf.Max(gridWaypointTolerance, baseRadius);
        if (followerCount <= 1)
        {
            Vector2 radialDirection = rb.position - targetCenter;
            if (radialDirection.sqrMagnitude <= 0.0001f)
            {
                radialDirection = -facingDirection;
            }
            return targetCenter + radialDirection.normalized * radius;
        }

        int remainingRank = followerRank;
        int ringIndex = 0;
        int ringCapacity = 6;
        while (remainingRank >= ringCapacity)
        {
            remainingRank -= ringCapacity;
            ringIndex++;
            ringCapacity = 6 * (ringIndex + 1);
        }

        int occupiedSlots = ringIndex == 0 && followerCount <= ringCapacity
            ? followerCount
            : ringCapacity;
        float bodySpacing = bodyCollider != null
            ? Mathf.Max(
                bodyCollider.bounds.size.x,
                bodyCollider.bounds.size.y) +
              characterAvoidanceClearancePadding
            : characterSeparationDistance;
        float ringSpacing = Mathf.Max(
            characterSeparationDistance,
            bodySpacing);
        radius += ringIndex * ringSpacing;
        float halfSlotAngle = Mathf.PI /
                              Mathf.Max(2, occupiedSlots);
        float chordFactor = 2f * Mathf.Sin(halfSlotAngle);
        if (chordFactor > 0.0001f)
        {
            // Ensure adjacent assigned points are separated by at least one
            // character footprint instead of merely differing numerically.
            radius = Mathf.Max(radius, ringSpacing / chordFactor);
        }

        // A target-derived rotation keeps slots deterministic across frames
        // without forcing every group to align to the same world axes.
        float targetRotation = Mathf.Repeat(
            Mathf.Abs(followTarget.GetInstanceID()) * 0.6180339f,
            1f) * 360f;
        float angle = targetRotation +
                      remainingRank * (360f / Mathf.Max(1, occupiedSlots));
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians));
        return targetCenter + direction * radius;
    }

    private Object GetSharedFollowTarget()
    {
        if (currentState == ZeldaAiState.Suspicious)
        {
            if (suspiciousGroundCardboardBox != null)
            {
                return suspiciousGroundCardboardBox;
            }
            if (suspiciousCardboardBox != null)
            {
                return suspiciousCardboardBox;
            }
        }
        else if (currentState == ZeldaAiState.Hostile &&
                 hostileCardboardBox != null)
        {
            return hostileCardboardBox;
        }

        return currentState == ZeldaAiState.Alert ||
               currentState == ZeldaAiState.Hostile
            ? targetMover
            : null;
    }

    protected virtual void OnStateEntered(ZeldaAiState previousState, ZeldaAiState newState) { }
    protected virtual void OnStateExited(ZeldaAiState oldState, ZeldaAiState nextState) { }

    protected virtual bool CanSeePlayer(ZeldaFourWayMover candidate)
    {
        if (IsIgnoringPlayer) return false;
        if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject == gameObject)
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        if (candidateData == null || candidateData.IsDead || !candidateData.CanBeDetectedByAi)
        {
            return false;
        }

        CardboardBoxWearState wornBox = candidate.ActiveCardboardBox;
        if (wornBox != null &&
            !IsActivelyHostileTo(candidate) &&
            !IsRecognizedPermissionTarget(candidate))
        {
            return false;
        }

        Vector2 origin = rb.position;
        Vector2 toTarget = (Vector2)candidate.transform.position - origin;
        float targetAngle = Vector2.Angle(facingDirection, toTarget);
        bool isInMainVision = targetAngle <= visionAngle * 0.5f &&
            toTarget.sqrMagnitude <= visionRadius * visionRadius;
        bool isInPeripheralVision = targetAngle <= peripheralVisionAngle * 0.5f &&
            toTarget.sqrMagnitude <= peripheralVisionRadius * peripheralVisionRadius;
        if (!isInMainVision && !isInPeripheralVision)
        {
            return false;
        }

        return !IsVisionBlocked(origin, toTarget.normalized, toTarget.magnitude, candidate.transform);
    }

    private bool IsRecognizedPermissionTarget(ZeldaFourWayMover candidate)
    {
        return candidate != null &&
            candidate == targetMover &&
            candidate == recognizedPermissionTarget;
    }

    protected virtual void TryAttackTarget(Vector2 toTarget)
    {
        if (attackCooldownTimer > 0f || !characterData.CanAttack)
        {
            return;
        }

        if (mover.TryPerformAttack(toTarget))
        {
            attackCooldownTimer = Mathf.Max(attackCooldown, characterData.AttackDuration);
        }
    }

    public virtual void OnCharacterDamagedBy(ZeldaCharacterData attacker)
    {
        if (IsIgnoringPlayer) return;
        if (IsStunned) return;
        if (!isActiveAndEnabled || possessionLocked || attacker == null ||
            attacker == characterData || attacker.IsDead)
        {
            return;
        }

        ZeldaFourWayMover attackerMover = attacker.GetComponent<ZeldaFourWayMover>();
        if (attackerMover == null || !attackerMover.isActiveAndEnabled)
        {
            return;
        }

        targetMover = attackerMover;
        activePermissionArea = FindHighestPermissionArea(attacker.transform.position);
        recognizedPermissionTarget = null;
        lastKnownTargetPosition = attacker.transform.position;
        suspicionValue = 0f;
        warningTimer = 0f;
        hostileUntilTargetDeath = true;
        retaliationHostile = true;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        moveDirection = Vector2.zero;
        SetFacingDirection((Vector2)attacker.transform.position - rb.position);
        ChangeState(ZeldaAiState.Hostile);
    }

    /// <summary>
    /// Makes a witnessed, player-caused DoorData destruction an immediate
    /// hostile act. This deliberately bypasses cardboard concealment: the AI
    /// has seen the attack happen, so a worn box cannot disguise its source.
    /// Returns true when this AI witnessed and reacted to the destruction.
    /// </summary>
    public virtual bool OnPlayerDestroyedDoor(ZeldaCharacterData attacker)
    {
        if (IsIgnoringPlayer) return false;
        if (IsStunned) return false;
        if (!isActiveAndEnabled || possessionLocked || attacker == null ||
            attacker == characterData || attacker.IsDead)
        {
            return false;
        }

        ZeldaFourWayMover attackerMover =
            attacker.GetComponent<ZeldaFourWayMover>();
        if (attackerMover == null || !attackerMover.isActiveAndEnabled ||
            ZeldaRuntimeRegistry.GetControlledMover() != attackerMover)
        {
            return false;
        }

        ZeldaCharacterData attackerData =
            attackerMover.GetComponent<ZeldaCharacterData>();
        if (attackerData == null || attackerData.IsDead ||
            attackerData.IsGhostLike)
        {
            return false;
        }

        if (!CanSeeWorldPoint(
                attackerMover.transform.position,
                attackerMover.transform))
        {
            return false;
        }

        BeginTemporaryHostility(attackerMover, false);
        return currentState == ZeldaAiState.Hostile &&
            targetMover == attackerMover;
    }

    public virtual void SetPossessionLock(bool isLocked)
    {
        if (possessionLocked == isLocked)
        {
            return;
        }

        possessionLocked = isLocked;
        moveDirection = Vector2.zero;

        if (isLocked)
        {
            CapturePossessionVisualBase();
        }
        else
        {
            RestorePossessionVisualBase();
        }
    }

    private void CapturePossessionVisualBase()
    {
        possessionVisualRenderer = FindCharacterRenderer();
        if (possessionVisualRenderer == null)
        {
            hasPossessionVisualBase = false;
            return;
        }

        possessionVisualBasePosition = possessionVisualRenderer.transform.localPosition;
        hasPossessionVisualBase = true;
    }

    private void UpdatePossessionLockVisual()
    {
        SpriteRenderer activeRenderer = FindCharacterRenderer();
        if (activeRenderer != possessionVisualRenderer || !hasPossessionVisualBase)
        {
            CapturePossessionVisualBase();
        }

        if (!hasPossessionVisualBase || possessionVisualRenderer == null)
        {
            return;
        }

        float shake = Mathf.Sin(Time.time * possessionShakeSpeed) * possessionShakeAmount;
        possessionVisualRenderer.transform.localPosition =
            possessionVisualBasePosition + new Vector3(shake, 0f, 0f);
    }

    private void RestorePossessionVisualBase()
    {
        if (hasPossessionVisualBase && possessionVisualRenderer != null)
        {
            possessionVisualRenderer.transform.localPosition = possessionVisualBasePosition;
        }

        possessionVisualRenderer = null;
        hasPossessionVisualBase = false;
    }

    protected void ChangeState(ZeldaAiState newState)
    {
        if (IsIgnoringPlayer && (newState == ZeldaAiState.Suspicious || newState == ZeldaAiState.Alert || newState == ZeldaAiState.Hostile)) return;
        if (IsStunned) return;
        if (currentState == newState)
        {
            return;
        }

        ZeldaAiState previousState = currentState;
        OnStateExited(previousState, newState);
        currentState = newState;
        if (newState == ZeldaAiState.Hostile)
        {
            retainLoadedUntilIdle = true;
        }
        else if (newState == ZeldaAiState.Idle)
        {
            retainLoadedUntilIdle = false;
            hasConsumedUnawareFirstHit = false;
        }
        InvalidateNavigationPlan();
        if (newState == ZeldaAiState.Hostile)
        {
            hostileIndicatorTimer = hostileIndicatorDuration;
        }
        OnStateEntered(previousState, newState);
    }

    protected void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            facingDirection = direction.normalized;
        }
    }

    protected virtual void UpdateFacingFromMovement()
    {
        if (!mover.IsAttacking && moveDirection.sqrMagnitude > 0.0001f)
        {
            SetFacingDirection(moveDirection);
        }
    }

    private void UpdateTargetAndState(float deltaTime)
    {
        if (IsIgnoringPlayer) return;
        ZeldaFourWayMover visiblePossessingPlayer = FindVisiblePossessingPlayer();
        if (visiblePossessingPlayer != null)
        {
            if (targetMover != visiblePossessingPlayer || !regularHostile)
            {
                BeginTemporaryHostility(visiblePossessingPlayer, true);
            }
            else
            {
                lastKnownTargetPosition = visiblePossessingPlayer.transform.position;
                regularHostileLostSightTimer = 0f;
                ChangeState(ZeldaAiState.Hostile);
            }

            return;
        }

        ZeldaFourWayMover visibleHostilePlayer = FindVisiblePlayer();
        if (visibleHostilePlayer != null &&
            !IsActivelyHostileTo(visibleHostilePlayer) &&
            IsNonGhostPlayerHostileToAnotherAi(visibleHostilePlayer))
        {
            BeginTemporaryHostility(visibleHostilePlayer, false);
            return;
        }

        if (hostileUntilTargetDeath)
        {
            if (!IsTargetAlive())
            {
                BeginRecovery();
                return;
            }

            if (retaliationHostile && CanSeePlayer(targetMover))
            {
                hostileUntilTargetDeath = false;
                retaliationHostile = false;
                regularHostile = true;
                regularHostileLostSightTimer = 0f;
            }

            if (!CanSeePlayer(targetMover))
            {
                CardboardBoxPickupItem visibleGroundBox =
                    FindNearestVisibleGroundBox();
                if (visibleGroundBox != null)
                {
                    hostileCardboardBox = visibleGroundBox;
                    ChangeState(ZeldaAiState.Hostile);
                    return;
                }
            }

            ChangeState(ZeldaAiState.Hostile);
            return;
        }

        if (regularHostile)
        {
            if (!IsTargetAlive())
            {
                BeginRecovery();
                return;
            }

            if (CanSeePlayer(targetMover))
            {
                hostileCardboardBox = null;
                regularHostileLostSightTimer = 0f;
                ChangeState(ZeldaAiState.Hostile);
                return;
            }

            CardboardBoxPickupItem visibleGroundBox =
                FindNearestVisibleGroundBox();
            if (visibleGroundBox != null)
            {
                hostileCardboardBox = visibleGroundBox;
                ChangeState(ZeldaAiState.Hostile);
                return;
            }

            regularHostileLostSightTimer += deltaTime;
            if (regularHostileLostSightTimer >= regularHostileLoseSightDelay)
            {
                BeginSearch();
                return;
            }

            ChangeState(ZeldaAiState.Hostile);
            return;
        }

        if (currentState == ZeldaAiState.Search)
        {
            ZeldaFourWayMover rediscoveredPlayer = FindVisiblePlayer();
            if (rediscoveredPlayer != null)
            {
                targetMover = rediscoveredPlayer;
                lastKnownTargetPosition = rediscoveredPlayer.transform.position;
                regularHostile = true;
                regularHostileLostSightTimer = 0f;
                ChangeState(ZeldaAiState.Hostile);
                return;
            }

            if (UpdateCardboardBoxSuspicion(deltaTime))
            {
                return;
            }

            if (!isTravelingToInvestigation)
            {
                searchTimer += deltaTime;
            }
            float activeSearchDuration = isDoorIncidentInvestigation
                ? investigationDuration
                : searchDuration;
            if (searchTimer >= activeSearchDuration)
            {
                BeginRecovery();
            }

            return;
        }

        if (currentState == ZeldaAiState.Recovery && FindVisiblePlayer() == null)
        {
            UpdateCardboardBoxSuspicion(deltaTime);
            return;
        }

        if (UpdateCardboardBoxSuspicion(deltaTime))
        {
            return;
        }

        if (!EnforcesPermissionAreas)
        {
            if (currentState == ZeldaAiState.Alert ||
                currentState == ZeldaAiState.Suspicious)
            {
                ResetPermissionResponse();
            }
            return;
        }

        if (activePermissionArea != null && currentState == ZeldaAiState.Alert)
        {
            CardboardBoxWearState targetBox = targetMover != null
                ? targetMover.ActiveCardboardBox
                : null;
            if (targetBox != null && targetBox.IsStationary &&
                !IsRecognizedPermissionTarget(targetMover))
            {
                BeginRecovery();
                return;
            }

            if (!IsTargetAlive() || !activePermissionArea.Contains(targetMover.transform.position))
            {
                BeginRecovery();
                return;
            }

            warningTimer += deltaTime;
            if (warningTimer >= warningEscalationTime)
            {
                hostileUntilTargetDeath = false;
                retaliationHostile = false;
                regularHostile = true;
                regularHostileLostSightTimer = 0f;
                ChangeState(ZeldaAiState.Hostile);
            }

            return;
        }

        ZeldaFourWayMover visiblePlayer = FindVisiblePlayer();
        if (visiblePlayer == null)
        {
            if (currentState == ZeldaAiState.Suspicious)
            {
                suspicionValue = Mathf.Max(0f,
                    suspicionValue - suspicionDecreasePerSecond * deltaTime);
                if (suspicionValue <= 0f)
                {
                    ResetPermissionResponse();
                }

                return;
            }

            ResetPermissionResponse();
            return;
        }

        ZeldaCharacterData playerData = visiblePlayer.GetComponent<ZeldaCharacterData>();
        PermissionArea permissionArea = FindHighestPermissionArea(visiblePlayer.transform.position);
        bool isBelowDefaultPermission = playerData != null &&
            playerData.PermissionLevel < PermissionArea.DefaultPermissionLevel;
        if (permissionArea == null && !isBelowDefaultPermission)
        {
            if (currentState == ZeldaAiState.Recovery)
            {
                return;
            }

            ResetPermissionResponse();
            return;
        }

        int requiredPermissionLevel = permissionArea != null
            ? Mathf.Max(PermissionArea.DefaultPermissionLevel, permissionArea.PermissionLevel)
            : PermissionArea.DefaultPermissionLevel;
        int permissionDifference = requiredPermissionLevel - playerData.PermissionLevel;
        if (permissionDifference <
            Mathf.Max(1, MinimumPermissionViolationDifference))
        {
            if (currentState == ZeldaAiState.Recovery)
            {
                return;
            }

            ResetPermissionResponse();
            return;
        }

        targetMover = visiblePlayer;
        activePermissionArea = permissionArea;
        recognizedPermissionTarget = visiblePlayer;
        lastKnownTargetPosition = visiblePlayer.transform.position;

        float targetDistance = Vector2.Distance(rb.position, visiblePlayer.transform.position);
        if (targetDistance <= immediateResponseDistance)
        {
            suspicionValue = 0f;
            BeginPermissionResponse(permissionDifference);
            return;
        }

        suspicionValue = Mathf.Min(maximumSuspicion,
            suspicionValue + suspicionIncreasePerSecond * deltaTime);
        ChangeState(ZeldaAiState.Suspicious);
        if (suspicionValue >= maximumSuspicion)
        {
            suspicionValue = 0f;
            BeginPermissionResponse(permissionDifference);
        }
    }

    private bool IsNonGhostPlayerHostileToAnotherAi(ZeldaFourWayMover candidate)
    {
        if (candidate == null || !candidate.isActiveAndEnabled)
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        if (candidateData == null || candidateData.IsGhostLike)
        {
            return false;
        }

        foreach (ZeldaCharacterAiBase otherAi in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (otherAi != null && otherAi != this && otherAi.isActiveAndEnabled &&
                otherAi.IsActivelyHostileTo(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginTemporaryHostility(ZeldaFourWayMover hostileTarget, bool allowGhostTarget)
    {
        if (hostileTarget == null || !hostileTarget.isActiveAndEnabled)
        {
            return;
        }

        ZeldaCharacterData targetData = hostileTarget.GetComponent<ZeldaCharacterData>();
        if (targetData == null || targetData.IsDead ||
            (!allowGhostTarget && targetData.IsGhostLike))
        {
            return;
        }

        targetMover = hostileTarget;
        lastKnownTargetPosition = hostileTarget.transform.position;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = true;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        suspicionValue = 0f;
        warningTimer = 0f;
        isTravelingToInvestigation = false;
        moveDirection = Vector2.zero;
        SetFacingDirection((Vector2)hostileTarget.transform.position - rb.position);
        ChangeState(ZeldaAiState.Hostile);
    }

    private ZeldaFourWayMover FindVisiblePossessingPlayer()
    {
        ZeldaFourWayMover closest = null;
        float closestDistance = float.MaxValue;
        foreach (ZeldaFourWayMover candidate in ZeldaRuntimeRegistry.Movers)
        {
            if (!CanSeePossessingPlayer(candidate))
            {
                continue;
            }

            float distance = ((Vector2)candidate.transform.position - rb.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool CanSeePossessingPlayer(ZeldaFourWayMover candidate)
    {
        if (IsIgnoringPlayer) return false;
        if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsPossessionInProgress ||
            candidate.gameObject == gameObject)
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        if (candidateData == null || candidateData.IsDead || candidateData.IsGhostLike)
        {
            return false;
        }

        if (candidate.ActiveCardboardBox != null &&
            !IsActivelyHostileTo(candidate))
        {
            return false;
        }

        Vector2 origin = rb.position;
        Vector2 toTarget = (Vector2)candidate.transform.position - origin;
        float targetAngle = Vector2.Angle(facingDirection, toTarget);
        bool isInMainVision = targetAngle <= visionAngle * 0.5f &&
            toTarget.sqrMagnitude <= visionRadius * visionRadius;
        bool isInPeripheralVision = targetAngle <= peripheralVisionAngle * 0.5f &&
            toTarget.sqrMagnitude <= peripheralVisionRadius * peripheralVisionRadius;
        if (!isInMainVision && !isInPeripheralVision)
        {
            return false;
        }

        return !IsVisionBlocked(origin, toTarget.normalized, toTarget.magnitude, candidate.transform);
    }

    private void BeginPermissionResponse(int permissionDifference)
    {
        recognizedPermissionTarget = targetMover;
        if (permissionDifference >= 2)
        {
            hostileUntilTargetDeath = false;
            retaliationHostile = false;
            regularHostile = true;
            regularHostileLostSightTimer = 0f;
            ChangeState(ZeldaAiState.Hostile);
            return;
        }

        warningTimer = 0f;
        ChangeState(ZeldaAiState.Alert);
    }

    private void BeginSearch()
    {
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        warningTimer = 0f;
        suspicionValue = 0f;
        searchTimer = 0f;
        searchWaypointTimer = 0f;
        isTravelingToInvestigation = false;
        isDoorIncidentInvestigation = false;
        isPausingDuringInvestigation = false;
        investigationPauseTimer = 0f;
        searchCenter = rb.position;
        ChooseNextSearchWaypoint();
        ChangeState(ZeldaAiState.Search);
    }

    private void BeginRecovery()
    {
        targetMover = null;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        warningTimer = 0f;
        suspicionValue = 0f;
        searchTimer = 0f;
        searchWaypointTimer = 0f;
        isTravelingToInvestigation = false;
        isDoorIncidentInvestigation = false;
        isPausingDuringInvestigation = false;
        investigationPauseTimer = 0f;
        moveDirection = Vector2.zero;
        ChangeState(ZeldaAiState.Recovery);
    }

    private void CompleteRecovery()
    {
        ResetPermissionResponse();
    }

    private void ChooseNextSearchWaypoint()
    {
        searchWaypointTimer = 0f;
        previousSearchWaypointDistance = float.PositiveInfinity;
        for (int attempt = 0; attempt < searchWaypointSamplingAttempts; attempt++)
        {
            Vector2 candidate =
                searchCenter + Random.insideUnitCircle * searchRadius;
            if (isDoorIncidentInvestigation &&
                (candidate - investigationIncidentPosition).sqrMagnitude <
                investigationApproachMinimumRadius *
                investigationApproachMinimumRadius)
            {
                continue;
            }

            if (!IsSearchWaypointClear(candidate))
            {
                continue;
            }

            searchWaypoint = candidate;
            return;
        }

        // The current position is known to be reachable. Waiting here and
        // sampling again is safer than committing to a point against a wall.
        searchWaypoint = rb.position;
    }

    private void UpdateSearchWaypointProgress(
        Vector2 waypointOffset,
        float deltaTime)
    {
        float distance = waypointOffset.magnitude;
        const float meaningfulProgress = 0.01f;
        if (distance < previousSearchWaypointDistance - meaningfulProgress)
        {
            previousSearchWaypointDistance = distance;
            searchWaypointTimer = 0f;
            return;
        }

        // This timer now measures continuous lack of progress rather than the
        // total time spent traveling. Detours around a wall therefore keep
        // their target while a genuinely stuck AI eventually resamples.
        searchWaypointTimer += deltaTime;
    }

    private bool IsSearchWaypointClear(Vector2 candidate)
    {
        Vector3 lossyScale = bodyCollider.transform.lossyScale;
        float bodyRadius = Mathf.Max(
            bodyCollider.size.x * Mathf.Abs(lossyScale.x),
            bodyCollider.size.y * Mathf.Abs(lossyScale.y)) * 0.5f;
        int overlapCount = Physics2D.OverlapCircleNonAlloc(
            candidate,
            bodyRadius + searchWaypointObstacleClearance,
            gridOverlapHits,
            solidCollisionLayers);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = gridOverlapHits[i];
            gridOverlapHits[i] = null;
            if (overlap == null || overlap == bodyCollider || overlap.isTrigger)
            {
                continue;
            }

            ZeldaCharacterData overlapCharacter =
                overlap.GetComponentInParent<ZeldaCharacterData>();
            if (overlapCharacter != null &&
                (!characterData.ParticipatesInCharacterCollision ||
                 !overlapCharacter.ParticipatesInCharacterCollision))
            {
                continue;
            }

            if (IsUnlockedDoorCollider(overlap))
            {
                continue;
            }

            return false;
        }

        // A saturated NonAlloc buffer means the overlap query was truncated.
        // Conservatively reject the waypoint instead of overlooking a wall.
        return overlapCount < gridOverlapHits.Length;
    }

    private void BeginInvestigationPause()
    {
        moveDirection = Vector2.zero;
        if (isDoorIncidentInvestigation)
        {
            SetFacingDirection(
                investigationIncidentPosition - rb.position);
        }
        searchWaypointTimer = 0f;
        previousSearchWaypointDistance = float.PositiveInfinity;
        isPausingDuringInvestigation = true;
        investigationPauseTimer = 0f;
    }

    private PermissionArea FindHighestPermissionArea(Vector2 worldPosition)
    {
        PermissionArea highestPermissionArea = null;

        foreach (PermissionArea area in ZeldaRuntimeRegistry.PermissionAreas)
        {
            if (area == null || !area.isActiveAndEnabled || !area.Contains(worldPosition))
            {
                continue;
            }

            if (highestPermissionArea == null || area.PermissionLevel > highestPermissionArea.PermissionLevel)
            {
                highestPermissionArea = area;
            }
        }

        return highestPermissionArea;
    }

    private bool IsTargetAlive()
    {
        if (targetMover == null)
        {
            return false;
        }

        ZeldaCharacterData targetData = targetMover.GetComponent<ZeldaCharacterData>();
        return targetData != null && !targetData.IsDead;
    }

    private void ResetPermissionResponse()
    {
        targetMover = null;
        activePermissionArea = null;
        recognizedPermissionTarget = null;
        hostileUntilTargetDeath = false;
        retaliationHostile = false;
        regularHostile = false;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        hostileCardboardBox = null;
        regularHostileLostSightTimer = 0f;
        searchTimer = 0f;
        searchWaypointTimer = 0f;
        isTravelingToInvestigation = false;
        isDoorIncidentInvestigation = false;
        isPausingDuringInvestigation = false;
        investigationPauseTimer = 0f;
        warningTimer = 0f;
        suspicionValue = 0f;
        moveDirection = Vector2.zero;
        ChangeState(ZeldaAiState.Idle);
    }

    private ZeldaFourWayMover FindVisiblePlayer()
    {
        ZeldaFourWayMover closest = null;
        float closestDistance = float.MaxValue;

        foreach (ZeldaFourWayMover candidate in ZeldaRuntimeRegistry.Movers)
        {
            if (!CanSeePlayer(candidate))
            {
                continue;
            }

            float distance = ((Vector2)candidate.transform.position - rb.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool UpdateCardboardBoxSuspicion(float deltaTime)
    {
        if (currentState == ZeldaAiState.Hostile || regularHostile ||
            hostileUntilTargetDeath ||
            IsRecognizedPermissionTarget(targetMover))
        {
            return false;
        }

        if (TryHandleRemovedSuspiciousWornBox())
        {
            return true;
        }

        bool trackedGroundBoxUnavailable =
            suspiciousGroundCardboardBox != null &&
            (!suspiciousGroundCardboardBox.isActiveAndEnabled ||
             suspiciousGroundCardboardBox.IsDestroyed ||
             !suspiciousGroundCardboardBox.IsClockworkPuppetDriven);
        bool trackedBoxReferenceWasLost =
            suspiciousGroundCardboardBox == null &&
            suspiciousCardboardBox == null &&
            suspiciousCardboardBoxWearer == null;
        if (currentState == ZeldaAiState.Suspicious &&
            hasCardboardBoxAttractionTarget &&
            (trackedGroundBoxUnavailable || trackedBoxReferenceWasLost))
        {
            // Unity's destroyed-object null semantics previously bypassed the
            // normal suspicion decay branch. A vanished attraction source
            // must still send this AI home from wherever it followed the box.
            BeginRecovery();
            return true;
        }

        if (isInspectingCardboardBox)
        {
            if (suspiciousGroundCardboardBox != null &&
                !suspiciousGroundCardboardBox.IsDestroyed &&
                suspiciousGroundCardboardBox.RemainingGroundBlockedAttacks > 0)
            {
                targetMover = null;
                lastKnownTargetPosition =
                    suspiciousGroundCardboardBox.WorldCenter;
                ChangeState(ZeldaAiState.Suspicious);
                return true;
            }

            if (suspiciousCardboardBox != null &&
                suspiciousCardboardBox.RemainingBlockedAttacks > 0)
            {
                targetMover = suspiciousCardboardBox.WearerMover;
                lastKnownTargetPosition = suspiciousCardboardBox.WorldCenter;
                ChangeState(ZeldaAiState.Suspicious);
                return true;
            }

            CompleteCardboardBoxInspection();
            return true;
        }

        // Once movement has exposed a worn box, keep investigating that same
        // box while it remains visible. Stopping is not enough to convince
        // the observer that the suspicious movement never happened.
        // Do not replace an already identified box merely because another
        // one enters the view. A stable target lock prevents suspicion from
        // jumping or resetting while several boxes/characters cross paths.
        bool hasLockedBox = suspiciousCardboardBox != null ||
                            suspiciousGroundCardboardBox != null;
        CardboardBoxWearState observedBox = suspiciousCardboardBox != null
            ? (CanSeeWornCardboardBox(suspiciousCardboardBox, false)
                ? suspiciousCardboardBox
                : null)
            : (!hasLockedBox ? FindVisibleMovingWornBox() : null);
        if (observedBox != null)
        {
            lastSuspiciousCardboardBoxSeenTime = Time.time;
            suspiciousGroundCardboardBox = null;
            suspiciousCardboardBox = observedBox;
            suspiciousCardboardBoxWearer = observedBox.WearerMover;
            hasCardboardBoxAttractionTarget = true;
            targetMover = observedBox.WearerMover;
            activePermissionArea = null;
            recognizedPermissionTarget = null;
            lastKnownTargetPosition = observedBox.WorldCenter;
            suspicionValue = Mathf.Min(
                maximumSuspicion,
                suspicionValue + suspicionIncreasePerSecond * deltaTime);
            ChangeState(ZeldaAiState.Suspicious);
            if (suspicionValue >= maximumSuspicion)
            {
                isInspectingCardboardBox = true;
                inspectedCardboardBoxWearer = observedBox.WearerMover;
                suspicionValue = maximumSuspicion;
            }
            return true;
        }

        CardboardBoxPickupItem observedGroundBox =
            suspiciousGroundCardboardBox != null
                ? (CanSeeDrivenGroundCardboardBox(
                    suspiciousGroundCardboardBox,
                    false)
                    ? suspiciousGroundCardboardBox
                    : null)
                : (!hasLockedBox ? FindVisibleMovingDrivenGroundBox() : null);
        if (observedGroundBox != null)
        {
            lastSuspiciousCardboardBoxSeenTime = Time.time;
            suspiciousCardboardBox = null;
            suspiciousCardboardBoxWearer = null;
            suspiciousGroundCardboardBox = observedGroundBox;
            hasCardboardBoxAttractionTarget = true;
            targetMover = null;
            activePermissionArea = null;
            recognizedPermissionTarget = null;
            lastKnownTargetPosition = observedGroundBox.WorldCenter;
            suspicionValue = Mathf.Min(
                maximumSuspicion,
                suspicionValue + suspicionIncreasePerSecond * deltaTime);
            ChangeState(ZeldaAiState.Suspicious);
            if (suspicionValue >= maximumSuspicion)
            {
                isInspectingCardboardBox = true;
                inspectedCardboardBoxWearer = null;
                suspicionValue = maximumSuspicion;
            }
            return true;
        }

        if (suspiciousCardboardBox == null &&
            suspiciousGroundCardboardBox == null)
        {
            return false;
        }

        bool isRememberingBox = Time.time -
            lastSuspiciousCardboardBoxSeenTime <=
            cardboardBoxSightMemoryDuration;
        if (isRememberingBox)
        {
            // Keep the current suspicion and target during momentary raycast,
            // corner or vision-cone interruptions. This is particularly
            // important for a moving puppet box, which can pass behind
            // narrow level geometry for a few physics frames.
            targetMover = suspiciousCardboardBox != null
                ? suspiciousCardboardBox.WearerMover
                : null;
            ChangeState(ZeldaAiState.Suspicious);
            return true;
        }

        suspicionValue = Mathf.Max(
            0f,
            suspicionValue - suspicionDecreasePerSecond * deltaTime);
        if (suspicionValue <= 0f)
        {
            suspiciousCardboardBox = null;
            suspiciousCardboardBoxWearer = null;
            suspiciousGroundCardboardBox = null;
            hasCardboardBoxAttractionTarget = false;
            isInspectingCardboardBox = false;
            inspectedCardboardBoxWearer = null;
            if (currentState == ZeldaAiState.Suspicious)
            {
                // A moving box can pull this character well away from its
                // authored post. Leaving that box-specific suspicion must use
                // the normal recovery route instead of dropping directly to
                // Idle at the inspection position. Return handled so the
                // remaining perception checks cannot overwrite Recovery in
                // this same frame.
                BeginRecovery();
                return true;
            }
            return false;
        }

        targetMover = suspiciousCardboardBox != null
            ? suspiciousCardboardBox.WearerMover
            : null;
        ChangeState(ZeldaAiState.Suspicious);
        return true;
    }

    private bool TryHandleRemovedSuspiciousWornBox()
    {
        ZeldaFourWayMover revealedMover = suspiciousCardboardBoxWearer;
        if (revealedMover == null)
        {
            return false;
        }

        CardboardBoxWearState activeBox = revealedMover.ActiveCardboardBox;
        if (suspiciousCardboardBox != null &&
            activeBox == suspiciousCardboardBox)
        {
            return false;
        }

        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        suspicionValue = 0f;
        moveDirection = Vector2.zero;

        ZeldaCharacterData revealedData =
            revealedMover.GetComponent<ZeldaCharacterData>();
        if (!revealedMover.isActiveAndEnabled || revealedData == null ||
            revealedData.IsDead || !EnforcesPermissionAreas)
        {
            BeginRecovery();
            return true;
        }

        PermissionArea permissionArea =
            FindHighestPermissionArea(revealedMover.transform.position);
        bool isBelowDefaultPermission =
            revealedData.PermissionLevel <
            PermissionArea.DefaultPermissionLevel;
        if (permissionArea == null && !isBelowDefaultPermission)
        {
            BeginRecovery();
            return true;
        }

        int requiredPermissionLevel = permissionArea != null
            ? Mathf.Max(
                PermissionArea.DefaultPermissionLevel,
                permissionArea.PermissionLevel)
            : PermissionArea.DefaultPermissionLevel;
        int permissionDifference =
            requiredPermissionLevel - revealedData.PermissionLevel;
        if (permissionDifference <
            Mathf.Max(1, MinimumPermissionViolationDifference))
        {
            BeginRecovery();
            return true;
        }

        // Removing the disguise in an illegal/hostile area should reveal the
        // same permission violation the AI would normally perceive instead
        // of making it abandon the incident and walk home.
        targetMover = revealedMover;
        activePermissionArea = permissionArea;
        lastKnownTargetPosition = revealedMover.transform.position;
        warningTimer = 0f;
        BeginPermissionResponse(permissionDifference);
        return true;
    }

    private void CompleteCardboardBoxInspection()
    {
        ZeldaFourWayMover revealedWearer = inspectedCardboardBoxWearer;
        isInspectingCardboardBox = false;
        inspectedCardboardBoxWearer = null;
        suspiciousCardboardBox = null;
        suspiciousCardboardBoxWearer = null;
        suspiciousGroundCardboardBox = null;
        hasCardboardBoxAttractionTarget = false;
        suspicionValue = 0f;
        moveDirection = Vector2.zero;

        ZeldaFourWayMover controlledMover =
            ZeldaRuntimeRegistry.GetControlledMover();
        if (revealedWearer == null || revealedWearer != controlledMover ||
            !revealedWearer.isActiveAndEnabled)
        {
            BeginRecovery();
            return;
        }

        ZeldaCharacterData playerData =
            revealedWearer.GetComponent<ZeldaCharacterData>();
        if (playerData == null || playerData.IsDead ||
            !EnforcesPermissionAreas)
        {
            BeginRecovery();
            return;
        }

        PermissionArea permissionArea =
            FindHighestPermissionArea(revealedWearer.transform.position);
        bool isBelowDefaultPermission =
            playerData.PermissionLevel < PermissionArea.DefaultPermissionLevel;
        if (permissionArea == null && !isBelowDefaultPermission)
        {
            BeginRecovery();
            return;
        }

        int requiredPermissionLevel = permissionArea != null
            ? Mathf.Max(
                PermissionArea.DefaultPermissionLevel,
                permissionArea.PermissionLevel)
            : PermissionArea.DefaultPermissionLevel;
        int permissionDifference =
            requiredPermissionLevel - playerData.PermissionLevel;
        if (permissionDifference <
            Mathf.Max(1, MinimumPermissionViolationDifference))
        {
            BeginRecovery();
            return;
        }

        targetMover = revealedWearer;
        activePermissionArea = permissionArea;
        lastKnownTargetPosition = revealedWearer.transform.position;
        warningTimer = 0f;
        BeginPermissionResponse(permissionDifference);
    }

    private CardboardBoxWearState FindVisibleMovingWornBox()
    {
        CardboardBoxWearState closest = null;
        float closestSquaredDistance = float.MaxValue;
        foreach (ZeldaFourWayMover candidate in ZeldaRuntimeRegistry.Movers)
        {
            CardboardBoxWearState box = candidate != null
                ? candidate.ActiveCardboardBox
                : null;
            if (!CanSeeWornCardboardBox(box, true))
            {
                continue;
            }

            float squaredDistance =
                (box.WorldCenter - rb.position).sqrMagnitude;
            if (squaredDistance < closestSquaredDistance)
            {
                closestSquaredDistance = squaredDistance;
                closest = box;
            }
        }

        return closest;
    }

    private CardboardBoxPickupItem FindVisibleMovingDrivenGroundBox()
    {
        CardboardBoxPickupItem closest = null;
        float closestSquaredDistance = float.MaxValue;
        foreach (CardboardBoxPickupItem box in
                 CardboardBoxPickupItem.WorldBoxes)
        {
            if (!CanSeeDrivenGroundCardboardBox(box, true))
            {
                continue;
            }

            float squaredDistance =
                (box.WorldCenter - rb.position).sqrMagnitude;
            if (squaredDistance < closestSquaredDistance)
            {
                closestSquaredDistance = squaredDistance;
                closest = box;
            }
        }

        return closest;
    }

    private bool CanSeeDrivenGroundCardboardBox(
        CardboardBoxPickupItem box,
        bool requireMovement)
    {
        if (box == null || box.IsDestroyed || !box.isActiveAndEnabled ||
            !box.IsClockworkPuppetDriven ||
            (requireMovement && !box.IsClockworkPuppetMoving))
        {
            return false;
        }

        return CanSeeWorldPoint(box.WorldCenter, box.transform);
    }

    private bool CanSeeWornCardboardBox(
        CardboardBoxWearState box,
        bool requireMovement)
    {
        if (box == null || box.WearerMover == null ||
            !box.WearerMover.isActiveAndEnabled ||
            (requireMovement && box.IsStationary))
        {
            return false;
        }

        return CanSeeWorldPoint(box.WorldCenter, box.transform);
    }

    private CardboardBoxPickupItem FindNearestVisibleGroundBox()
    {
        CardboardBoxPickupItem closest = null;
        float closestSquaredDistance = float.MaxValue;
        foreach (CardboardBoxPickupItem box in
                 CardboardBoxPickupItem.WorldBoxes)
        {
            if (box == null || box.IsDestroyed || !box.isActiveAndEnabled ||
                !CanSeeWorldPoint(box.WorldCenter, box.transform))
            {
                continue;
            }

            float squaredDistance =
                (box.WorldCenter - rb.position).sqrMagnitude;
            if (squaredDistance < closestSquaredDistance)
            {
                closestSquaredDistance = squaredDistance;
                closest = box;
            }
        }

        return closest;
    }

    private bool CanSeeWorldPoint(Vector2 worldPoint, Transform target)
    {
        Vector2 origin = rb.position;
        Vector2 toTarget = worldPoint - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float targetAngle = Vector2.Angle(facingDirection, toTarget);
        bool isInMainVision = targetAngle <= visionAngle * 0.5f &&
            toTarget.sqrMagnitude <= visionRadius * visionRadius;
        bool isInPeripheralVision =
            targetAngle <= peripheralVisionAngle * 0.5f &&
            toTarget.sqrMagnitude <=
                peripheralVisionRadius * peripheralVisionRadius;
        return (isInMainVision || isInPeripheralVision) &&
            !IsVisionBlocked(
                origin,
                toTarget.normalized,
                toTarget.magnitude,
                target);
    }

    private void TickCurrentState(float deltaTime)
    {
        if (mover.IsAttacking)
        {
            moveDirection = Vector2.zero;
            return;
        }

        switch (currentState)
        {
            case ZeldaAiState.Suspicious: TickSuspicious(deltaTime); break;
            case ZeldaAiState.Alert: TickAlert(deltaTime); break;
            case ZeldaAiState.Search: TickSearch(deltaTime); break;
            case ZeldaAiState.Recovery: TickRecovery(deltaTime); break;
            case ZeldaAiState.Hostile: TickHostile(deltaTime); break;
            default: TickIdle(deltaTime); break;
        }
    }

    private Vector2 GetBlockedDisplacement(Vector2 displacement)
    {
        Vector2 directDisplacement =
            GetLinearBlockedDisplacement(displacement, out Vector2 blockingNormal);
        if ((directDisplacement - displacement).sqrMagnitude <= 0.0000001f)
        {
            return directDisplacement;
        }

        Vector2 desiredDirection = displacement.normalized;
        Vector2 bestDisplacement = directDisplacement;
        float bestProgress = Vector2.Dot(bestDisplacement, desiredDirection);

        if (blockingNormal.sqrMagnitude > 0.0001f)
        {
            Vector2 remaining = displacement - directDisplacement;
            Vector2 tangent =
                remaining - blockingNormal * Vector2.Dot(remaining, blockingNormal);
            Vector2 tangentDisplacement =
                GetLinearBlockedDisplacement(tangent, out _);
            float tangentProgress =
                Vector2.Dot(tangentDisplacement, desiredDirection);
            if (tangentProgress > bestProgress)
            {
                bestDisplacement = tangentDisplacement;
                bestProgress = tangentProgress;
            }
        }

        // When a diagonal route brushes a collider corner, retain the best
        // unblocked axis component. This also prevents an AI grid waypoint
        // from repeatedly steering into the same convex corner.
        Vector2 horizontal =
            GetLinearBlockedDisplacement(new Vector2(displacement.x, 0f), out _);
        Vector2 vertical =
            GetLinearBlockedDisplacement(new Vector2(0f, displacement.y), out _);
        float horizontalProgress = Vector2.Dot(horizontal, desiredDirection);
        float verticalProgress = Vector2.Dot(vertical, desiredDirection);

        if (horizontalProgress > bestProgress &&
            horizontalProgress >= verticalProgress)
        {
            return horizontal;
        }

        return verticalProgress > bestProgress ? vertical : bestDisplacement;
    }

    private Vector2 GetLinearBlockedDisplacement(
        Vector2 displacement,
        out Vector2 blockingNormal)
    {
        blockingNormal = Vector2.zero;
        float distance = displacement.magnitude;
        if (distance <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 direction = displacement / distance;
        Vector2 castCenter = bodyCollider.transform.TransformPoint(bodyCollider.offset);
        Vector3 lossyScale = bodyCollider.transform.lossyScale;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.02f,
                bodyCollider.size.x * Mathf.Abs(lossyScale.x) -
                collisionSkinWidth * 2f),
            Mathf.Max(0.02f,
                bodyCollider.size.y * Mathf.Abs(lossyScale.y) -
                collisionSkinWidth * 2f));
        float castAngle = bodyCollider.transform.eulerAngles.z;
        int hitCount = Physics2D.BoxCastNonAlloc(
            castCenter,
            castSize,
            castAngle,
            direction,
            movementHits,
            distance + collisionSkinWidth,
            solidCollisionLayers);
        float allowedDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = movementHits[i].collider;
            if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger)
            {
                continue;
            }

            if (IsNonParticipatingCharacterCollider(hitCollider))
            {
                continue;
            }

            if (IsUnlockedDoorCollider(hitCollider))
            {
                continue;
            }

            if (IsMovingAwayFromCharacter(hitCollider, direction))
            {
                continue;
            }

            if (Vector2.Dot(direction, movementHits[i].normal) >= -0.001f)
            {
                continue;
            }

            float hitAllowedDistance =
                Mathf.Max(0f, movementHits[i].distance - collisionSkinWidth);
            if (hitAllowedDistance < allowedDistance)
            {
                allowedDistance = hitAllowedDistance;
                blockingNormal = movementHits[i].normal;
            }
        }

        return direction * allowedDistance;
    }

    private Vector2 PlanDirectionTo(
        Vector2 worldTarget,
        float stoppingDistance = 0f)
    {
        gridPathTarget = worldTarget;
        gridPathStoppingDistance = Mathf.Max(0f, stoppingDistance);
        hasNavigationDebugTarget = true;
        Vector2 directOffset = worldTarget - rb.position;
        float stoppingDistanceSquared =
            gridPathStoppingDistance * gridPathStoppingDistance;
        if (directOffset.sqrMagnitude <=
            Mathf.Max(0.0001f, stoppingDistanceSquared))
        {
            ClearGridPath();
            return Vector2.zero;
        }

        if (!useGridPathfinding)
        {
            return PlanDirectionAroundObstacles(directOffset.normalized);
        }

        float targetChangeThreshold = gridCellSize *
            (currentState == ZeldaAiState.Hostile ? 1.35f : 0.65f);
        // Compare against the target used by the last real A* build rather
        // than the previous frame. This both ignores harmless jitter and
        // still detects small changes once they accumulate far enough.
        bool targetMovedSincePlan = !hasGridPathPlan ||
            (worldTarget - gridPathPlannedTarget).sqrMagnitude >
            targetChangeThreshold * targetChangeThreshold;
        bool stoppingDistanceChanged = !hasGridPathPlan || Mathf.Abs(
            gridPathPlannedStoppingDistance - gridPathStoppingDistance) >
            gridCellSize * 0.25f;
        bool hasUsablePath = gridPath.Count > 0 && gridPathIndex < gridPath.Count;
        bool repathDelayElapsed = Time.time >= nextGridPathRefreshTime;
        bool maintenanceDue = hasGridPathPlan &&
                              Time.time >= nextGridPathMaintenanceTime;
        bool needsGridPath = !hasUsablePath ||
            (repathDelayElapsed &&
             (targetMovedSincePlan || stoppingDistanceChanged || maintenanceDue));
        if (!hasUsablePath &&
            Time.time < nextGridPathRefreshTime)
        {
            return GetSafeGridFallbackDirection(worldTarget, directOffset);
        }

        if (needsGridPath)
        {
            if (TryAcquireAStarBuildBudget())
            {
                bool pathBuilt = BuildGridPath(
                    rb.position,
                    worldTarget,
                    gridPathStoppingDistance);
                gridPathPlannedTarget = worldTarget;
                gridPathPlannedStoppingDistance = gridPathStoppingDistance;
                hasGridPathPlan = true;

                float refreshInterval = currentState == ZeldaAiState.Hostile
                    ? hostileGridPathRefreshInterval
                    : gridPathRefreshInterval;
                if (targetMovedSincePlan)
                {
                    refreshInterval = Mathf.Max(
                        refreshInterval,
                        movingTargetGridRepathInterval);
                }

                refreshInterval *= GetHostileCrowdIntervalMultiplier();
                if (!pathBuilt)
                {
                    // Failed graphs otherwise make every waiting NPC request
                    // another complete physics grid as soon as possible.
                    refreshInterval = Mathf.Max(
                        refreshInterval,
                        globalAStarBuildInterval * 2f);
                }

                float stagger = Mathf.Abs(GetInstanceID() % 13) / 13f;
                float staggerMultiplier = Mathf.Lerp(0.85f, 1.15f, stagger);
                nextGridPathRefreshTime = Time.time +
                    refreshInterval * staggerMultiplier;
                nextGridPathMaintenanceTime = Time.time +
                    gridPathMaintenanceInterval * staggerMultiplier;
            }
            else if (!hasUsablePath)
            {
                // Another actor has already performed the expensive grid scan
                // this frame. Use the cheap collision-safe fallback and retry
                // later instead of creating a synchronized frame spike.
                return GetSafeGridFallbackDirection(worldTarget, directOffset);
            }
        }

        if (gridPath.Count == 0)
        {
            return GetSafeGridFallbackDirection(worldTarget, directOffset);
        }

        float tolerance = Mathf.Max(gridWaypointTolerance, gridCellSize * 0.25f);
        while (gridPathIndex < gridPath.Count &&
               (gridPath[gridPathIndex] - rb.position).sqrMagnitude <=
               tolerance * tolerance)
        {
            gridPathIndex++;
        }

        if (gridPathIndex >= gridPath.Count)
        {
            // The discrete endpoint has been reached. Only switch to the exact
            // target when that final segment is physically clear.
            if (IsBodyPathSegmentClear(rb.position, worldTarget, 0f))
            {
                ResetObstacleFollowing();
                return directOffset.normalized;
            }

            ClearGridPath();
            nextGridPathRefreshTime = 0f;
            return GetSafeGridFallbackDirection(worldTarget, directOffset);
        }

        if (Time.time >= nextGridPathValidationTime)
        {
            float validationStagger = Mathf.Lerp(
                0.9f,
                1.1f,
                Mathf.Abs(GetInstanceID() % 17) / 17f);
            nextGridPathValidationTime = Time.time +
                gridPathValidationInterval * validationStagger;

            // Static walls do not need four BoxCasts per NPC every frame.
            // Validate and simplify at a staggered interval; FixedUpdate's
            // displacement cast remains the final per-step collision guard.
            int furthestVisibleIndex = gridPathIndex;
            int lookAheadEnd = Mathf.Min(gridPath.Count - 1, gridPathIndex + 4);
            for (int i = lookAheadEnd; i > gridPathIndex; i--)
            {
                if (IsBodyPathSegmentClear(
                    rb.position,
                    gridPath[i],
                    activeGridClearance))
                {
                    furthestVisibleIndex = i;
                    break;
                }
            }
            gridPathIndex = furthestVisibleIndex;

            if (!IsBodyPathSegmentClear(
                    rb.position,
                    gridPath[gridPathIndex],
                    0f))
            {
                ClearGridPath();
                nextGridPathRefreshTime = Time.time +
                    Mathf.Max(0.02f, globalAStarBuildInterval);
                return Vector2.zero;
            }
        }

        Vector2 waypointOffset = gridPath[gridPathIndex] - rb.position;

        if (waypointOffset.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        // A* has already validated the entire body-sized segment. A fixed
        // forward avoidance probe can see a wall beyond a nearby turn node
        // and steer away before reaching that node, which is especially
        // harmful in narrow corridors. Make the A* segment authoritative;
        // the physics displacement check remains the final safety gate.
        ResetObstacleFollowing();
        return waypointOffset.normalized;
    }

    private bool TryAcquireAStarBuildBudget()
    {
        int frame = Time.frameCount;
        if (aStarBudgetFrame != frame)
        {
            aStarBudgetFrame = frame;
            aStarBuildsThisFrame = 0;
        }

        int frameLimit = Mathf.Clamp(maximumAStarBuildsPerFrame, 1, 8);
        if (aStarBuildsThisFrame >= frameLimit)
        {
            return false;
        }

        if (Time.time < nextGlobalAStarBuildTime)
        {
            return false;
        }

        aStarBuildsThisFrame++;
        nextGlobalAStarBuildTime = Time.time +
            Mathf.Max(0.01f, globalAStarBuildInterval) *
            GetHostileCrowdIntervalMultiplier();
        return true;
    }

    private bool TryAcquireExpensiveGridFallbackBudget()
    {
        if (Time.time < nextExpensiveGridFallbackTime)
        {
            return false;
        }

        nextExpensiveGridFallbackTime = Time.time +
            Mathf.Max(0.05f, expensiveGridFallbackInterval) *
            GetHostileCrowdIntervalMultiplier();
        return true;
    }

    private static float GetHostileCrowdIntervalMultiplier()
    {
        int frame = Time.frameCount;
        if (hostileCountCacheFrame != frame)
        {
            hostileCountCacheFrame = frame;
            hostileCountCache = 0;
            foreach (ZeldaCharacterAiBase ai in ZeldaRuntimeRegistry.AiCharacters)
            {
                if (ai != null && ai.isActiveAndEnabled &&
                    ai.currentState == ZeldaAiState.Hostile)
                {
                    hostileCountCache++;
                }
            }
        }

        return 1f + Mathf.Clamp(
            Mathf.Max(0, hostileCountCache - 4) * 0.08f,
            0f,
            1.5f);
    }

    private Vector2 GetSafeGridFallbackDirection(
        Vector2 worldTarget,
        Vector2 directOffset)
    {
        if (directOffset.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        Vector2 desiredDirection = directOffset.normalized;
        if (IsBodyPathSegmentClear(rb.position, worldTarget, 0f))
        {
            return PlanDirectionAroundObstacles(desiredDirection);
        }

        // A missing or temporarily invalid grid path must never degrade into
        // an unchecked direct movement. Retain the obstacle-following side so
        // the AI moves around the wall while the grid is rebuilt.
        cachedPlannedDirection =
            CalculateDirectionAroundObstacles(desiredDirection);
        return cachedPlannedDirection;
    }

    private bool IsBodyPathSegmentClear(
        Vector2 fromPosition,
        Vector2 toPosition,
        float extraClearance)
    {
        Vector2 offset = toPosition - fromPosition;
        float distance = offset.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        Vector2 direction = offset / distance;
        Vector3 lossyScale = bodyCollider.transform.lossyScale;
        Vector2 castSize = new Vector2(
            bodyCollider.size.x * Mathf.Abs(lossyScale.x),
            bodyCollider.size.y * Mathf.Abs(lossyScale.y));
        castSize += Vector2.one * (Mathf.Max(0f, extraClearance) * 2f);
        Vector2 colliderOffset = transform.TransformVector(bodyCollider.offset);
        Vector2 castOrigin = fromPosition + colliderOffset;
        int hitCount = Physics2D.BoxCastNonAlloc(
            castOrigin,
            castSize,
            bodyCollider.transform.eulerAngles.z,
            direction,
            movementHits,
            distance,
            solidCollisionLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = movementHits[i].collider;
            if (hit == null || hit == bodyCollider || hit.isTrigger ||
                hit.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(hit) ||
                hit.GetComponentInParent<ZeldaCharacterData>() != null)
            {
                continue;
            }

            if (movementHits[i].distance <=
                    extraClearance + collisionSkinWidth &&
                Vector2.Dot(direction, movementHits[i].normal) >= -0.001f)
            {
                continue;
            }

            return false;
        }

        // A saturated NonAlloc buffer means the cast was truncated. Treat it
        // as blocked so path simplification cannot skip an unreported wall.
        return hitCount < movementHits.Length;
    }

    private bool BuildGridPath(
        Vector2 startWorld,
        Vector2 requestedGoalWorld,
        float stoppingDistance)
    {
        float coarseCellSize = Mathf.Max(0.2f, gridCellSize);
        float fineCellSize = Mathf.Clamp(
            minimumGridCellSize,
            0.1f,
            coarseCellSize);
        int normalDimension = Mathf.Max(15, maximumGridDimension);
        int fallbackDimension = Mathf.Max(
            normalDimension,
            expandedGridDimension);
        int normalPadding = Mathf.Max(2, gridSearchPadding);
        int expandedPadding = Mathf.Max(normalPadding + 4, normalPadding * 4);

        // Most routes are solved by this inexpensive pass. More expensive
        // passes are only used after A* proves that the local graph has no
        // route, keeping the common case fast when many AI actors are active.
        if (TryBuildGridPath(
                startWorld,
                requestedGoalWorld,
                coarseCellSize,
                gridWallClearance,
                normalDimension,
                normalPadding,
                stoppingDistance,
                false,
                false))
        {
            return true;
        }

        // A route may need to initially move away from the target to leave a
        // room or pass the end of a long wall. Retry using the unused cells
        // on both axes as a full square instead of another narrow corridor.
        if (TryBuildGridPath(
                startWorld,
                requestedGoalWorld,
                coarseCellSize,
                gridWallClearance,
                normalDimension,
                normalPadding,
                stoppingDistance,
                true,
                false))
        {
            return true;
        }

        if (!TryAcquireExpensiveGridFallbackBudget())
        {
            return false;
        }

        if (fallbackDimension > normalDimension &&
            TryBuildGridPath(
                startWorld,
                requestedGoalWorld,
                coarseCellSize,
                gridWallClearance,
                fallbackDimension,
                expandedPadding,
                stoppingDistance,
                false,
                false))
        {
            return true;
        }

        // A finer lattice prevents a valid narrow passage from disappearing
        // merely because no coarse cell center happens to align with it. The
        // reduced optional clearance still retains the collider's full size.
        float narrowClearance = Mathf.Min(
            gridWallClearance,
            coarseCellSize * 0.1f);
        int finePadding = Mathf.Max(
            expandedPadding,
            Mathf.CeilToInt(
                expandedPadding * coarseCellSize / fineCellSize));
        if (TryBuildGridPath(
                startWorld,
                requestedGoalWorld,
                fineCellSize,
                narrowClearance,
                fallbackDimension,
                finePadding,
                stoppingDistance,
                false,
                false))
        {
            return true;
        }

        // The final pass may return the best reachable frontier node. This
        // lets long-distance navigation progress around a large obstacle and
        // replan from there instead of discarding A* and walking into a wall.
        return TryBuildGridPath(
            startWorld,
            requestedGoalWorld,
            fineCellSize,
            0f,
            fallbackDimension,
            finePadding,
            stoppingDistance,
            true,
            true);
    }

    private bool TryBuildGridPath(
        Vector2 startWorld,
        Vector2 requestedGoalWorld,
        float cellSize,
        float clearance,
        int maxDimension,
        int padding,
        float stoppingDistance,
        bool fullSquareSearch,
        bool allowPartialPath)
    {
        ClearGridPath();
        activeGridCellSize = Mathf.Max(0.1f, cellSize);
        activeGridClearance = Mathf.Max(0f, clearance);
        padding = Mathf.Clamp(padding, 2, Mathf.Max(2, maxDimension / 3));
        maxDimension = Mathf.Max(15, maxDimension);
        Vector2 maximumOffset = requestedGoalWorld - startWorld;
        float maximumTravelDistance =
            Mathf.Max(
                activeGridCellSize,
                (maxDimension - padding * 2 - 2) * activeGridCellSize);
        bool planningToFinalGoal =
            maximumOffset.magnitude <= maximumTravelDistance;
        Vector2 goalWorld = !planningToFinalGoal
            ? startWorld + maximumOffset.normalized * maximumTravelDistance
            : requestedGoalWorld;

        Vector2Int startCell = WorldToGridCell(
            startWorld,
            activeGridCellSize);
        Vector2Int goalCell = WorldToGridCell(
            goalWorld,
            activeGridCellSize);
        int deltaX = Mathf.Abs(goalCell.x - startCell.x);
        int deltaY = Mathf.Abs(goalCell.y - startCell.y);
        if (fullSquareSearch)
        {
            gridWidth = maxDimension;
            gridHeight = maxDimension;
            int spareX = Mathf.Max(0, gridWidth - deltaX - 1);
            int spareY = Mathf.Max(0, gridHeight - deltaY - 1);
            gridOriginCell = new Vector2Int(
                Mathf.Min(startCell.x, goalCell.x) - spareX / 2,
                Mathf.Min(startCell.y, goalCell.y) - spareY / 2);
        }
        else
        {
            int minX = Mathf.Min(startCell.x, goalCell.x) - padding;
            int maxX = Mathf.Max(startCell.x, goalCell.x) + padding;
            int minY = Mathf.Min(startCell.y, goalCell.y) - padding;
            int maxY = Mathf.Max(startCell.y, goalCell.y) + padding;
            gridWidth = Mathf.Min(maxDimension, maxX - minX + 1);
            gridHeight = Mathf.Min(maxDimension, maxY - minY + 1);
            gridOriginCell = new Vector2Int(minX, minY);

            // When one axis was clamped, keep the start inside the grid and
            // let repeated local paths progressively cover long journeys.
            gridOriginCell.x = Mathf.Clamp(
                gridOriginCell.x,
                startCell.x - gridWidth + 1,
                startCell.x);
            gridOriginCell.y = Mathf.Clamp(
                gridOriginCell.y,
                startCell.y - gridHeight + 1,
                startCell.y);
        }
        goalCell.x = Mathf.Clamp(
            goalCell.x,
            gridOriginCell.x,
            gridOriginCell.x + gridWidth - 1);
        goalCell.y = Mathf.Clamp(
            goalCell.y,
            gridOriginCell.y,
            gridOriginCell.y + gridHeight - 1);

        int nodeCount = gridWidth * gridHeight;
        EnsureGridCapacity(nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            gridCosts[i] = float.PositiveInfinity;
            gridParents[i] = -1;
            gridStates[i] = 0;
            gridWalkability[i] = 0;
            gridObstaclePenalties[i] = -1f;
            gridHeapPositions[i] = -1;
        }
        gridHeapCount = 0;

        int startIndex = CellToIndex(startCell);
        int requestedGoalIndex = CellToIndex(goalCell);
        if (startIndex < 0 || requestedGoalIndex < 0)
        {
            return false;
        }

        bool exactGoalIsWalkable = IsGridCellWalkable(goalCell);
        int stoppingRadius = Mathf.Max(
            0,
            Mathf.FloorToInt(
                (planningToFinalGoal
                    ? Mathf.Max(0f, stoppingDistance)
                    : 0f) /
                activeGridCellSize));
        int blockedGoalRadius = exactGoalIsWalkable
            ? 0
            : CalculateGridGoalSnapRadius();
        activeGridGoalRadius = Mathf.Max(
            stoppingRadius,
            blockedGoalRadius);
        bool requiresExactGoal = exactGoalIsWalkable &&
                                 activeGridGoalRadius == 0;
        int goalIndex = requiresExactGoal ? requestedGoalIndex : -1;

        gridWalkability[startIndex] = 1;
        gridCosts[startIndex] = 0f;
        gridStates[startIndex] = 1;
        HeapPush(startIndex, goalCell);

        bool found = false;
        int bestReachableIndex = startIndex;
        float bestReachableHeuristic =
            GridHeuristic(startCell, goalCell, activeGridGoalRadius);
        float bestReachableCost = 0f;
        while (gridHeapCount > 0)
        {
            int currentIndex = HeapPop(goalCell);
            if (gridStates[currentIndex] == 2)
            {
                continue;
            }
            gridStates[currentIndex] = 2;
            Vector2Int currentCell = IndexToCell(currentIndex);
            float currentHeuristic =
                GridHeuristic(
                    currentCell,
                    goalCell,
                    activeGridGoalRadius);
            if (currentHeuristic < bestReachableHeuristic - 0.0001f ||
                (Mathf.Abs(
                     currentHeuristic - bestReachableHeuristic) <= 0.0001f &&
                 gridCosts[currentIndex] < bestReachableCost))
            {
                bestReachableHeuristic = currentHeuristic;
                bestReachableCost = gridCosts[currentIndex];
                bestReachableIndex = currentIndex;
            }

            bool reachedGoal = requiresExactGoal
                ? currentIndex == goalIndex
                : IsGridGoalCandidate(currentCell, goalCell);
            if (reachedGoal)
            {
                goalIndex = currentIndex;
                found = true;
                break;
            }

            for (int directionIndex = 0;
                 directionIndex < GridDirections.Length;
                 directionIndex++)
            {
                Vector2Int direction = GridDirections[directionIndex];
                Vector2Int neighborCell = currentCell + direction;
                int neighborIndex = CellToIndex(neighborCell);
                if (neighborIndex < 0 || gridStates[neighborIndex] == 2 ||
                    !IsGridCellWalkable(neighborCell) ||
                    !IsGridTransitionWalkable(currentCell, neighborCell))
                {
                    continue;
                }

                bool diagonal = direction.x != 0 && direction.y != 0;
                if (diagonal &&
                    (!IsGridCellWalkable(new Vector2Int(
                         currentCell.x + direction.x,
                         currentCell.y)) ||
                     !IsGridCellWalkable(new Vector2Int(
                         currentCell.x,
                         currentCell.y + direction.y))))
                {
                    continue;
                }

                float movementCost = diagonal ? 1.4142135f : 1f;
                int parentIndex = gridParents[currentIndex];
                if (parentIndex >= 0)
                {
                    Vector2Int previousDirection =
                        currentCell - IndexToCell(parentIndex);
                    if (previousDirection != direction)
                    {
                        movementCost += gridTurnCost;
                    }
                }
                float nextCost = gridCosts[currentIndex] + movementCost +
                                 GetGridObstaclePenalty(neighborCell);
                if (nextCost >= gridCosts[neighborIndex])
                {
                    continue;
                }

                gridCosts[neighborIndex] = nextCost;
                gridParents[neighborIndex] = currentIndex;
                if (gridStates[neighborIndex] == 0)
                {
                    gridStates[neighborIndex] = 1;
                    HeapPush(neighborIndex, goalCell);
                }
                else
                {
                    HeapMoveUp(gridHeapPositions[neighborIndex], goalCell);
                }
            }
        }

        if (!found && allowPartialPath && bestReachableIndex != startIndex)
        {
            goalIndex = bestReachableIndex;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        int pathNode = goalIndex;
        int pathSafety = nodeCount;
        while (pathNode >= 0 && pathNode != startIndex)
        {
            gridPath.Add(GridCellToWorld(
                IndexToCell(pathNode),
                activeGridCellSize));
            pathNode = gridParents[pathNode];
            pathSafety--;
            if (pathSafety <= 0)
            {
                ClearGridPath();
                return false;
            }
        }
        if (pathNode != startIndex)
        {
            ClearGridPath();
            return false;
        }
        gridPath.Reverse();
        SimplifyGridPath(startWorld);
        gridPathIndex = 0;
        return gridPath.Count > 0;
    }

    private int CalculateGridGoalSnapRadius()
    {
        Vector2 lossyScale = transform.lossyScale;
        float bodyDiameter = Mathf.Max(
            bodyCollider.size.x * Mathf.Abs(lossyScale.x),
            bodyCollider.size.y * Mathf.Abs(lossyScale.y));
        return Mathf.Clamp(
            Mathf.CeilToInt(
                (bodyDiameter + activeGridClearance) /
                activeGridCellSize) + 2,
            3,
            12);
    }

    private bool IsGridGoalCandidate(
        Vector2Int candidate,
        Vector2Int requestedGoal)
    {
        int dx = Mathf.Abs(candidate.x - requestedGoal.x);
        int dy = Mathf.Abs(candidate.y - requestedGoal.y);
        return dx * dx + dy * dy <=
               activeGridGoalRadius * activeGridGoalRadius;
    }

    private void SimplifyGridPath(Vector2 startWorld)
    {
        if (gridPath.Count <= 1)
        {
            return;
        }

        simplifiedGridPath.Clear();
        Vector2 anchor = startWorld;
        int nextIndex = 0;
        while (nextIndex < gridPath.Count)
        {
            int furthestClearIndex = nextIndex;
            for (int candidate = gridPath.Count - 1;
                 candidate > nextIndex;
                 candidate--)
            {
                if (!IsBodyPathSegmentClear(
                        anchor,
                        gridPath[candidate],
                        activeGridClearance))
                {
                    continue;
                }

                furthestClearIndex = candidate;
                break;
            }

            Vector2 waypoint = gridPath[furthestClearIndex];
            simplifiedGridPath.Add(waypoint);
            anchor = waypoint;
            nextIndex = furthestClearIndex + 1;
        }

        gridPath.Clear();
        gridPath.AddRange(simplifiedGridPath);
        simplifiedGridPath.Clear();
    }

    private bool IsGridCellWalkable(Vector2Int cell)
    {
        int index = CellToIndex(cell);
        if (index < 0)
        {
            return false;
        }
        if (gridWalkability[index] != 0)
        {
            return gridWalkability[index] == 1;
        }

        Vector2 colliderOffset = transform.TransformVector(bodyCollider.offset);
        Vector2 center =
            GridCellToWorld(cell, activeGridCellSize) + colliderOffset;
        Vector2 lossyScale = transform.lossyScale;
        Vector2 probeSize = Vector2.Scale(
            bodyCollider.size,
            new Vector2(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)));
        probeSize.x = Mathf.Max(
            0.05f,
            probeSize.x + activeGridClearance * 2f);
        probeSize.y = Mathf.Max(
            0.05f,
            probeSize.y + activeGridClearance * 2f);
        int hitCount = Physics2D.OverlapBoxNonAlloc(
            center,
            probeSize,
            0f,
            gridOverlapHits,
            solidCollisionLayers);
        bool walkable = true;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = gridOverlapHits[i];
            if (hit == null || hit.isTrigger || hit == bodyCollider ||
                hit.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(hit))
            {
                continue;
            }

            ZeldaCharacterData hitCharacter =
                hit.GetComponentInParent<ZeldaCharacterData>();
            if (hitCharacter != null)
            {
                // Characters are dynamic and are handled by separation rather
                // than being baked into a rapidly stale grid.
                continue;
            }
            walkable = false;
            break;
        }
        if (hitCount >= gridOverlapHits.Length)
        {
            walkable = false;
        }
        gridWalkability[index] = walkable ? (byte)1 : (byte)2;
        return walkable;
    }

    private bool IsGridTransitionWalkable(
        Vector2Int fromCell,
        Vector2Int toCell)
    {
        Vector2 colliderOffset = transform.TransformVector(bodyCollider.offset);
        Vector2 from =
            GridCellToWorld(fromCell, activeGridCellSize) + colliderOffset;
        Vector2 to =
            GridCellToWorld(toCell, activeGridCellSize) + colliderOffset;
        Vector2Int actualStartCell =
            WorldToGridCell(rb.position, activeGridCellSize);
        bool transitionStartsAtAi = fromCell == actualStartCell;
        if (transitionStartsAtAi)
        {
            // The start cell is force-enabled because the AI is already
            // standing there. Cast from its real position rather than a cell
            // center that may happen to lie inside nearby wall geometry.
            from = rb.position + colliderOffset;
        }
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        Vector3 lossyScale = transform.lossyScale;
        Vector2 castSize = Vector2.Scale(
            bodyCollider.size,
            new Vector2(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)));
        float transitionClearance =
            transitionStartsAtAi ? 0f : activeGridClearance;
        castSize += Vector2.one * (transitionClearance * 2f);
        castSize.x = Mathf.Max(0.05f, castSize.x);
        castSize.y = Mathf.Max(0.05f, castSize.y);

        Vector2 transitionDirection = offset / distance;
        int hitCount = Physics2D.BoxCastNonAlloc(
            from,
            castSize,
            bodyCollider.transform.eulerAngles.z,
            transitionDirection,
            movementHits,
            distance,
            solidCollisionLayers);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = movementHits[i].collider;
            if (hit == null || hit.isTrigger || hit == bodyCollider ||
                hit.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(hit))
            {
                continue;
            }

            // Moving characters are handled by local separation and should
            // not permanently remove otherwise valid grid connections.
            if (hit.GetComponentInParent<ZeldaCharacterData>() != null)
            {
                continue;
            }

            // An expanded planning cast may already touch a nearby wall at
            // the real start position. It must still be allowed to travel
            // parallel to or away from that wall.
            if (movementHits[i].distance <=
                    transitionClearance + collisionSkinWidth &&
                Vector2.Dot(transitionDirection, movementHits[i].normal) >=
                    -0.001f)
            {
                continue;
            }

            return false;
        }

        return hitCount < movementHits.Length;
    }

    private float GetGridObstaclePenalty(Vector2Int cell)
    {
        int index = CellToIndex(cell);
        if (index < 0 || preferredGridObstacleDistance <= 0f ||
            gridObstacleProximityCost <= 0f)
        {
            return 0f;
        }

        if (gridObstaclePenalties[index] >= 0f)
        {
            return gridObstaclePenalties[index];
        }

        Vector3 lossyScale = transform.lossyScale;
        float bodyRadius = Mathf.Max(
            bodyCollider.size.x * Mathf.Abs(lossyScale.x),
            bodyCollider.size.y * Mathf.Abs(lossyScale.y)) * 0.5f;
        Vector2 colliderOffset = transform.TransformVector(bodyCollider.offset);
        Vector2 center =
            GridCellToWorld(cell, activeGridCellSize) + colliderOffset;
        float queryRadius =
            bodyRadius + activeGridClearance +
            preferredGridObstacleDistance;
        int overlapCount = Physics2D.OverlapCircleNonAlloc(
            center,
            queryRadius,
            gridOverlapHits,
            solidCollisionLayers);

        float closestGap = preferredGridObstacleDistance;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D obstacle = gridOverlapHits[i];
            gridOverlapHits[i] = null;
            if (obstacle == null || obstacle == bodyCollider ||
                obstacle.isTrigger || obstacle.transform.IsChildOf(transform) ||
                IsUnlockedDoorCollider(obstacle) ||
                obstacle.GetComponentInParent<ZeldaCharacterData>() != null)
            {
                continue;
            }

            Vector2 closestPoint = obstacle.ClosestPoint(center);
            float surfaceGap = Mathf.Max(
                0f,
                Vector2.Distance(center, closestPoint) - bodyRadius -
                activeGridClearance);
            closestGap = Mathf.Min(closestGap, surfaceGap);
        }

        float proximity =
            1f - Mathf.Clamp01(closestGap / preferredGridObstacleDistance);
        // Squaring keeps distant nodes inexpensive while strongly
        // discouraging routes that skim directly along a collider.
        float penalty = proximity * proximity * gridObstacleProximityCost;
        gridObstaclePenalties[index] = penalty;
        return penalty;
    }

    private void EnsureGridCapacity(int nodeCount)
    {
        if (gridCosts != null && gridCosts.Length >= nodeCount)
        {
            return;
        }
        gridCosts = new float[nodeCount];
        gridParents = new int[nodeCount];
        gridStates = new byte[nodeCount];
        gridWalkability = new byte[nodeCount];
        gridObstaclePenalties = new float[nodeCount];
        gridHeap = new int[nodeCount];
        gridHeapPositions = new int[nodeCount];
    }

    private void HeapPush(int nodeIndex, Vector2Int goal)
    {
        int position = gridHeapCount++;
        gridHeap[position] = nodeIndex;
        gridHeapPositions[nodeIndex] = position;
        HeapMoveUp(position, goal);
    }

    private int HeapPop(Vector2Int goal)
    {
        int result = gridHeap[0];
        gridHeapCount--;
        gridHeapPositions[result] = -1;
        if (gridHeapCount > 0)
        {
            gridHeap[0] = gridHeap[gridHeapCount];
            gridHeapPositions[gridHeap[0]] = 0;
            HeapMoveDown(0, goal);
        }
        return result;
    }

    private void HeapMoveUp(int position, Vector2Int goal)
    {
        while (position > 0)
        {
            int parent = (position - 1) / 2;
            if (GridNodeScore(gridHeap[parent], goal) <=
                GridNodeScore(gridHeap[position], goal))
            {
                break;
            }
            HeapSwap(parent, position);
            position = parent;
        }
    }

    private void HeapMoveDown(int position, Vector2Int goal)
    {
        while (true)
        {
            int left = position * 2 + 1;
            if (left >= gridHeapCount)
            {
                return;
            }
            int right = left + 1;
            int best = right < gridHeapCount &&
                       GridNodeScore(gridHeap[right], goal) <
                       GridNodeScore(gridHeap[left], goal)
                ? right
                : left;
            if (GridNodeScore(gridHeap[position], goal) <=
                GridNodeScore(gridHeap[best], goal))
            {
                return;
            }
            HeapSwap(position, best);
            position = best;
        }
    }

    private void HeapSwap(int first, int second)
    {
        int temp = gridHeap[first];
        gridHeap[first] = gridHeap[second];
        gridHeap[second] = temp;
        gridHeapPositions[gridHeap[first]] = first;
        gridHeapPositions[gridHeap[second]] = second;
    }

    private float GridNodeScore(int nodeIndex, Vector2Int goal)
    {
        Vector2Int cell = IndexToCell(nodeIndex);
        return gridCosts[nodeIndex] +
               GridHeuristic(cell, goal, activeGridGoalRadius);
    }

    private static float GridHeuristic(
        Vector2Int cell,
        Vector2Int goal,
        int acceptedGoalRadius)
    {
        int radius = Mathf.Max(0, acceptedGoalRadius);
        int dx = Mathf.Max(0, Mathf.Abs(cell.x - goal.x) - radius);
        int dy = Mathf.Max(0, Mathf.Abs(cell.y - goal.y) - radius);
        // Octile distance is admissible for an eight-connected grid. A tiny
        // deterministic tie breaker favours nodes that continue progressing
        // toward the goal and prevents equal-cost zig-zag choices.
        float diagonalDistance = Mathf.Min(dx, dy);
        float straightDistance = Mathf.Max(dx, dy) - diagonalDistance;
        return diagonalDistance * 1.4142135f + straightDistance +
               (dx + dy) * 0.0001f;
    }

    private int CellToIndex(Vector2Int cell)
    {
        int x = cell.x - gridOriginCell.x;
        int y = cell.y - gridOriginCell.y;
        return x < 0 || y < 0 || x >= gridWidth || y >= gridHeight
            ? -1
            : y * gridWidth + x;
    }

    private Vector2Int IndexToCell(int index)
    {
        return new Vector2Int(
            gridOriginCell.x + index % gridWidth,
            gridOriginCell.y + index / gridWidth);
    }

    private static Vector2Int WorldToGridCell(Vector2 world, float cellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(world.x / cellSize),
            Mathf.FloorToInt(world.y / cellSize));
    }

    private static Vector2 GridCellToWorld(Vector2Int cell, float cellSize)
    {
        return new Vector2(
            (cell.x + 0.5f) * cellSize,
            (cell.y + 0.5f) * cellSize);
    }

    private void ClearGridPath()
    {
        gridPath.Clear();
        gridPathIndex = 0;
        nextGridPathValidationTime = 0f;
    }

    private Vector2 PlanDirectionAroundObstacles(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= 0f)
        {
            ResetObstacleFollowing();
            InvalidateNavigationPlan();
            return Vector2.zero;
        }

        desiredDirection.Normalize();
        float directionDot = lastRequestedNavigationDirection.sqrMagnitude > 0f
            ? Vector2.Dot(lastRequestedNavigationDirection, desiredDirection)
            : -1f;
        float directionThreshold = Mathf.Cos(navigationDirectionChangeAngle * Mathf.Deg2Rad);
        bool directionChangedSignificantly = directionDot < directionThreshold;

        if (Time.time < nextNavigationRefreshTime && !directionChangedSignificantly)
        {
            return cachedPlannedDirection;
        }

        float refreshInterval = currentState == ZeldaAiState.Hostile
            ? hostileNavigationRefreshInterval
            : navigationRefreshInterval;
        // Distributing the fractional phase by instance ID prevents a crowd of
        // AI characters from performing all route probes on the same frame.
        float stagger = Mathf.Abs(GetInstanceID() % 11) / 11f;
        nextNavigationRefreshTime = Time.time + refreshInterval * Mathf.Lerp(0.85f, 1.15f, stagger);
        lastRequestedNavigationDirection = desiredDirection;
        cachedPlannedDirection = CalculateDirectionAroundObstacles(desiredDirection);
        return cachedPlannedDirection;
    }

    private Vector2 CalculateDirectionAroundObstacles(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= 0f)
        {
            ResetObstacleFollowing();
            return Vector2.zero;
        }

        desiredDirection.Normalize();
        float desiredClearance = GetObstacleClearance(desiredDirection, obstacleProbeDistance);
        bool directRouteIsClear = desiredClearance >= obstacleProbeDistance;
        if (directRouteIsClear && !isFollowingObstacle)
        {
            avoidanceDirectionTimer = 0f;
            return desiredDirection;
        }

        if (!directRouteIsClear)
        {
            directRouteClearTimer = 0f;
            if (!isFollowingObstacle)
            {
                BeginObstacleFollowing(desiredDirection);
            }
        }
        else
        {
            directRouteClearTimer += Time.deltaTime;
            if (directRouteClearTimer >= wallFollowExitDelay)
            {
                ResetObstacleFollowing();
                return desiredDirection;
            }
        }

        if (avoidanceDirectionTimer > 0f &&
            GetObstacleClearance(avoidanceDirection, obstacleProbeDistance * 0.7f) >=
            obstacleProbeDistance * 0.7f)
        {
            return avoidanceDirection;
        }

        Vector2 followDirection = FindObstacleFollowDirection(desiredDirection, obstacleFollowSide);
        if (followDirection.sqrMagnitude <= 0f)
        {
            // A concave corner can temporarily close the selected side. Switch
            // only when that side has no usable direction at all.
            int oppositeSide = -obstacleFollowSide;
            followDirection = FindObstacleFollowDirection(desiredDirection, oppositeSide);
            if (followDirection.sqrMagnitude > 0f)
            {
                obstacleFollowSide = oppositeSide;
            }
        }

        if (followDirection.sqrMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        avoidanceDirection = followDirection;
        avoidanceDirectionTimer = avoidanceDirectionHoldTime;
        return avoidanceDirection;
    }

    private void BeginObstacleFollowing(Vector2 desiredDirection)
    {
        isFollowingObstacle = true;
        directRouteClearTimer = 0f;

        Vector2 leftDirection = RotateDirection(desiredDirection, 90f);
        Vector2 rightDirection = RotateDirection(desiredDirection, -90f);
        float leftClearance = GetObstacleClearance(leftDirection, obstacleProbeDistance);
        float rightClearance = GetObstacleClearance(rightDirection, obstacleProbeDistance);
        if (Mathf.Abs(leftClearance - rightClearance) <= collisionSkinWidth)
        {
            obstacleFollowSide = (GetInstanceID() & 1) == 0 ? 1 : -1;
        }
        else
        {
            obstacleFollowSide = leftClearance > rightClearance ? 1 : -1;
        }
    }

    private Vector2 FindObstacleFollowDirection(Vector2 desiredDirection, int followSide)
    {
        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        // First retain the configurable fine-angle probes used for small
        // obstacles, then extend the same side to a full half-circle so a
        // character can round the corner of a large or concave obstacle.
        for (int step = 1; step <= avoidanceDirectionSteps; step++)
        {
            float angle = Mathf.Min(180f, avoidanceAngleStep * step) * followSide;
            EvaluateAvoidanceDirection(RotateDirection(desiredDirection, angle), desiredDirection,
                ref bestDirection, ref bestScore);
        }

        for (int angle = 30; angle <= 180; angle += 30)
        {
            EvaluateAvoidanceDirection(
                RotateDirection(desiredDirection, angle * followSide),
                desiredDirection,
                ref bestDirection,
                ref bestScore);
        }

        return bestDirection;
    }

    private void ResetObstacleFollowing()
    {
        isFollowingObstacle = false;
        directRouteClearTimer = 0f;
        avoidanceDirectionTimer = 0f;
        avoidanceDirection = Vector2.zero;
    }

    private void InvalidateNavigationPlan()
    {
        nextNavigationRefreshTime = 0f;
        float separationStagger =
            Mathf.Abs(GetInstanceID() % 11) / 11f *
            Mathf.Max(0.01f, characterSeparationRefreshInterval);
        nextSeparationRefreshTime = Time.time + separationStagger;
        lastSeparationRefreshTime = Time.time + separationStagger;
        cachedPlannedDirection = Vector2.zero;
        lastRequestedNavigationDirection = Vector2.zero;
        cachedSeparation = Vector2.zero;
        cachedCharacterYieldWeight = 0f;
        cachedSeparatedDirection = Vector2.zero;
        lastSeparationDesiredDirection = Vector2.zero;
        float initialGridStagger =
            Mathf.Abs(GetInstanceID() % 7) / 7f * 0.06f;
        nextGridPathRefreshTime = Time.time + initialGridStagger;
        nextGridPathMaintenanceTime = 0f;
        hasGridPathPlan = false;
        ClearGridPath();
    }

    private Vector2 ApplyCharacterSeparation(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f ||
            characterSeparationDistance <= 0f || characterSeparationStrength <= 0f)
        {
            cachedSeparation = Vector2.zero;
            cachedCharacterYieldWeight = 0f;
            cachedSeparatedDirection = desiredDirection;
            lastSeparationDesiredDirection = desiredDirection;
            return desiredDirection;
        }

        Vector2 normalizedDesiredDirection = desiredDirection.normalized;
        bool separationDirectionChanged =
            lastSeparationDesiredDirection.sqrMagnitude <= 0f ||
            Vector2.Dot(lastSeparationDesiredDirection, normalizedDesiredDirection) <
            Mathf.Cos(navigationDirectionChangeAngle * Mathf.Deg2Rad);
        float separationCrowdMultiplier = Mathf.Min(
            2f,
            GetHostileCrowdIntervalMultiplier());
        bool forcedRefreshReady = separationDirectionChanged &&
            Time.time >= lastSeparationRefreshTime +
            Mathf.Max(0.01f, characterSeparationForcedRefreshInterval) *
            separationCrowdMultiplier;
        if (Time.time >= nextSeparationRefreshTime || forcedRefreshReady)
        {
            cachedSeparation = Vector2.zero;
            cachedCharacterYieldWeight = 0f;
            nextSeparationRefreshTime = Time.time +
                characterSeparationRefreshInterval *
                separationCrowdMultiplier;
            lastSeparationRefreshTime = Time.time;
            lastSeparationDesiredDirection = normalizedDesiredDirection;
            float avoidanceDistance = Mathf.Max(
                characterSeparationDistance,
                characterAvoidanceLookAheadDistance);
            float avoidanceDistanceSquared = avoidanceDistance * avoidanceDistance;
            foreach (ZeldaFourWayMover otherMover in ZeldaRuntimeRegistry.Movers)
            {
                ZeldaCharacterData otherCharacter = otherMover != null
                    ? otherMover.GetComponent<ZeldaCharacterData>()
                    : null;
                ZeldaCharacterAiBase otherAi = otherMover != null
                    ? otherMover.GetComponent<ZeldaCharacterAiBase>()
                    : null;
                bool otherIsOperational = otherMover != null &&
                    (otherMover.isActiveAndEnabled ||
                     (otherAi != null && otherAi.isActiveAndEnabled));
                if (otherCharacter == null || otherCharacter == characterData ||
                    otherCharacter.IsDead || !otherIsOperational ||
                    otherMover == targetMover ||
                    !characterData.ParticipatesInCharacterCollision ||
                    !otherCharacter.ParticipatesInCharacterCollision)
                {
                    continue;
                }

                Vector2 awayFromCharacter =
                    rb.position - (Vector2)otherCharacter.transform.position;
                float distanceSquared = awayFromCharacter.sqrMagnitude;
                if (distanceSquared > avoidanceDistanceSquared)
                {
                    continue;
                }

                if (distanceSquared <= 0.0001f)
                {
                    float side = GetInstanceID() < otherCharacter.GetInstanceID() ? -1f : 1f;
                    awayFromCharacter =
                        new Vector2(-desiredDirection.y, desiredDirection.x) * side;
                    distanceSquared = 0.0001f;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 awayDirection = awayFromCharacter / distance;
                Vector2 toOtherDirection = -awayDirection;
                Vector2 otherDirection = GetOtherCharacterMovementDirection(
                    otherMover,
                    otherAi);
                bool otherIsMoving = otherDirection.sqrMagnitude > 0.0001f;
                Vector2 relativeDirection = normalizedDesiredDirection - otherDirection;
                bool isClosing = Vector2.Dot(
                    relativeDirection,
                    toOtherDirection) > 0.05f;
                bool isAhead = Vector2.Dot(
                    normalizedDesiredDirection,
                    toOtherDirection) > 0.05f;
                float corridorHalfWidth = GetCharacterAvoidanceHalfWidth(
                    normalizedDesiredDirection,
                    otherCharacter);
                float signedLateralOffset =
                    normalizedDesiredDirection.x * toOtherDirection.y * distance -
                    normalizedDesiredDirection.y * toOtherDirection.x * distance;
                float lateralDistance = Mathf.Abs(signedLateralOffset);
                bool pathsConflict = isAhead && isClosing &&
                                     lateralDistance <= corridorHalfWidth;
                bool shouldYield = pathsConflict &&
                    (!otherIsMoving || !HasAvoidancePriorityOver(otherAi));

                if (shouldYield)
                {
                    float proximityWeight =
                        1f - Mathf.Clamp01(distance / avoidanceDistance);
                    Vector2 sideDirection = ChooseCharacterYieldSide(
                        normalizedDesiredDirection,
                        otherCharacter,
                        signedLateralOffset);
                    float requiredLateralShift = Mathf.Max(
                        0f,
                        corridorHalfWidth - lateralDistance);
                    float forwardDistance = Mathf.Max(
                        0.05f,
                        Vector2.Dot(
                            (Vector2)otherCharacter.transform.position - rb.position,
                            normalizedDesiredDirection));
                    // The necessary steering angle grows with collider width
                    // and with how late the obstacle is encountered. A static
                    // character receives a stronger, nearly lateral bypass so
                    // the moving AI clears the whole collider before turning
                    // back toward its path.
                    float requiredSideStrength = Mathf.Clamp(
                        requiredLateralShift / forwardDistance,
                        0.8f,
                        3.2f);
                    if (!otherIsMoving)
                    {
                        requiredSideStrength *= 1.45f;
                        if (forwardDistance < corridorHalfWidth * 1.25f)
                        {
                            requiredSideStrength *= 1.35f;
                        }
                    }

                    float clearanceUrgency = corridorHalfWidth > 0.0001f
                        ? Mathf.Clamp01(requiredLateralShift / corridorHalfWidth)
                        : 0f;
                    cachedSeparation +=
                        sideDirection * requiredSideStrength +
                        awayDirection * Mathf.Lerp(0.2f, 0.55f, clearanceUrgency);
                    cachedCharacterYieldWeight = Mathf.Max(
                        cachedCharacterYieldWeight,
                        Mathf.Max(proximityWeight, clearanceUrgency));
                    continue;
                }

                // The right-of-way character stays close to its planned route.
                // Radial separation is retained only at immediate contact so
                // small collider overlaps cannot deadlock either participant.
                if (distance < characterSeparationDistance * 0.55f)
                {
                    float contactWeight = 1f - Mathf.Clamp01(
                        distance / Mathf.Max(
                            0.05f,
                            characterSeparationDistance * 0.55f));
                    cachedSeparation += awayDirection * contactWeight * 0.35f;
                }
            }

            if (cachedSeparation.sqrMagnitude <= 0.0001f)
            {
                cachedSeparatedDirection = desiredDirection;
            }
            else
            {
                Vector2 separatedDirection =
                    (normalizedDesiredDirection +
                     cachedSeparation * characterSeparationStrength).normalized;
                float staticClearance = GetObstacleClearance(
                    separatedDirection,
                    obstacleProbeDistance * 0.7f,
                    true);
                Vector2 selectedDirection;
                if (staticClearance > collisionSkinWidth)
                {
                    selectedDirection = separatedDirection;
                }
                else
                {
                    // In a narrow passage the forward/side blend can touch a
                    // corner even though a pure lateral step is possible.
                    // Prefer that lateral step rather than falling back into
                    // the character or invalidating the A* wall path.
                    Vector2 lateralDirection = cachedSeparation.normalized;
                    float lateralClearance = GetObstacleClearance(
                        lateralDirection,
                        obstacleProbeDistance * 0.7f,
                        true);
                    selectedDirection = lateralClearance > collisionSkinWidth
                        ? lateralDirection
                        : Vector2.zero;
                }
                float speedMultiplier = Mathf.Lerp(
                    1f,
                    characterYieldSpeedMultiplier,
                    Mathf.Clamp01(cachedCharacterYieldWeight));
                cachedSeparatedDirection = selectedDirection * speedMultiplier;
            }
        }

        if (cachedCharacterYieldWeight > 0.001f)
        {
            // A zero direction here intentionally means wait for the
            // higher-priority character. Returning desiredDirection would
            // repeatedly drive into it in a passage with no passing room.
            return cachedSeparatedDirection;
        }

        return cachedSeparatedDirection.sqrMagnitude > 0.0001f
            ? cachedSeparatedDirection
            : desiredDirection;
    }

    private Vector2 GetOtherCharacterMovementDirection(
        ZeldaFourWayMover otherMover,
        ZeldaCharacterAiBase otherAi)
    {
        if (otherMover != null && otherMover.isActiveAndEnabled)
        {
            return otherMover.IsMoving ? otherMover.FacingDirection.normalized : Vector2.zero;
        }

        if (otherAi != null && otherAi.isActiveAndEnabled &&
            otherAi.moveDirection.sqrMagnitude > 0.0001f)
        {
            return otherAi.moveDirection.normalized;
        }

        return Vector2.zero;
    }

    private bool HasAvoidancePriorityOver(ZeldaCharacterAiBase otherAi)
    {
        // Player-controlled characters and stationary non-AI characters always
        // receive right of way. Between two AI actors the state rank is used
        // first, followed by a stable instance-ID tie breaker. Consequently
        // only one participant yields instead of both deviating from the path.
        if (otherAi == null || !otherAi.isActiveAndEnabled)
        {
            return false;
        }

        int ownPriority = GetAvoidanceStatePriority(currentState);
        int otherPriority = GetAvoidanceStatePriority(otherAi.currentState);
        if (ownPriority != otherPriority)
        {
            return ownPriority > otherPriority;
        }

        return GetInstanceID() < otherAi.GetInstanceID();
    }

    private static int GetAvoidanceStatePriority(ZeldaAiState state)
    {
        switch (state)
        {
            case ZeldaAiState.Hostile: return 6;
            case ZeldaAiState.Alert: return 5;
            case ZeldaAiState.Suspicious: return 4;
            case ZeldaAiState.Search: return 3;
            case ZeldaAiState.Recovery: return 2;
            default: return 1;
        }
    }

    private float GetCharacterAvoidanceHalfWidth(
        Vector2 travelDirection,
        ZeldaCharacterData otherCharacter)
    {
        Vector2 lateralAxis = new Vector2(
            -travelDirection.y,
            travelDirection.x).normalized;
        Bounds ownBounds = bodyCollider.bounds;
        float ownHalfWidth =
            Mathf.Abs(lateralAxis.x) * ownBounds.extents.x +
            Mathf.Abs(lateralAxis.y) * ownBounds.extents.y;
        Collider2D otherCollider = otherCharacter != null
            ? otherCharacter.GetComponent<Collider2D>()
            : null;
        float otherHalfWidth = ownHalfWidth;
        if (otherCollider != null)
        {
            Bounds otherBounds = otherCollider.bounds;
            otherHalfWidth =
                Mathf.Abs(lateralAxis.x) * otherBounds.extents.x +
                Mathf.Abs(lateralAxis.y) * otherBounds.extents.y;
        }

        return ownHalfWidth + otherHalfWidth +
               characterAvoidanceClearancePadding + collisionSkinWidth * 2f;
    }

    private Vector2 ChooseCharacterYieldSide(
        Vector2 forward,
        ZeldaCharacterData otherCharacter,
        float signedLateralOffset)
    {
        Vector2 left = new Vector2(-forward.y, forward.x);
        // If the characters are already offset, continue away from the other
        // collider instead of crossing back through its centre line. When
        // perfectly aligned, use the stable ID choice to prevent side jitter.
        float stableSide = Mathf.Abs(signedLateralOffset) > collisionSkinWidth
            ? -Mathf.Sign(signedLateralOffset)
            : (GetInstanceID() < otherCharacter.GetInstanceID() ? -1f : 1f);
        Vector2 preferred = left * stableSide;
        Vector2 alternate = -preferred;
        float probeDistance = Mathf.Max(
            characterSeparationDistance,
            obstacleProbeDistance * 0.7f);
        float preferredClearance = GetObstacleClearance(
            preferred,
            probeDistance,
            true);
        if (Mathf.Abs(signedLateralOffset) > collisionSkinWidth)
        {
            // Once committed to a side, never cross back through the other
            // character merely because the wall is close. Waiting is stable;
            // alternating sides is what caused narrow-corridor oscillation.
            return preferred;
        }

        float alternateClearance = GetObstacleClearance(
            alternate,
            probeDistance,
            true);
        return alternateClearance > preferredClearance + collisionSkinWidth
            ? alternate
            : preferred;
    }

    private void EvaluateAvoidanceDirection(
        Vector2 candidate,
        Vector2 desiredDirection,
        ref Vector2 bestDirection,
        ref float bestScore)
    {
        float clearance = GetObstacleClearance(candidate, obstacleProbeDistance);
        if (clearance <= collisionSkinWidth)
        {
            return;
        }

        float clearanceScore = clearance / obstacleProbeDistance;
        float targetAlignment = Vector2.Dot(candidate, desiredDirection);
        float score = clearanceScore * 2f + targetAlignment;
        if (score > bestScore)
        {
            bestScore = score;
            bestDirection = candidate;
        }
    }

    private float GetObstacleClearance(
        Vector2 direction,
        float probeDistance,
        bool ignoreCharacters = false)
    {
        if (direction.sqrMagnitude <= 0f || probeDistance <= 0f)
        {
            return 0f;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(solidCollisionLayers);
        filter.useTriggers = false;
        int hitCount = bodyCollider.Cast(direction.normalized, filter, movementHits, probeDistance);
        float clearance = probeDistance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = movementHits[i].collider;
            bool isTargetCollider = targetMover != null &&
                (hitCollider != null && (hitCollider.transform == targetMover.transform ||
                hitCollider.transform.IsChildOf(targetMover.transform)));
            if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger || isTargetCollider)
            {
                continue;
            }

            ZeldaCharacterData hitCharacter = hitCollider.GetComponentInParent<ZeldaCharacterData>();
            if (ignoreCharacters && hitCharacter != null)
            {
                continue;
            }

            if (IsNonParticipatingCharacterCollider(hitCollider))
            {
                continue;
            }

            if (IsUnlockedDoorCollider(hitCollider))
            {
                continue;
            }

            if (IsMovingAwayFromCharacter(hitCollider, direction))
            {
                continue;
            }

            if (Vector2.Dot(direction.normalized, movementHits[i].normal) >=
                -0.001f)
            {
                continue;
            }

            clearance = Mathf.Min(clearance, Mathf.Max(0f, movementHits[i].distance - collisionSkinWidth));
        }

        return clearance;
    }

    private bool IsNonParticipatingCharacterCollider(Collider2D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        ZeldaCharacterData otherCharacter =
            candidate.GetComponentInParent<ZeldaCharacterData>();
        if (otherCharacter == null ||
            (characterData.ParticipatesInCharacterCollision &&
             otherCharacter.ParticipatesInCharacterCollision))
        {
            return false;
        }

        // Establish the physics-layer exception as soon as a movement probe
        // encounters a non-colliding character. This removes the brief frame
        // where a newly spawned ghost could still stop a kinematic AI before
        // the ghost's periodic collision refresh runs.
        if (bodyCollider != null && candidate != bodyCollider &&
            !Physics2D.GetIgnoreCollision(bodyCollider, candidate))
        {
            Physics2D.IgnoreCollision(bodyCollider, candidate, true);
        }

        return true;
    }

    private bool IsMovingAwayFromCharacter(Collider2D hitCollider, Vector2 movementDirection)
    {
        ZeldaCharacterData otherCharacter = hitCollider.GetComponentInParent<ZeldaCharacterData>();
        if (otherCharacter == null || otherCharacter == characterData)
        {
            return false;
        }

        Vector2 awayFromOther = (Vector2)bodyCollider.bounds.center - (Vector2)hitCollider.bounds.center;
        if (awayFromOther.sqrMagnitude <= 0.0001f)
        {
            float escapeSide = GetInstanceID() < otherCharacter.GetInstanceID() ? -1f : 1f;
            awayFromOther = Vector2.right * escapeSide;
        }

        return Vector2.Dot(movementDirection, awayFromOther.normalized) > 0.001f;
    }

    private bool IsUnlockedDoorCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        DoorHingeInteraction door = hitCollider.GetComponentInParent<DoorHingeInteraction>();
        return door != null && !door.IsLocked;
    }

    private static Vector2 RotateDirection(Vector2 direction, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine).normalized;
    }

    private bool IsVisionBlocked(Vector2 origin, Vector2 direction, float distance, Transform ignoredTarget)
    {
        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            direction,
            visionHits,
            distance,
            visionObstacleLayers);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = visionHits[i].collider;
            if (!IsVisionOccluder(hitCollider, ignoredTarget))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private float GetVisibleRayDistance(Vector2 origin, Vector2 direction, float maximumDistance)
    {
        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            direction,
            visionHits,
            maximumDistance,
            visionObstacleLayers);
        float closestDistance = maximumDistance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = visionHits[i].collider;
            if (!IsVisionOccluder(hitCollider, null))
            {
                continue;
            }

            closestDistance = Mathf.Min(closestDistance, visionHits[i].distance);
        }

        return closestDistance;
    }

    private bool IsVisionOccluder(
        Collider2D hitCollider,
        Transform ignoredTarget)
    {
        if (hitCollider == null || hitCollider.isTrigger ||
            hitCollider.transform.IsChildOf(transform) ||
            (ignoredTarget != null &&
             hitCollider.transform.IsChildOf(ignoredTarget)))
        {
            return false;
        }

        // Characters are dynamic occupants of the visible space rather than
        // opaque level geometry. Ignoring every character collider here keeps
        // perception and the rendered cone consistent when actors overlap.
        return hitCollider.GetComponentInParent<ZeldaCharacterData>() == null;
    }

    private void CreateVisionVisual()
    {
        Transform existing = transform.Find(VisionObjectName);
        GameObject visionObject = existing != null ? existing.gameObject : new GameObject(VisionObjectName);
        visionObject.transform.SetParent(transform, false);
        visionObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        MeshFilter filter = visionObject.GetComponent<MeshFilter>();
        if (filter == null) filter = visionObject.AddComponent<MeshFilter>();
        visionRenderer = visionObject.GetComponent<MeshRenderer>();
        if (visionRenderer == null) visionRenderer = visionObject.AddComponent<MeshRenderer>();

        visionMesh = new Mesh { name = "Zelda AI Vision Cone" };
        visionMesh.MarkDynamic();
        filter.sharedMesh = visionMesh;
        visionMaterial = new Material(Shader.Find("Sprites/Default"));
        visionMaterial.color = visionColor;
        visionRenderer.sharedMaterial = visionMaterial;
        visionRenderer.sortingOrder = 0;
    }

    private void SetVisionVisible(bool isVisible)
    {
        if (visionRenderer != null)
        {
            visionRenderer.enabled = isVisible;
        }
    }

    private void CreateStateIndicator()
    {
        const string indicatorObjectName = "AI State Indicator";
        Transform existing = transform.Find(indicatorObjectName);
        GameObject indicatorObject = existing != null
            ? existing.gameObject
            : new GameObject(indicatorObjectName);
        indicatorObject.transform.SetParent(transform, false);
        indicatorObject.transform.localPosition = stateIndicatorOffset;
        indicatorObject.transform.localScale = Vector3.one * stateIndicatorScale;

        stateIndicator = indicatorObject.GetComponent<ZeldaAiStateIndicator>();
        if (stateIndicator == null)
        {
            stateIndicator = indicatorObject.AddComponent<ZeldaAiStateIndicator>();
        }
    }

    private void SetStateIndicatorVisible(bool isVisible)
    {
        if (stateIndicator != null)
        {
            stateIndicator.SetComponentVisible(isVisible);
        }
    }

    private void UpdateStateIndicator()
    {
        if (stateIndicator == null)
        {
            return;
        }

        stateIndicator.transform.localPosition = stateIndicatorOffset;
        stateIndicator.transform.localScale = Vector3.one * stateIndicatorScale;
        float warningProgress = warningEscalationTime > 0f
            ? Mathf.Clamp01(warningTimer / warningEscalationTime)
            : 1f;
        stateIndicator.SetState(
            currentState,
            SuspicionProgress,
            warningProgress,
            hostileIndicatorTimer > 0f);
    }

    private void UpdateVisionMesh()
    {
        if (visionMesh == null)
        {
            return;
        }

        int rayCount = Mathf.Max(4, visionRayCount);
        bool topologyChanged = EnsureVisionMeshBuffers(rayCount);
        Vector2 origin = transform.position;
        float facingAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
        float displayedAngle = Mathf.Max(visionAngle, peripheralVisionAngle);
        float startAngle = facingAngle - displayedAngle * 0.5f;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = startAngle + displayedAngle * i / rayCount;
            float angleFromFacing = Mathf.Abs(Mathf.DeltaAngle(facingAngle, angle));
            float rayMaximumDistance = angleFromFacing <= visionAngle * 0.5f
                ? visionRadius
                : peripheralVisionRadius;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float distance = GetVisibleRayDistance(origin, direction, rayMaximumDistance);
            visionVertices[i + 1] =
                transform.InverseTransformPoint(origin + direction * distance);
        }

        if (topologyChanged)
        {
            visionMesh.Clear();
        }
        visionMesh.vertices = visionVertices;
        if (topologyChanged)
        {
            visionMesh.triangles = visionTriangles;
        }
        visionMesh.RecalculateBounds();
        visionMaterial.color = visionColor;
    }

    private bool EnsureVisionMeshBuffers(int rayCount)
    {
        if (cachedVisionRayCount == rayCount && visionVertices != null &&
            visionTriangles != null)
        {
            return false;
        }

        cachedVisionRayCount = rayCount;
        visionVertices = new Vector3[rayCount + 2];
        visionTriangles = new int[rayCount * 3];
        for (int i = 0; i < rayCount; i++)
        {
            int triangleIndex = i * 3;
            visionTriangles[triangleIndex] = 0;
            visionTriangles[triangleIndex + 1] = i + 1;
            visionTriangles[triangleIndex + 2] = i + 2;
        }

        return true;
    }

    private SpriteRenderer FindCharacterRenderer()
    {
        Transform visual = transform.Find("Visual");
        if (visual != null)
        {
            SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
            if (visualRenderer != null)
            {
                return visualRenderer;
            }
        }

        return GetComponent<SpriteRenderer>();
    }

    private void UpdateCharacterVisual()
    {
        SpriteRenderer activeRenderer = FindCharacterRenderer();
        if (activeRenderer != null && activeRenderer != characterRenderer)
        {
            characterRenderer = activeRenderer;
        }

        characterData.ApplyCharacterVisualWithMovement(characterRenderer, facingDirection,
            moveDirection.sqrMagnitude > 0f, mover.IsAttacking);
    }

    protected virtual void OnDestroy()
    {
        ZeldaRuntimeRegistry.Unregister(this);
        if (visionMesh != null) Destroy(visionMesh);
        if (visionMaterial != null) Destroy(visionMaterial);
        if (navigationDebugMaterial != null) Destroy(navigationDebugMaterial);
    }

    private void CreateNavigationDebugVisual()
    {
        GameObject lineObject = new GameObject("AI Navigation Debug Path");
        lineObject.transform.SetParent(transform, false);
        navigationDebugLine = lineObject.AddComponent<LineRenderer>();
        navigationDebugLine.useWorldSpace = true;
        navigationDebugLine.loop = false;
        navigationDebugLine.textureMode = LineTextureMode.Stretch;
        navigationDebugLine.numCapVertices = 2;
        navigationDebugLine.numCornerVertices = 2;
        navigationDebugLine.positionCount = 0;
        navigationDebugLine.sortingOrder = navigationDebugSortingOrder;
        navigationDebugMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            name = name + " Navigation Debug Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        navigationDebugLine.sharedMaterial = navigationDebugMaterial;

        EnsureNavigationTargetSprite();
        GameObject targetObject = new GameObject("AI Navigation Debug Target");
        targetObject.transform.SetParent(transform, false);
        navigationDebugTarget = targetObject.AddComponent<SpriteRenderer>();
        navigationDebugTarget.sprite = navigationTargetSprite;
        navigationDebugTarget.sortingOrder = navigationDebugSortingOrder + 1;
        SetNavigationDebugVisible(false);
    }

    private void UpdateNavigationDebugVisual()
    {
        if (navigationDebugLine == null || navigationDebugTarget == null)
        {
            return;
        }

        bool visible = showNavigationDebug && hasNavigationDebugTarget &&
                       isActiveAndEnabled && !characterData.IsDead;
        SetNavigationDebugVisible(visible);
        if (!visible)
        {
            return;
        }

        navigationDebugLine.startColor = navigationPathColor;
        navigationDebugLine.endColor = navigationPathColor;
        navigationDebugLine.startWidth = navigationPathWidth;
        navigationDebugLine.endWidth = navigationPathWidth;
        navigationDebugLine.sortingOrder = navigationDebugSortingOrder;

        int remainingNodeCount = Mathf.Max(0, gridPath.Count - gridPathIndex);
        Vector2 finalPathPoint = remainingNodeCount > 0
            ? gridPath[gridPath.Count - 1]
            : rb.position;
        bool finalTargetSegmentIsClear = IsBodyPathSegmentClear(
            finalPathPoint,
            gridPathTarget,
            0f);
        bool showLocalFallback = remainingNodeCount == 0 &&
                                 !finalTargetSegmentIsClear &&
                                 cachedPlannedDirection.sqrMagnitude > 0.0001f;
        int pointCount = 1 + remainingNodeCount +
                         (finalTargetSegmentIsClear || showLocalFallback ? 1 : 0);
        navigationDebugLine.positionCount = pointCount;
        int positionIndex = 0;
        navigationDebugLine.SetPosition(positionIndex++, rb.position);
        for (int i = gridPathIndex; i < gridPath.Count; i++)
        {
            navigationDebugLine.SetPosition(positionIndex++, gridPath[i]);
        }
        if (finalTargetSegmentIsClear)
        {
            navigationDebugLine.SetPosition(positionIndex, gridPathTarget);
        }
        else if (showLocalFallback)
        {
            navigationDebugLine.SetPosition(
                positionIndex,
                rb.position + cachedPlannedDirection.normalized *
                obstacleProbeDistance);
        }

        navigationDebugTarget.transform.position = gridPathTarget;
        navigationDebugTarget.transform.rotation = Quaternion.identity;
        navigationDebugTarget.transform.localScale =
            Vector3.one * navigationTargetMarkerSize;
        navigationDebugTarget.color = navigationTargetColor;
        navigationDebugTarget.sortingOrder = navigationDebugSortingOrder + 1;
    }

    private void SetNavigationDebugVisible(bool visible)
    {
        if (navigationDebugLine != null)
        {
            navigationDebugLine.enabled = visible;
        }
        if (navigationDebugTarget != null)
        {
            navigationDebugTarget.enabled = visible;
        }
    }

    private static void EnsureNavigationTargetSprite()
    {
        if (navigationTargetSprite != null)
        {
            return;
        }

        const int size = 9;
        Texture2D texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime AI Navigation Target";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        int center = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                bool diamondBorder = distance == center ||
                                     (x == center && y == center);
                texture.SetPixel(
                    x,
                    y,
                    diamondBorder ? Color.white : Color.clear);
            }
        }
        texture.Apply(false, true);
        navigationTargetSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        navigationTargetSprite.name = "Runtime AI Navigation Target";
    }

    protected virtual void OnValidate()
    {
        visionRadius = Mathf.Max(0.1f, visionRadius);
        peripheralVisionRadius = Mathf.Max(0.1f, peripheralVisionRadius);
        peripheralVisionAngle = Mathf.Max(visionAngle, peripheralVisionAngle);
        visionRayCount = Mathf.Clamp(visionRayCount, 4, 180);
        visionVisualRefreshInterval =
            Mathf.Max(0.02f, visionVisualRefreshInterval);
        cardboardBoxApproachDistance =
            Mathf.Max(0.1f, cardboardBoxApproachDistance);
        cardboardBoxSightMemoryDuration =
            Mathf.Max(0f, cardboardBoxSightMemoryDuration);
        attackDistance = Mathf.Max(0f, attackDistance);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        regularHostileLoseSightDelay = Mathf.Max(0f, regularHostileLoseSightDelay);
        searchDuration = Mathf.Max(0f, searchDuration);
        searchRadius = Mathf.Max(0.1f, searchRadius);
        searchWaypointTolerance = Mathf.Max(0.01f, searchWaypointTolerance);
        searchWaypointChangeInterval = Mathf.Max(0.1f, searchWaypointChangeInterval);
        searchWaypointObstacleClearance =
            Mathf.Max(0.05f, searchWaypointObstacleClearance);
        searchWaypointSamplingAttempts =
            Mathf.Clamp(searchWaypointSamplingAttempts, 1, 32);
        investigationDuration = Mathf.Max(0f, investigationDuration);
        investigationWaypointPauseDuration =
            Mathf.Max(0f, investigationWaypointPauseDuration);
        investigationTargetRelocationRadius =
            Mathf.Max(0.1f, investigationTargetRelocationRadius);
        investigationRelocationSamplesPerRing =
            Mathf.Clamp(investigationRelocationSamplesPerRing, 4, 32);
        investigationApproachMinimumRadius =
            Mathf.Max(0.1f, investigationApproachMinimumRadius);
        investigationApproachMaximumRadius = Mathf.Max(
            investigationApproachMinimumRadius,
            investigationApproachMaximumRadius);
        investigationApproachTargetSpacing =
            Mathf.Max(0f, investigationApproachTargetSpacing);
        recoveryPositionTolerance = Mathf.Max(0.01f, recoveryPositionTolerance);
        collisionSkinWidth = Mathf.Max(0f, collisionSkinWidth);
        warningFollowDistance = Mathf.Max(0.1f, warningFollowDistance);
        warningEscalationTime = Mathf.Max(0f, warningEscalationTime);
        warningDistanceTolerance = Mathf.Max(0f, warningDistanceTolerance);
        immediateResponseDistance = Mathf.Clamp(immediateResponseDistance, 0f, visionRadius);
        maximumSuspicion = Mathf.Max(0.01f, maximumSuspicion);
        suspicionIncreasePerSecond = Mathf.Max(0f, suspicionIncreasePerSecond);
        suspicionDecreasePerSecond = Mathf.Max(0f, suspicionDecreasePerSecond);
        hostileIndicatorDuration = Mathf.Max(0f, hostileIndicatorDuration);
        stateIndicatorScale = Mathf.Max(0.1f, stateIndicatorScale);
        possessionShakeAmount = Mathf.Max(0f, possessionShakeAmount);
        possessionShakeSpeed = Mathf.Max(0f, possessionShakeSpeed);
        obstacleProbeDistance = Mathf.Max(0.05f, obstacleProbeDistance);
        avoidanceDirectionSteps = Mathf.Clamp(avoidanceDirectionSteps, 1, 6);
        avoidanceDirectionHoldTime = Mathf.Max(0f, avoidanceDirectionHoldTime);
        wallFollowExitDelay = Mathf.Max(0f, wallFollowExitDelay);
        cornerStuckDetectionDuration =
            Mathf.Max(0.05f, cornerStuckDetectionDuration);
        cornerEscapeHoldDuration =
            Mathf.Max(0.05f, cornerEscapeHoldDuration);
        cornerEscapeDirectionSamples =
            Mathf.Clamp(cornerEscapeDirectionSamples, 8, 24);
        cornerOscillationWindowDuration =
            Mathf.Max(0.1f, cornerOscillationWindowDuration);
        cornerMinimumProgressRatio =
            Mathf.Clamp(cornerMinimumProgressRatio, 0.1f, 0.9f);
        navigationRefreshInterval = Mathf.Max(0.01f, navigationRefreshInterval);
        hostileNavigationRefreshInterval =
            Mathf.Clamp(hostileNavigationRefreshInterval, 0.01f, navigationRefreshInterval);
        navigationDirectionChangeAngle = Mathf.Clamp(navigationDirectionChangeAngle, 1f, 90f);
        characterSeparationDistance = Mathf.Max(0.05f, characterSeparationDistance);
        characterSeparationStrength = Mathf.Max(0f, characterSeparationStrength);
        characterAvoidanceLookAheadDistance = Mathf.Max(
            characterSeparationDistance,
            characterAvoidanceLookAheadDistance);
        characterAvoidanceClearancePadding =
            Mathf.Max(0f, characterAvoidanceClearancePadding);
        characterYieldSpeedMultiplier = Mathf.Clamp(
            characterYieldSpeedMultiplier,
            0.1f,
            1f);
        characterSeparationRefreshInterval =
            Mathf.Max(0.01f, characterSeparationRefreshInterval);
        characterSeparationForcedRefreshInterval =
            Mathf.Clamp(
                characterSeparationForcedRefreshInterval,
                0.01f,
                characterSeparationRefreshInterval);
        gridCellSize = Mathf.Max(0.2f, gridCellSize);
        gridSearchPadding = Mathf.Clamp(gridSearchPadding, 2, 12);
        maximumGridDimension = Mathf.Clamp(maximumGridDimension, 15, 81);
        expandedGridDimension = Mathf.Clamp(
            expandedGridDimension,
            Mathf.Max(31, maximumGridDimension),
            161);
        minimumGridCellSize = Mathf.Clamp(
            minimumGridCellSize,
            0.1f,
            gridCellSize);
        gridPathRefreshInterval = Mathf.Max(0.05f, gridPathRefreshInterval);
        hostileGridPathRefreshInterval =
            Mathf.Clamp(hostileGridPathRefreshInterval, 0.05f, gridPathRefreshInterval);
        movingTargetGridRepathInterval =
            Mathf.Max(0.05f, movingTargetGridRepathInterval);
        gridPathMaintenanceInterval = Mathf.Max(
            0.2f,
            Mathf.Max(movingTargetGridRepathInterval, gridPathMaintenanceInterval));
        maximumAStarBuildsPerFrame =
            Mathf.Clamp(maximumAStarBuildsPerFrame, 1, 8);
        globalAStarBuildInterval =
            Mathf.Max(0.01f, globalAStarBuildInterval);
        expensiveGridFallbackInterval =
            Mathf.Max(0.05f, expensiveGridFallbackInterval);
        gridPathValidationInterval =
            Mathf.Max(0.02f, gridPathValidationInterval);
        gridWaypointTolerance = Mathf.Max(0.02f, gridWaypointTolerance);
        gridWallClearance = Mathf.Max(0f, gridWallClearance);
        maximumWallSeparationStep =
            Mathf.Max(0.01f, maximumWallSeparationStep);
        preferredGridObstacleDistance =
            Mathf.Max(0f, preferredGridObstacleDistance);
        gridObstacleProximityCost =
            Mathf.Max(0f, gridObstacleProximityCost);
        gridTurnCost = Mathf.Max(0f, gridTurnCost);
        navigationPathWidth = Mathf.Max(0.005f, navigationPathWidth);
        navigationTargetMarkerSize =
            Mathf.Max(0.05f, navigationTargetMarkerSize);
    }
}
