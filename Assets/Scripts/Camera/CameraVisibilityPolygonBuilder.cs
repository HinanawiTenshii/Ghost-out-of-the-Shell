using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds an exact, source-centred 2D visibility polygon. Besides a regular
/// circular fan it casts immediately to either side of nearby collider
/// vertices, so wall corners create stable shadow edges instead of being
/// approximated by interpolation between fixed rays.
/// </summary>
public sealed class CameraVisibilityPolygonBuilder
{
    private const int InitialOverlapCapacity = 256;
    private const int MaximumOverlapCapacity = 2048;
    private const float TwoPi = Mathf.PI * 2f;
    private const float MinimumAngleSeparation = 0.00001f;

    public sealed class SourceQueryCache
    {
        internal readonly List<Collider2D> Blockers =
            new List<Collider2D>(128);
        internal Vector2 QueryOrigin;
        internal float QueryRadius;
        internal int LayerMask;
        internal int BlockerRevision;
        internal bool IsValid;

        public void Invalidate()
        {
            IsValid = false;
            Blockers.Clear();
        }
    }

    private sealed class BlockerGeometryCache
    {
        public Collider2D Collider;
        public Matrix4x4 LocalToWorld;
        public Bounds Bounds;
        public Vector2 Offset;
        public Vector2 Size;
        public int PathCount;
        public int PointCount;
        public bool UsesBoundsTangents;
        public readonly List<Vector2> WorldPoints =
            new List<Vector2>(16);
    }

    private Collider2D[] overlapResults =
        new Collider2D[InitialOverlapCapacity];
    private Vector2[] pathPoints = new Vector2[32];
    private readonly List<float> candidateAngles = new List<float>(512);
    private readonly List<Vector2> polygonPathPoints =
        new List<Vector2>(64);
    private readonly Dictionary<int, BlockerGeometryCache>
        blockerGeometryCaches =
            new Dictionary<int, BlockerGeometryCache>(256);

    public void ClearCaches()
    {
        blockerGeometryCaches.Clear();
    }

    public void AppendVisibilityPolygon(
        Transform localSpace,
        Vector2 origin,
        float radius,
        LayerMask blockingLayers,
        float blockerSkin,
        int circleSegments,
        float cornerAngleOffsetDegrees,
        SourceQueryCache queryCache,
        int blockerRevision,
        float queryMovementThreshold,
        float queryPadding,
        List<Vector3> vertices,
        List<int> triangles)
    {
        radius = Mathf.Max(0.1f, radius);
        circleSegments = Mathf.Clamp(circleSegments, 48, 256);
        candidateAngles.Clear();

        float circleStep = TwoPi / circleSegments;
        for (int index = 0; index < circleSegments; index++)
        {
            candidateAngles.Add(circleStep * index);
        }

        IReadOnlyList<Collider2D> nearbyBlockers = GetNearbyBlockers(
            origin,
            radius,
            blockingLayers,
            queryCache,
            blockerRevision,
            queryMovementThreshold,
            queryPadding);
        float cornerOffset = Mathf.Clamp(
            cornerAngleOffsetDegrees,
            0.005f,
            0.25f) * Mathf.Deg2Rad;
        for (int index = 0; index < nearbyBlockers.Count; index++)
        {
            Collider2D blocker = nearbyBlockers[index];
            if (blocker == null || !blocker.enabled ||
                !blocker.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddColliderCornerAngles(
                blocker,
                origin,
                radius,
                cornerOffset);
        }

        candidateAngles.Sort();
        int centerIndex = vertices.Count;
        vertices.Add(localSpace.InverseTransformPoint(origin));
        int firstBoundaryIndex = vertices.Count;
        float previousAngle = float.NegativeInfinity;
        for (int index = 0; index < candidateAngles.Count; index++)
        {
            float angle = candidateAngles[index];
            if (angle - previousAngle < MinimumAngleSeparation)
            {
                continue;
            }

            previousAngle = angle;
            Vector2 direction = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle));
            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction,
                radius,
                blockingLayers);
            float distance = hit.collider != null
                ? Mathf.Max(0f, hit.distance - blockerSkin)
                : radius;
            Vector2 point = origin + direction * distance;
            vertices.Add(localSpace.InverseTransformPoint(point));
        }

        int boundaryCount = vertices.Count - firstBoundaryIndex;
        if (boundaryCount < 3)
        {
            vertices.RemoveRange(centerIndex, vertices.Count - centerIndex);
            return;
        }

        for (int index = 0; index < boundaryCount; index++)
        {
            triangles.Add(centerIndex);
            triangles.Add(firstBoundaryIndex + index);
            triangles.Add(firstBoundaryIndex +
                (index + 1) % boundaryCount);
        }
    }

    private IReadOnlyList<Collider2D> GetNearbyBlockers(
        Vector2 origin,
        float radius,
        LayerMask blockingLayers,
        SourceQueryCache queryCache,
        int blockerRevision,
        float movementThreshold,
        float queryPadding)
    {
        if (queryCache == null)
        {
            throw new System.ArgumentNullException(nameof(queryCache));
        }

        movementThreshold = Mathf.Max(0.05f, movementThreshold);
        queryPadding = Mathf.Max(
            movementThreshold + 0.05f,
            queryPadding);
        float requestedQueryRadius = radius + queryPadding;
        bool movedOutsideCache = !queryCache.IsValid ||
            (origin - queryCache.QueryOrigin).sqrMagnitude >=
            movementThreshold * movementThreshold;
        bool radiusChanged = !Mathf.Approximately(
            queryCache.QueryRadius,
            requestedQueryRadius);
        bool layersChanged = queryCache.LayerMask != blockingLayers.value;
        bool blockersChanged = queryCache.BlockerRevision != blockerRevision;
        if (!movedOutsideCache && !radiusChanged && !layersChanged &&
            !blockersChanged)
        {
            return queryCache.Blockers;
        }

        int overlapCount = QueryNearbyBlockers(
            origin,
            requestedQueryRadius,
            blockingLayers);
        queryCache.Blockers.Clear();
        for (int index = 0; index < overlapCount; index++)
        {
            Collider2D blocker = overlapResults[index];
            if (blocker != null)
            {
                queryCache.Blockers.Add(blocker);
            }
        }
        queryCache.QueryOrigin = origin;
        queryCache.QueryRadius = requestedQueryRadius;
        queryCache.LayerMask = blockingLayers.value;
        queryCache.BlockerRevision = blockerRevision;
        queryCache.IsValid = true;
        return queryCache.Blockers;
    }

    private int QueryNearbyBlockers(
        Vector2 origin,
        float radius,
        LayerMask blockingLayers)
    {
        while (true)
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                origin,
                radius,
                overlapResults,
                blockingLayers);
            if (count < overlapResults.Length ||
                overlapResults.Length >= MaximumOverlapCapacity)
            {
                return Mathf.Min(count, overlapResults.Length);
            }

            int newCapacity = Mathf.Min(
                overlapResults.Length * 2,
                MaximumOverlapCapacity);
            overlapResults = new Collider2D[newCapacity];
        }
    }

    private void AddColliderCornerAngles(
        Collider2D collider,
        Vector2 origin,
        float radius,
        float cornerOffset)
    {
        BlockerGeometryCache geometry = GetBlockerGeometry(collider);
        for (int index = 0; index < geometry.WorldPoints.Count; index++)
        {
            AddWorldPoint(
                geometry.WorldPoints[index],
                origin,
                radius,
                cornerOffset);
        }

        if (geometry.UsesBoundsTangents)
        {
            AddCurvedColliderTangents(
                geometry.Bounds,
                origin,
                cornerOffset);
        }
    }

    private BlockerGeometryCache GetBlockerGeometry(Collider2D collider)
    {
        int instanceId = collider.GetInstanceID();
        BlockerGeometryCache geometry;
        if (!blockerGeometryCaches.TryGetValue(instanceId, out geometry))
        {
            geometry = new BlockerGeometryCache();
            blockerGeometryCaches.Add(instanceId, geometry);
        }

        Matrix4x4 localToWorld = collider.transform.localToWorldMatrix;
        Bounds bounds = collider.bounds;
        Vector2 offset = GetColliderOffset(collider);
        Vector2 size = collider is BoxCollider2D box
            ? box.size
            : Vector2.zero;
        int pathCount = GetColliderPathCount(collider);
        int pointCount = GetColliderPointCount(collider);
        bool geometryChanged = geometry.Collider != collider ||
            geometry.LocalToWorld != localToWorld ||
            geometry.Bounds != bounds ||
            geometry.Offset != offset ||
            geometry.Size != size ||
            geometry.PathCount != pathCount ||
            geometry.PointCount != pointCount;
        if (!geometryChanged)
        {
            return geometry;
        }

        geometry.Collider = collider;
        geometry.LocalToWorld = localToWorld;
        geometry.Bounds = bounds;
        geometry.Offset = offset;
        geometry.Size = size;
        geometry.PathCount = pathCount;
        geometry.PointCount = pointCount;
        geometry.UsesBoundsTangents = collider is CircleCollider2D ||
            collider is CapsuleCollider2D;
        geometry.WorldPoints.Clear();
        PopulateWorldPoints(collider, geometry.WorldPoints);
        return geometry;
    }

    private void PopulateWorldPoints(
        Collider2D collider,
        List<Vector2> worldPoints)
    {
        if (collider is BoxCollider2D box)
        {
            Vector2 halfSize = box.size * 0.5f;
            AddTransformedPoint(worldPoints, box.transform, box.offset +
                new Vector2(-halfSize.x, -halfSize.y));
            AddTransformedPoint(worldPoints, box.transform, box.offset +
                new Vector2(-halfSize.x, halfSize.y));
            AddTransformedPoint(worldPoints, box.transform, box.offset +
                new Vector2(halfSize.x, halfSize.y));
            AddTransformedPoint(worldPoints, box.transform, box.offset +
                new Vector2(halfSize.x, -halfSize.y));
            return;
        }

        if (collider is PolygonCollider2D polygon)
        {
            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                polygonPathPoints.Clear();
                polygon.GetPath(pathIndex, polygonPathPoints);
                for (int index = 0;
                     index < polygonPathPoints.Count;
                     index++)
                {
                    AddTransformedPoint(
                        worldPoints,
                        polygon.transform,
                        polygonPathPoints[index] + polygon.offset);
                }
            }
            return;
        }

        if (collider is CompositeCollider2D composite)
        {
            for (int pathIndex = 0; pathIndex < composite.pathCount; pathIndex++)
            {
                int requiredCount = composite.GetPathPointCount(pathIndex);
                EnsurePathCapacity(requiredCount);
                int received = composite.GetPath(pathIndex, pathPoints);
                for (int index = 0; index < received; index++)
                {
                    AddTransformedPoint(
                        worldPoints,
                        composite.transform,
                        pathPoints[index] + composite.offset);
                }
            }
            return;
        }

        if (collider is EdgeCollider2D edge)
        {
            Vector2[] points = edge.points;
            for (int index = 0; index < points.Length; index++)
            {
                AddTransformedPoint(
                    worldPoints,
                    edge.transform,
                    points[index] + edge.offset);
            }
            return;
        }

        AddBoundsCorners(worldPoints, collider.bounds);
    }

    private static void AddTransformedPoint(
        List<Vector2> worldPoints,
        Transform pointTransform,
        Vector2 localPoint)
    {
        worldPoints.Add(pointTransform.TransformPoint(localPoint));
    }

    private static void AddBoundsCorners(
        List<Vector2> worldPoints,
        Bounds bounds)
    {
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        worldPoints.Add(new Vector2(minimum.x, minimum.y));
        worldPoints.Add(new Vector2(minimum.x, maximum.y));
        worldPoints.Add(new Vector2(maximum.x, maximum.y));
        worldPoints.Add(new Vector2(maximum.x, minimum.y));
    }

    private static Vector2 GetColliderOffset(Collider2D collider)
    {
        if (collider is BoxCollider2D box)
        {
            return box.offset;
        }
        if (collider is PolygonCollider2D polygon)
        {
            return polygon.offset;
        }
        if (collider is CompositeCollider2D composite)
        {
            return composite.offset;
        }
        if (collider is EdgeCollider2D edge)
        {
            return edge.offset;
        }
        if (collider is CircleCollider2D circle)
        {
            return circle.offset;
        }
        if (collider is CapsuleCollider2D capsule)
        {
            return capsule.offset;
        }
        return Vector2.zero;
    }

    private static int GetColliderPathCount(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon)
        {
            return polygon.pathCount;
        }
        if (collider is CompositeCollider2D composite)
        {
            return composite.pathCount;
        }
        return 0;
    }

    private static int GetColliderPointCount(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon)
        {
            return polygon.GetTotalPointCount();
        }
        if (collider is CompositeCollider2D composite)
        {
            return composite.pointCount;
        }
        if (collider is EdgeCollider2D edge)
        {
            return edge.pointCount;
        }
        return 0;
    }

    private void AddCurvedColliderTangents(
        Bounds bounds,
        Vector2 origin,
        float cornerOffset)
    {
        Vector2 offset = (Vector2)bounds.center - origin;
        float distance = offset.magnitude;
        float approximateRadius = Mathf.Max(
            bounds.extents.x,
            bounds.extents.y);
        if (distance <= approximateRadius + 0.0001f)
        {
            return;
        }

        float centerAngle = Mathf.Atan2(offset.y, offset.x);
        float tangentOffset = Mathf.Asin(Mathf.Clamp01(
            approximateRadius / distance));
        AddAngleWithSides(centerAngle - tangentOffset, cornerOffset);
        AddAngleWithSides(centerAngle + tangentOffset, cornerOffset);
    }

    private void AddWorldPoint(
        Vector2 point,
        Vector2 origin,
        float radius,
        float cornerOffset)
    {
        Vector2 offset = point - origin;
        if (offset.sqrMagnitude >
            (radius + 0.5f) * (radius + 0.5f) ||
            offset.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        AddAngleWithSides(
            Mathf.Atan2(offset.y, offset.x),
            cornerOffset);
    }

    private void AddAngleWithSides(float angle, float cornerOffset)
    {
        candidateAngles.Add(NormalizeAngle(angle - cornerOffset));
        candidateAngles.Add(NormalizeAngle(angle));
        candidateAngles.Add(NormalizeAngle(angle + cornerOffset));
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, TwoPi);
    }

    private void EnsurePathCapacity(int requiredCount)
    {
        if (pathPoints.Length >= requiredCount)
        {
            return;
        }

        int capacity = pathPoints.Length;
        while (capacity < requiredCount)
        {
            capacity *= 2;
        }
        pathPoints = new Vector2[capacity];
    }
}
