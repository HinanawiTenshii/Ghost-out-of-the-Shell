using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor/runtime cable preview. One batched mesh, no collider, only cardinal segments.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BatterySocket))]
public sealed class BatterySocketCable : MonoBehaviour
{
    private static readonly HashSet<BatterySocketCable> ActiveCables = new HashSet<BatterySocketCable>();
    [SerializeField, Min(.04f), InspectorName("电缆宽度")] private float cableWidth = .12f;
    [SerializeField, Min(0f), InspectorName("离墙间距")] private float wallClearance = .08f;
    [SerializeField, Min(1f), InspectorName("最大绕行范围")] private float detourMargin = 32f;
    [SerializeField, Range(1000, 120000)] private int maximumRoutingNodes = 40000;
    [SerializeField, Min(.1f), InspectorName("刷新间隔")] private float refreshInterval = .5f;
    [SerializeField, Min(0f), InspectorName("自然错位幅度"), Tooltip("适度增加至多两个直角；设为 0 使用简洁走线。")]
    private float naturalStagger = .45f;
    [SerializeField, Min(0f), InspectorName("电缆间距"), Tooltip("优先避开其他电缆的长段重合；狭窄处允许短暂交叉。")]
    private float cableSpacing = .18f;
    [SerializeField, Min(.1f), InspectorName("底部接线段长度")]
    private float bottomLeadLength = .28f;
    [SerializeField, Min(0f), InspectorName("额外设备间距"), Tooltip("在电缆半宽和离墙间距之外，再为其他电池槽、控制器与拉杆保留的间隔。")]
    private float deviceClearance = .12f;
    [SerializeField, Tooltip("地面线路的排序最高为 0，始终低于角色与幽灵（1）。")]
    private int sortingOrder;
    private BatterySocket socket;
    private GameObject generated;
    private Mesh mesh;
    private MeshRenderer cableRenderer;
    private Material material;
    private Collider2D[] colliders;
    private readonly List<Component> devices = new List<Component>();
    private CameraVisionObjectStreaming[] streaming;
    private readonly Dictionary<Collider2D, Rect> previousBounds = new Dictionary<Collider2D, Rect>();
    private readonly List<Collider2D> deadBounds = new List<Collider2D>();
    private readonly List<Rect> obstacles = new List<Rect>();
    private readonly List<Rect> previousObstacles = new List<Rect>();
    private readonly List<Rect> reservations = new List<Rect>();
    private readonly List<Rect> previousReservations = new List<Rect>();
    private readonly List<BatterySocketCable> earlierCables = new List<BatterySocketCable>();
    private readonly List<Vector2> route = new List<Vector2>();
    private Vector2 lastStart, lastEnd;
    private MonoBehaviour lastBinding;
    private Matrix4x4 lastMatrix;
    private Matrix4x4 lastBindingMatrix;
    private float nextRefresh, nextScan;
    private bool dirty = true, warned;
    private string lastOrderKey;
    public bool HasRoute { get; private set; }

    private void OnEnable() { ActiveCables.Add(this); socket = GetComponent<BatterySocket>(); dirty = true; nextRefresh = nextScan = 0; }
    private void OnValidate()
    {
        cableWidth = Mathf.Max(.04f, cableWidth); wallClearance = Mathf.Max(0f, wallClearance);
        detourMargin = Mathf.Max(1f, detourMargin); refreshInterval = Mathf.Max(.1f, refreshInterval);
        maximumRoutingNodes = Mathf.Clamp(maximumRoutingNodes, 1000, 120000);
        naturalStagger = Mathf.Max(0f, naturalStagger); cableSpacing = Mathf.Max(0f, cableSpacing);
        bottomLeadLength = Mathf.Max(.1f, bottomLeadLength);
        deviceClearance = Mathf.Max(0f, deviceClearance);
        sortingOrder = Mathf.Min(0, sortingOrder);
        dirty = true;
    }
    [ContextMenu("重新生成电缆")]
    public void RebuildCable() { dirty = true; nextRefresh = nextScan = 0; Refresh(); }
    private void LateUpdate()
    {
        if (socket == null) socket = GetComponent<BatterySocket>();
        MonoBehaviour binding = socket != null ? socket.LinkedMechanism : null;
        // Port/parent movement is immediate; only background obstacle polling is throttled.
        bool endpointsChanged = binding != lastBinding || (binding != null &&
            (GetBottomPort(transform, GetLocalBodyBounds(socket), out _) != lastStart
             || GetBottomPort(binding.transform, GetLocalBodyBounds(binding), out _) != lastEnd
             || transform.localToWorldMatrix != lastMatrix || binding.transform.localToWorldMatrix != lastBindingMatrix));
        if (dirty || endpointsChanged || Time.realtimeSinceStartup >= nextRefresh) Refresh();
    }

    private void Refresh()
    {
        nextRefresh = Time.realtimeSinceStartup + refreshInterval;
        if (socket == null) socket = GetComponent<BatterySocket>();
        MonoBehaviour binding = socket != null ? socket.LinkedMechanism : null;
        if (binding == null || binding.gameObject.scene != gameObject.scene)
        {
            if (generated != null) generated.SetActive(false);
            HasRoute = false; lastBinding = null; dirty = false; return;
        }
        Vector2 start = GetBottomPort(transform, GetLocalBodyBounds(socket), out Rect startBody);
        Vector2 end = GetBottomPort(binding.transform, GetLocalBodyBounds(binding), out Rect endBody);
        if (colliders == null || Time.realtimeSinceStartup >= nextScan)
        {
            nextScan = Time.realtimeSinceStartup + 2f;
            var found = new List<Collider2D>();
            devices.Clear();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<Collider2D>(true));
                // Include unbound devices and devices with no cable component. Their visuals
                // need protecting regardless of collision layer or behaviour.enabled.
                devices.AddRange(root.GetComponentsInChildren<BatterySocket>(true));
                devices.AddRange(root.GetComponentsInChildren<WaterwayGateController>(true));
                devices.AddRange(root.GetComponentsInChildren<LeverData>(true));
            }
            colliders = found.ToArray();
            streaming = FindObjectsOfType<CameraVisionObjectStreaming>();
            deadBounds.Clear();
            foreach (var cached in previousBounds) if (cached.Key == null) deadBounds.Add(cached.Key);
            foreach (var dead in deadBounds) previousBounds.Remove(dead);
        }
        obstacles.Clear();
        int blocks = LayerMask.NameToLayer("Blocks");
        foreach (Collider2D obstacle in colliders)
        {
            if (obstacle == null || !obstacle.enabled || obstacle.isTrigger || obstacle.gameObject.layer != blocks
                || obstacle.transform == transform || obstacle.transform.IsChildOf(transform)
                || obstacle.transform == binding.transform || obstacle.transform.IsChildOf(binding.transform)) continue;
            if (!IsPresentForRouting(obstacle.gameObject)) continue;
            Rect bounds;
            if (!TryGetBounds(obstacle, out bounds)) continue;
            // Geometry is scene-local; don't collect distant walls irrelevant to the bounded search.
            if (bounds.xMax < Mathf.Min(start.x, end.x) - detourMargin - 1f
                || bounds.xMin > Mathf.Max(start.x, end.x) + detourMargin + 1f
                || bounds.yMax < Mathf.Min(start.y, end.y) - detourMargin - 1f
                || bounds.yMin > Mathf.Max(start.y, end.y) + detourMargin + 1f) continue;
            obstacles.Add(bounds);
        }
        AddDeviceObstacles(binding);
        string orderKey = GetOrderKey();
        CollectCableReservations(orderKey);
        bool changed = dirty || binding != lastBinding || start != lastStart || end != lastEnd || orderKey != lastOrderKey
            || transform.localToWorldMatrix != lastMatrix || binding.transform.localToWorldMatrix != lastBindingMatrix
            || obstacles.Count != previousObstacles.Count;
        if (!changed)
            for (int i = 0; i < obstacles.Count; i++) if (obstacles[i] != previousObstacles[i]) { changed = true; break; }
        if (reservations.Count != previousReservations.Count) changed = true;
        if (!changed)
            for (int i = 0; i < reservations.Count; i++) if (reservations[i] != previousReservations[i]) { changed = true; break; }
        if (!changed) return;
        dirty = false; lastBinding = binding; lastStart = start; lastEnd = end; lastMatrix = transform.localToWorldMatrix;
        lastBindingMatrix = binding.transform.localToWorldMatrix;
        previousObstacles.Clear(); previousObstacles.AddRange(obstacles);
        previousReservations.Clear(); previousReservations.AddRange(reservations); lastOrderKey = orderKey;
        HasRoute = false;
        float margin = Mathf.Min(4f, detourMargin);
        while (true)
        {
            HasRoute = OrthogonalCableRouter.TryRoutePorts(start, end, GetPortDirection(transform),
                GetPortDirection(binding.transform), startBody, endBody, bottomLeadLength, obstacles,
                (cableWidth + .04f) * .5f + wallClearance, margin, maximumRoutingNodes, .20f, route,
                reservations, naturalStagger, StableVariation(orderKey));
            if (HasRoute || margin >= detourMargin) break;
            margin = Mathf.Min(detourMargin, margin * 2f);
        }
        if (!HasRoute)
        {
            if (generated != null) generated.SetActive(false);
            if (!warned) Debug.LogWarning("电池槽电缆无法找到避开 Blocks 墙体与其他设备的直角路径。请检查底部接口是否被墙体或其他设备挡住，或增大最大绕行范围／寻路节点上限。", this);
            warned = true; return;
        }
        warned = false; DrawRoute();
    }

    private bool IsPresentForRouting(GameObject target)
    {
        if (target.activeInHierarchy) return true;
        if (streaming != null)
            foreach (var manager in streaming)
                if (manager != null && manager.IsSuspendedByStreamingOrAncestor(target)) return true;
        return false;
    }

    private void AddDeviceObstacles(MonoBehaviour binding)
    {
        foreach (Component device in devices)
        {
            if (device == null || device.gameObject == gameObject || device.gameObject == binding.gameObject
                || !IsPresentForRouting(device.gameObject)) continue;
            GetBottomPort(device.transform, GetLocalBodyBounds(device), out Rect body);
            // Hard keepouts, NOT cable reservations: no congestion fallback may pass
            // through an unrelated device. Avoid using renderer.bounds (which could
            // include generated cables/prompts and make the obstacle grow recursively).
            obstacles.Add(Rect.MinMaxRect(body.xMin - deviceClearance, body.yMin - deviceClearance,
                body.xMax + deviceClearance, body.yMax + deviceClearance));
        }
    }

    private static Rect GetLocalBodyBounds(Component device)
    {
        // Actual visible silhouettes, not prompt/cable renderer bounds or oversized interaction colliders.
        if (device is WaterwayGateController) return Rect.MinMaxRect(-.43f, -.62f, .43f, .62f);
        if (device is LeverData)
            return Rect.MinMaxRect(-9f / 26.666667f, (3f - 5.28f) / 26.666667f,
                9f / 26.666667f, (23f - 5.28f) / 26.666667f);
        return Rect.MinMaxRect(-9f / 24f, -11f / 24f, 9f / 24f, 11f / 24f);
    }

    private static Vector2 GetBottomPort(Transform owner, Rect body, out Rect worldBody)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = owner.TransformPoint(new Vector2((i & 1) == 0 ? body.xMin : body.xMax,
                (i & 2) == 0 ? body.yMin : body.yMax));
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        worldBody = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        // The device's OWN bottom edge follows its transform (including 180-degree
        // rotation and negative scale); it is not the lowest point on the screen.
        return owner.TransformPoint(new Vector2((body.xMin + body.xMax) * .5f, body.yMin));
    }

    private static Vector2 GetPortDirection(Transform owner)
    {
        Vector2 outward = owner.TransformVector(new Vector3(0f, -1f, 0f));
        // Keep the physical anchor exact, but choose a cardinal exit for non-quarter
        // turns as well, so rotating a prop never introduces diagonal cable geometry.
        if (Mathf.Abs(outward.x) > Mathf.Abs(outward.y))
            return new Vector2(outward.x >= 0f ? 1f : -1f, 0f);
        return new Vector2(0f, outward.y > 0f ? 1f : -1f);
    }

    private string GetOrderKey()
    {
        // Scene hierarchy order survives reloads; no per-frame randomness or runtime instance-ID seed.
        string key = string.Empty;
        for (Transform current = transform; current != null; current = current.parent)
            key = current.GetSiblingIndex().ToString("D6") + "/" + key;
        return key;
    }
    private static int StableVariation(string key)
    {
        int value = 17;
        unchecked { foreach (char c in key) value = value * 31 + c; }
        return value & 0x7fffffff;
    }
    private void CollectCableReservations(string orderKey)
    {
        reservations.Clear(); earlierCables.Clear();
        foreach (var other in ActiveCables)
        {
            if (other == null || other == this || !other.isActiveAndEnabled || !other.HasRoute
                || other.gameObject.scene != gameObject.scene
                || string.CompareOrdinal(other.GetOrderKey(), orderKey) >= 0) continue;
            earlierCables.Add(other);
        }
        // One-way priority prevents cables endlessly dodging each other. Later cables
        // yield to earlier routes; dependency changes propagate once, never oscillate.
        earlierCables.Sort((a, b) => string.CompareOrdinal(a.GetOrderKey(), b.GetOrderKey()));
        foreach (var other in earlierCables)
        {
            float radius = (cableWidth + other.cableWidth + .08f) * .5f + cableSpacing;
            for (int i = 1; i < other.route.Count; i++)
            {
                Vector2 a = other.route[i - 1], b = other.route[i];
                reservations.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - radius, Mathf.Min(a.y, b.y) - radius,
                    Mathf.Max(a.x, b.x) + radius, Mathf.Max(a.y, b.y) + radius));
            }
        }
    }

    private bool TryGetBounds(Collider2D collider, out Rect result)
    {
        // Box walls are evaluated directly from their transforms even when streaming disables them.
        if (collider is BoxCollider2D box)
        {
            Vector2 half = box.size * .5f + Vector2.one * box.edgeRadius;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = box.transform.TransformPoint(box.offset + new Vector2((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y));
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }
        // Most other collider bounds are supplied by physics; cached bounds retain unloaded walls.
        Bounds bounds = collider.bounds;
        if (bounds.size.sqrMagnitude > .000001f)
        {
            result = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            previousBounds[collider] = result; return true;
        }
        // Inactive colliders have empty physics bounds. Reconstruct authored geometry
        // instead of treating walls outside the camera as gaps in the route.
        var points = new List<Vector2>();
        if (collider is PolygonCollider2D polygon)
            for (int p = 0; p < polygon.pathCount; p++) points.AddRange(polygon.GetPath(p));
        else if (collider is EdgeCollider2D edge) points.AddRange(edge.points);
        else if (collider is CompositeCollider2D composite)
        {
            for (int p = 0; p < composite.pathCount; p++)
            { var vertices = new Vector2[composite.GetPathPointCount(p)]; composite.GetPath(p, vertices); points.AddRange(vertices); }
        }
        else
        {
            Vector2 half = collider is CircleCollider2D circle ? Vector2.one * circle.radius
                : collider is CapsuleCollider2D capsule ? capsule.size * .5f : Vector2.zero;
            if (half != Vector2.zero)
            { points.Add(-half); points.Add(half); points.Add(new Vector2(-half.x, half.y)); points.Add(new Vector2(half.x, -half.y)); }
        }
        if (points.Count > 0)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (Vector2 point in points)
            {
                Vector2 world = collider.transform.TransformPoint(point + collider.offset);
                min = Vector2.Min(min, world); max = Vector2.Max(max, world);
            }
            result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            previousBounds[collider] = result; return true;
        }
        return previousBounds.TryGetValue(collider, out result);
    }

    private void DrawRoute()
    {
        if (generated == null)
        {
            generated = new GameObject("Generated Orthogonal Battery Cable", typeof(MeshFilter), typeof(MeshRenderer))
                { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            generated.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Red Grey Cable", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            generated.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            cableRenderer = generated.GetComponent<MeshRenderer>();
            cableRenderer.sharedMaterial = material;
            cableRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cableRenderer.receiveShadows = false;
        }
        // Do not inherit a socket's legacy post-mask layer, even before its visual updates.
        generated.layer = 0;
        cableRenderer.sortingLayerID = 0;
        cableRenderer.sortingOrder = Mathf.Min(0, sortingOrder);
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
        DrawBand(cableWidth + .04f, new Color(.17f, .21f, .23f), .02f, vertices, colors, triangles);
        DrawBand(cableWidth, new Color(.60f, .65f, .66f), .019f, vertices, colors, triangles);
        DrawBand(cableWidth * .45f, new Color(.84f, .09f, .055f), .018f, vertices, colors, triangles);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds(); generated.SetActive(true);
    }
    private void DrawBand(float width, Color color, float z, List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 a = route[i - 1], b = route[i];
            float left = Mathf.Min(a.x, b.x) - width * .5f, right = Mathf.Max(a.x, b.x) + width * .5f;
            float bottom = Mathf.Min(a.y, b.y) - width * .5f, top = Mathf.Max(a.y, b.y) + width * .5f;
            int first = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(new Vector3(left, bottom, transform.position.z + z)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(left, top, transform.position.z + z)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(right, top, transform.position.z + z)));
            vertices.Add(transform.InverseTransformPoint(new Vector3(right, bottom, transform.position.z + z)));
            for (int n = 0; n < 4; n++) colors.Add(color);
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
    }
    private void OnDisable() { ActiveCables.Remove(this); HasRoute = false; if (generated != null) generated.SetActive(false); }
    private void OnDestroy() { ActiveCables.Remove(this); Release(generated); Release(mesh); Release(material); }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
