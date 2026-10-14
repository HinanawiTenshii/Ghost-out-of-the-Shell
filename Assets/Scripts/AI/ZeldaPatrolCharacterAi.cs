using UnityEngine;

/// <summary>
/// Idle patrol along a configurable loop, relative to the scene-load position.
/// Existing serialized four-point routes remain authoritative until the author
/// explicitly converts an instance/prefab to the editable waypoint list.
/// </summary>
public class ZeldaPatrolCharacterAi : ZeldaCharacterAiBase
{
    // Specialized AI can reuse the route implementation without changing its original idle variant.
    public virtual bool HasIdlePatrol => true;
    [Header("Idle Patrol")]
    [Tooltip("Movement speed multiplier used while following the patrol route. Other AI states continue to use full movement speed.")]
    [SerializeField, Min(0.01f)] private float patrolSpeedMultiplier = 0.75f;
    // Missing fields in existing scenes/prefabs retain the original four-point loop.
    // Reducing the count does not erase the inactive points or their wait times.
    [SerializeField, Range(2, 4)] private int legacyPatrolPointCount = 4;
    [Tooltip("Patrol point 2, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint2Offset = new Vector2(2f, 0f);
    [Tooltip("Patrol point 3, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint3Offset = new Vector2(2f, 2f);
    [Tooltip("Patrol point 4, relative to the character's scene-load position.")]
    [SerializeField] private Vector2 patrolPoint4Offset = new Vector2(0f, 2f);
    [SerializeField, Min(0.01f)] private float patrolPointTolerance = 0.12f;
    [Header("Patrol Point Wait Times")]
    [Tooltip("Seconds to wait at the scene-load position (patrol point 1).")]
    [SerializeField, Min(0f)] private float waitAtPoint1Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 2.")]
    [SerializeField, Min(0f)] private float waitAtPoint2Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 3.")]
    [SerializeField, Min(0f)] private float waitAtPoint3Duration = 1f;
    [Tooltip("Seconds to wait at patrol point 4.")]
    [SerializeField, Min(0f)] private float waitAtPoint4Duration = 1f;

    [System.Serializable]
    public struct PatrolWaypoint
    {
        public Vector2 offset;
        [Min(0f)] public float waitDuration;
    }

    // Do not auto-migrate in Awake/OnValidate: legacy prefab-instance overrides
    // must keep working, including overrides of individual point wait times.
    [SerializeField] private bool useCustomPatrolRoute;
    [SerializeField] private PatrolWaypoint[] customPatrolPoints = new PatrolWaypoint[0];

    private Vector2[] patrolPoints = new Vector2[4];
    private Bounds streamingRouteBounds;
    // Read the absolute route even while distance-streamed inactive. Never use
    // the moving transform as the origin, and never copy/re-plan it per check.
    internal int StreamingRoutePointCount => HasIdlePatrol ? patrolPoints.Length : 0;
    internal Vector2 GetStreamingRoutePoint(int index) => patrolPoints[index];
    internal Bounds StreamingRouteBounds => streamingRouteBounds;
    private float[] inheritedWaitDurations;
    private int currentPatrolPointIndex;
    private float waitTimer;

    // Assigned once by an optional scene grouping component, not a global per-frame lookup.
    internal PatrolStreamingGroup StreamingGroup { get; set; }
    private bool StreamingPatrolPaused => HasIdlePatrol && StreamingGroup != null && StreamingGroup.PatrolPaused &&
        CurrentState == ZeldaAiState.Idle && !RequiresVisionStreamingRetention;

    internal void FreezeStreamingPatrol()
    {
        if (!StreamingPatrolPaused || !isActiveAndEnabled) return;
        AiMoveDirection = Vector2.zero;
        var body = GetComponent<Rigidbody2D>();
        if (body == null) return;
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        // Commit the physics endpoint once before staggered SetActive calls;
        // otherwise interpolation can freeze different members between steps.
        Vector2 position = body.position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    // A squad owns one absolute route. Successors inherit progress and timing,
    // not offsets relative to their own spawn positions.
    internal sealed class PatrolRouteSnapshot
    {
        internal Vector2[] points = new Vector2[0];
        internal int index;
        internal float remainingWait, speed, tolerance;
        internal float[] waits = new float[0];

        internal void Resize(int count)
        {
            if (points.Length == count) return;
            points = new Vector2[count];
            waits = new float[count];
        }
    }

    internal void CapturePatrolRoute(PatrolRouteSnapshot snapshot)
    {
        snapshot.Resize(patrolPoints.Length);
        for (int i = 0; i < patrolPoints.Length; i++)
        { snapshot.points[i] = patrolPoints[i]; snapshot.waits[i] = GetWaitDuration(i); }
        snapshot.index = currentPatrolPointIndex;
        snapshot.remainingWait = waitTimer;
        snapshot.speed = patrolSpeedMultiplier;
        snapshot.tolerance = patrolPointTolerance;
    }

    internal void ApplyPatrolRoute(PatrolRouteSnapshot snapshot)
    {
        int count = snapshot.points.Length;
        if (patrolPoints.Length != count) patrolPoints = new Vector2[count];
        if (inheritedWaitDurations == null || inheritedWaitDurations.Length != count)
            inheritedWaitDurations = new float[count];
        System.Array.Copy(snapshot.points, patrolPoints, count);
        RefreshStreamingRouteBounds();
        System.Array.Copy(snapshot.waits, inheritedWaitDurations, count);
        currentPatrolPointIndex = Mathf.Clamp(snapshot.index, 0, Mathf.Max(0, count - 1));
        waitTimer = snapshot.remainingWait;
        patrolSpeedMultiplier = snapshot.speed;
        patrolPointTolerance = snapshot.tolerance;
    }

    protected override float AiMovementSpeedMultiplier =>
        HasIdlePatrol && CurrentState == ZeldaAiState.Idle ? patrolSpeedMultiplier : 1f;

    protected override void Awake()
    {
        base.Awake();
        RebuildPatrolPoints();
        currentPatrolPointIndex = FirstDestinationIndex();
    }

    protected override void TickIdle(float deltaTime)
    {
        if (!HasIdlePatrol)
        {
            base.TickIdle(deltaTime);
            return;
        }
        if (StreamingPatrolPaused)
        {
            AiMoveDirection = Vector2.zero;
            return;
        }
        if (patrolPoints.Length == 0)
        {
            AiMoveDirection = Vector2.zero;
            return;
        }
        if (waitTimer > 0f)
        {
            waitTimer = Mathf.Max(0f, waitTimer - deltaTime);
            AiMoveDirection = Vector2.zero;
            return;
        }

        // Use the same reachable destination for arrival and navigation. A leashed
        // skeleton must not wait forever for an authored point outside its crystal area.
        Vector2 destination = ConstrainAiDestination(patrolPoints[currentPatrolPointIndex]);
        Vector2 toPatrolPoint = destination - AiPosition;
        if (toPatrolPoint.sqrMagnitude <= patrolPointTolerance * patrolPointTolerance)
        {
            // Stop at the current destination before selecting and travelling
            // toward the next point in the loop.
            AiMoveDirection = Vector2.zero;
            waitTimer = GetWaitDuration(currentPatrolPointIndex);
            currentPatrolPointIndex = (currentPatrolPointIndex + 1) % patrolPoints.Length;
            return;
        }

        AiMoveDirection = PlanAiPathTo(destination);
    }

    protected override void FixedUpdate()
    {
        // The manager freezes the group in LateUpdate before budgeted suspension.
        // Also gate physics so a member cannot take one last stale movement step.
        if (StreamingPatrolPaused)
        {
            AiMoveDirection = Vector2.zero;
            return;
        }
        base.FixedUpdate();
    }

    protected override void OnStateEntered(ZeldaAiState previousState, ZeldaAiState newState)
    {
        base.OnStateEntered(previousState, newState);
        if (HasIdlePatrol && newState == ZeldaAiState.Idle && previousState == ZeldaAiState.Recovery)
        {
            // Recovery returns to the spawn position. Legacy routes resume at
            // point 2 exactly as before; custom routes may start elsewhere.
            RebuildPatrolPoints();
            currentPatrolPointIndex = FirstDestinationIndex();
            waitTimer = StartsAtOrigin() ? GetWaitDuration(0) : 0f;
        }
    }

    private float GetWaitDuration(int pointIndex)
    {
        if (inheritedWaitDurations != null) return inheritedWaitDurations[pointIndex];
        return GetConfiguredWaitDuration(pointIndex);
    }

    private float GetConfiguredWaitDuration(int pointIndex)
    {
        if (useCustomPatrolRoute) return Mathf.Max(0f, customPatrolPoints[pointIndex].waitDuration);
        switch (pointIndex)
        {
            case 0: return waitAtPoint1Duration;
            case 1: return waitAtPoint2Duration;
            case 2: return waitAtPoint3Duration;
            default: return waitAtPoint4Duration;
        }
    }

    private void RebuildPatrolPoints()
    {
        inheritedWaitDurations = null;
        int count = ConfiguredPointCount;
        if (patrolPoints.Length != count) patrolPoints = new Vector2[count];
        for (int i = 0; i < count; i++)
            patrolPoints[i] = InitialScenePosition + GetConfiguredOffset(i);
        RefreshStreamingRouteBounds();
    }

    private void RefreshStreamingRouteBounds()
    {
        streamingRouteBounds = new Bounds(patrolPoints.Length > 0 ? (Vector3)patrolPoints[0] : Vector3.zero, Vector3.zero);
        for (int i = 1; i < patrolPoints.Length; i++) streamingRouteBounds.Encapsulate((Vector3)patrolPoints[i]);
    }

    private int ConfiguredPointCount => useCustomPatrolRoute ? (customPatrolPoints?.Length ?? 0) : Mathf.Clamp(legacyPatrolPointCount, 2, 4);

    private Vector2 GetConfiguredOffset(int index)
    {
        if (useCustomPatrolRoute) return customPatrolPoints[index].offset;
        switch (index)
        {
            case 0: return Vector2.zero;
            case 1: return patrolPoint2Offset;
            case 2: return patrolPoint3Offset;
            default: return patrolPoint4Offset;
        }
    }

    private bool StartsAtOrigin() => patrolPoints.Length > 0 &&
        (patrolPoints[0] - InitialScenePosition).sqrMagnitude < .000001f;

    private int FirstDestinationIndex() => patrolPoints.Length > 1 && StartsAtOrigin() ? 1 : 0;

    protected override void OnValidate()
    {
        base.OnValidate();
        patrolSpeedMultiplier = Mathf.Max(0.01f, patrolSpeedMultiplier);
        legacyPatrolPointCount = Mathf.Clamp(legacyPatrolPointCount, 2, 4);
        patrolPointTolerance = Mathf.Max(0.01f, patrolPointTolerance);
        waitAtPoint1Duration = Mathf.Max(0f, waitAtPoint1Duration);
        waitAtPoint2Duration = Mathf.Max(0f, waitAtPoint2Duration);
        waitAtPoint3Duration = Mathf.Max(0f, waitAtPoint3Duration);
        waitAtPoint4Duration = Mathf.Max(0f, waitAtPoint4Duration);
        if (customPatrolPoints != null)
            for (int i = 0; i < customPatrolPoints.Length; i++)
                customPatrolPoints[i].waitDuration = Mathf.Max(0f, customPatrolPoints[i].waitDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (!HasIdlePatrol) return;
        int count = Application.isPlaying ? patrolPoints.Length : ConfiguredPointCount;
        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        for (int i = 0; i < count; i++)
        {
            Vector3 point = PreviewPoint(i);
            Gizmos.DrawWireSphere(point, 0.12f);
            if (count > 1) Gizmos.DrawLine(point, PreviewPoint((i + 1) % count));
        }

        Gizmos.color = previousColor;
    }

    private Vector3 PreviewPoint(int index)
    {
        Vector2 point = Application.isPlaying ? patrolPoints[index] : (Vector2)transform.position + GetConfiguredOffset(index);
        return new Vector3(point.x, point.y, transform.position.z);
    }
}
