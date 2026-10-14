using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class RuntimeMiniMapGraphic
{
    private readonly List<RuntimeMiniMapSurfaceData> surfaces = new List<RuntimeMiniMapSurfaceData>();
    private readonly List<WaterwayGateTargetFade> surfaceFaders = new List<WaterwayGateTargetFade>();
    private readonly List<float> surfaceOpacities = new List<float>();
    private readonly List<Vector2[]> surfaceTriangles = new List<Vector2[]>();
    private float nextSurfaceRefresh;
    private int surfaceGeometryVersion, displayedSurfaceGeometryVersion = -1;
    private RuntimeMiniMapLayerGraphic surfaceLayer;

    private void ClearSurfaces()
    {
        surfaces.Clear(); surfaceFaders.Clear(); surfaceOpacities.Clear(); surfaceTriangles.Clear();
        surfaceGeometryVersion++;
        nextSurfaceRefresh = 0f;
    }

    private void CaptureSurfaces(Scene scene)
    {
        ClearSurfaces();
        var captured = new HashSet<SpriteRenderer>();
        // Explicit opt-in keeps furniture, fountains, particles and other levels unchanged.
        foreach (var root in scene.GetRootGameObjects())
        foreach (var group in root.GetComponentsInChildren<MiniMapSurfaceGroup>(true))
        {
            if (!group.enabled) continue;
            foreach (var sprite in group.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.sprite == null || sprite.gameObject.hideFlags != HideFlags.None ||
                    sprite.GetComponentInParent<MiniMapSurfaceGroup>(true) != group || !captured.Add(sprite)) continue;
                Bounds bounds = sprite.sprite.bounds;
                Vector2 size = sprite.drawMode == SpriteDrawMode.Simple ? (Vector2)bounds.size : sprite.size;
                Vector2 center = bounds.center;
                if (sprite.drawMode != SpriteDrawMode.Simple)
                    center = Vector2.Scale(center, new Vector2(size.x / bounds.size.x, size.y / bounds.size.y));
                if (sprite.flipX) center.x = -center.x;
                if (sprite.flipY) center.y = -center.y;
                var fader = sprite.GetComponentInParent<WaterwayGateTargetFade>(true);
                var kind = group.Kind;
                AddSurface(new RuntimeMiniMapSurfaceData {
                    points = RuntimeMiniMapSceneData.CreateBoxPath(sprite.transform.localToWorldMatrix, center, size),
                    kind = kind,
                    visibilityId = fader ? SceneTravelStateManager.GetMapObjectId(fader.transform) : string.Empty,
                    // Ordinary distance streaming must not erase mapped roads/water.
                    initiallyVisible = fader == null || fader.gameObject.activeSelf
                }, fader);
            }
        }
        RefreshSurfaceVisibility(true);
    }

    private void LoadSurfaces(RuntimeMiniMapSceneData snapshot)
    {
        ClearSurfaces();
        if (snapshot != null && snapshot.Surfaces != null)
            foreach (var data in snapshot.Surfaces) AddSurface(data, null);
        RefreshSurfaceVisibility(true);
    }

    private void AddSurface(RuntimeMiniMapSurfaceData data, WaterwayGateTargetFade source)
    {
        if (!IsClosedPath(data.points)) return;
        surfaces.Add(data); surfaceFaders.Add(source);
        surfaceOpacities.Add(data.initiallyVisible ? 1f : 0f); surfaceTriangles.Add(GetCachedTriangles(data.points));
        foreach (var point in data.points) Encapsulate(point);
    }

    private void RefreshSurfaceVisibility(bool force = false)
    {
        if (!force && Time.unscaledTime < nextSurfaceRefresh) return;
        nextSurfaceRefresh = Time.unscaledTime + .1f;
        bool changed = force;
        // Cached references / snapshot dictionary lookups only; no per-frame scene scan.
        for (int i = 0; i < surfaces.Count; i++)
        {
            float opacity = surfaces[i].initiallyVisible ? 1f : 0f;
            if (surfaceFaders[i] != null && Application.isPlaying)
                opacity = surfaceFaders[i].gameObject.activeSelf
                    ? Mathf.SmoothStep(0f, 1f, surfaceFaders[i].Visibility) : 0f;
            else if (!string.IsNullOrEmpty(surfaces[i].visibilityId) && SceneTravelStateManager.Instance != null &&
                SceneTravelStateManager.Instance.TryGetMapObjectActive(geometrySceneName, surfaces[i].visibilityId, out bool active))
                opacity = active ? 1f : 0f;
            if (Mathf.Abs(surfaceOpacities[i] - opacity) < .005f) continue;
            surfaceOpacities[i] = opacity; changed = true;
        }
        if (changed) { surfaceGeometryVersion++; SyncRenderLayers(); }
    }

    internal void PopulateSurfaces(VertexHelper vh)
    {
        vh.Clear();
        if (!hasBounds) return;
        // Opaque, subdued fills prevent bright seams at road intersections.
        // Water is above roads; walls and markers are separate higher siblings.
        for (int pass = 0; pass < 3; pass++)
        for (int i = 0; i < surfaces.Count; i++)
        {
            int kind = pass == 1 ? 2 : pass == 2 ? 1 : 0;
            if ((int)surfaces[i].kind != kind || surfaceOpacities[i] <= 0f) continue;
            Color color = kind == 0 ? (Color)new Color32(45, 60, 78, 255)
                : kind == 1 ? (Color)new Color32(20, 77, 119, 255) : new Color32(29, 42, 59, 255);
            color.a = surfaceOpacities[i];
            var triangles = surfaceTriangles[i];
            for (int p = 0; p < triangles.Length && vh.currentVertCount < 64000; p += 3)
                AddSolidTriangle(vh,
                    MapPoint(triangles[p], mapBounds.center, Vector2.zero, wallScale),
                    MapPoint(triangles[p + 1], mapBounds.center, Vector2.zero, wallScale),
                    MapPoint(triangles[p + 2], mapBounds.center, Vector2.zero, wallScale), color);
        }
    }
}
