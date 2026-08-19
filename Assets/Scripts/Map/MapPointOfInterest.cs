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
    Exclamation
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

    public string Id => id;
    public string SceneName => sceneName;
    public string Title => title;
    public string Description => description;
    public Vector2 WorldPosition => worldPosition;
    public MapPointIconShape IconShape => iconShape;
    public Color IconColor => iconColor;
    public bool VisibleOnMap => visibleOnMap;
    public bool Discovered => discovered;
    public bool Marked => marked;

    internal void Apply(MapPointOfInterest source)
    {
        id = source.PointId;
        sceneName = source.SceneName;
        title = source.Title;
        description = source.Description;
        worldPosition = source.transform.position;
        iconShape = source.IconShape;
        iconColor = source.IconColor;
        visibleOnMap = source.VisibleOnMap;
    }

    internal void SetDiscovered(bool value) => discovered = value;
    internal void SetMarked(bool value) => marked = value;
    internal void SetVisible(bool value) => visibleOnMap = value;

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

        MapPointOfInterestRecord record = GetOrCreateRecord(source.PointId);
        bool wasDiscovered = record.Discovered;
        record.Apply(source);
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
            !record.Discovered)
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

public sealed class MapPointOfInterest : MonoBehaviour
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
        resolvedPointId = ResolvePointId();
        MapPointOfInterestManager.GetOrCreate().Register(this);
    }

    private void OnEnable()
    {
        nextVisibilityCheckTime = 0f;
    }

    private void Update()
    {
        MapPointOfInterestManager manager =
            MapPointOfInterestManager.GetOrCreate();
        if (manager.IsDiscovered(PointId) ||
            Time.unscaledTime < nextVisibilityCheckTime)
        {
            return;
        }

        nextVisibilityCheckTime =
            Time.unscaledTime + visibilityCheckInterval;
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 viewport = camera.WorldToViewportPoint(transform.position);
        if (viewport.z > 0f &&
            viewport.x >= -viewportPadding &&
            viewport.x <= 1f + viewportPadding &&
            viewport.y >= -viewportPadding &&
            viewport.y <= 1f + viewportPadding)
        {
            manager.Discover(this);
        }
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

    public void Configure(
        string configuredPointId,
        Action<string, RectTransform> pointerEntered,
        Action<string> pointerExited)
    {
        pointId = configuredPointId;
        entered = pointerEntered;
        exited = pointerExited;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        entered?.Invoke(pointId, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        exited?.Invoke(pointId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            MapPointOfInterestManager.GetOrCreate().ToggleMarked(pointId);
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RuntimeMiniMapPointIconGraphic : MaskableGraphic
{
    private MapPointIconShape shape;
    private bool marked;

    public void Configure(MapPointIconShape configuredShape, bool isMarked)
    {
        shape = configuredShape;
        marked = isMarked;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) *
                       (marked ? 0.30f : 0.25f);

        switch (shape)
        {
            case MapPointIconShape.Circle:
                AddFan(vertexHelper, center, radius, 18, color, 0f);
                break;
            case MapPointIconShape.Square:
                AddQuad(vertexHelper, center, radius, color);
                break;
            case MapPointIconShape.Star:
                AddStar(vertexHelper, center, radius * 1.25f, color);
                break;
            case MapPointIconShape.Exclamation:
                AddQuad(vertexHelper, center + new Vector2(0f, 2f),
                    new Vector2(radius * 0.28f, radius), color);
                AddFan(vertexHelper, center + new Vector2(0f, -radius * 1.35f),
                    radius * 0.26f, 10, color, 0f);
                break;
            default:
                AddDiamond(vertexHelper, center, radius * 1.15f, color);
                break;
        }

        if (marked)
        {
            Color frameColor = Color.Lerp(color, Color.white, 0.35f);
            float frameRadius = Mathf.Min(rect.width, rect.height) * 0.47f;
            AddFrame(vertexHelper, center, frameRadius, frameColor);
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
