using UnityEngine;
using UnityEngine.UI;

// Static wall geometry is independent of blinking/moving markers. Panning only
// translates its cached mesh; zoom/resize/bridge changes rebuild it once.
public sealed partial class RuntimeMiniMapGraphic
{
    private RuntimeMiniMapLayerGraphic wallLayer, markerLayer;
    private int wallGeometryVersion;
    private int displayedWallGeometryVersion = -1;
    private float wallScale = -1f;
    private Rect lastMapRect;
    private bool hasMapRect;

    protected override void OnPopulateMesh(VertexHelper vh) { vh.Clear(); }

    private void EnsureRenderLayers()
    {
        if (surfaceLayer == null)
        {
            surfaceLayer = CreateLayer("Cached Map Surfaces", false);
            surfaceLayer.DrawSurfaces = true;
        }
        if (wallLayer == null) wallLayer = CreateLayer("Cached Map Walls", true);
        if (markerLayer == null) markerLayer = CreateLayer("Dynamic Map Markers", false);
        wallLayer.transform.SetAsFirstSibling();
        surfaceLayer.transform.SetAsFirstSibling();
        markerLayer.transform.SetSiblingIndex(wallLayer.transform.GetSiblingIndex() + 1);
        SyncRenderLayers();
    }

    private RuntimeMiniMapLayerGraphic CreateLayer(string label, bool walls)
    {
        var existing = transform.Find(label);
        var go = existing != null && existing.GetComponent<RuntimeMiniMapLayerGraphic>() != null
            ? existing.gameObject : new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RuntimeMiniMapLayerGraphic));
        go.hideFlags = HideFlags.DontSave;
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var layer = go.GetComponent<RuntimeMiniMapLayerGraphic>();
        layer.Owner = this;
        layer.DrawWalls = walls;
        layer.raycastTarget = false;
        layer.rectTransform.anchorMin = layer.rectTransform.anchorMax = new Vector2(.5f, .5f);
        layer.rectTransform.pivot = new Vector2(.5f, .5f);
        return layer;
    }

    protected override void OnDestroy()
    {
        if (surfaceLayer != null) { if (Application.isPlaying) Destroy(surfaceLayer.gameObject); else DestroyImmediate(surfaceLayer.gameObject); }
        if (wallLayer != null) { if (Application.isPlaying) Destroy(wallLayer.gameObject); else DestroyImmediate(wallLayer.gameObject); }
        if (markerLayer != null) { if (Application.isPlaying) Destroy(markerLayer.gameObject); else DestroyImmediate(markerLayer.gameObject); }
        base.OnDestroy();
    }

    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();
        if (markerLayer != null) markerLayer.SetVerticesDirty();
        SyncRenderLayers();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        Rect current = rectTransform.rect;
        if (hasMapRect && current.Equals(lastMapRect)) return;
        lastMapRect = current;
        hasMapRect = true;
        base.OnRectTransformDimensionsChange();
        SyncRenderLayers();
    }

    private void SyncRenderLayers()
    {
        if (wallLayer == null || markerLayer == null) return;
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width * .9f / Mathf.Max(.01f, mapBounds.size.x),
            rect.height * .9f / Mathf.Max(.01f, mapBounds.size.y)) * zoom;
        Vector2 center = new Vector2(mapBounds.min.x + mapBounds.size.x * viewCenterNormalized.x,
            mapBounds.min.y + mapBounds.size.y * viewCenterNormalized.y);
        // Children are anchored at the parent's rect centre; marker coordinates
        // retain the parent's pivot convention, including non-centred pivots.
        markerLayer.rectTransform.localPosition = Vector3.zero;
        markerLayer.rectTransform.sizeDelta = rect.size;
        markerLayer.rectTransform.pivot = rectTransform.pivot;
        wallLayer.rectTransform.localPosition = (Vector3)(rect.center + ((Vector2)mapBounds.center - center) * scale);
        wallLayer.rectTransform.sizeDelta = (Vector2)mapBounds.size * scale + Vector2.one * 8f;
        wallLayer.enabled = markerLayer.enabled = enabled && hasBounds;
        wallLayer.material = markerLayer.material = material;
        wallLayer.maskable = markerLayer.maskable = maskable;
        if (surfaceLayer != null)
        {
            surfaceLayer.rectTransform.localPosition = wallLayer.rectTransform.localPosition;
            surfaceLayer.rectTransform.sizeDelta = wallLayer.rectTransform.sizeDelta;
            surfaceLayer.enabled = enabled && hasBounds && surfaces.Count > 0;
            surfaceLayer.material = material;
            surfaceLayer.maskable = maskable;
            if (wallScale != scale || displayedSurfaceGeometryVersion != surfaceGeometryVersion)
            {
                displayedSurfaceGeometryVersion = surfaceGeometryVersion;
                surfaceLayer.SetVerticesDirty();
            }
        }
        if (wallScale != scale || displayedWallGeometryVersion != wallGeometryVersion)
        {
            wallScale = scale;
            displayedWallGeometryVersion = wallGeometryVersion;
            wallLayer.SetVerticesDirty();
        }
    }

    internal void PopulateWalls(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!hasBounds) return;
        float scale = wallScale;
        Vector2 rectCenter = Vector2.zero;
        Vector2 worldCenter = mapBounds.center;
        Color32 blockColor = Color.white;
        const int maximumFilledWallTriangles = 20000;
        int filledTriangleCount = Mathf.Min(
            filledWallTriangles.Count / 3,
            maximumFilledWallTriangles);
        for (int triangleIndex = 0;
             triangleIndex < filledTriangleCount;
             triangleIndex++)
        {
            int vertexIndex = triangleIndex * 3;
            AddSolidTriangle(
                vertexHelper,
                MapPoint(
                    filledWallTriangles[vertexIndex],
                    worldCenter,
                    rectCenter,
                    scale),
                MapPoint(
                    filledWallTriangles[vertexIndex + 1],
                    worldCenter,
                    rectCenter,
                    scale),
                MapPoint(
                    filledWallTriangles[vertexIndex + 2],
                    worldCenter,
                    rectCenter,
                    scale),
                blockColor);
        }

        const int maximumBlockSegments = 10000;
        int renderedBlockSegments = 0;
        for (int pathIndex = 0; pathIndex < blockPaths.Count; pathIndex++)
        {
            Vector2[] path = blockPaths[pathIndex];
            if (IsClosedPath(path))
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < path.Length - 1; pointIndex++)
            {
                if (renderedBlockSegments >= maximumBlockSegments)
                {
                    break;
                }
                AddLine(
                    vertexHelper,
                    MapPoint(path[pointIndex], worldCenter, rectCenter, scale),
                    MapPoint(path[pointIndex + 1], worldCenter, rectCenter, scale),
                    2.2f,
                    blockColor);
                renderedBlockSegments++;
            }

            if (renderedBlockSegments >= maximumBlockSegments)
            {
                break;
            }
        }

    }
}
