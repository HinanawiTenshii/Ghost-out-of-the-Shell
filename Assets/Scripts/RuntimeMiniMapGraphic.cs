using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Draws a proportional wireframe of the active scene's Blocks colliders,
/// transition markers and the current camera position in a single UI mesh.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RuntimeMiniMapGraphic : MaskableGraphic
{
    private const float DefaultZoom = 2.5f;
    private const float MinimumZoom = 1f;
    private const float MaximumZoom = 5f;
    private readonly List<Vector2[]> blockPaths = new List<Vector2[]>();
    private readonly List<Vector2> filledWallTriangles = new List<Vector2>();
    private readonly List<RuntimeMiniMapTransitionData> transitions =
        new List<RuntimeMiniMapTransitionData>();
    private readonly List<RuntimeMiniMapQuestTargetData> questTargets =
        new List<RuntimeMiniMapQuestTargetData>();
    private Bounds mapBounds;
    private Vector2 cameraWorldPosition;
    private bool hasBounds;
    private bool showCameraMarker = true;
    private float iconScale = 1f;
    private float lastMarkerAlpha = -1f;
    private float zoom = DefaultZoom;
    private Vector2 viewCenterNormalized = new Vector2(0.5f, 0.5f);
    private bool showTrackedQuestTarget;
    private bool clampQuestTargetToBorder;
    private Vector2 trackedQuestTargetPosition;

    public float Zoom => zoom;
    public float ZoomPercentage => zoom / DefaultZoom * 100f;
    public IReadOnlyList<RuntimeMiniMapTransitionData> Transitions => transitions;
    public float HorizontalPan01 => GetPan01(viewCenterNormalized.x);
    public float VerticalPan01 => GetPan01(viewCenterNormalized.y);
    public event Action ViewChanged;

    public bool TryGetQuestTarget(string questId, out Vector2 position)
    {
        for (int index = 0; index < questTargets.Count; index++)
        {
            if (questTargets[index].questId == questId)
            {
                position = questTargets[index].position;
                return true;
            }
        }
        position = Vector2.zero;
        return false;
    }

    public void SetTrackedQuestTarget(bool visible, Vector2 worldPosition, bool clampToBorder)
    {
        showTrackedQuestTarget = visible;
        trackedQuestTargetPosition = worldPosition;
        clampQuestTargetToBorder = clampToBorder;
        SetVerticesDirty();
    }

    public void ResetView()
    {
        zoom = DefaultZoom;
        viewCenterNormalized = new Vector2(0.5f, 0.5f);
        NotifyViewChanged();
    }

    public void CenterOnWorldPosition(Vector2 worldPosition)
    {
        if (!hasBounds)
        {
            return;
        }

        viewCenterNormalized = new Vector2(
            Mathf.InverseLerp(mapBounds.min.x, mapBounds.max.x, worldPosition.x),
            Mathf.InverseLerp(mapBounds.min.y, mapBounds.max.y, worldPosition.y));
        ClampViewCenter();
        NotifyViewChanged();
    }

    public void ZoomBy(float amount)
    {
        float nextZoom = Mathf.Clamp(
            zoom + amount,
            MinimumZoom,
            MaximumZoom);
        if (Mathf.Approximately(nextZoom, zoom))
        {
            return;
        }

        zoom = nextZoom;
        ClampViewCenter();
        NotifyViewChanged();
    }

    public void SetZoomToMaximum()
    {
        if (Mathf.Approximately(zoom, MaximumZoom))
        {
            return;
        }

        zoom = MaximumZoom;
        ClampViewCenter();
        NotifyViewChanged();
    }

    public void SetHorizontalPan01(float value)
    {
        viewCenterNormalized.x = Pan01ToCenter(value);
        NotifyViewChanged();
    }

    public void SetVerticalPan01(float value)
    {
        viewCenterNormalized.y = Pan01ToCenter(value);
        NotifyViewChanged();
    }

    public void PanByPixels(Vector2 pointerDelta)
    {
        Rect drawingRect = GetPixelAdjustedRect();
        float worldWidth = Mathf.Max(0.01f, mapBounds.size.x);
        float worldHeight = Mathf.Max(0.01f, mapBounds.size.y);
        float baseScale = Mathf.Min(
            drawingRect.width * 0.9f / worldWidth,
            drawingRect.height * 0.9f / worldHeight);
        float scaled = Mathf.Max(0.001f, baseScale * zoom);
        viewCenterNormalized.x -= pointerDelta.x / (scaled * worldWidth);
        viewCenterNormalized.y -= pointerDelta.y / (scaled * worldHeight);
        ClampViewCenter();
        NotifyViewChanged();
    }

    public bool TryWorldToLocal(Vector2 worldPosition, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (!hasBounds)
        {
            return false;
        }

        Rect drawingRect = GetPixelAdjustedRect();
        float worldWidth = Mathf.Max(0.01f, mapBounds.size.x);
        float worldHeight = Mathf.Max(0.01f, mapBounds.size.y);
        float scale = Mathf.Min(
            drawingRect.width * 0.9f / worldWidth,
            drawingRect.height * 0.9f / worldHeight) * zoom;
        Vector2 worldCenter = new Vector2(
            mapBounds.min.x + mapBounds.size.x * viewCenterNormalized.x,
            mapBounds.min.y + mapBounds.size.y * viewCenterNormalized.y);
        localPosition = MapPoint(
            worldPosition,
            worldCenter,
            drawingRect.center,
            scale);
        return true;
    }

    public void RebuildFromScene(Scene scene)
    {
        blockPaths.Clear();
        transitions.Clear();
        questTargets.Clear();
        hasBounds = false;

        int blocksLayer = LayerMask.NameToLayer("Blocks");
        Collider2D[] colliders = FindObjectsOfType<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider == null || collider.gameObject.scene != scene ||
                collider.gameObject.layer != blocksLayer)
            {
                continue;
            }

            // DoorHingeInteraction lives on the hinge root while the visible,
            // collidable door is commonly a Blocks-layer child. Neither part
            // should become permanent wall geometry on the minimap.
            if (collider.GetComponentInParent<DoorHingeInteraction>(true) != null)
            {
                continue;
            }

            CompositeCollider2D siblingComposite =
                collider.GetComponent<CompositeCollider2D>();
            if (siblingComposite != null && collider != siblingComposite)
            {
                continue;
            }

            AddColliderPaths(collider);
        }

        SceneTransitionPoint[] regularTransitions =
            FindObjectsOfType<SceneTransitionPoint>(true);
        for (int index = 0; index < regularTransitions.Length; index++)
        {
            SceneTransitionPoint point = regularTransitions[index];
            if (point != null && point.gameObject.scene == scene)
            {
                AddTransition(
                    point.transform.position,
                    point.TargetSceneName,
                    false);
            }
        }

        PersistentSceneTransitionPoint[] persistentTransitions =
            FindObjectsOfType<PersistentSceneTransitionPoint>(true);
        for (int index = 0; index < persistentTransitions.Length; index++)
        {
            PersistentSceneTransitionPoint point = persistentTransitions[index];
            if (point != null && point.gameObject.scene == scene)
            {
                AddTransition(
                    point.transform.position,
                    point.TargetSceneName,
                    true);
            }
        }

        CollectKnownQuestTargets(scene);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            SetCameraPosition(mainCamera.transform.position);
        }

        EnsureUsableBounds();
        RebuildFilledWallTriangles();
        SetVerticesDirty();
    }

    public void RebuildFromSnapshot(RuntimeMiniMapSceneData snapshot)
    {
        blockPaths.Clear();
        transitions.Clear();
        questTargets.Clear();
        hasBounds = false;

        if (snapshot != null)
        {
            RuntimeMiniMapPathData[] paths = snapshot.BlockPaths;
            if (paths != null)
            {
                for (int index = 0; index < paths.Length; index++)
                {
                    Vector2[] points = paths[index].points;
                    if (points == null || points.Length < 2)
                    {
                        continue;
                    }

                    Vector2[] copy = (Vector2[])points.Clone();
                    blockPaths.Add(copy);
                    for (int pointIndex = 0; pointIndex < copy.Length; pointIndex++)
                    {
                        Encapsulate(copy[pointIndex]);
                    }
                }
            }

            RuntimeMiniMapTransitionData[] snapshotTransitions =
                snapshot.Transitions;
            if (snapshotTransitions != null)
            {
                for (int index = 0;
                     index < snapshotTransitions.Length;
                     index++)
                {
                    RuntimeMiniMapTransitionData transition =
                        snapshotTransitions[index];
                    AddTransition(
                        transition.position,
                        transition.targetSceneName,
                        transition.preservesSceneState);
                }
            }

            if (snapshot.QuestTargets != null)
            {
                questTargets.AddRange(snapshot.QuestTargets);
            }
        }

        EnsureUsableBounds();
        RebuildFilledWallTriangles();
        SetVerticesDirty();
    }

    public void SetCameraMarkerVisible(bool visible)
    {
        if (showCameraMarker == visible)
        {
            return;
        }

        showCameraMarker = visible;
        SetVerticesDirty();
    }

    public void SetIconScale(float scale)
    {
        float nextScale = Mathf.Clamp(scale, 0.2f, 2f);
        if (Mathf.Approximately(iconScale, nextScale))
        {
            return;
        }

        iconScale = nextScale;
        SetVerticesDirty();
    }

    public void CopyCurrentGeometryTo(
        RuntimeMiniMapSceneData destination,
        string sceneName,
        string displayName)
    {
        if (destination != null)
        {
            destination.Configure(
                sceneName,
                displayName,
                blockPaths,
                transitions,
                questTargets);
        }
    }

    public void SetCameraPosition(Vector2 worldPosition)
    {
        cameraWorldPosition = worldPosition;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        float alpha = Mathf.Lerp(
            0.38f,
            0.95f,
            Mathf.Sin(Time.unscaledTime * 5.2f) * 0.5f + 0.5f);
        if (Mathf.Abs(alpha - lastMarkerAlpha) > 0.015f)
        {
            lastMarkerAlpha = alpha;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!hasBounds)
        {
            return;
        }

        Rect drawingRect = GetPixelAdjustedRect();
        float worldWidth = Mathf.Max(0.01f, mapBounds.size.x);
        float worldHeight = Mathf.Max(0.01f, mapBounds.size.y);
        float scale = Mathf.Min(
            drawingRect.width * 0.9f / worldWidth,
            drawingRect.height * 0.9f / worldHeight);
        scale *= zoom;
        Vector2 rectCenter = drawingRect.center;
        Vector2 worldCenter = new Vector2(
            mapBounds.min.x + mapBounds.size.x * viewCenterNormalized.x,
            mapBounds.min.y + mapBounds.size.y * viewCenterNormalized.y);

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

        for (int index = 0; index < transitions.Count; index++)
        {
            RuntimeMiniMapTransitionData transition = transitions[index];
            Vector2 center = MapPoint(
                transition.position,
                worldCenter,
                rectCenter,
                scale);
            Color iconColor = transition.preservesSceneState
                ? new Color32(65, 175, 235, 255)
                : ZeldaUiPalette.Ghost;
            AddTransitionIcon(vertexHelper, center, iconColor, iconScale);
        }

        if (showCameraMarker)
        {
            Vector2 cameraCenter = MapPoint(
                cameraWorldPosition,
                worldCenter,
                rectCenter,
                scale);
            Color markerColor = new Color(
                0.45f,
                0.82f,
                1f,
                lastMarkerAlpha < 0f ? 0.7f : lastMarkerAlpha);
            AddDiamond(vertexHelper, cameraCenter, 9f * iconScale, markerColor);
        }


        if (showTrackedQuestTarget)
        {
            Vector2 targetCenter = MapPoint(
                trackedQuestTargetPosition,
                worldCenter,
                rectCenter,
                scale);
            if (clampQuestTargetToBorder &&
                !drawingRect.Contains(targetCenter))
            {
                // Intersect the direction from the minimap centre to the
                // off-screen objective with the actual frame. Keeping the
                // marker centre on this intersection makes it sit on the
                // border instead of floating one icon-radius inside it.
                Vector2 direction = targetCenter - drawingRect.center;
                float horizontalFactor = Mathf.Abs(direction.x) > 0.0001f
                    ? drawingRect.width * 0.5f / Mathf.Abs(direction.x)
                    : float.PositiveInfinity;
                float verticalFactor = Mathf.Abs(direction.y) > 0.0001f
                    ? drawingRect.height * 0.5f / Mathf.Abs(direction.y)
                    : float.PositiveInfinity;
                float borderFactor = Mathf.Min(
                    horizontalFactor,
                    verticalFactor);
                targetCenter = drawingRect.center + direction * borderFactor;
            }
            AddQuestTargetMarker(vertexHelper, targetCenter, iconScale);
        }
    }

    private void CollectKnownQuestTargets(Scene scene)
    {
        AddNamedQuestTarget(scene, "level0.escape_prison", "FinalTarget");
        AddNamedQuestTarget(
            scene,
            "level0.obtain_gate_key",
            "GoldenKey",
            new Vector2(0f, 0.72f));
        AddNamedQuestTarget(scene, "level1.escape_castle", "FinalTarget");
        AddNamedQuestTarget(scene, "level1.craft_super_bomb", "WorkTable");
        AddNamedQuestTarget(scene, "level1.open_monster_cage", "Behemoth");
        AddNamedQuestTarget(scene, "level1.find_castle_gate_key", "MainGate");
    }

    private void AddNamedQuestTarget(Scene scene, string questId, string objectName)
    {
        AddNamedQuestTarget(scene, questId, objectName, Vector2.zero);
    }

    private void AddNamedQuestTarget(
        Scene scene,
        string questId,
        string objectName,
        Vector2 positionOffset)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate != null && candidate.gameObject.scene == scene &&
                candidate.name == objectName)
            {
                Transform markerTransform = candidate;
                if (objectName == "MainGate")
                {
                    Transform doorChild = candidate.Find("Door");
                    if (doorChild != null)
                    {
                        markerTransform = doorChild;
                    }
                }
                questTargets.Add(new RuntimeMiniMapQuestTargetData
                {
                    questId = questId,
                    position = (Vector2)markerTransform.position + positionOffset
                });
                return;
            }
        }
    }

    private void AddColliderPaths(Collider2D collider)
    {
        if (collider is BoxCollider2D box)
        {
            Vector2 half = box.size * 0.5f;
            Vector2 center = box.offset;
            AddClosedLocalPath(collider.transform, new[]
            {
                center + new Vector2(-half.x, -half.y),
                center + new Vector2(-half.x, half.y),
                center + new Vector2(half.x, half.y),
                center + new Vector2(half.x, -half.y)
            });
            return;
        }

        if (collider is PolygonCollider2D polygon)
        {
            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] points = polygon.GetPath(pathIndex);
                for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    points[pointIndex] += polygon.offset;
                }
                AddClosedLocalPath(collider.transform, points);
            }
            return;
        }

        if (collider is EdgeCollider2D edge)
        {
            Vector2[] points = edge.points;
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                points[pointIndex] += edge.offset;
            }
            AddOpenLocalPath(collider.transform, points);
            return;
        }

        if (collider is CompositeCollider2D composite)
        {
            for (int pathIndex = 0; pathIndex < composite.pathCount; pathIndex++)
            {
                int pointCount = composite.GetPathPointCount(pathIndex);
                Vector2[] points = new Vector2[pointCount];
                composite.GetPath(pathIndex, points);
                if (composite.geometryType == CompositeCollider2D.GeometryType.Polygons)
                {
                    AddClosedLocalPath(collider.transform, points);
                }
                else
                {
                    AddOpenLocalPath(collider.transform, points);
                }
            }
            return;
        }

        if (collider is CircleCollider2D circle)
        {
            const int segments = 20;
            Vector2[] points = new Vector2[segments];
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                points[index] = circle.offset +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
            }
            AddClosedLocalPath(collider.transform, points);
            return;
        }

        AddWorldBoundsPath(collider.bounds);
    }

    private void AddClosedLocalPath(Transform owner, Vector2[] localPoints)
    {
        if (localPoints == null || localPoints.Length < 2)
        {
            return;
        }

        Vector2[] worldPoints = new Vector2[localPoints.Length + 1];
        for (int index = 0; index < localPoints.Length; index++)
        {
            worldPoints[index] = owner.TransformPoint(localPoints[index]);
            Encapsulate(worldPoints[index]);
        }
        worldPoints[worldPoints.Length - 1] = worldPoints[0];
        blockPaths.Add(worldPoints);
    }

    private void AddOpenLocalPath(Transform owner, Vector2[] localPoints)
    {
        if (localPoints == null || localPoints.Length < 2)
        {
            return;
        }

        Vector2[] worldPoints = new Vector2[localPoints.Length];
        for (int index = 0; index < localPoints.Length; index++)
        {
            worldPoints[index] = owner.TransformPoint(localPoints[index]);
            Encapsulate(worldPoints[index]);
        }
        blockPaths.Add(worldPoints);
    }

    private void AddWorldBoundsPath(Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;
        Vector2[] points =
        {
            new Vector2(min.x, min.y),
            new Vector2(min.x, max.y),
            new Vector2(max.x, max.y),
            new Vector2(max.x, min.y),
            new Vector2(min.x, min.y)
        };
        for (int index = 0; index < points.Length; index++)
        {
            Encapsulate(points[index]);
        }
        blockPaths.Add(points);
    }

    private void AddTransition(
        Vector2 position,
        string targetSceneName,
        bool preservesSceneState)
    {
        transitions.Add(new RuntimeMiniMapTransitionData
        {
            position = position,
            targetSceneName = targetSceneName,
            preservesSceneState = preservesSceneState
        });
        Encapsulate(position);
    }

    private void Encapsulate(Vector2 point)
    {
        if (!hasBounds)
        {
            mapBounds = new Bounds(point, Vector3.zero);
            hasBounds = true;
        }
        else
        {
            mapBounds.Encapsulate(point);
        }
    }

    private void EnsureUsableBounds()
    {
        if (!hasBounds)
        {
            mapBounds = new Bounds(cameraWorldPosition, new Vector3(10f, 10f, 0f));
            hasBounds = true;
            return;
        }

        Vector3 size = mapBounds.size;
        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);
        mapBounds.size = size;
    }

    private void RebuildFilledWallTriangles()
    {
        filledWallTriangles.Clear();
        for (int pathIndex = 0; pathIndex < blockPaths.Count; pathIndex++)
        {
            Vector2[] path = blockPaths[pathIndex];
            if (IsClosedPath(path))
            {
                TriangulateClosedPath(path, filledWallTriangles);
            }
        }
    }

    private static bool IsClosedPath(Vector2[] path)
    {
        return path != null &&
               path.Length >= 4 &&
               (path[0] - path[path.Length - 1]).sqrMagnitude < 0.0001f;
    }

    private static void TriangulateClosedPath(
        Vector2[] path,
        List<Vector2> destination)
    {
        int pointCount = path.Length - 1;
        if (pointCount < 3)
        {
            return;
        }

        float signedArea = 0f;
        for (int index = 0; index < pointCount; index++)
        {
            Vector2 current = path[index];
            Vector2 next = path[(index + 1) % pointCount];
            signedArea += current.x * next.y - next.x * current.y;
        }
        bool counterClockwise = signedArea > 0f;

        List<int> remaining = new List<int>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            remaining.Add(index);
        }

        int safety = pointCount * pointCount;
        while (remaining.Count > 2 && safety-- > 0)
        {
            bool clippedEar = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                int previousIndex = remaining[
                    (index - 1 + remaining.Count) % remaining.Count];
                int currentIndex = remaining[index];
                int nextIndex = remaining[(index + 1) % remaining.Count];
                Vector2 previous = path[previousIndex];
                Vector2 current = path[currentIndex];
                Vector2 next = path[nextIndex];
                float cross = Cross(current - previous, next - current);
                if ((counterClockwise && cross <= 0.00001f) ||
                    (!counterClockwise && cross >= -0.00001f))
                {
                    continue;
                }

                bool containsPoint = false;
                for (int testIndex = 0;
                     testIndex < remaining.Count;
                     testIndex++)
                {
                    int candidateIndex = remaining[testIndex];
                    if (candidateIndex == previousIndex ||
                        candidateIndex == currentIndex ||
                        candidateIndex == nextIndex)
                    {
                        continue;
                    }

                    if (PointInsideTriangle(
                            path[candidateIndex],
                            previous,
                            current,
                            next))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint)
                {
                    continue;
                }

                destination.Add(previous);
                destination.Add(current);
                destination.Add(next);
                remaining.RemoveAt(index);
                clippedEar = true;
                break;
            }

            if (!clippedEar)
            {
                break;
            }
        }
    }

    private static bool PointInsideTriangle(
        Vector2 point,
        Vector2 first,
        Vector2 second,
        Vector2 third)
    {
        float firstCross = Cross(second - first, point - first);
        float secondCross = Cross(third - second, point - second);
        float thirdCross = Cross(first - third, point - third);
        bool hasNegative = firstCross < -0.00001f ||
                           secondCross < -0.00001f ||
                           thirdCross < -0.00001f;
        bool hasPositive = firstCross > 0.00001f ||
                           secondCross > 0.00001f ||
                           thirdCross > 0.00001f;
        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static Vector2 MapPoint(
        Vector2 worldPoint,
        Vector2 worldCenter,
        Vector2 rectCenter,
        float scale)
    {
        return rectCenter + (worldPoint - worldCenter) * scale;
    }

    private float GetPan01(float center)
    {
        float extent = GetPanExtent();
        return extent <= 0.0001f
            ? 0.5f
            : Mathf.InverseLerp(0.5f - extent, 0.5f + extent, center);
    }

    private float Pan01ToCenter(float value)
    {
        float extent = GetPanExtent();
        return Mathf.Lerp(0.5f - extent, 0.5f + extent, Mathf.Clamp01(value));
    }

    private float GetPanExtent()
    {
        return Mathf.Max(0f, (1f - 1f / zoom) * 0.5f);
    }

    private void ClampViewCenter()
    {
        float extent = GetPanExtent();
        viewCenterNormalized.x = Mathf.Clamp(
            viewCenterNormalized.x,
            0.5f - extent,
            0.5f + extent);
        viewCenterNormalized.y = Mathf.Clamp(
            viewCenterNormalized.y,
            0.5f - extent,
            0.5f + extent);
    }

    private void NotifyViewChanged()
    {
        SetVerticesDirty();
        ViewChanged?.Invoke();
    }

    private static void AddTransitionIcon(
        VertexHelper vertexHelper,
        Vector2 center,
        Color color,
        float scale)
    {
        float lineWidth = Mathf.Max(0.75f, 2.4f * scale);
        AddLine(vertexHelper, center + new Vector2(-9f, -14f) * scale, center + new Vector2(-9f, 14f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(-9f, 14f) * scale, center + new Vector2(5f, 14f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(5f, 14f) * scale, center + new Vector2(5f, 5f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(5f, -5f) * scale, center + new Vector2(5f, -14f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(5f, -14f) * scale, center + new Vector2(-9f, -14f) * scale, lineWidth, color);

        AddLine(vertexHelper, center + new Vector2(13f, 5f) * scale, center + new Vector2(-1f, 5f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(13f, -5f) * scale, center + new Vector2(-1f, -5f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(13f, 5f) * scale, center + new Vector2(13f, -5f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(-1f, 9f) * scale, center + new Vector2(-8f, 0f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(-8f, 0f) * scale, center + new Vector2(-1f, -9f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(-1f, 9f) * scale, center + new Vector2(-1f, 5f) * scale, lineWidth, color);
        AddLine(vertexHelper, center + new Vector2(-1f, -9f) * scale, center + new Vector2(-1f, -5f) * scale, lineWidth, color);
    }

    private static void AddDiamond(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        Color color)
    {
        int startIndex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, center + new Vector2(0f, radius), color);
        AddVertex(vertexHelper, center + new Vector2(radius, 0f), color);
        AddVertex(vertexHelper, center + new Vector2(0f, -radius), color);
        AddVertex(vertexHelper, center + new Vector2(-radius, 0f), color);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddQuestTargetMarker(
        VertexHelper vertexHelper,
        Vector2 center,
        float scale)
    {
        Color border = Color.white;
        Color outer = new Color32(18, 92, 108, 255);
        Color inner = new Color32(86, 190, 245, 255);
        AddDiamond(vertexHelper, center, 14f * scale, border);
        AddDiamond(vertexHelper, center, 11.4f * scale, outer);
        AddDiamond(vertexHelper, center, 5.8f * scale, inner);
    }

    private static void AddSolidTriangle(
        VertexHelper vertexHelper,
        Vector2 first,
        Vector2 second,
        Vector2 third,
        Color color)
    {
        int startIndex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, first, color);
        AddVertex(vertexHelper, second, color);
        AddVertex(vertexHelper, third, color);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
    }

    private static void AddLine(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int startIndex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, start - perpendicular, color);
        AddVertex(vertexHelper, start + perpendicular, color);
        AddVertex(vertexHelper, end + perpendicular, color);
        AddVertex(vertexHelper, end - perpendicular, color);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertexHelper.AddVert(vertex);
    }
}

/// <summary>Mouse and scrollbar controls for the runtime map viewport.</summary>
public sealed class RuntimeMiniMapViewportInput : MonoBehaviour,
    IScrollHandler,
    IBeginDragHandler,
    IDragHandler
{
    private RuntimeMiniMapGraphic map;
    private Scrollbar horizontalScrollbar;
    private Scrollbar verticalScrollbar;
    private Text zoomPercentageText;
    private bool synchronizing;

    public void Configure(
        RuntimeMiniMapGraphic configuredMap,
        Scrollbar horizontal,
        Scrollbar vertical,
        Text percentageText)
    {
        map = configuredMap;
        horizontalScrollbar = horizontal;
        verticalScrollbar = vertical;
        zoomPercentageText = percentageText;
        horizontalScrollbar.onValueChanged.AddListener(SetHorizontalPan);
        verticalScrollbar.onValueChanged.AddListener(SetVerticalPan);
        map.ViewChanged += SynchronizeControls;
        SynchronizeControls();
    }

    private void OnDestroy()
    {
        if (map != null)
        {
            map.ViewChanged -= SynchronizeControls;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (map != null)
        {
            map.ZoomBy(eventData.scrollDelta.y * 0.35f);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (map == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        float scaleFactor = 1f;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            scaleFactor = Mathf.Max(0.01f, canvas.scaleFactor);
        }
        map.PanByPixels(eventData.delta / scaleFactor);
    }

    private void SetHorizontalPan(float value)
    {
        if (!synchronizing && map != null)
        {
            map.SetHorizontalPan01(value);
        }
    }

    private void SetVerticalPan(float value)
    {
        if (!synchronizing && map != null)
        {
            map.SetVerticalPan01(value);
        }
    }

    private void SynchronizeControls()
    {
        if (map == null || horizontalScrollbar == null || verticalScrollbar == null)
        {
            return;
        }

        synchronizing = true;
        float visibleFraction = 1f / map.Zoom;
        horizontalScrollbar.size = visibleFraction;
        verticalScrollbar.size = visibleFraction;
        horizontalScrollbar.SetValueWithoutNotify(map.HorizontalPan01);
        verticalScrollbar.SetValueWithoutNotify(map.VerticalPan01);
        if (zoomPercentageText != null)
        {
            zoomPercentageText.text =
                Mathf.RoundToInt(map.ZoomPercentage) + "%";
        }
        synchronizing = false;
    }
}

/// <summary>Simple geometric magnifier icon used by the map zoom buttons.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RuntimeMiniMapZoomIcon : MaskableGraphic
{
    private bool zoomIn = true;

    public void Configure(bool isZoomIn)
    {
        zoomIn = isZoomIn;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center + new Vector2(2.5f, 2.5f);
        const float radius = 6.5f;
        const float width = 1.6f;
        const int segments = 18;
        for (int index = 0; index < segments; index++)
        {
            float firstAngle = index * Mathf.PI * 2f / segments;
            float secondAngle = (index + 1) * Mathf.PI * 2f / segments;
            AddIconLine(
                vertexHelper,
                center + new Vector2(
                    Mathf.Cos(firstAngle),
                    Mathf.Sin(firstAngle)) * radius,
                center + new Vector2(
                    Mathf.Cos(secondAngle),
                    Mathf.Sin(secondAngle)) * radius,
                width,
                color);
        }

        AddIconLine(
            vertexHelper,
            center + new Vector2(-4.5f, -4.5f),
            center + new Vector2(-10f, -10f),
            2f,
            color);
        AddIconLine(
            vertexHelper,
            center + new Vector2(-3.2f, 0f),
            center + new Vector2(3.2f, 0f),
            width,
            color);
        if (zoomIn)
        {
            AddIconLine(
                vertexHelper,
                center + new Vector2(0f, -3.2f),
                center + new Vector2(0f, 3.2f),
                width,
                color);
        }
    }

    private static void AddIconLine(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 perpendicular =
            new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int startIndex = vertexHelper.currentVertCount;
        AddIconVertex(vertexHelper, start - perpendicular, color);
        AddIconVertex(vertexHelper, start + perpendicular, color);
        AddIconVertex(vertexHelper, end + perpendicular, color);
        AddIconVertex(vertexHelper, end - perpendicular, color);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddIconVertex(
        VertexHelper vertexHelper,
        Vector2 position,
        Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertexHelper.AddVert(vertex);
    }
}
