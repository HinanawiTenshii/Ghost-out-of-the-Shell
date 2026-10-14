using UnityEngine;

/// <summary>
/// Keeps a visibility ray fan outside occluders during transient body/wall overlap.
/// Moves only the query origin, never a character/camera or global physics settings.
/// Each source retains its own approach side; repeated visibility queries share a result.
/// </summary>
public sealed class CameraVisionOriginResolver
{
    private const float MaximumRecoveryDistance = 2f;
    private const float ContactEpsilon = .005f;
    private Object sourceKey;
    private Vector2 safeOrigin, cachedRequested, cachedResult;
    private bool hasSafeOrigin, wasCorrected;
    private int cachedFrame = -1, cachedLayers, cachedBlockerRevision;
    private float cachedClearance, cachedPhysicsTime;
    public int Revision { get; private set; }

    public void Reset()
    {
        sourceKey = null;
        hasSafeOrigin = wasCorrected = false;
        cachedFrame = -1;
        Revision++;
    }

    public Vector2 Resolve(Transform source, Vector2 requested, LayerMask layers,
        float blockerSkin, int blockerRevision)
    {
        if (sourceKey != source || cachedLayers != layers.value)
        {
            Reset();
            sourceKey = source;
        }
        // Clearance also avoids unstable zero-distance hits exactly on an edge.
        float clearance = Mathf.Clamp(blockerSkin + .01f, .02f, .08f);
        if (cachedFrame == Time.frameCount && cachedPhysicsTime == Time.fixedTime &&
            cachedBlockerRevision == blockerRevision && cachedRequested == requested &&
            Mathf.Approximately(cachedClearance, clearance)) return cachedResult;
        cachedFrame = Time.frameCount;
        cachedPhysicsTime = Time.fixedTime;
        cachedBlockerRevision = blockerRevision;
        cachedRequested = requested;
        cachedLayers = layers.value;
        cachedClearance = clearance;

        bool corrected = !IsClear(requested, clearance, layers);
        Vector2 result = requested;
        if (corrected)
        {
            // A valid recent approach is preferable to the nearest exit, which
            // could otherwise switch to the far side of a thin wall and reveal it.
            if (hasSafeOrigin && (safeOrigin - requested).sqrMagnitude <=
                MaximumRecoveryDistance * MaximumRecoveryDistance && IsClear(safeOrigin, clearance, layers))
                result = AdvanceFromClearPoint(safeOrigin, requested, clearance, layers);
            else
            {
                hasSafeOrigin = false;
                var body = source != null ? source.GetComponent<Collider2D>() : null;
                Vector2 bodyCenter = body != null ? (Vector2)body.bounds.center : requested;
                if (body != null && body.enabled && (bodyCenter - requested).sqrMagnitude <=
                    MaximumRecoveryDistance * MaximumRecoveryDistance && IsClear(bodyCenter, clearance, layers))
                    result = AdvanceFromClearPoint(bodyCenter, requested, clearance, layers);
                else TryFindLocalOrigin(requested, clearance, layers, out result);
            }
        }
        if (corrected != wasCorrected) Revision++;
        wasCorrected = corrected;
        cachedResult = result;
        // An intentionally invalid spawn deep in solid geometry must not reuse a
        // remote room's history or ignore the wall. Normal contact recovers locally.
        if (!corrected || IsClear(result, clearance, layers))
        {
            safeOrigin = result;
            hasSafeOrigin = true;
        }
        return result;
    }

    private static bool IsClear(Vector2 point, float clearance, LayerMask layers) =>
        Physics2D.OverlapCircle(point, clearance, layers) == null;

    private static Vector2 AdvanceFromClearPoint(Vector2 anchor, Vector2 requested,
        float clearance, LayerMask layers)
    {
        Vector2 delta = requested - anchor;
        float distance = delta.magnitude;
        if (distance <= ContactEpsilon) return anchor;
        Vector2 direction = delta / distance;
        RaycastHit2D hit = Physics2D.CircleCast(anchor, clearance, direction, distance, layers);
        Vector2 result = hit.collider != null
            ? anchor + direction * Mathf.Max(0f, hit.distance - ContactEpsilon) : requested;
        if (hit.collider != null && !IsClear(result, clearance, layers))
        {
            // Box2D casts and overlaps use different contact tolerances. Pull
            // outward along the actual face normal before attempting to slide.
            Vector2 separated = hit.point + hit.normal *
                (clearance + Physics2D.defaultContactOffset + ContactEpsilon);
            if (IsClear(separated, clearance, layers)) result = separated;
        }
        if (hit.collider != null && IsClear(result, clearance, layers))
        {
            // Follow motion along the near face too: clamping only the approach
            // segment would leave the view behind while a body slides along a wall.
            Vector2 remainder = requested - result;
            Vector2 tangent = remainder - hit.normal * Vector2.Dot(remainder, hit.normal);
            float slideDistance = tangent.magnitude;
            if (slideDistance > ContactEpsilon)
            {
                Vector2 slideDirection = tangent / slideDistance;
                var slideHit = Physics2D.CircleCast(result, clearance, slideDirection, slideDistance, layers);
                float allowed = slideHit.collider != null ? Mathf.Max(0f, slideHit.distance - ContactEpsilon) : slideDistance;
                Vector2 slid = result + slideDirection * allowed;
                if (IsClear(slid, clearance, layers)) result = slid;
            }
        }
        if (IsClear(result, clearance, layers)) return result;
        // Tangencies and multiple overlapping corner colliders can have slightly
        // different contact tolerances. Stay on the clear side in those cases too.
        Vector2 blocked = result;
        for (int i = 0; i < 10; i++)
        {
            Vector2 midpoint = (anchor + blocked) * .5f;
            if (IsClear(midpoint, clearance, layers)) anchor = midpoint;
            else blocked = midpoint;
        }
        return anchor;
    }

    private static bool TryFindLocalOrigin(Vector2 requested, float clearance,
        LayerMask layers, out Vector2 result)
    {
        result = requested;
        // Only used without usable history (e.g. loading/possessing an overlapping
        // body). Test real collider geometry, including rotated/concave walls.
        for (float radius = .0625f; radius <= MaximumRecoveryDistance; radius *= 2f)
        {
            bool found = false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector2 candidate = requested + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsClear(candidate, clearance, layers)) continue;
                candidate = AdvanceFromClearPoint(candidate, requested, clearance, layers);
                float distance = (candidate - requested).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                result = candidate;
                found = true;
            }
            if (found) return true;
        }
        return false;
    }
}
