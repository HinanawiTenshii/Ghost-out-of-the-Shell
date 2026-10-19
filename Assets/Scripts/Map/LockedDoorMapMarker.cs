using UnityEngine;

/// <summary>Automatic, saved discovery for real hinged doors, not bridge pivots.</summary>
[DisallowMultipleComponent]
public sealed class LockedDoorMapMarker : MonoBehaviour
{
    private DoorHingeInteraction hinge;
    private DoorData[] leaves;
    private SpriteRenderer[] renderers;
    private string pointId;
    private Vector2 lineStart, lineEnd;
    private Color tint;
    private float nextVisibilityCheck;
    private bool broken;

    private void Start()
    {
        hinge = GetComponent<DoorHingeInteraction>();
        // Stable scene IDs have been assigned before Start. Cache before a broken child is removed.
        pointId = gameObject.scene.name + ":locked-door:" + SceneTravelStateManager.GetMapObjectId(transform);
        var candidates = GetComponentsInChildren<DoorData>(true);
        var owned = new System.Collections.Generic.List<DoorData>();
        foreach (var leaf in candidates)
            if (leaf.GetComponentInParent<DoorHingeInteraction>() == hinge) owned.Add(leaf);
        leaves = owned.ToArray(); renderers = new SpriteRenderer[leaves.Length];
        for (int i = 0; i < leaves.Length; i++) renderers[i] = leaves[i].GetComponent<SpriteRenderer>();
        UpdateMarker();
    }

    private void LateUpdate() => UpdateMarker();

    public void RefreshLockState() => UpdateMarker();

    private void UpdateMarker()
    {
        if (hinge == null || leaves == null || GameSaveSystem.IsLoading) return;
        bool intact = !broken && leaves.Length > 0;
        for (int i = 0; i < leaves.Length; i++)
            if (leaves[i] == null || leaves[i].IsDestroyed || !leaves[i].gameObject.activeSelf ||
                renderers[i] == null || renderers[i].sprite == null) intact = false;
        bool eligible = hinge.IsLocked && intact;
        var manager = MapPointOfInterestManager.GetOrCreate();
        bool discovered = manager.IsDiscovered(pointId);
        if (!eligible && !discovered) return;
        if (intact && gameObject.activeInHierarchy) RefreshLineGeometry();
        bool seen = false;
        if (eligible && gameObject.activeInHierarchy && !discovered && Time.unscaledTime >= nextVisibilityCheck)
        {
            nextVisibilityCheck = Time.unscaledTime + .15f;
            seen = IsVisibleToPlayer();
        }
        manager.UpdateLockedDoor(pointId, gameObject.scene.name, lineStart, lineEnd, tint, eligible, seen,
            hinge.PasswordLockEnabled, hinge.RequiredUnlockItemDisplayName);
    }

    public void NotifyLeafDestroyed()
    {
        broken = true;
        if (!string.IsNullOrEmpty(pointId) && MapPointOfInterestManager.Instance != null)
            MapPointOfInterestManager.Instance.UpdateLockedDoor(pointId, gameObject.scene.name,
                lineStart, lineEnd, tint, false, false, hinge != null && hinge.PasswordLockEnabled,
                hinge != null ? hinge.RequiredUnlockItemDisplayName : null);
    }

    private bool IsVisibleToPlayer()
    {
        Camera camera = Camera.main;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (camera == null || player == null ||
            ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) != gameObject.scene) return false;
        CameraCircularVision vision = camera.GetComponent<CameraCircularVision>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            // Test the leaf rather than the pivot, including partially visible long doors.
            Bounds bounds = renderer.bounds;
            Vector3 min = camera.WorldToViewportPoint(bounds.min);
            Vector3 max = camera.WorldToViewportPoint(bounds.max);
            if (max.z <= 0 || Mathf.Max(min.x,max.x) < 0 || Mathf.Min(min.x,max.x) > 1 ||
                Mathf.Max(min.y,max.y) < 0 || Mathf.Min(min.y,max.y) > 1) continue;
            if ((vision == null || !vision.isActiveAndEnabled) || vision.IsWorldBoundsVisible(bounds, transform)) return true;
        }
        return false;
    }

    private void RefreshLineGeometry()
    {
        GetLeafLine(renderers[0], out Vector2 first, out Vector2 last);
        Vector2 axis = (last - first).normalized;
        float min = 0, max = Vector2.Distance(first, last);
        // Double doors share one line spanning both leaves. Handles never enlarge it.
        for (int i = 1; i < renderers.Length; i++)
        {
            GetLeafLine(renderers[i], out Vector2 a, out Vector2 b);
            min = Mathf.Min(min, Vector2.Dot(a-first, axis), Vector2.Dot(b-first, axis));
            max = Mathf.Max(max, Vector2.Dot(a-first, axis), Vector2.Dot(b-first, axis));
        }
        lineStart = first + axis * min; lineEnd = first + axis * max;
        tint = renderers[0].color;
    }

    internal static void GetLeafLine(SpriteRenderer renderer, out Vector2 start, out Vector2 end)
    {
        Bounds bounds = renderer.drawMode == SpriteDrawMode.Simple
            ? renderer.sprite.bounds : new Bounds(Vector3.zero, renderer.size);
        Vector3 x = renderer.transform.TransformVector(new Vector3(bounds.extents.x,0,0));
        Vector3 y = renderer.transform.TransformVector(new Vector3(0,bounds.extents.y,0));
        Vector3 axis = x.sqrMagnitude >= y.sqrMagnitude ? x : y;
        Vector3 center = renderer.transform.TransformPoint(bounds.center);
        start = center - axis; end = center + axis;
    }
    // No OnDisable/OnDestroy removal: streaming and scene travel retain discovered markers.
    // Actual destruction explicitly reports above before Unity destroys the door hierarchy.
}
