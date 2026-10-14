using System.Collections.Generic;
using UnityEngine;

/// <summary>Boss roaming controller. Segments follow distance along the head's trail; character data and colliders are separate.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Characters/Sandworm Boss Prototype")]
public sealed class SandwormBossPrototype : MonoBehaviour
{
    [Header("Parts / 部位（朝向本地 +X）")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform[] bodySegments = new Transform[0];
    [SerializeField] private Transform tail;
    [SerializeField, Min(.2f), Tooltip("相邻部位沿头部路径的间距，随根对象缩放。")]
    private float segmentSpacing = .85f;

    [Header("Roaming Area / 游荡区域（相对于根对象）")]
    [SerializeField] private Vector2 wanderCenterOffset = Vector2.zero;
    [SerializeField] private Vector2 wanderSize = new Vector2(24f, 18f);
    [SerializeField, Min(.1f), Tooltip("连续游荡目标之间的最小距离（本地单位），采样失败也不会缩短。")]
    private float minDestinationDistance = 3.5f;
    [SerializeField, Range(4, 128)] private int destinationAttempts = 48;
    [SerializeField, Min(.1f)] private float retryDelay = .5f;

    [Header("Movement / 移动")]
    [SerializeField] private bool movementEnabled = true;
    [SerializeField, Min(.1f)] private float moveSpeed = 3f;
    [SerializeField, Range(15f, 720f)] private float turnSpeed = 150f;
    [SerializeField, Min(.1f), Tooltip("游荡曲线的最小转弯半径（随根对象缩放），避免长身体紧密盘绕。")]
    private float minimumTurnRadius = .95f;
    [SerializeField, Min(.02f)] private float arrivalDistance = .2f;
    [SerializeField, Min(0f), Tooltip("仅查询 Blocks 的导航余量，不产生碰撞或伤害。")]
    private float obstacleClearance = .8f;

    private readonly List<Vector2> trail = new List<Vector2>(256);
    private readonly SandwormObstacleQuery obstacles = new SandwormObstacleQuery();
    private Transform[] followers = new Transform[0];
    private Vector2 heading = Vector2.right;
    private Vector2 destination;
    private bool hasDestination, initialized, dirty = true;
    private bool stopAtDestination, externalPathControl;
    public int CompletedRoamingLegs { get; private set; }
    public bool ArrivedAtDirectedDestination { get; private set; }
    public float WorldSegmentSpacing => Spacing;
    public float WorldMoveSpeed => moveSpeed * WorldScale *
        SpeedBoostArea.GetSpeedMultiplier(head != null ? head : transform);
    public bool MovementEnabled => movementEnabled;
    private float DirectedArrivalTolerance => Mathf.Max(.001f, WorldScale*.002f);

    public bool IsApproachingDirectedPoint(Vector2 point)
    {
        return stopAtDestination && hasDestination && route != null &&
            Vector2.Distance(route.End, point) <= DirectedArrivalTolerance;
    }

    public bool HasArrivedAtDirectedPoint(Vector2 point)
    {
        return stopAtDestination && ArrivedAtDirectedDestination && !hasDestination &&
            Vector2.Distance(destination, point) <= DirectedArrivalTolerance &&
            Vector2.Distance(HeadPosition, point) <= DirectedArrivalTolerance;
    }

    /// <summary>Actual remaining directed-path travel time, not straight-line proximity.</summary>
    public float GetDirectedArrivalSeconds(Vector2 point)
    {
        if (HasArrivedAtDirectedPoint(point)) return 0f;
        if (!IsApproachingDirectedPoint(point) || externalPathControl || !movementEnabled)
            return float.PositiveInfinity;
        float remaining=Vector2.Distance(HeadPosition,route.points[routeIndex]);
        for (int i=routeIndex+1;i<route.points.Length;i++)
            remaining+=Vector2.Distance(route.points[i-1],route.points[i]);
        return remaining/Mathf.Max(.001f,WorldMoveSpeed);
    }
    private float chooseTimer, obstacleRefreshTimer;
    private Matrix4x4 lastRootMatrix;
    private Route route, nextRoute;
    private int routeIndex;
    private float planRetryTimer;
    private IEnumerator<Route> pendingRouteSearch;
    private readonly Vector2[] curvePoints = new Vector2[257];
    private readonly Vector2[] curveTangents = new Vector2[257];
    private const int CoverageResolution = 5;
    private readonly float[] visitHeat = new float[CoverageResolution * CoverageResolution];
    private readonly List<Vector2> recentWaypoints = new List<Vector2>(8);

    private struct Candidate
    {
        public Vector2 point;
        public float score;
    }

    // Two short sampled curves are enough: one being traversed, one planned ahead.
    // Their endpoint tangents agree; movement spends the same distance budget across
    // both curves, including the frame in which a waypoint is reached.
    private sealed class Route
    {
        public Vector2[] points, tangents;
        public Vector2 hint;
        public bool hasHint;
        public Vector2 End => points[points.Length - 1];
        public Vector2 ExitHeading => tangents[tangents.Length - 1];

        public void Transform(Matrix4x4 delta)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = delta.MultiplyPoint3x4(points[i]);
                tangents[i] = ((Vector2)delta.MultiplyVector(tangents[i])).normalized;
            }
            hint = delta.MultiplyPoint3x4(hint);
        }
    }

    public Transform Head => head;
    public Transform Tail => tail;
    public Transform[] BodySegments => bodySegments;
    public bool HasDestination => hasDestination;
    public Vector2 CurrentDestination => destination;
    public Vector2 HeadPosition => head != null ? (Vector2)head.position : (Vector2)transform.position;
    public int TrailPointCount => trail.Count;
    private float WorldScale => Mathf.Max(.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
    private float Spacing => segmentSpacing * WorldScale;
    private float Clearance => obstacleClearance * WorldScale;
    private float SampleSpacing => Mathf.Max(.01f, Spacing * .08f);
    private Vector2 UsableHalfSize => wanderSize * .5f - new Vector2(
        Clearance / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x)),
        Clearance / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y)));

    private void OnEnable() { dirty = true; initialized = false; }
    private void OnDisable() { initialized = false; ClearRoute(); }
    private void OnValidate()
    {
        segmentSpacing = Mathf.Max(.2f, segmentSpacing);
        wanderSize = new Vector2(Mathf.Max(1f, wanderSize.x), Mathf.Max(1f, wanderSize.y));
        moveSpeed = Mathf.Max(.1f, moveSpeed);
        turnSpeed = Mathf.Clamp(turnSpeed, 15f, 720f);
        minimumTurnRadius = Mathf.Max(.1f, minimumTurnRadius);
        arrivalDistance = Mathf.Max(.02f, arrivalDistance);
        obstacleClearance = Mathf.Max(0f, obstacleClearance);
        minDestinationDistance = Mathf.Max(.1f, minDestinationDistance);
        destinationAttempts = Mathf.Clamp(destinationAttempts, 4, 128);
        retryDelay = Mathf.Max(.1f, retryDelay);
        if (bodySegments == null) bodySegments = new Transform[0];
        if (bodySegments.Length > 32) System.Array.Resize(ref bodySegments, 32);
        dirty = true;
    }

    private void Update()
    {
        if (externalPathControl) return;
        if (!Application.isPlaying)
        {
            if (dirty) { dirty = false; ResetPose(); }
            return;
        }
        Advance(Time.deltaTime);
    }

    /// <summary>Future boss phases can suspend roaming without destroying its path or parts.</summary>
    public void SetMovementEnabled(bool enabled) { movementEnabled = enabled; }

    /// <summary>Locks a single endpoint, arrives facing up, and does not choose a new roaming leg.</summary>
    public bool TrySetEruptionDestination(Vector2 point)
    {
        if (!IsDestinationAllowed(point)) return false;
        if (IsApproachingDirectedPoint(point) || HasArrivedAtDirectedPoint(point)) return true;
        if (Vector2.Distance(HeadPosition, point) < .05f * WorldScale)
        {
            ClearRoute(); stopAtDestination = true; ArrivedAtDirectedDestination = true;
            destination = point;
            head.position = new Vector3(point.x, point.y, head.position.z);
            heading = Vector2.up; SetPartFacing(head, heading); RecordHead(point);
            return true;
        }
        if (!TryBuildRoute(HeadPosition, heading, point, Vector2.up, false, out Route planned) &&
            !TryBuildUpwardApproach(point, out planned) &&
            // Final buried alignment must not inherit roaming's large turning circle.
            // Keep a sampled curve, exact endpoint and all Blocks/area checks, but
            // permit a tighter turn when a late dodge leaves the target beside us.
            !TryBuildTightApproach(point, out planned)) return false;
        BeginRoute(planned);
        stopAtDestination = true;
        ArrivedAtDirectedDestination = false;
        return true;
    }

    private bool TryBuildUpwardApproach(Vector2 point, out Route planned)
    {
        // A late lateral dodge can make a single upward-arriving curve too tight.
        // Approach from a safe staging point below the target rather than snapping
        // the head or breaching at a stale endpoint. Both curves retain wall checks.
        planned = null;
        float radius = Mathf.Max(minimumTurnRadius*WorldScale,
            moveSpeed*WorldScale/(turnSpeed*Mathf.Deg2Rad)*1.12f);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector2 staging = point - Vector2.up*radius*(2f+attempt*1.5f);
            if (!IsInsideArea(staging) ||
                !TryBuildRoute(HeadPosition, heading, staging, Vector2.up, false, out Route arrival) ||
                !TryBuildRoute(staging, Vector2.up, point, Vector2.up, false, out Route finish)) continue;
            int count = arrival.points.Length + finish.points.Length - 1;
            var points = new Vector2[count]; var tangents = new Vector2[count];
            System.Array.Copy(arrival.points, points, arrival.points.Length);
            System.Array.Copy(arrival.tangents, tangents, arrival.tangents.Length);
            System.Array.Copy(finish.points, 1, points, arrival.points.Length, finish.points.Length-1);
            System.Array.Copy(finish.tangents, 1, tangents, arrival.tangents.Length, finish.tangents.Length-1);
            planned = new Route { points = points, tangents = tangents };
            return true;
        }
        return false;
    }

    private bool TryBuildTightApproach(Vector2 point, out Route planned)
    {
        planned = null;
        Vector2 direction = (point-HeadPosition).normalized;
        bool collinearReversal = Mathf.Abs(direction.x) < .02f && Mathf.Abs(heading.x) < .02f &&
            (Vector2.Dot(direction, heading) < 0f || direction.y < 0f);
        if (!collinearReversal && TryBuildRoute(HeadPosition, heading, point, Vector2.up, false, out planned,
            enforceTurnRadius: false)) return true;
        // A target directly behind an upward-facing head creates a collinear
        // reversal/cusp even without the turn-radius limit. A lateral waypoint
        // gives both curves real length/tangents, instead of retrying that cusp.
        float offset = Mathf.Max(Spacing, Vector2.Distance(HeadPosition, point)*.6f);
        Vector2 middle = (HeadPosition+point)*.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 waypoint = middle + Vector2.right*(offset*side);
            if (!IsInsideArea(waypoint) ||
                !TryBuildRoute(HeadPosition, heading, waypoint, Vector2.down, false, out Route first,
                    enforceTurnRadius: false) ||
                !TryBuildRoute(waypoint, Vector2.down, point, Vector2.up, false, out Route last,
                    enforceTurnRadius: false)) continue;
            int count = first.points.Length+last.points.Length-1;
            var points = new Vector2[count]; var tangents = new Vector2[count];
            System.Array.Copy(first.points, points, first.points.Length);
            System.Array.Copy(first.tangents, tangents, first.tangents.Length);
            System.Array.Copy(last.points, 1, points, first.points.Length, last.points.Length-1);
            System.Array.Copy(last.tangents, 1, tangents, first.tangents.Length, last.tangents.Length-1);
            planned = new Route { points = points, tangents = tangents };
            return true;
        }
        return false;
    }

    /// <summary>Read-only plan for the buried head after landing, long enough for the tail to enter.</summary>
    public bool TryPlanBuriedDeparture(Vector2 start, float requiredLength, List<Vector2> points)
    {
        points.Clear();
        if (!IsDestinationAllowed(start)) return false;
        points.Add(start);
        Vector2 forward = Vector2.down;
        float length = 0f;
        for (int leg = 0; leg < 4 && length < requiredLength; leg++)
        {
            // Burial is not a roaming goal: requiring a second random roaming
            // route and its large turn radius here rejects valid edge landings.
            // A bounded interior escape can tighten its first buried turn while
            // retaining the downward entry tangent and every Blocks check.
            if (!TryBuildBuriedDepartureLeg(start, forward, out Route planned)) { points.Clear(); return false; }
            for (int i = 1; i < planned.points.Length; i++)
            {
                length += Vector2.Distance(points[points.Count-1], planned.points[i]);
                points.Add(planned.points[i]);
            }
            // Keep the whole final leg: its unused portion is the immediate
            // roaming continuation once the tail has passed below ground.
            if (length >= requiredLength) return true;
            start = planned.End; forward = planned.ExitHeading;
        }
        return length >= requiredLength;
    }

    private bool TryBuildBuriedDepartureLeg(Vector2 start, Vector2 forward, out Route planned)
    {
        planned = null;
        Vector2 half = UsableHalfSize;
        Vector2 localStart = (Vector2)transform.InverseTransformPoint(start) - wanderCenterOffset;
        // Begin toward the interior. If this is a collinear reversal, the two
        // diagonal interior targets supply room to turn instead of a cusp.
        float side = localStart.x > 0f ? -1f : 1f;
        float vertical = localStart.y > 0f ? -1f : 1f;
        float handleLimit = AreaHandleLimit(start, forward);
        for (int pass = 0; pass < 2; pass++)
        for (int candidate = 0; candidate < 9; candidate++)
        {
            Vector2 offset;
            switch (candidate)
            {
                case 0: offset = Vector2.zero; break;
                case 1: offset = new Vector2(side * .55f, vertical * .55f); break;
                case 2: offset = new Vector2(-side * .55f, vertical * .55f); break;
                case 3: offset = new Vector2(side * .65f, 0f); break;
                case 4: offset = new Vector2(-side * .65f, 0f); break;
                case 5: offset = new Vector2(0f, vertical * .65f); break;
                case 6: offset = new Vector2(side * .55f, -vertical * .55f); break;
                case 7: offset = new Vector2(-side * .55f, -vertical * .55f); break;
                default: offset = new Vector2(0f, -vertical * .65f); break;
            }
            Vector2 goal = transform.TransformPoint(wanderCenterOffset + Vector2.Scale(half, offset));
            Vector2 chord = goal - start;
            if (chord.magnitude < Spacing * 2f || !IsDestinationAllowed(goal)) continue;
            if (TryBuildRoute(start, forward, goal, chord.normalized, false, out planned,
                enforceTurnRadius: pass == 0, startHandleLimit: handleLimit)) return true;
        }
        return false;
    }

    private float AreaHandleLimit(Vector2 point, Vector2 direction)
    {
        Vector2 local = (Vector2)transform.InverseTransformPoint(point) - wanderCenterOffset;
        Vector2 ray = transform.InverseTransformVector(direction.normalized);
        Vector2 half = UsableHalfSize;
        float limit = float.PositiveInfinity;
        if (Mathf.Abs(ray.x) > .00001f)
            limit = Mathf.Min(limit, ((ray.x > 0f ? half.x : -half.x) - local.x) / ray.x);
        if (Mathf.Abs(ray.y) > .00001f)
            limit = Mathf.Min(limit, ((ray.y > 0f ? half.y : -half.y) - local.y) / ray.y);
        return Mathf.Max(0f, limit * .85f);
    }

    public void CopyHeadHistory(List<Vector2> output)
    {
        output.Clear();
        if (Initialize()) output.AddRange(trail);
    }

    public bool IsBuriedPathClear(List<Vector2> points)
    {
        if (points == null || points.Count < 2 || !Initialize()) return false;
        for (int i = 1; i < points.Count; i++)
            if (!IsInsideArea(points[i]) || !obstacles.IsClear(points[i-1], points[i], Clearance)) return false;
        return true;
    }

    public void BeginExternalPathControl() { externalPathControl = true; ClearRoute(); }

    /// <summary>Prepare the exact remainder of the landing path while the attack still owns the parts.</summary>
    public bool PrepareRoamingContinuation(List<Vector2> path, float handoffDistance)
    {
        if (!externalPathControl || path == null || path.Count < 2) return false;
        float remaining = Mathf.Max(0f, handoffDistance);
        var points = new List<Vector2>();
        for (int i = 1; i < path.Count; i++)
        {
            float span = Vector2.Distance(path[i-1], path[i]);
            if (points.Count == 0)
            {
                if (remaining >= span) { remaining -= span; continue; }
                points.Add(Vector2.Lerp(path[i-1], path[i], remaining / span));
            }
            points.Add(path[i]);
        }
        if (points.Count < 2) return false;
        var tangents = new Vector2[points.Count];
        for (int i = 0; i < points.Count; i++)
            tangents[i] = (points[Mathf.Min(i+1, points.Count-1)] - points[Mathf.Max(i-1, 0)]).normalized;
        stopAtDestination = false;
        ArrivedAtDirectedDestination = false;
        BeginRoute(new Route { points = points.ToArray(), tangents = tangents });
        PrepareNextRoute(0f);
        return true;
    }

    /// <summary>Spread look-ahead work across airborne frames, without moving any segments.</summary>
    public void AdvancePreparedRoaming(float deltaTime)
    {
        if (!externalPathControl || !hasDestination || deltaTime <= 0f) return;
        obstacleRefreshTimer -= deltaTime;
        if (obstacleRefreshTimer <= 0f) { obstacles.Refresh(gameObject.scene); obstacleRefreshTimer = 1f; }
        PrepareNextRoute(deltaTime);
    }

    /// <summary>Reuse both prepared routes and the exact curved attack trail; no search or pose reset at landing.</summary>
    public bool ResumePreparedRoaming(List<Vector2> headHistory)
    {
        if (!externalPathControl || !hasDestination || route == null || headHistory == null || headHistory.Count < 2)
            return false;
        trail.Clear(); trail.AddRange(headHistory);
        heading = route.tangents[0];
        externalPathControl = false; stopAtDestination = false;
        ArrivedAtDirectedDestination = false;
        chooseTimer = 0f;
        return true;
    }

    public void ResumeRoamingFromCurrentPose()
    {
        externalPathControl = false; stopAtDestination = false;
        ArrivedAtDirectedDestination = false;
        ClearRoute(); initialized = false; chooseTimer = 0f;
    }

    [ContextMenu("Arrange Straight Preview / 整理为直线预览")]
    public void ResetPose()
    {
        if (!BuildFollowers()) return;
        Vector3 direction = head.TransformVector(Vector3.right).normalized;
        for (int i = 0; i < followers.Length; i++)
        {
            followers[i].position = head.position - direction * Spacing * (i + 1);
            SetPartFacing(followers[i], direction);
        }
        initialized = false;
        hasDestination = false;
        ArrivedAtDirectedDestination = false;
    }

    private bool BuildFollowers()
    {
        if (head == null || tail == null || head == tail || head == transform || tail == transform ||
            !head.IsChildOf(transform) || !tail.IsChildOf(transform)) return false;
        var parts = new List<Transform>();
        if (bodySegments != null)
            foreach (var part in bodySegments)
            {
                if (part == null || part == head || part == tail || part == transform ||
                    !part.IsChildOf(transform) || parts.Contains(part)) return false;
                parts.Add(part);
            }
        parts.Add(tail);
        followers = parts.ToArray();
        return true;
    }

    private bool Initialize()
    {
        if (initialized && !dirty) return true;
        if (!BuildFollowers()) return false;
        dirty = false;
        trail.Clear();
        trail.Add(HeadPosition);
        Vector2 previous = HeadPosition;
        float historyLength = 0f;
        foreach (var part in followers)
        {
            Vector2 end = part.position;
            float distance = Vector2.Distance(previous, end);
            historyLength += distance;
            int samples = Mathf.Clamp(Mathf.CeilToInt(distance / SampleSpacing), 1, 512);
            for (int i = 1; i <= samples; i++) trail.Add(Vector2.Lerp(previous, end, i / (float)samples));
            previous = end;
        }
        heading = ((Vector2)head.TransformVector(Vector3.right)).normalized;
        if (heading.sqrMagnitude < .5f) heading = Vector2.right;
        // Additional history behind the tail gives the last part a stable tangent.
        Vector2 tailDirection = trail.Count > 1 ? (trail[trail.Count - 2] - previous).normalized : heading;
        if (tailDirection.sqrMagnitude < .5f) tailDirection = heading;
        float extraLength = Mathf.Max(Spacing, Spacing * (followers.Length + 2) - historyLength);
        int extraSamples = Mathf.CeilToInt(extraLength / SampleSpacing);
        for (int i = 1; i <= extraSamples; i++) trail.Add(previous - tailDirection * SampleSpacing * i);
        lastRootMatrix = transform.localToWorldMatrix;
        obstacles.Refresh(gameObject.scene);
        obstacleRefreshTimer = 1f;
        hasDestination = false;
        stopAtDestination = false;
        ArrivedAtDirectedDestination = false;
        chooseTimer = 0f;
        route = nextRoute = null;
        CancelRouteSearch();
        planRetryTimer = 0f;
        System.Array.Clear(visitHeat, 0, visitHeat.Length);
        recentWaypoints.Clear();
        recentWaypoints.Add(HeadPosition);
        initialized = true;
        return true;
    }

    private void ApplyRootTransformChange()
    {
        Matrix4x4 current = transform.localToWorldMatrix;
        if (current == lastRootMatrix) return;
        Matrix4x4 delta = current * lastRootMatrix.inverse;
        for (int i = 0; i < trail.Count; i++) trail[i] = delta.MultiplyPoint3x4(trail[i]);
        destination = delta.MultiplyPoint3x4(destination);
        heading = ((Vector2)delta.MultiplyVector(heading)).normalized;
        route?.Transform(delta);
        nextRoute?.Transform(delta);
        for (int i = 0; i < recentWaypoints.Count; i++)
            recentWaypoints[i] = delta.MultiplyPoint3x4(recentWaypoints[i]);
        // An in-progress search holds world-space candidates from the old root pose.
        CancelRouteSearch();
        planRetryTimer = 0f;
        lastRootMatrix = current;
        // A root teleport may move the old destination behind another wall.
        if (hasDestination && !IsDestinationAllowed(destination)) ClearRoute();
    }

    public bool IsDestinationAllowed(Vector2 point)
    {
        return Initialize() && IsInsideArea(point) && obstacles.IsPointClear(point, Clearance);
    }

    private bool IsInsideArea(Vector2 point)
    {
        Vector2 local = transform.InverseTransformPoint(new Vector3(point.x, point.y, transform.position.z));
        Vector2 offset = local - wanderCenterOffset;
        // Keep the head, not just its center, inside the configured level region.
        Vector2 half = UsableHalfSize;
        return half.x > 0f && half.y > 0f && Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
    }

    public bool TrySetDestination(Vector2 point)
    {
        if (!IsDestinationAllowed(point) || !obstacles.IsClear(HeadPosition, point, Clearance)) return false;
        // Explicit phase/script goals need not obey roaming's minimum trip length.
        Vector2 direction = (point - HeadPosition).normalized;
        Route planned = null;
        float turnSign = Vector2.SignedAngle(heading, direction) >= 0f ? 1f : -1f;
        for (int attempt = 0; attempt < 3 && planned == null; attempt++)
        {
            // A lateral goal may require completing a wider turn on arrival,
            // rather than forcing its final tangent to point along the chord.
            Vector2 exit = Quaternion.Euler(0, 0, turnSign * 30f * attempt) * direction;
            TryBuildRoute(HeadPosition, heading, point, exit, false, out planned);
        }
        if (planned == null) return false;
        stopAtDestination = false; ArrivedAtDirectedDestination = false;
        BeginRoute(planned);
        return true;
    }

    public bool TryPickDestination()
    {
        if (!Initialize()) return false;
        if (FindRoute(HeadPosition, heading, Vector2.zero, false, out Route planned))
        {
            stopAtDestination = false; ArrivedAtDirectedDestination = false;
            BeginRoute(planned);
            return true;
        }
        ClearRoute();
        chooseTimer = retryDelay;
        return false;
    }

    private void ClearRoute()
    {
        hasDestination = false;
        ArrivedAtDirectedDestination = false;
        route = nextRoute = null;
        CancelRouteSearch();
    }

    private void BeginRoute(Route planned)
    {
        route = planned;
        ArrivedAtDirectedDestination = false;
        routeIndex = 1;
        destination = route.End;
        hasDestination = true;
        nextRoute = null;
        CancelRouteSearch();
        chooseTimer = planRetryTimer = 0f;
    }

    private void GatherCandidates(Vector2 start, Vector2 forward, List<Candidate> output, bool avoidBody)
    {
        output.Clear();
        Vector2 half = UsableHalfSize;
        if (half.x <= 0f || half.y <= 0f) return;
        float minimum = Mathf.Max(minDestinationDistance, arrivalDistance * 2f) * WorldScale;
        float diagonal = Vector2.Scale(wanderSize, new Vector2(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y))).magnitude;
        // Do not maximize distance: that repeatedly selects opposing arena edges
        // and settles into an oval. Mix medium/long legs and unexplored regions.
        float preferredDistance = Random.Range(minimum * 1.2f, Mathf.Max(minimum * 1.2f, diagonal * .65f));
        for (int i = 0; i < destinationAttempts; i++)
        {
            Vector2 local = wanderCenterOffset + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            Vector2 point = transform.TransformPoint(local);
            Vector2 difference = point - start;
            float distance = difference.magnitude;
            if (distance < minimum) continue;
            float alignment = Vector2.Dot(forward, difference / distance);
            // Avoid immediate hairpins; large turns are spread across long curves.
            if (alignment < (avoidBody ? -.75f : -.995f)) continue;
            float bodyDistance = minimum;
            if (avoidBody)
                foreach (Transform part in followers)
                    bodyDistance = Mathf.Min(bodyDistance, Vector2.Distance(point, part.position));
            float margin = Mathf.Min(half.x - Mathf.Abs(local.x - wanderCenterOffset.x),
                half.y - Mathf.Abs(local.y - wanderCenterOffset.y)) * WorldScale;
            float recentDistance = minimum;
            foreach (Vector2 recent in recentWaypoints)
                recentDistance = Mathf.Min(recentDistance, Vector2.Distance(point, recent));
            output.Add(new Candidate { point = point,
                score = Random.value * 2.5f - Mathf.Abs(distance - preferredDistance) / preferredDistance * .7f +
                    alignment * .15f + bodyDistance / minimum * .25f + recentDistance / minimum * .65f +
                    Mathf.Clamp01(margin / Mathf.Max(Spacing, .01f)) * .3f -
                    Mathf.Min(2f, visitHeat[CoverageCell(point)] * .22f) });
        }
        output.Sort((a, b) => b.score.CompareTo(a.score));
    }

    private int CoverageCell(Vector2 point)
    {
        Vector2 local = (Vector2)transform.InverseTransformPoint(point) - wanderCenterOffset;
        int x = Mathf.Clamp(Mathf.FloorToInt((local.x / wanderSize.x + .5f) * CoverageResolution), 0, CoverageResolution - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((local.y / wanderSize.y + .5f) * CoverageResolution), 0, CoverageResolution - 1);
        return y * CoverageResolution + x;
    }

    private bool FindRoute(Vector2 start, Vector2 forward, Vector2 preferred, bool hasPreferred, out Route planned)
    {
        planned = null;
        foreach (Route result in SearchRoutes(start, forward, preferred, hasPreferred))
            if (result != null) { planned = result; return true; }
        return false;
    }

    private IEnumerable<Route> SearchRoutes(Vector2 start, Vector2 forward, Vector2 preferred, bool hasPreferred)
    {
        var candidates = new List<Candidate>(destinationAttempts + 1);
        var hints = new List<Candidate>(destinationAttempts);
        GatherCandidates(start, forward, candidates, true);
        if (hasPreferred && Vector2.Distance(start, preferred) >= minDestinationDistance * WorldScale)
            candidates.Insert(0, new Candidate { point = preferred, score = float.MaxValue });
        // Bounded planning; never silently relax the trip length or wall clearance.
        for (int c = 0; c < candidates.Count && c < 16; c++)
        {
            Vector2 goal = candidates[c].point;
            if (!IsDestinationAllowed(goal) || !obstacles.IsClear(start, goal, Clearance)) continue;
            Vector2 incoming = (goal - start).normalized;
            GatherCandidates(goal, incoming, hints, false);
            for (int h = 0; h < hints.Count && h < 12; h++)
            {
                Vector2 hint = hints[h].point;
                Vector2 outgoing = (hint - goal).normalized;
                Vector2 exitHeading = (incoming + outgoing).normalized;
                if (IsDestinationAllowed(hint) && obstacles.IsClear(goal, hint, Clearance) &&
                    TryBuildRoute(start, forward, goal, exitHeading, true, out Route planned) &&
                    // Confirm an escape before committing to the current endpoint.
                    TryBuildRoute(goal, exitHeading, hint, outgoing, true, out _, false))
                {
                    planned.hint = hint;
                    planned.hasHint = true;
                    yield return planned;
                    yield break;
                }
                // Next-route planning is spread across frames while the current
                // route keeps moving. No threads or asynchronous physics queries.
                yield return null;
            }
            yield return null;
        }
    }

    private void CancelRouteSearch()
    {
        pendingRouteSearch?.Dispose();
        pendingRouteSearch = null;
    }

    private void PrepareNextRoute(float deltaTime)
    {
        if (stopAtDestination || !hasDestination || nextRoute != null) return;
        planRetryTimer -= deltaTime;
        if (pendingRouteSearch == null)
        {
            if (planRetryTimer > 0f) return;
            pendingRouteSearch = SearchRoutes(route.End, route.ExitHeading, route.hint, route.hasHint).GetEnumerator();
        }
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (!pendingRouteSearch.MoveNext())
            {
                CancelRouteSearch();
                planRetryTimer = retryDelay;
                return;
            }
            if (pendingRouteSearch.Current == null) continue;
            nextRoute = pendingRouteSearch.Current;
            CancelRouteSearch();
            return;
        }
    }

    private bool TryBuildRoute(Vector2 start, Vector2 forward, Vector2 goal, Vector2 exitHeading,
        bool roaming, out Route planned, bool storeRoute = true, bool enforceTurnRadius = true,
        float startHandleLimit = float.PositiveInfinity)
    {
        planned = null;
        float distance = Vector2.Distance(start, goal);
        if (distance < .001f || forward.sqrMagnitude < .5f || exitHeading.sqrMagnitude < .5f) return false;
        // A little margin keeps sampled-polyline interpolation below the angular limit.
        float radius = moveSpeed * WorldScale / (turnSpeed * Mathf.Deg2Rad) * 1.12f;
        if (roaming) radius = Mathf.Max(radius, minimumTurnRadius * WorldScale);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float factor = attempt == 0 ? .45f : attempt == 1 ? .65f : attempt == 2 ? .3f : .9f;
            Vector2 p1 = start + forward * Mathf.Min(distance * factor, startHandleLimit);
            Vector2 p2 = goal - exitHeading * distance * factor;
            float polygonLength = Vector2.Distance(start, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, goal);
            int count = Mathf.Clamp(Mathf.CeilToInt(polygonLength / SampleSpacing), 24, 256);
            bool valid = true;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count, u = 1f - t;
                Vector2 point = u * u * u * start + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * goal;
                Vector2 velocity = 3f * u * u * (p1 - start) + 6f * u * t * (p2 - p1) + 3f * t * t * (goal - p2);
                Vector2 acceleration = 6f * u * (p2 - 2f * p1 + start) + 6f * t * (goal - 2f * p2 + p1);
                float cross = Mathf.Abs(velocity.x * acceleration.y - velocity.y * acceleration.x);
                if (velocity.sqrMagnitude < .00001f ||
                    // Directed collinear curves have zero curvature even when their
                    // velocity flips at a cusp between two samples. Reject that
                    // fold so an upper approach uses a real lateral turn.
                    (!roaming && i > 0 && Vector2.Dot(curveTangents[i - 1], velocity.normalized) < 0f) ||
                    (enforceTurnRadius && cross * radius > Mathf.Pow(velocity.sqrMagnitude, 1.5f)) ||
                    !IsInsideArea(point))
                { valid = false; break; }
                curvePoints[i] = point;
                curveTangents[i] = velocity.normalized;
            }
            if (!valid) continue;
            // Reject invalid geometry before doing physics queries, and reuse the
            // sample buffers even when a candidate is rejected.
            for (int i = 1; i <= count; i++)
                if (!obstacles.IsClear(curvePoints[i - 1], curvePoints[i], Clearance))
                { valid = false; break; }
            if (!valid) continue;
            if (!storeRoute) return true;
            var points = new Vector2[count + 1];
            var tangents = new Vector2[count + 1];
            System.Array.Copy(curvePoints, points, count + 1);
            System.Array.Copy(curveTangents, tangents, count + 1);
            planned = new Route { points = points, tangents = tangents };
            return true;
        }
        return false;
    }

    /// <summary>Delta-driven update, also usable by a future boss director and isolated tests.</summary>
    public void Advance(float deltaTime)
    {
        if (externalPathControl || !movementEnabled || deltaTime <= 0f || !Initialize()) return;
        ApplyRootTransformChange();
        float decay = Mathf.Exp(-Mathf.Min(deltaTime, .5f) / 90f);
        for (int i = 0; i < visitHeat.Length; i++) visitHeat[i] *= decay;
        obstacleRefreshTimer -= deltaTime;
        if (obstacleRefreshTimer <= 0f)
        {
            obstacles.Refresh(gameObject.scene);
            obstacleRefreshTimer = 1f;
        }
        PrepareNextRoute(deltaTime);
        // Small steps retain smooth history through frame hitches; cap catch-up work.
        float remaining = Mathf.Min(deltaTime, .5f);
        while (remaining > .00001f)
        {
            float step = Mathf.Min(.025f, remaining);
            StepHead(step);
            remaining -= step;
        }
        FollowTrail();
    }

    private void StepHead(float deltaTime)
    {
        if (!hasDestination)
        {
            if (stopAtDestination) return;
            chooseTimer -= deltaTime;
            if (chooseTimer <= 0f) TryPickDestination();
            if (!hasDestination) return;
        }
        float travel = WorldMoveSpeed * deltaTime;
        while (travel > .000001f && hasDestination)
        {
            Vector2 position = HeadPosition;
            Vector2 sample = route.points[routeIndex];
            float distance = Vector2.Distance(position, sample);
            float step = Mathf.Min(distance, travel);
            Vector2 next = distance > .000001f ? Vector2.MoveTowards(position, sample, step) : sample;
            // Dynamic/moved walls are checked again, even for an already planned curve.
            if (!IsInsideArea(next) || !obstacles.IsClear(position, next, Clearance))
            { ClearRoute(); chooseTimer = retryDelay; return; }
            float span = Vector2.Distance(route.points[routeIndex - 1], sample);
            float t = span > .000001f ? 1f - Vector2.Distance(next, sample) / span : 1f;
            heading = Vector2.Lerp(route.tangents[routeIndex - 1], route.tangents[routeIndex], t).normalized;
            SetPartFacing(head, heading);
            head.position = new Vector3(next.x, next.y, head.position.z);
            visitHeat[CoverageCell(next)] += step / Spacing;
            RecordHead(next);
            travel -= step;
            if (step < distance - .000001f) break;
            routeIndex++;
            if (routeIndex < route.points.Length) continue;
            if (stopAtDestination)
            {
                ClearRoute(); ArrivedAtDirectedDestination = true;
                break;
            }
            CompletedRoamingLegs++;
            if (recentWaypoints.Count == 8) recentWaypoints.RemoveAt(0);
            recentWaypoints.Add(route.End);
            // Usually already prepared seconds before arrival. Finish any pending
            // search only after an exceptional large delta/configuration change.
            if (nextRoute == null && pendingRouteSearch != null)
                while (pendingRouteSearch.MoveNext())
                    if (pendingRouteSearch.Current != null) { nextRoute = pendingRouteSearch.Current; break; }
            CancelRouteSearch();
            if (nextRoute == null)
                FindRoute(route.End, route.ExitHeading, route.hint, route.hasHint, out nextRoute);
            if (nextRoute != null) BeginRoute(nextRoute);
            else { ClearRoute(); chooseTimer = retryDelay; }
        }
    }

    private void RecordHead(Vector2 point)
    {
        if (trail.Count < 2) trail.Insert(0, point);
        else if (Vector2.Distance(point, trail[1]) >= SampleSpacing) trail.Insert(0, point);
        else trail[0] = point;
        float length = 0f;
        float keepLength = Spacing * (followers.Length + 2);
        for (int i = 1; i < trail.Count; i++)
        {
            length += Vector2.Distance(trail[i - 1], trail[i]);
            if (length > keepLength && i + 1 < trail.Count)
            {
                trail.RemoveRange(i + 1, trail.Count - i - 1);
                break;
            }
        }
        if (trail.Count > 2048) trail.RemoveRange(2048, trail.Count - 2048);
    }

    private Vector2 SampleTrail(float distance)
    {
        for (int i = 1; i < trail.Count; i++)
        {
            float length = Vector2.Distance(trail[i - 1], trail[i]);
            if (length >= distance && length > .00001f) return Vector2.Lerp(trail[i - 1], trail[i], distance / length);
            distance -= length;
        }
        Vector2 last = trail[trail.Count - 1];
        Vector2 backward = trail.Count > 1 ? (last - trail[trail.Count - 2]).normalized : -heading;
        if (backward.sqrMagnitude < .5f) backward = -heading;
        return last + backward * Mathf.Max(0f, distance);
    }

    private void FollowTrail()
    {
        for (int i = 0; i < followers.Length; i++)
        {
            float distance = Spacing * (i + 1);
            Vector2 point = SampleTrail(distance);
            Vector2 tangent = SampleTrail(distance - Spacing * .2f) - SampleTrail(distance + Spacing * .2f);
            Transform part = followers[i];
            part.position = new Vector3(point.x, point.y, head.position.z);
            if (tangent.sqrMagnitude > .000001f)
                SetPartFacing(part, tangent);
        }
    }

    private static void SetPartFacing(Transform part, Vector2 worldDirection)
    {
        Vector3 local = part.parent != null ? part.parent.InverseTransformVector(worldDirection) : (Vector3)worldDirection;
        if (part.localScale.x < 0f) local = -local;
        part.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(.2f, .9f, 1f, .65f);
        Gizmos.DrawWireCube(wanderCenterOffset, wanderSize);
        Gizmos.matrix = previous;
        if (hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(destination, .2f * WorldScale);
            if (route != null)
                for (int i = Mathf.Max(1, routeIndex); i < route.points.Length; i++)
                    Gizmos.DrawLine(i == routeIndex ? HeadPosition : route.points[i - 1], route.points[i]);
            if (nextRoute != null)
            {
                Gizmos.color = new Color(1f, .7f, .2f, .45f);
                for (int i = 1; i < nextRoute.points.Length; i++)
                    Gizmos.DrawLine(nextRoute.points[i - 1], nextRoute.points[i]);
            }
        }
    }
}
