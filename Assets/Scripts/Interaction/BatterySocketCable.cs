using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor/runtime cable preview. One batched mesh, no collider, only cardinal segments.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BatterySocket))]
public sealed class BatterySocketCable : MonoBehaviour
{
    private static readonly HashSet<BatterySocketCable> ActiveCables = new HashSet<BatterySocketCable>();
    private static readonly Color PoweredCoreColor = new Color(.84f, .09f, .055f);
    private static readonly Color UnpoweredCoreColor = new Color(.76f, .78f, .80f);
    [SerializeField, Min(.04f), InspectorName("电缆宽度")] private float cableWidth = .12f;
    [SerializeField, Min(0f), InspectorName("端口留白")] private float wallClearance = .08f;
    [SerializeField, Min(1f), InspectorName("接线搜索余量")] private float detourMargin = 32f;
    [SerializeField, Range(1000, 120000)] private int maximumRoutingNodes = 40000;
    [SerializeField, Min(.1f), InspectorName("刷新间隔")] private float refreshInterval = .5f;
    [SerializeField, Min(0f), InspectorName("自然错位幅度"), Tooltip("适度增加至多两个直角；设为 0 使用简洁走线。")]
    private float naturalStagger = .45f;
    [SerializeField, Min(0f), InspectorName("电缆间距"), Tooltip("优先避开其他电缆的长段重合；狭窄处允许短暂交叉。")]
    private float cableSpacing = .18f;
    [SerializeField, Min(.1f), InspectorName("底部接线段长度")]
    private float bottomLeadLength = .28f;
    [SerializeField, Tooltip("地面线路最高为 -1，低于普通墙体（0）和角色（1）；Blocks 仍由既有遮罩相机最终覆盖。")]
    private int sortingOrder;
    [Header("电流输送光效")]
    [SerializeField, InspectorName("显示输送光效")] private bool showCurrentFlow = true;
    [SerializeField, Min(.1f), InspectorName("光带移动速度")] private float currentFlowSpeed = 2.4f;
    [SerializeField, Min(.2f), InspectorName("光带发射间隔")] private float currentFlowInterval = 2f;
    [SerializeField, Min(.05f), InspectorName("光带长度")] private float currentFlowLength = .75f;
    [SerializeField, Range(0f, 3f), InspectorName("光带亮度")] private float currentFlowIntensity = 1.2f;
    private BatterySocket socket;
    private GameObject generated;
    private Mesh mesh;
    private MeshRenderer cableRenderer;
    private Material material;
    private Mesh currentFlowMesh;
    private MeshRenderer currentFlowRenderer;
    private Material currentFlowMaterial;
    private float currentRouteLength, currentTravel;
    private bool currentFlowRunning;
    private static readonly int FlowTravelId = Shader.PropertyToID("_FlowTravel");
    private static readonly int PulseSpacingId = Shader.PropertyToID("_PulseSpacing");
    private static readonly int PulseLengthId = Shader.PropertyToID("_PulseLength");
    private static readonly int FlowIntensityId = Shader.PropertyToID("_Intensity");
    private readonly List<Color> vertexColors = new List<Color>();
    private int coreVertexStart;
    private bool lastHasPower;
    // Persist the chosen route, not the obstacles that happened to shape it.
    [SerializeField, HideInInspector] private List<Vector2> savedRoute = new List<Vector2>();
    [SerializeField, HideInInspector] private MonoBehaviour savedBinding;
    [SerializeField, HideInInspector] private Matrix4x4 savedSocketMatrix, savedBindingMatrix;
    private static readonly List<Rect> NoObstacles = new List<Rect>();
    private readonly List<Rect> reservations = new List<Rect>();
    private readonly List<BatterySocketCable> earlierCables = new List<BatterySocketCable>();
    private readonly List<Vector2> route = new List<Vector2>();
    private Vector2 lastStart, lastEnd;
    private MonoBehaviour lastBinding;
    private Matrix4x4 lastMatrix;
    private Matrix4x4 lastBindingMatrix;
    private float nextRefresh;
    private bool dirty = true, forceNewRoute;
    public bool HasRoute { get; private set; }

    private void OnEnable() { ActiveCables.Add(this); socket = GetComponent<BatterySocket>(); dirty = true; nextRefresh = 0; }
    private void OnValidate()
    {
        cableWidth = Mathf.Max(.04f, cableWidth); wallClearance = Mathf.Max(0f, wallClearance);
        detourMargin = Mathf.Max(1f, detourMargin); refreshInterval = Mathf.Max(.1f, refreshInterval);
        maximumRoutingNodes = Mathf.Clamp(maximumRoutingNodes, 1000, 120000);
        naturalStagger = Mathf.Max(0f, naturalStagger); cableSpacing = Mathf.Max(0f, cableSpacing);
        bottomLeadLength = Mathf.Max(.1f, bottomLeadLength);
        sortingOrder = Mathf.Min(-1, sortingOrder);
        currentFlowSpeed = Mathf.Max(.1f, currentFlowSpeed);
        currentFlowInterval = Mathf.Max(.2f, currentFlowInterval);
        currentFlowLength = Mathf.Max(.05f, currentFlowLength);
        currentFlowIntensity = Mathf.Clamp(currentFlowIntensity, 0f, 3f);
        dirty = true;
    }
    [ContextMenu("重新生成电缆")]
    public void RebuildCable() { dirty = true; forceNewRoute = true; nextRefresh = 0; Refresh(); }
    private void LateUpdate()
    {
        if (socket == null) socket = GetComponent<BatterySocket>();
        MonoBehaviour binding = socket != null ? socket.LinkedMechanism : null;
        // Endpoint movement is immediate. Walls/devices never invalidate an existing route.
        bool endpointsChanged = binding != lastBinding || (binding != null &&
            (GetBottomPort(transform, GetLocalBodyBounds(socket), out _) != lastStart
             || GetMechanismPort(binding, out _) != lastEnd
             || transform.localToWorldMatrix != lastMatrix || binding.transform.localToWorldMatrix != lastBindingMatrix));
        if (dirty || endpointsChanged || Time.realtimeSinceStartup >= nextRefresh) Refresh();
        // Battery installation/removal, depletion and save restoration update immediately,
        // without running the route solver again.
        RefreshCoreColor();
        UpdateCurrentFlow();
    }

    private void Refresh()
    {
        nextRefresh = Time.realtimeSinceStartup + refreshInterval;
        if (socket == null) socket = GetComponent<BatterySocket>();
        MonoBehaviour binding = socket != null ? socket.LinkedMechanism : null;
        if (binding == null || binding.gameObject.scene != gameObject.scene || (binding is ElectricGatePillar pillar && !pillar.HasPanel))
        {
            if (generated != null) generated.SetActive(false);
            HasRoute = false; lastBinding = null; dirty = false; return;
        }
        Vector2 start = GetBottomPort(transform, GetLocalBodyBounds(socket), out Rect startBody);
        Vector2 end = GetMechanismPort(binding, out Rect endBody);
        bool endpointsSame = binding == lastBinding && start == lastStart && end == lastEnd
            && transform.localToWorldMatrix == lastMatrix && binding.transform.localToWorldMatrix == lastBindingMatrix;
        if (!dirty && !forceNewRoute && endpointsSame && HasRoute) return;

        bool restored = !forceNewRoute && TryRestoreRoute(binding, start, end);
        if (!restored)
        {
            string orderKey = GetOrderKey();
            CollectCableReservations(orderKey); // New routes yield; old routes never reshuffle.
            // Protect only the two connector housings. No scene walls, colliders,
            // other devices or streaming state participate in route selection.
            bool found = OrthogonalCableRouter.TryRoutePorts(start, end, GetPortDirection(transform),
                binding is ElectricGatePillar gate ? gate.CableDirection : GetPortDirection(binding.transform),
                startBody, endBody, bottomLeadLength, NoObstacles,
                (cableWidth + .04f) * .5f + wallClearance, detourMargin, maximumRoutingNodes, .20f, route,
                reservations, naturalStagger, StableVariation(orderKey));
            if (!found)
            {
                // Even overlapping housings/ports may connect. Keep cardinal leads
                // rather than hide the wire or attempt scene obstacle avoidance.
                Vector2 exit = start + GetPortDirection(transform) * bottomLeadLength;
                Vector2 entry = end + (binding is ElectricGatePillar p ? p.CableDirection : GetPortDirection(binding.transform)) * bottomLeadLength;
                route.Clear(); route.Add(start); route.Add(exit);
                route.Add(new Vector2(entry.x, exit.y)); route.Add(entry); route.Add(end);
            }
        }
        dirty = forceNewRoute = false; lastBinding = binding; lastStart = start; lastEnd = end;
        lastMatrix = transform.localToWorldMatrix; lastBindingMatrix = binding.transform.localToWorldMatrix;
        HasRoute = route.Count > 0;
        SaveRoute(binding);
        DrawRoute();
    }

    private bool TryRestoreRoute(MonoBehaviour binding, Vector2 start, Vector2 end)
    {
        // Disabling/re-enabling a socket or changing width/color must not reroute it.
        if (binding == lastBinding && transform.localToWorldMatrix == lastMatrix &&
            binding.transform.localToWorldMatrix == lastBindingMatrix && MatchesPorts(route, start, end)) return true;
        if (savedBinding == binding && savedSocketMatrix == transform.localToWorldMatrix &&
            savedBindingMatrix == binding.transform.localToWorldMatrix && MatchesPorts(savedRoute, start, end))
        {
            route.Clear(); route.AddRange(savedRoute); return true;
        }
        // First upgrade / script reload: the previous implementation had no saved
        // waypoints. Recover its already generated three-band mesh where available.
        // Never reinterpret a previously saved route after its endpoints moved.
        if (savedRoute != null && savedRoute.Count > 0) return false;
        Transform existing = generated != null ? generated.transform : transform.Find("Generated Orthogonal Battery Cable");
        var filter = existing != null ? existing.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return false;
        if (!TryRecoverMeshRoute(filter.sharedMesh.vertices, existing.localToWorldMatrix, start, end, route)) return false;
        // Adopt native objects retained through a script reload instead of leaving
        // duplicate hidden meshes/materials behind when managed fields were lost.
        generated = existing.gameObject; mesh = filter.sharedMesh;
        cableRenderer = generated.GetComponent<MeshRenderer>(); material = cableRenderer.sharedMaterial;
        Transform flow = existing.Find("Battery Cable Current");
        if (flow != null)
        {
            currentFlowRenderer = flow.GetComponent<MeshRenderer>();
            currentFlowMesh = flow.GetComponent<MeshFilter>().sharedMesh;
            currentFlowMaterial = currentFlowRenderer.sharedMaterial;
        }
        return true;
    }

    private static bool MatchesPorts(IReadOnlyList<Vector2> points, Vector2 start, Vector2 end)
    {
        return points != null && points.Count > 0 && (points[0] - start).sqrMagnitude < .000001f
            && (points[points.Count - 1] - end).sqrMagnitude < .000001f;
    }

    private static bool TryRecoverMeshRoute(Vector3[] vertices, Matrix4x4 matrix,
        Vector2 start, Vector2 end, List<Vector2> result)
    {
        if (vertices == null || vertices.Length == 0 || vertices.Length % 12 != 0) return false;
        var recovered = new List<Vector2> { start };
        int segments = vertices.Length / 12; // Four vertices per segment, three bands.
        Vector2 previous = start;
        for (int i = 0; i < segments; i++)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int j = 0; j < 4; j++)
            {
                Vector2 point = matrix.MultiplyPoint3x4(vertices[i * 4 + j]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            bool horizontal = max.x - min.x >= max.y - min.y;
            float half = Mathf.Min(max.x - min.x, max.y - min.y) * .5f;
            Vector2 a = horizontal ? new Vector2(min.x + half, (min.y + max.y) * .5f)
                : new Vector2((min.x + max.x) * .5f, min.y + half);
            Vector2 b = horizontal ? new Vector2(max.x - half, a.y) : new Vector2(a.x, max.y - half);
            if ((b - previous).sqrMagnitude < (a - previous).sqrMagnitude) { Vector2 swap = a; a = b; b = swap; }
            if ((a - previous).sqrMagnitude > .000001f) return false;
            b = horizontal ? new Vector2(b.x, previous.y) : new Vector2(previous.x, b.y);
            recovered.Add(b); previous = b;
        }
        if (!MatchesPorts(recovered, start, end)) return false;
        result.Clear(); result.AddRange(recovered); return true;
    }

    private void SaveRoute(MonoBehaviour binding)
    {
        bool identical = savedBinding == binding && savedSocketMatrix == transform.localToWorldMatrix
            && savedBindingMatrix == binding.transform.localToWorldMatrix && savedRoute != null && savedRoute.Count == route.Count;
        if (identical)
            for (int i = 0; i < route.Count; i++) if (savedRoute[i] != route[i]) { identical = false; break; }
        if (identical) return;
        savedBinding = binding; savedSocketMatrix = transform.localToWorldMatrix;
        savedBindingMatrix = binding.transform.localToWorldMatrix; savedRoute = new List<Vector2>(route);
#if UNITY_EDITOR
        if (!Application.isPlaying && !UnityEditor.EditorUtility.IsPersistent(this) && gameObject.scene.IsValid())
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private static Rect GetLocalBodyBounds(Component device)
    {
        if (device is ElectricGatePillar gate)
        {
            Vector2 d = gate.HasPanel ? gate.PanelDirection * .11f : Vector2.zero;
            return Rect.MinMaxRect(-.5f + Mathf.Min(0,d.x), -.5f + Mathf.Min(0,d.y), .5f + Mathf.Max(0,d.x), .5f + Mathf.Max(0,d.y));
        }
        // Actual visible silhouettes, not prompt/cable renderer bounds or oversized interaction colliders.
        if (device is WaterwayGateController) return Rect.MinMaxRect(-.43f, -.62f, .43f, .62f);
        if (device is AlarmDevice) return Rect.MinMaxRect(-.45f, -.55f, .45f, .55f);
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

    private static Vector2 GetMechanismPort(MonoBehaviour binding, out Rect body)
    {
        Vector2 bottom = GetBottomPort(binding.transform, GetLocalBodyBounds(binding), out body);
        return binding is ElectricGatePillar gate ? gate.CablePort : bottom;
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
        // yield to earlier routes when first generated; existing routes stay fixed.
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
        cableRenderer.sortingOrder = Mathf.Min(-1, sortingOrder);
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        vertexColors.Clear();
        DrawBand(cableWidth + .04f, new Color(.17f, .21f, .23f), .02f, vertices, vertexColors, triangles);
        DrawBand(cableWidth, new Color(.60f, .65f, .66f), .019f, vertices, vertexColors, triangles);
        coreVertexStart = vertexColors.Count;
        lastHasPower = socket != null && socket.HasPower;
        DrawBand(cableWidth * .45f, lastHasPower ? PoweredCoreColor : UnpoweredCoreColor, .018f, vertices, vertexColors, triangles);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(vertexColors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds(); generated.SetActive(true);
        BuildCurrentFlow();
    }

    private void BuildCurrentFlow()
    {
        StopCurrentFlow();
        currentRouteLength = 0f;
        if (!Application.isPlaying || !showCurrentFlow) return;
        if (currentFlowMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("BatteryCableFlow");
            if (shader == null) return;
            currentFlowMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (currentFlowRenderer == null)
        {
            var flow = new GameObject("Battery Cable Current", typeof(MeshFilter), typeof(MeshRenderer))
                { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            flow.transform.SetParent(generated.transform, false);
            currentFlowMesh = new Mesh { name = "Battery Cable Current Strip", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            flow.GetComponent<MeshFilter>().sharedMesh = currentFlowMesh;
            currentFlowRenderer = flow.GetComponent<MeshRenderer>();
            currentFlowRenderer.sharedMaterial = currentFlowMaterial;
            currentFlowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            currentFlowRenderer.receiveShadows = false;
        }
        // Same world layer/order as the casing; slightly nearer Z puts light over the
        // wire, but below walls (normally 0), characters (1) and the vision mask (30000).
        currentFlowRenderer.gameObject.layer = 0;
        currentFlowRenderer.sortingLayerID = 0;
        currentFlowRenderer.sortingOrder = Mathf.Min(-1, sortingOrder);
        currentFlowRenderer.enabled = false;
        var positions = new List<Vector2>(); var uv = new List<Vector2>(); var indices = new List<int>();
        currentRouteLength = BuildFlowStrip(route, (cableWidth + .04f) * 1.6f, positions, uv, indices);
        var vertices = new List<Vector3>(positions.Count);
        foreach (Vector2 point in positions)
            vertices.Add(transform.InverseTransformPoint(new Vector3(point.x, point.y, transform.position.z + .016f)));
        currentFlowMesh.Clear(); currentFlowMesh.SetVertices(vertices); currentFlowMesh.SetUVs(0, uv);
        currentFlowMesh.SetTriangles(indices, 0); currentFlowMesh.RecalculateBounds();
        float spacing = currentFlowSpeed * currentFlowInterval;
        currentFlowMaterial.SetFloat(PulseSpacingId, spacing);
        // Preserve a dark interval even if an unusually long light band is configured.
        currentFlowMaterial.SetFloat(PulseLengthId, Mathf.Min(currentFlowLength, spacing * .6f));
        currentFlowMaterial.SetFloat(FlowIntensityId, currentFlowIntensity);
    }

    private void UpdateCurrentFlow()
    {
        if (!Application.isPlaying || !showCurrentFlow || !HasRoute || socket == null || !socket.HasPower
            || currentFlowIntensity <= 0f || generated == null || !generated.activeInHierarchy
            || currentRouteLength <= .0001f || currentFlowRenderer == null || currentFlowMaterial == null)
        { StopCurrentFlow(); return; }
        // Restart at the battery port after installation, reload, rerouting or re-enable.
        // Scaled deltaTime freezes the effect with game pause; no mesh or routing work per frame.
        if (!currentFlowRunning) { currentTravel = 0f; currentFlowRunning = true; }
        else currentTravel = AdvanceFlowTravel(currentTravel, Time.deltaTime, currentFlowSpeed,
            currentRouteLength, currentFlowSpeed * currentFlowInterval);
        currentFlowMaterial.SetFloat(FlowTravelId, currentTravel);
        currentFlowRenderer.enabled = true;
    }

    private void StopCurrentFlow()
    {
        currentFlowRunning = false; currentTravel = 0f;
        if (currentFlowRenderer != null) currentFlowRenderer.enabled = false;
    }

    private static float AdvanceFlowTravel(float travel, float deltaTime, float speed, float length, float spacing)
    {
        travel += Mathf.Max(0f, deltaTime) * speed;
        // Bound precision for long sessions, without introducing pulses ahead of the
        // first wave or changing the periodic phase anywhere along the visible route.
        if (travel > length + spacing) travel = length + Mathf.Repeat(travel - length, spacing);
        return travel;
    }

    private static float BuildFlowStrip(IReadOnlyList<Vector2> points, float width, List<Vector2> positions,
        List<Vector2> uv, List<int> indices)
    {
        positions.Clear(); uv.Clear(); indices.Clear();
        if (points == null || points.Count < 2 || width <= 0f) return 0f;
        var clean = new List<Vector2>(points.Count);
        foreach (Vector2 point in points)
            if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude > .0000000001f) clean.Add(point);
        if (clean.Count < 2) return 0f;
        float distance = 0f;
        for (int i = 0; i < clean.Count; i++)
        {
            if (i > 0) distance += Vector2.Distance(clean[i-1], clean[i]);
            Vector2 incoming = i > 0 ? (clean[i]-clean[i-1]).normalized : (clean[1]-clean[0]).normalized;
            Vector2 outgoing = i+1 < clean.Count ? (clean[i+1]-clean[i]).normalized : incoming;
            Vector2 a = new Vector2(-incoming.y, incoming.x), b = new Vector2(-outgoing.y, outgoing.x);
            float denominator = 1f + Vector2.Dot(a, b);
            float half = width*.5f;
            // Very short connector jogs must not invert adjacent miter triangles.
            if (i > 0 && i+1 < clean.Count && Vector2.Dot(incoming, outgoing) < .9999f)
                half = Mathf.Min(half, .45f * Mathf.Min(Vector2.Distance(clean[i-1], clean[i]), Vector2.Distance(clean[i], clean[i+1])));
            Vector2 offset = denominator > .001f ? (a+b) * (half / denominator) : b * half;
            // Shared mitered vertices join the horizontal/vertical pieces without
            // a diagonal shortcut or doubled additive brightness at each elbow.
            positions.Add(clean[i]-offset); positions.Add(clean[i]+offset);
            uv.Add(new Vector2(distance, 0f)); uv.Add(new Vector2(distance, 1f));
            if (i == 0) continue;
            int first = (i-1)*2;
            indices.Add(first); indices.Add(first+1); indices.Add(first+3);
            indices.Add(first); indices.Add(first+3); indices.Add(first+2);
        }
        return distance;
    }

    private void RefreshCoreColor()
    {
        if (mesh == null || vertexColors.Count == 0) return;
        bool hasPower = socket != null && socket.HasPower;
        if (hasPower == lastHasPower) return;
        lastHasPower = hasPower;
        Color color = hasPower ? PoweredCoreColor : UnpoweredCoreColor;
        for (int i = coreVertexStart; i < vertexColors.Count; i++) vertexColors[i] = color;
        mesh.SetColors(vertexColors);
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
    private void OnDisable() { ActiveCables.Remove(this); HasRoute = false; StopCurrentFlow(); if (generated != null) generated.SetActive(false); }
    private void OnDestroy()
    {
        ActiveCables.Remove(this); Release(generated); Release(mesh); Release(material);
        Release(currentFlowMesh); Release(currentFlowMaterial);
    }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
