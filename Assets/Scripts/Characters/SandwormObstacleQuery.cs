using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only Blocks queries for the sandworm's roaming controller. Live
/// obstacles use physics; streamed-out obstacles use conservative shape bounds.
/// Refresh is the only scene scan and may allocate. Queries do neither.
/// </summary>
public sealed class SandwormObstacleQuery
{
    private struct Obstacle
    {
        public Collider2D collider;
        public Bounds localBounds;
        public bool hasBounds;
    }

    private const int QueryCapacity = 128;
    private readonly Collider2D[] overlaps = new Collider2D[QueryCapacity];
    private readonly RaycastHit2D[] casts = new RaycastHit2D[QueryCapacity];
    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private CameraVisionObjectStreaming[] streamers = new CameraVisionObjectStreaming[0];
    private ContactFilter2D filter;
    private Scene scene;
    private int blocksLayer = -1;
    private bool initialized;

    public void Refresh(Scene targetScene)
    {
        scene = targetScene;
        initialized = scene.IsValid() && scene.isLoaded;
        blocksLayer = LayerMask.NameToLayer("Blocks");
        filter = new ContactFilter2D();
        filter.SetLayerMask(blocksLayer >= 0 ? 1 << blocksLayer : 0);
        filter.useTriggers = false;
        obstacles.Clear();
        // The camera can live in DontDestroyOnLoad, outside the gameplay scene.
        streamers = Object.FindObjectsOfType<CameraVisionObjectStreaming>(true);
        if (!initialized || blocksLayer < 0) return;

        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D collider in colliders)
            {
                if (collider == null || collider.isTrigger ||
                    collider.gameObject.layer != blocksLayer) continue;
                Bounds localBounds;
                bool hasBounds = TryGetLocalBounds(collider, out localBounds);
                obstacles.Add(new Obstacle
                {
                    collider = collider,
                    localBounds = localBounds,
                    hasBounds = hasBounds
                });
            }
        }
    }

    public bool IsPointClear(Vector2 point, float clearance)
    {
        if (!initialized || blocksLayer < 0 || !IsFinite(point) ||
            float.IsNaN(clearance) || float.IsInfinity(clearance)) return false;
        clearance = Mathf.Max(0f, clearance);
        if (!IsLivePointClear(point, clearance)) return false;
        return IsStreamedSegmentClear(point, point, clearance);
    }

    public bool IsClear(Vector2 from, Vector2 to, float clearance)
    {
        if (!initialized || blocksLayer < 0 || !IsFinite(from) || !IsFinite(to) ||
            float.IsNaN(clearance) || float.IsInfinity(clearance)) return false;
        clearance = Mathf.Max(0f, clearance);
        // Explicit start overlap also works when queriesStartInColliders is false.
        if (!IsLivePointClear(from, clearance) ||
            !IsLivePointClear(to, clearance)) return false;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance > 0.00001f)
        {
            int count = Physics2D.CircleCast(from, clearance, delta / distance,
                filter, casts, distance);
            for (int i = 0; i < count; i++)
                if (IsRelevantLiveCollider(casts[i].collider)) return false;
            // Never declare a saturated result buffer safe: a relevant hit could
            // have been truncated behind unrelated additive-scene colliders.
            if (count >= casts.Length) return false;
        }
        return IsStreamedSegmentClear(from, to, clearance);
    }

    private bool IsLivePointClear(Vector2 point, float radius)
    {
        int count = Physics2D.OverlapCircle(point, radius, filter, overlaps);
        for (int i = 0; i < count; i++)
            if (IsRelevantLiveCollider(overlaps[i])) return false;
        return count < overlaps.Length;
    }

    private bool IsRelevantLiveCollider(Collider2D collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger &&
            collider.gameObject.activeInHierarchy && collider.gameObject.scene == scene &&
            collider.gameObject.layer == blocksLayer;
    }

    private bool IsStreamedSegmentClear(Vector2 from, Vector2 to, float clearance)
    {
        for (int i = 0; i < obstacles.Count; i++)
        {
            Obstacle entry = obstacles[i];
            Collider2D collider = entry.collider;
            if (collider == null || !collider.enabled || collider.isTrigger ||
                collider.gameObject.scene != scene ||
                collider.gameObject.layer != blocksLayer ||
                collider.gameObject.activeInHierarchy ||
                (collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated) ||
                !IsOnlyStreamingSuspended(collider.gameObject)) continue;

            // Unknown collider types without usable geometry must fail closed,
            // rather than accepting an empty bounds returned by a disabled body.
            if (!entry.hasBounds) return false;
            Bounds bounds = TransformBounds(collider.transform, entry.localBounds);
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            if (SegmentIntersectsBox(from, to,
                minimum.x - clearance, minimum.y - clearance,
                maximum.x + clearance, maximum.y + clearance)) return false;
        }
        return true;
    }

    private bool IsOnlyStreamingSuspended(GameObject candidate)
    {
        bool suspended = false;
        for (int i = 0; i < streamers.Length; i++)
        {
            if (streamers[i] != null && streamers[i].IsSuspendedByStreamingOrAncestor(candidate))
            {
                suspended = true;
                break;
            }
        }
        if (!suspended) return false;

        // A deliberately inactive child beneath a streamed-out parent remains
        // inactive gameplay content, and must not become an obstacle by accident.
        for (Transform current = candidate.transform; current != null; current = current.parent)
        {
            if (current.gameObject.activeSelf) continue;
            bool owned = false;
            for (int i = 0; i < streamers.Length; i++)
            {
                if (streamers[i] != null && streamers[i].IsSuspendedByStreaming(current.gameObject))
                {
                    owned = true;
                    break;
                }
            }
            if (!owned) return false;
        }
        return true;
    }

    private static bool TryGetLocalBounds(Collider2D collider, out Bounds bounds)
    {
        bounds = new Bounds();
        if (collider is BoxCollider2D box)
        {
            Vector2 size = new Vector2(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y));
            bounds = new Bounds(box.offset, size + Vector2.one * (2f * box.edgeRadius));
            return true;
        }
        if (collider is CircleCollider2D circle)
        {
            bounds = new Bounds(circle.offset, Vector2.one * (2f * Mathf.Abs(circle.radius)));
            return true;
        }
        if (collider is CapsuleCollider2D capsule)
        {
            bounds = new Bounds(capsule.offset,
                new Vector2(Mathf.Abs(capsule.size.x), Mathf.Abs(capsule.size.y)));
            return true;
        }

        bool found = false;
        if (collider is PolygonCollider2D polygon)
        {
            for (int p = 0; p < polygon.pathCount; p++)
                EncapsulatePoints(polygon.GetPath(p), polygon.offset, ref bounds, ref found);
        }
        else if (collider is EdgeCollider2D edge)
        {
            EncapsulatePoints(edge.points, edge.offset, ref bounds, ref found);
            if (found) bounds.Expand(2f * Mathf.Max(0f, edge.edgeRadius));
        }
        else if (collider is CompositeCollider2D composite)
        {
            for (int p = 0; p < composite.pathCount; p++)
            {
                Vector2[] points = new Vector2[composite.GetPathPointCount(p)];
                int count = composite.GetPath(p, points);
                for (int n = 0; n < count; n++)
                    EncapsulatePoint(points[n] + composite.offset, ref bounds, ref found);
            }
            if (found) bounds.Expand(2f * Mathf.Max(0f, composite.edgeRadius));
        }
        if (found) return true;

        // Tilemaps/custom collider types can expose a valid world bounds while
        // active. Inactive colliders often return an empty bounds, so try art too.
        Bounds world = collider.bounds;
        if (world.size.sqrMagnitude > 0f)
        {
            EncapsulateWorldBounds(collider.transform, world, ref bounds, ref found);
            return true;
        }
        Renderer[] renderers = collider.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            world = renderer.bounds;
            if (world.size.sqrMagnitude > 0f)
                EncapsulateWorldBounds(collider.transform, world, ref bounds, ref found);
        }
        return found;
    }

    private static void EncapsulatePoints(Vector2[] points, Vector2 offset,
        ref Bounds bounds, ref bool found)
    {
        for (int i = 0; i < points.Length; i++)
            EncapsulatePoint(points[i] + offset, ref bounds, ref found);
    }

    private static void EncapsulatePoint(Vector3 point, ref Bounds bounds, ref bool found)
    {
        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
        else bounds.Encapsulate(point);
    }

    private static void EncapsulateWorldBounds(Transform transform, Bounds world,
        ref Bounds local, ref bool found)
    {
        Vector3 min = world.min, max = world.max;
        for (int corner = 0; corner < 8; corner++)
            EncapsulatePoint(transform.InverseTransformPoint(new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z)), ref local, ref found);
    }

    private static Bounds TransformBounds(Transform transform, Bounds local)
    {
        Vector3 min = local.min, max = local.max;
        Bounds world = new Bounds();
        bool found = false;
        for (int corner = 0; corner < 8; corner++)
            EncapsulatePoint(transform.TransformPoint(new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z)), ref world, ref found);
        return world;
    }

    private static bool SegmentIntersectsBox(Vector2 from, Vector2 to,
        float minX, float minY, float maxX, float maxY)
    {
        Vector2 delta = to - from;
        float enter = 0f, exit = 1f;
        return ClipAxis(from.x, delta.x, minX, maxX, ref enter, ref exit) &&
            ClipAxis(from.y, delta.y, minY, maxY, ref enter, ref exit);
    }

    private static bool ClipAxis(float origin, float delta, float minimum, float maximum,
        ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.000001f) return origin >= minimum && origin <= maximum;
        float first = (minimum - origin) / delta;
        float second = (maximum - origin) / delta;
        if (first > second) { float swap = first; first = second; second = swap; }
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, second);
        return enter <= exit;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
