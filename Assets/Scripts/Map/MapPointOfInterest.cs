using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MapPointIconShape
{
    Diamond,
    Circle,
    Square,
    Star,
    Exclamation,
    Lever,
    LockedDoor,
    ElectricGate,
    Cauldron,
    BatterySocket,
    WaterwayGate,
    WaterwayController,
    Ballista,
    BreakableGlass,
    ElectricField,
    SceneTransition,
    KeyLockedDoor,
    PasswordLockedDoor
}

[Serializable]
public sealed class MapPointOfInterestRecord
{
    [SerializeField] private string id;
    [SerializeField] private string sceneName;
    [SerializeField] private string title;
    [SerializeField] private string description;
    [SerializeField] private Vector2 worldPosition;
    [SerializeField] private MapPointIconShape iconShape;
    [SerializeField] private Color iconColor;
    [SerializeField] private bool visibleOnMap;
    [SerializeField] private bool discovered;
    [SerializeField] private bool marked;
    [SerializeField] private Vector2 lineStart;
    [SerializeField] private Vector2 lineEnd;

    public string Id => id;
    public string SceneName => sceneName;
    public string Title => title;
    public string Description => description;
    public Vector2 WorldPosition => worldPosition;
    public MapPointIconShape IconShape => iconShape;
    public Color IconColor => iconColor;
    public bool VisibleOnMap => visibleOnMap;
    public bool Discovered => discovered;
    public bool CanBeMarked => !IsHoverOnly(iconShape);
    public bool IsLineMarker => iconShape == MapPointIconShape.ElectricField;
    public static bool IsDoorIcon(MapPointIconShape shape) => shape == MapPointIconShape.LockedDoor ||
        shape == MapPointIconShape.KeyLockedDoor || shape == MapPointIconShape.PasswordLockedDoor;
    public static bool IsHoverOnly(MapPointIconShape shape) => shape >= MapPointIconShape.Lever;
    public Vector2 LineStart => lineStart;
    public Vector2 LineEnd => lineEnd;

    internal bool ApplyLockedDoor(string pointId, string scene, Vector2 start, Vector2 end,
        Color tint, bool lockedAndIntact, bool seen, bool passwordRequired, string requiredKeyName)
    {
        MapPointIconShape nextShape = passwordRequired ? MapPointIconShape.PasswordLockedDoor : MapPointIconShape.KeyLockedDoor;
        // Store the cached key name in the existing serialized description, not a live item reference.
        string nextDescription = passwordRequired ? string.Empty : requiredKeyName ?? string.Empty;
        bool nextDiscovered = discovered || seen;
        bool changed = id != pointId || sceneName != scene || iconShape != nextShape ||
            lineStart != start || lineEnd != end || iconColor != tint ||
            visibleOnMap != lockedAndIntact || discovered != nextDiscovered || description != nextDescription;
        id = pointId; sceneName = scene; title = passwordRequired ? "锁住的门（密码）" : "锁住的门（钥匙）"; description = nextDescription;
        iconShape = nextShape; iconColor = tint;
        lineStart = start; lineEnd = end; worldPosition = (start + end) * .5f;
        visibleOnMap = lockedAndIntact; discovered = nextDiscovered; marked = false;
        return changed;
    }
    // Ignore legacy lever marks from saves made before levers became hover-only.
    public bool Marked => CanBeMarked && marked;
    public string TooltipText => iconShape == MapPointIconShape.Lever
        ? "拉杆"
        : iconShape == MapPointIconShape.LockedDoor ? "锁住的门"
        : iconShape == MapPointIconShape.KeyLockedDoor ? title + "\n需要：" +
            (string.IsNullOrWhiteSpace(description) ? "对应钥匙" : description)
        : !CanBeMarked ? title
        : string.IsNullOrWhiteSpace(description) ? title : title + "\n" + description;

    internal void Apply(MapPointOfInterest source)
    {
        id = source.PointId;
        sceneName = source.SceneName;
        title = source.Title;
        description = source.Description;
        worldPosition = source.MapWorldPosition;
        iconShape = source.IconShape;
        iconColor = source.IconColor;
        visibleOnMap = source.VisibleOnMap;
    }

    internal void SetDiscovered(bool value) => discovered = value;
    internal void SetMarked(bool value) => marked = value;
    internal void SetVisible(bool value) => visibleOnMap = value;

    internal bool ApplyElectricField(string pointId, string scene, Vector2 start, Vector2 end, bool powered, bool seen)
    {
        bool nextDiscovered = discovered || seen;
        bool changed = id != pointId || sceneName != scene || lineStart != start || lineEnd != end ||
            visibleOnMap != powered || discovered != nextDiscovered;
        id = pointId; sceneName = scene; title = "电流区"; description = "";
        iconShape = MapPointIconShape.ElectricField; iconColor = new Color32(65, 175, 235, 210);
        lineStart = start; lineEnd = end; worldPosition = (start + end) * .5f;
        visibleOnMap = powered; discovered = nextDiscovered; marked = false;
        return changed;
    }

    internal void SetInformation(
        string configuredTitle,
        string configuredDescription,
        MapPointIconShape configuredShape,
        Color configuredColor)
    {
        title = configuredTitle ?? string.Empty;
        description = configuredDescription ?? string.Empty;
        iconShape = configuredShape;
        iconColor = configuredColor;
    }
}

[DefaultExecutionOrder(-450)]
public sealed class MapPointOfInterestManager : MonoBehaviour
{
    public List<MapPointOfInterestRecord> CaptureSaveState() => new List<MapPointOfInterestRecord>(points.Values);
    public void ApplySaveState(List<MapPointOfInterestRecord> records)
    {
        points.Clear();
        if (records != null) foreach (var record in records) points[record.Id] = record;
        PointsChanged?.Invoke();
    }
    private readonly Dictionary<string, MapPointOfInterestRecord> points =
        new Dictionary<string, MapPointOfInterestRecord>();

    public static MapPointOfInterestManager Instance { get; private set; }
    public event Action PointsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCreatedBeforeSceneLoad()
    {
        GetOrCreate();
    }

    public static MapPointOfInterestManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        return new GameObject("Map Point Of Interest Manager")
            .AddComponent<MapPointOfInterestManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Register(MapPointOfInterest source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.PointId))
        {
            return;
        }

        // A revisited scene instantiates its authored glass before the travel
        // snapshot removes destroyed objects. Never resurrect its saved marker
        // during this brief registration window.
        bool keepBrokenGlassHidden = source.IconShape == MapPointIconShape.BreakableGlass &&
            points.TryGetValue(source.PointId, out var previous) && !previous.VisibleOnMap;
        MapPointOfInterestRecord record = GetOrCreateRecord(source.PointId);
        bool wasDiscovered = record.Discovered;
        record.Apply(source);
        if (keepBrokenGlassHidden) record.SetVisible(false);
        record.SetDiscovered(wasDiscovered);
        if (wasDiscovered)
        {
            PointsChanged?.Invoke();
        }
    }

    public bool IsDiscovered(string pointId)
    {
        return !string.IsNullOrWhiteSpace(pointId) &&
               points.TryGetValue(pointId, out MapPointOfInterestRecord record) &&
               record.Discovered;
    }

    // Shares the normal POI save payload. Do not create records for unseen doors.
    public void UpdateLockedDoor(string pointId, string scene, Vector2 start, Vector2 end,
        Color tint, bool lockedAndIntact, bool seen, bool passwordRequired = false, string requiredKeyName = null)
    {
        if (string.IsNullOrEmpty(pointId)) return;
        if (!points.TryGetValue(pointId, out MapPointOfInterestRecord record))
        {
            if (!lockedAndIntact || !seen) return;
            record = GetOrCreateRecord(pointId);
        }
        if (record.ApplyLockedDoor(pointId, scene, start, end, tint, lockedAndIntact, seen, passwordRequired, requiredKeyName))
            PointsChanged?.Invoke();
    }

    public void CopyVisibleLockedDoors(string scene, List<MapPointOfInterestRecord> destination)
    {
        destination.Clear();
        foreach (var record in points.Values)
            if (MapPointOfInterestRecord.IsDoorIcon(record.IconShape) && record.Discovered &&
                record.VisibleOnMap && record.SceneName == scene) destination.Add(record);
    }

    public void CopyVisiblePoints(string scene, List<MapPointOfInterestRecord> destination)
    {
        destination.Clear();
        foreach (var record in points.Values)
            if (record.Discovered && record.VisibleOnMap && record.SceneName == scene) destination.Add(record);
    }

    public void UpdateElectricField(string pointId, string scene, Vector2 start, Vector2 end, bool powered, bool seen)
    {
        if (string.IsNullOrEmpty(pointId)) return;
        if (!points.TryGetValue(pointId, out var record))
        {
            if (!powered || !seen) return;
            record = GetOrCreateRecord(pointId);
        }
        if (record.ApplyElectricField(pointId, scene, start, end, powered, seen)) PointsChanged?.Invoke();
    }

    public void Discover(MapPointOfInterest source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.PointId))
        {
            return;
        }

        MapPointOfInterestRecord record = GetOrCreateRecord(source.PointId);
        bool changed = !record.Discovered;
        record.Apply(source);
        record.SetDiscovered(true);
        if (changed)
        {
            PointsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Event-facing API for changing an already registered point at runtime.
    /// It can update its text, geometric icon and color without rediscovery.
    /// </summary>
    public bool UpdatePoint(
        string pointId,
        string title,
        string description,
        MapPointIconShape iconShape,
        Color iconColor)
    {
        if (!points.TryGetValue(pointId, out MapPointOfInterestRecord record))
        {
            return false;
        }

        record.SetInformation(title, description, iconShape, iconColor);
        if (record.Discovered)
        {
            PointsChanged?.Invoke();
        }
        return true;
    }

    public bool SetPointVisible(string pointId, bool visible)
    {
        if (!points.TryGetValue(pointId, out MapPointOfInterestRecord record))
        {
            return false;
        }

        if (record.VisibleOnMap == visible) return true;
        record.SetVisible(visible);
        if (record.Discovered)
        {
            PointsChanged?.Invoke();
        }
        return true;
    }

    public bool ToggleMarked(string pointId)
    {
        if (!points.TryGetValue(pointId, out MapPointOfInterestRecord record) ||
            !record.Discovered || !record.CanBeMarked)
        {
            return false;
        }

        record.SetMarked(!record.Marked);
        PointsChanged?.Invoke();
        return record.Marked;
    }

    public bool TryGetPoint(
        string pointId,
        out MapPointOfInterestRecord record)
    {
        return points.TryGetValue(pointId, out record) && record.Discovered;
    }

    public List<MapPointOfInterestRecord> GetDiscoveredPoints(string sceneName)
    {
        List<MapPointOfInterestRecord> result =
            new List<MapPointOfInterestRecord>();
        foreach (MapPointOfInterestRecord record in points.Values)
        {
            if (record.Discovered &&
                record.VisibleOnMap &&
                string.Equals(
                    record.SceneName,
                    sceneName,
                    StringComparison.Ordinal))
            {
                result.Add(record);
            }
        }
        return result;
    }

    public void ResetAllPoints()
    {
        if (points.Count == 0)
        {
            return;
        }

        points.Clear();
        PointsChanged?.Invoke();
    }

    private MapPointOfInterestRecord GetOrCreateRecord(string pointId)
    {
        if (!points.TryGetValue(pointId, out MapPointOfInterestRecord record))
        {
            record = new MapPointOfInterestRecord();
            points.Add(pointId, record);
        }
        return record;
    }
}

public sealed partial class MapPointOfInterest : MonoBehaviour
{
    [SerializeField, Tooltip("Stable ID used to preserve discovery across scenes.")]
    private string pointId;
    [SerializeField] private string pointTitle = "兴趣点";
    [SerializeField, TextArea(2, 5)] private string description;
    [SerializeField] private MapPointIconShape iconShape =
        MapPointIconShape.Diamond;
    [SerializeField] private Color iconColor =
        new Color32(65, 175, 235, 255);
    [SerializeField] private bool visibleOnMap = true;
    [SerializeField, Min(0.05f)] private float visibilityCheckInterval = 0.15f;
    [SerializeField, Range(0f, 0.25f)] private float viewportPadding;

    private float nextVisibilityCheckTime;
    private string resolvedPointId;
    private LeverData lever;
    private SpriteRenderer discoveryRenderer;

    public string PointId => string.IsNullOrEmpty(resolvedPointId)
        ? ResolvePointId()
        : resolvedPointId;
    public string SceneName => gameObject.scene.name;
    public string Title => string.IsNullOrWhiteSpace(pointTitle)
        ? gameObject.name
        : pointTitle.Trim();
    public string Description => description ?? string.Empty;
    public MapPointIconShape IconShape => iconShape;
    public Color IconColor => iconColor;
    public bool VisibleOnMap => visibleOnMap;

    private void Awake()
    {
        lever = GetComponent<LeverData>();
        if (lever != null)
        {
            pointTitle = "拉杆";
            description = "用于控制与其相连的机关。";
            iconShape = MapPointIconShape.Lever;
            visibleOnMap = true;
            discoveryRenderer = GetComponent<SpriteRenderer>();
        }
        ConfigureMechanism();
        resolvedPointId = ResolvePointId();
        MapPointOfInterestManager.GetOrCreate().Register(this);
    }

    private void OnEnable()
    {
        nextVisibilityCheckTime = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextVisibilityCheckTime) return;
        nextVisibilityCheckTime = Time.unscaledTime + visibilityCheckInterval;
        MapPointOfInterestManager manager =
            MapPointOfInterestManager.GetOrCreate();
        if (!visibleOnMap || manager.IsDiscovered(PointId))
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 viewport = camera.WorldToViewportPoint(MapWorldPosition);
        if ((isMechanism ? IsMechanismInViewport(camera) : viewport.z > 0f &&
            viewport.x >= -viewportPadding &&
            viewport.x <= 1f + viewportPadding &&
            viewport.y >= -viewportPadding &&
            viewport.y <= 1f + viewportPadding) &&
            IsVisibleToPlayer(camera))
        {
            manager.Discover(this);
        }
    }

    private bool IsVisibleToPlayer(Camera camera)
    {
        // Keep authored POI behavior, but levers require actual player vision,
        // not merely a position inside the camera rectangle behind a wall.
        if (lever == null && !isMechanism) return true;
        if (GameSaveSystem.IsLoading) return false;
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene)
            return false;

        CameraCircularVision vision = camera.GetComponent<CameraCircularVision>();
        // A minimal test scene without a vision mask exposes its whole viewport.
        if (vision == null || !vision.isActiveAndEnabled) return true;
        if (isMechanism) return vision.IsWorldBoundsVisible(MechanismBounds, transform);
        return discoveryRenderer != null && discoveryRenderer.sprite != null
            ? vision.IsWorldBoundsVisible(discoveryRenderer.bounds, transform)
            : vision.IsWorldPositionVisible(transform.position, transform);
    }

    public void ForceDiscover()
    {
        MapPointOfInterestManager.GetOrCreate().Discover(this);
    }

    public void ChangeMapAppearance(MapPointIconShape shape, Color color)
    {
        iconShape = shape;
        iconColor = color;
        PushRuntimeChanges();
    }

    public void ChangeInformation(string title, string details)
    {
        pointTitle = title;
        description = details;
        PushRuntimeChanges();
    }

    public void SetVisibleOnMap(bool visible)
    {
        visibleOnMap = visible;
        MapPointOfInterestManager.GetOrCreate()
            .SetPointVisible(PointId, visible);
    }

    private void PushRuntimeChanges()
    {
        MapPointOfInterestManager.GetOrCreate().UpdatePoint(
            PointId,
            Title,
            Description,
            iconShape,
            iconColor);
    }

    private string BuildFallbackId()
    {
        Vector3 position = transform.position;
        return gameObject.scene.name + ":" + gameObject.name + ":" +
               position.x.ToString("0.###") + ":" +
               position.y.ToString("0.###");
    }

    private string ResolvePointId()
    {
        if (lever != null)
            return gameObject.scene.name + ":lever:" + SceneTravelStateManager.GetMapObjectId(transform);
        if (isMechanism)
            return gameObject.scene.name + ":mechanism:" + SceneTravelStateManager.GetMapObjectId(transform);
        return string.IsNullOrWhiteSpace(pointId)
            ? BuildFallbackId()
            : pointId.Trim();
    }

    private void OnValidate()
    {
        visibilityCheckInterval = Mathf.Max(0.05f, visibilityCheckInterval);
    }
}

public sealed class MapPointOfInterestUiInteraction : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private string pointId;
    private Action<string, RectTransform> entered;
    private Action<string> exited;
    private bool hoverOnly;

    public void Configure(
        string configuredPointId,
        Action<string, RectTransform> pointerEntered,
        Action<string> pointerExited,
        bool configuredHoverOnly = false)
    {
        pointId = configuredPointId;
        entered = pointerEntered;
        exited = pointerExited;
        hoverOnly = configuredHoverOnly;
        SetHovered(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        entered?.Invoke(pointId, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
        exited?.Invoke(pointId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hoverOnly && eventData.button == PointerEventData.InputButton.Left)
        {
            MapPointOfInterestManager.GetOrCreate().ToggleMarked(pointId);
        }
    }

    private void SetHovered(bool value)
    {
        RuntimeMiniMapPointIconGraphic graphic = GetComponent<RuntimeMiniMapPointIconGraphic>();
        if (graphic != null) graphic.SetHovered(hoverOnly && value);
    }

    private void OnDisable()
    {
        SetHovered(false);
        exited?.Invoke(pointId);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed partial class RuntimeMiniMapPointIconGraphic : MaskableGraphic
{
    private sealed class IconGeometry { public UIVertex[] vertices; public int[] indices; }
    private static readonly Dictionary<MapPointIconShape, IconGeometry> IconCache = new Dictionary<MapPointIconShape, IconGeometry>();
    private MapPointIconShape shape;
    private bool marked;
    private bool hovered;

    public void Configure(MapPointIconShape configuredShape, bool isMarked)
    {
        shape = configuredShape;
        marked = !MapPointOfInterestRecord.IsHoverOnly(configuredShape) && isMarked;
        hovered = false;
        SetVerticesDirty();
    }

    public void SetHovered(bool value)
    {
        bool next = MapPointOfInterestRecord.IsHoverOnly(shape) && value;
        if (hovered == next) return;
        hovered = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) *
                       (marked ? 0.30f : 0.25f);
        // Enlarge only the drawing, keeping the hover hitbox stable at the edges.
        if (hovered) radius *= 1.3f;

        DrawIcon(vertexHelper, shape, center, radius, color, marked);
    }

    // Shared geometry for the HUD map and the interactive journal map.
    public static void DrawIcon(VertexHelper vertexHelper, MapPointIconShape shape,
        Vector2 center, float radius, Color color, bool marked = false)
    {
        if (!IconCache.TryGetValue(shape, out var geometry))
        {
            using (var builder = new VertexHelper())
            {
                BuildIcon(builder, shape, Vector2.zero, 1f, Color.white);
                geometry = new IconGeometry { vertices = new UIVertex[builder.currentVertCount] };
                for (int i = 0; i < geometry.vertices.Length; i++) builder.PopulateUIVertex(ref geometry.vertices[i], i);
                var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                builder.FillMesh(mesh); geometry.indices = mesh.triangles;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                IconCache.Add(shape, geometry);
            }
        }
        int start = vertexHelper.currentVertCount;
        for (int i = 0; i < geometry.vertices.Length; i++)
        {
            UIVertex v = geometry.vertices[i];
            v.position = (Vector3)center + v.position * radius;
            if (v.color.r == 255 && v.color.g == 255 && v.color.b == 255) v.color = color;
            else { var background = v.color; background.a = (byte)(background.a * color.a); v.color = background; }
            vertexHelper.AddVert(v);
        }
        for (int i = 0; i < geometry.indices.Length; i += 3)
            vertexHelper.AddTriangle(start + geometry.indices[i], start + geometry.indices[i+1], start + geometry.indices[i+2]);
        if (marked) AddFrame(vertexHelper, center, radius * 1.85f, Color.Lerp(color, Color.white, .35f));
    }

    private static void BuildIcon(VertexHelper vertexHelper, MapPointIconShape shape,
        Vector2 center, float radius, Color color)
    {
        Color background = new Color32(15, 33, 60, 245);
        // Exits use the same badge and inset as all other points of interest.
        // Their smaller map footprint is controlled by RuntimeMiniMapGraphic.
        AddDiamond(vertexHelper, center, radius * 1.6f, color);
        AddDiamond(vertexHelper, center, radius * 1.38f, background);
        // Keep the door-leaf tint on the badge rim, but keep key/keypad symbols
        // cyan so even dark-coloured doors have readable lock information.
        Color glyphColor = MapPointOfInterestRecord.IsDoorIcon(shape) ? (Color)new Color32(91,211,241,255) : color;
        if (DrawMechanismGlyph(vertexHelper, shape, center, radius * .72f, glyphColor)) return;

        switch (shape)
        {
            case MapPointIconShape.Circle:
                AddFan(vertexHelper, center, radius * .8f, 18, color, 0f);
                break;
            case MapPointIconShape.Square:
                AddQuad(vertexHelper, center, radius * .65f, color);
                break;
            case MapPointIconShape.Star:
                AddStar(vertexHelper, center, radius, color);
                break;
            case MapPointIconShape.Exclamation:
                AddQuad(vertexHelper, center + new Vector2(0f, radius * .35f),
                    new Vector2(radius * 0.2f, radius * .6f), color);
                AddFan(vertexHelper, center + new Vector2(0f, -radius * .75f),
                    radius * 0.2f, 10, color, 0f);
                break;
            default:
                AddDiamond(vertexHelper, center, radius * 1.15f, color);
                break;
        }

    }

    private static void AddDiamond(
        VertexHelper helper,
        Vector2 center,
        float radius,
        Color color)
    {
        AddPolygon(helper, center, new[]
        {
            new Vector2(0f, radius), new Vector2(radius, 0f),
            new Vector2(0f, -radius), new Vector2(-radius, 0f)
        }, color);
    }

    private static void AddQuad(
        VertexHelper helper,
        Vector2 center,
        float radius,
        Color color)
    {
        AddQuad(helper, center, new Vector2(radius, radius), color);
    }

    private static void AddQuad(
        VertexHelper helper,
        Vector2 center,
        Vector2 halfSize,
        Color color)
    {
        AddPolygon(helper, center, new[]
        {
            new Vector2(-halfSize.x, -halfSize.y),
            new Vector2(-halfSize.x, halfSize.y),
            new Vector2(halfSize.x, halfSize.y),
            new Vector2(halfSize.x, -halfSize.y)
        }, color);
    }

    private static void AddFan(
        VertexHelper helper,
        Vector2 center,
        float radius,
        int segments,
        Color color,
        float angleOffset)
    {
        Vector2[] points = new Vector2[segments];
        for (int index = 0; index < segments; index++)
        {
            float angle = angleOffset + index * Mathf.PI * 2f / segments;
            points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        AddPolygon(helper, center, points, color);
    }

    private static void AddStar(
        VertexHelper helper,
        Vector2 center,
        float radius,
        Color color)
    {
        Vector2[] points = new Vector2[10];
        for (int index = 0; index < points.Length; index++)
        {
            float pointRadius = index % 2 == 0 ? radius : radius * 0.44f;
            float angle = Mathf.PI * 0.5f + index * Mathf.PI * 2f / points.Length;
            points[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;
        }
        AddPolygon(helper, center, points, color);
    }

    private static void AddPolygon(
        VertexHelper helper,
        Vector2 center,
        Vector2[] points,
        Color color)
    {
        int start = helper.currentVertCount;
        AddVertex(helper, center, color);
        for (int index = 0; index < points.Length; index++)
        {
            AddVertex(helper, center + points[index], color);
        }
        for (int index = 0; index < points.Length; index++)
        {
            helper.AddTriangle(
                start,
                start + 1 + index,
                start + 1 + (index + 1) % points.Length);
        }
    }

    private static void AddFrame(
        VertexHelper helper,
        Vector2 center,
        float radius,
        Color color)
    {
        const float thickness = 1.5f;
        AddQuad(helper, center + new Vector2(0f, radius),
            new Vector2(radius, thickness * 0.5f), color);
        AddQuad(helper, center + new Vector2(0f, -radius),
            new Vector2(radius, thickness * 0.5f), color);
        AddQuad(helper, center + new Vector2(radius, 0f),
            new Vector2(thickness * 0.5f, radius), color);
        AddQuad(helper, center + new Vector2(-radius, 0f),
            new Vector2(thickness * 0.5f, radius), color);
    }

    private static void AddVertex(
        VertexHelper helper,
        Vector2 position,
        Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        helper.AddVert(vertex);
    }
}
