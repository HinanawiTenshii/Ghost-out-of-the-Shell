using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable form of the Zelda AI runtime-grid rules for non-character agents:
/// eight-direction A*, no diagonal corner cutting, body-sized probes, path
/// simplification and local character avoidance.
/// </summary>
public sealed class ZeldaReusableGridNavigator : MonoBehaviour
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    [SerializeField, Min(0.15f)] private float gridCellSize = 0.35f;
    [SerializeField, Range(2, 12)] private int gridPadding = 6;
    [SerializeField, Range(21, 101)] private int maximumGridDimension = 71;
    [SerializeField, Min(0f)] private float wallClearance = 0.12f;
    [SerializeField, Min(0.05f)] private float waypointTolerance = 0.12f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.45f;
    [SerializeField, Min(0.1f)] private float characterAvoidanceDistance = 1.45f;
    [SerializeField, Range(0f, 2.5f)] private float characterAvoidanceStrength = 1.65f;
    [SerializeField, Min(0f)] private float characterClearancePadding = 0.24f;
    [SerializeField] private LayerMask solidLayers = ~0;

    private readonly List<Vector2> path = new List<Vector2>(48);
    private readonly List<Vector2> simplifiedPath = new List<Vector2>(48);
    private readonly Collider2D[] overlaps = new Collider2D[128];
    private readonly RaycastHit2D[] casts = new RaycastHit2D[128];
    private readonly RaycastHit2D[] pathIntersectionHits =
        new RaycastHit2D[32];
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Component ignoredTarget;
    private Vector2 plannedTarget;
    private float plannedStoppingDistance;
    private float nextRepathTime;
    private int pathIndex;
    private Vector2 currentDirection;
    private bool hasPathPlan;
    private bool hasRequestedTarget;
    private Vector2 requestedTarget;
    private bool showNavigationDebug;
    private Color navigationPathColor;
    private Color navigationTargetColor;
    private float navigationPathWidth;
    private float navigationTargetMarkerSize;
    private int navigationDebugSortingOrder;
    private LineRenderer navigationDebugLine;
    private SpriteRenderer navigationDebugTarget;
    private Material navigationDebugMaterial;
    private static Sprite navigationTargetSprite;

    private float[] costs;
    private int[] parents;
    private byte[] states;
    private int[] heap;
    private int[] heapPositions;
    private int heapCount;
    private int width;
    private int height;
    private Vector2Int origin;

    public void Configure(Rigidbody2D configuredBody, Collider2D configuredCollider)
    {
        body = configuredBody;
        bodyCollider = configuredCollider;
        InvalidatePath();
    }

    public void SetIgnoredTarget(Component target)
    {
        if (ignoredTarget == target) return;
        ignoredTarget = target;
        InvalidatePath();
    }

    public void InvalidatePath()
    {
        path.Clear();
        pathIndex = 0;
        nextRepathTime = 0f;
        currentDirection = Vector2.zero;
        hasPathPlan = false;
        hasRequestedTarget = false;
    }

    public void ConfigureNavigationDebug(
        bool visible,
        Color pathColor,
        Color targetColor,
        float pathWidth,
        float targetMarkerSize,
        int sortingOrder)
    {
        showNavigationDebug = visible;
        navigationPathColor = pathColor;
        navigationTargetColor = targetColor;
        navigationPathWidth = Mathf.Max(0.005f, pathWidth);
        navigationTargetMarkerSize = Mathf.Max(0.05f, targetMarkerSize);
        navigationDebugSortingOrder = sortingOrder;
        if (showNavigationDebug)
        {
            EnsureNavigationDebugVisual();
        }
        else
        {
            SetNavigationDebugVisible(false);
        }
    }

    public Vector2 GetDirection(Vector2 target, float stoppingDistance)
    {
        if (body == null || bodyCollider == null)
        {
            currentDirection = Vector2.zero;
            return Vector2.zero;
        }
        requestedTarget = target;
        hasRequestedTarget = true;
        Vector2 position = body.position;
        if (Vector2.Distance(position, target) <= stoppingDistance)
        {
            currentDirection = Vector2.zero;
            return Vector2.zero;
        }

        bool targetChanged = (target - plannedTarget).sqrMagnitude > gridCellSize * gridCellSize * 0.25f ||
            Mathf.Abs(stoppingDistance - plannedStoppingDistance) > 0.01f;
        if (targetChanged || !hasPathPlan || Time.time >= nextRepathTime)
        {
            BuildPath(position, target, stoppingDistance);
            hasPathPlan = true;
            plannedTarget = target;
            plannedStoppingDistance = stoppingDistance;
            nextRepathTime = Time.time + repathInterval;
        }

        while (pathIndex < path.Count &&
               Vector2.Distance(position, path[pathIndex]) <= waypointTolerance)
            pathIndex++;

        Vector2 desired = pathIndex < path.Count
            ? (path[pathIndex] - position).normalized
            : (SegmentClear(position, target)
                ? (target - position).normalized
                : Vector2.zero);
        currentDirection = ApplyCharacterAvoidance(desired);
        return currentDirection;
    }

    private void LateUpdate()
    {
        UpdateNavigationDebugVisual();
    }

    public bool DoesCurrentPathIntersect(
        Collider2D targetCollider,
        float lookAheadDistance)
    {
        if (bodyCollider == null || targetCollider == null ||
            !targetCollider.enabled || targetCollider.isTrigger ||
            currentDirection.sqrMagnitude <= 0.0001f ||
            lookAheadDistance <= 0f)
        {
            return false;
        }

        ColliderDistance2D separation =
            bodyCollider.Distance(targetCollider);
        if (separation.isOverlapped)
        {
            return true;
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };
        int hitCount = bodyCollider.Cast(
            currentDirection.normalized,
            filter,
            pathIntersectionHits,
            lookAheadDistance);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider2D hit = pathIntersectionHits[hitIndex].collider;
            if (hit == targetCollider ||
                (hit != null &&
                 (hit.transform.IsChildOf(targetCollider.transform) ||
                  targetCollider.transform.IsChildOf(hit.transform))))
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetSafeDisplacement(Vector2 direction, float distance)
    {
        if (body == null || bodyCollider == null || direction.sqrMagnitude < 0.0001f || distance <= 0f)
            return Vector2.zero;
        direction.Normalize();
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(0.05f, bounds.size.x),
            Mathf.Max(0.05f, bounds.size.y));
        int hitCount = Physics2D.BoxCastNonAlloc(
            bounds.center, size, body.rotation, direction, casts,
            distance + 0.01f, solidLayers);
        float allowed = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = casts[i].collider;
            if (ShouldIgnore(hit, true)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, casts[i].distance - 0.01f));
        }
        return direction * allowed;
    }

    private bool BuildPath(Vector2 start, Vector2 requestedGoal, float stoppingDistance)
    {
        // The inexpensive first pass uses the original narrow corridor. If a
        // valid route temporarily moves away from the goal (doorways, U-shaped
        // rooms, long walls), retry with a square search region so that the
        // required detour is actually represented in the A* graph.
        if (BuildPathAttempt(
                start,
                requestedGoal,
                stoppingDistance,
                false))
        {
            return true;
        }

        return BuildPathAttempt(
            start,
            requestedGoal,
            stoppingDistance,
            true);
    }

    private bool BuildPathAttempt(
        Vector2 start,
        Vector2 requestedGoal,
        float stoppingDistance,
        bool expandedSearch)
    {
        path.Clear();
        pathIndex = 0;
        float cell = Mathf.Max(0.15f, gridCellSize);
        Vector2 offset = requestedGoal - start;
        float maximumDistance = (maximumGridDimension - gridPadding * 2 - 2) * cell;
        Vector2 goal = offset.magnitude > maximumDistance
            ? start + offset.normalized * maximumDistance
            : requestedGoal;
        Vector2Int startCell = WorldToCell(start, cell);
        Vector2Int goalCell = WorldToCell(goal, cell);
        int deltaX = Mathf.Abs(goalCell.x - startCell.x);
        int deltaY = Mathf.Abs(goalCell.y - startCell.y);
        if (expandedSearch)
        {
            width = maximumGridDimension;
            height = maximumGridDimension;
            int spareX = Mathf.Max(0, width - deltaX - 1);
            int spareY = Mathf.Max(0, height - deltaY - 1);
            origin = new Vector2Int(
                Mathf.Min(startCell.x, goalCell.x) - spareX / 2,
                Mathf.Min(startCell.y, goalCell.y) - spareY / 2);
        }
        else
        {
            int minX = Mathf.Min(startCell.x, goalCell.x) - gridPadding;
            int minY = Mathf.Min(startCell.y, goalCell.y) - gridPadding;
            width = Mathf.Min(maximumGridDimension,
                deltaX + gridPadding * 2 + 1);
            height = Mathf.Min(maximumGridDimension,
                deltaY + gridPadding * 2 + 1);
            origin = new Vector2Int(minX, minY);
            origin.x = Mathf.Clamp(
                origin.x,
                startCell.x - width + 1,
                startCell.x);
            origin.y = Mathf.Clamp(
                origin.y,
                startCell.y - height + 1,
                startCell.y);
        }
        goalCell.x = Mathf.Clamp(goalCell.x, origin.x, origin.x + width - 1);
        goalCell.y = Mathf.Clamp(goalCell.y, origin.y, origin.y + height - 1);

        int count = width * height;
        EnsureCapacity(count);
        for (int i = 0; i < count; i++)
        {
            costs[i] = float.PositiveInfinity;
            parents[i] = -1;
            states[i] = 0;
            heapPositions[i] = -1;
        }
        heapCount = 0;
        int startIndex = ToIndex(startCell);
        if (startIndex < 0) return false;
        costs[startIndex] = 0f;
        states[startIndex] = 1;
        HeapPush(startIndex, goalCell);
        int goalIndex = -1;
        int goalRadius = Mathf.Max(1, Mathf.CeilToInt(stoppingDistance / cell));

        while (heapCount > 0)
        {
            int currentIndex = HeapPop(goalCell);
            if (states[currentIndex] == 2) continue;
            states[currentIndex] = 2;
            Vector2Int current = FromIndex(currentIndex);
            Vector2Int goalDelta = current - goalCell;
            if (goalDelta.x * goalDelta.x + goalDelta.y * goalDelta.y <= goalRadius * goalRadius)
            {
                goalIndex = currentIndex;
                break;
            }
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int step = Directions[d];
                Vector2Int next = current + step;
                int nextIndex = ToIndex(next);
                if (nextIndex < 0 || states[nextIndex] == 2 ||
                    (nextIndex != startIndex && IsBlocked(CellToWorld(next, cell))))
                    continue;
                bool diagonal = step.x != 0 && step.y != 0;
                if (diagonal &&
                    (IsBlocked(CellToWorld(current + new Vector2Int(step.x, 0), cell)) ||
                     IsBlocked(CellToWorld(current + new Vector2Int(0, step.y), cell))))
                    continue;
                if (!SegmentClear(CellToWorld(current, cell), CellToWorld(next, cell)))
                    continue;
                float nextCost = costs[currentIndex] + (diagonal ? 1.4142135f : 1f);
                if (nextCost >= costs[nextIndex]) continue;
                costs[nextIndex] = nextCost;
                parents[nextIndex] = currentIndex;
                if (states[nextIndex] == 0)
                {
                    states[nextIndex] = 1;
                    HeapPush(nextIndex, goalCell);
                }
                else HeapMoveUp(heapPositions[nextIndex], goalCell);
            }
        }
        if (goalIndex < 0) return false;
        for (int node = goalIndex, safety = count;
             node >= 0 && node != startIndex && safety > 0;
             node = parents[node], safety--)
            path.Add(CellToWorld(FromIndex(node), cell));
        path.Reverse();
        Simplify(start);
        return path.Count > 0;
    }

    private bool IsBlocked(Vector2 position)
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(0.05f, bounds.size.x + wallClearance * 2f),
            Mathf.Max(0.05f, bounds.size.y + wallClearance * 2f));
        int count = Physics2D.OverlapBoxNonAlloc(position, size, body.rotation, overlaps, solidLayers);
        for (int i = 0; i < count; i++)
            if (!ShouldIgnore(overlaps[i], false)) return true;
        return count >= overlaps.Length;
    }

    private bool SegmentClear(Vector2 from, Vector2 to)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        if (distance <= 0.0001f) return true;
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(0.05f, bounds.size.x + wallClearance * 2f),
            Mathf.Max(0.05f, bounds.size.y + wallClearance * 2f));
        int count = Physics2D.BoxCastNonAlloc(
            from, size, body.rotation, offset / distance, casts, distance, solidLayers);
        for (int i = 0; i < count; i++)
            if (!ShouldIgnore(casts[i].collider, false)) return false;
        return count < casts.Length;
    }

    private bool ShouldIgnore(Collider2D hit, bool includeCharacters)
    {
        if (hit == null || hit.isTrigger || hit == bodyCollider ||
            hit.transform.IsChildOf(transform)) return true;
        if (bodyCollider != null && Physics2D.GetIgnoreCollision(bodyCollider, hit))
            return true;
        if (ignoredTarget != null && hit.transform.IsChildOf(ignoredTarget.transform)) return true;
        DoorHingeInteraction door = hit.GetComponentInParent<DoorHingeInteraction>();
        if (door != null && !door.IsLocked) return true;
        if (!includeCharacters && hit.GetComponentInParent<ZeldaCharacterData>() != null)
            return true;
        return false;
    }

    private Vector2 ApplyCharacterAvoidance(Vector2 desired)
    {
        if (desired.sqrMagnitude < 0.0001f) return desired;
        Vector2 position = body.position;
        float ownRadius = bodyCollider != null
            ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y)
            : 0.2f;
        ZeldaFourWayMover closest = null;
        float scanDistance = characterAvoidanceDistance + ownRadius + 0.75f;
        float best = scanDistance * scanDistance;
        foreach (ZeldaFourWayMover mover in ZeldaRuntimeRegistry.Movers)
        {
            if (mover == null || !mover.gameObject.activeInHierarchy) continue;
            float sqr = ((Vector2)mover.transform.position - position).sqrMagnitude;
            if (sqr < best) { best = sqr; closest = mover; }
        }
        if (closest == null) return desired;
        Vector2 toward = (Vector2)closest.transform.position - position;
        if (Vector2.Dot(desired, toward) <= 0f) return desired;
        float side = Vector3.Cross(desired, toward).z >= 0f ? -1f : 1f;
        if (Mathf.Abs(Vector3.Cross(desired, toward).z) < 0.02f)
            side = GetInstanceID() < closest.GetInstanceID() ? -1f : 1f;
        Vector2 lateral = new Vector2(-desired.y, desired.x) * side;
        Collider2D otherCollider = closest.GetComponent<Collider2D>();
        float otherRadius = otherCollider != null
            ? Mathf.Max(otherCollider.bounds.extents.x, otherCollider.bounds.extents.y)
            : 0.3f;
        float requiredClearance = ownRadius + otherRadius + characterClearancePadding;
        float currentDistance = Mathf.Sqrt(best);
        float avoidanceRange = Mathf.Max(characterAvoidanceDistance, requiredClearance + 0.65f);
        float proximity = 1f - Mathf.Clamp01(
            (currentDistance - requiredClearance) /
            Mathf.Max(0.05f, avoidanceRange - requiredClearance));
        // Collider-aware lateral weight makes the puppet commit to a visibly
        // wider pass instead of making a small correction and catching on a
        // stationary character's corner.
        float lateralWeight = characterAvoidanceStrength * Mathf.Lerp(0.45f, 1f, proximity);
        return (desired * Mathf.Lerp(1f, 0.38f, proximity) +
                lateral * lateralWeight).normalized;
    }

    private void Simplify(Vector2 start)
    {
        if (path.Count <= 1) return;
        simplifiedPath.Clear();
        Vector2 anchor = start;
        int next = 0;
        while (next < path.Count)
        {
            int furthest = next;
            for (int candidate = path.Count - 1; candidate > next; candidate--)
                if (SegmentClear(anchor, path[candidate])) { furthest = candidate; break; }
            Vector2 point = path[furthest];
            simplifiedPath.Add(point);
            anchor = point;
            next = furthest + 1;
        }
        path.Clear();
        path.AddRange(simplifiedPath);
    }

    private void EnsureNavigationDebugVisual()
    {
        if (navigationDebugLine != null && navigationDebugTarget != null)
        {
            return;
        }

        navigationDebugMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            name = name + " Navigation Debug Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        GameObject lineObject = new GameObject("Puppet Navigation Debug Path");
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(transform, false);
        navigationDebugLine = lineObject.AddComponent<LineRenderer>();
        navigationDebugLine.useWorldSpace = true;
        navigationDebugLine.loop = false;
        navigationDebugLine.textureMode = LineTextureMode.Stretch;
        navigationDebugLine.numCapVertices = 2;
        navigationDebugLine.numCornerVertices = 2;
        navigationDebugLine.positionCount = 0;
        navigationDebugLine.sharedMaterial = navigationDebugMaterial;

        EnsureNavigationTargetSprite();
        GameObject targetObject = new GameObject("Puppet Navigation Debug Target");
        targetObject.layer = gameObject.layer;
        targetObject.transform.SetParent(transform, false);
        navigationDebugTarget = targetObject.AddComponent<SpriteRenderer>();
        navigationDebugTarget.sprite = navigationTargetSprite;
        navigationDebugTarget.sharedMaterial = navigationDebugMaterial;
        SetNavigationDebugVisible(false);
    }

    private void UpdateNavigationDebugVisual()
    {
        if (!showNavigationDebug)
        {
            SetNavigationDebugVisible(false);
            return;
        }
        EnsureNavigationDebugVisual();
        bool visible = hasRequestedTarget && body != null &&
                       bodyCollider != null && isActiveAndEnabled;
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

        int remainingNodeCount = Mathf.Max(0, path.Count - pathIndex);
        Vector2 finalPathPoint = remainingNodeCount > 0
            ? path[path.Count - 1]
            : body.position;
        bool appendTarget = SegmentClear(finalPathPoint, requestedTarget) &&
                            (finalPathPoint - requestedTarget).sqrMagnitude >
                            0.000001f;
        navigationDebugLine.positionCount =
            1 + remainingNodeCount + (appendTarget ? 1 : 0);
        int outputIndex = 0;
        navigationDebugLine.SetPosition(outputIndex++, body.position);
        for (int index = pathIndex; index < path.Count; index++)
        {
            navigationDebugLine.SetPosition(outputIndex++, path[index]);
        }
        if (appendTarget)
        {
            navigationDebugLine.SetPosition(outputIndex, requestedTarget);
        }

        navigationDebugTarget.transform.position = requestedTarget;
        navigationDebugTarget.transform.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        navigationDebugTarget.transform.localScale = new Vector3(
            navigationTargetMarkerSize / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            navigationTargetMarkerSize / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            1f);
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
        int center = size / 2;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false)
        {
            name = "Runtime Puppet Navigation Target",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                texture.SetPixel(
                    x,
                    y,
                    distance == center || (x == center && y == center)
                        ? Color.white
                        : Color.clear);
            }
        }
        texture.Apply(false, true);
        navigationTargetSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        navigationTargetSprite.name = "Runtime Puppet Navigation Target";
    }

    private void OnDisable()
    {
        SetNavigationDebugVisible(false);
    }

    private void OnDestroy()
    {
        if (navigationDebugMaterial != null)
        {
            Destroy(navigationDebugMaterial);
        }
    }

    private void EnsureCapacity(int count)
    {
        if (costs != null && costs.Length >= count) return;
        costs = new float[count]; parents = new int[count]; states = new byte[count];
        heap = new int[count]; heapPositions = new int[count];
    }
    private int ToIndex(Vector2Int cell)
    {
        int x = cell.x - origin.x, y = cell.y - origin.y;
        return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
    }
    private Vector2Int FromIndex(int index) => new Vector2Int(origin.x + index % width, origin.y + index / width);
    private static Vector2Int WorldToCell(Vector2 p, float size) => new Vector2Int(Mathf.RoundToInt(p.x / size), Mathf.RoundToInt(p.y / size));
    private static Vector2 CellToWorld(Vector2Int c, float size) => new Vector2(c.x * size, c.y * size);
    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + 0.4142135f * Mathf.Min(dx, dy);
    }
    private float Score(int index, Vector2Int goal) => costs[index] + Heuristic(FromIndex(index), goal);
    private void HeapPush(int node, Vector2Int goal)
    {
        int position = heapCount++; heap[position] = node; heapPositions[node] = position;
        HeapMoveUp(position, goal);
    }
    private void HeapMoveUp(int position, Vector2Int goal)
    {
        while (position > 0)
        {
            int parent = (position - 1) / 2;
            if (Score(heap[parent], goal) <= Score(heap[position], goal)) break;
            SwapHeap(parent, position); position = parent;
        }
    }
    private int HeapPop(Vector2Int goal)
    {
        int result = heap[0]; heapCount--; heapPositions[result] = -1;
        if (heapCount <= 0) return result;
        heap[0] = heap[heapCount]; heapPositions[heap[0]] = 0;
        int position = 0;
        while (true)
        {
            int left = position * 2 + 1;
            if (left >= heapCount) break;
            int right = left + 1;
            int best = right < heapCount && Score(heap[right], goal) < Score(heap[left], goal) ? right : left;
            if (Score(heap[position], goal) <= Score(heap[best], goal)) break;
            SwapHeap(position, best); position = best;
        }
        return result;
    }
    private void SwapHeap(int a, int b)
    {
        int temp = heap[a]; heap[a] = heap[b]; heap[b] = temp;
        heapPositions[heap[a]] = a; heapPositions[heap[b]] = b;
    }
}
