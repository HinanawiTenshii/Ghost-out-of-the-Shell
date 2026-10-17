using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Draws proportional geometry from Blocks and Visible Non Blocking colliders,
/// transition markers and the current camera position using separate wall/marker meshes.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed partial class RuntimeMiniMapGraphic : MaskableGraphic
{
    private const float DefaultZoom = 2.5f;
    private const float MinimumZoom = 1f;
    private const float MaximumZoom = 5f;
    private readonly List<Vector2[]> blockPaths = new List<Vector2[]>();
    private readonly List<Vector2> filledWallTriangles = new List<Vector2>();
    private readonly List<RuntimeMiniMapBridgeData> bridgeLayouts = new List<RuntimeMiniMapBridgeData>();
    private readonly List<RotatingBridgeMechanism> liveBridges = new List<RotatingBridgeMechanism>();
    private readonly List<bool> displayedBridgeStates = new List<bool>();
    private int staticPathCount;
    private string geometrySceneName;
    private readonly List<MapPointOfInterestRecord> lockedDoorMarkers = new List<MapPointOfInterestRecord>();
    private MapPointOfInterestManager subscribedPointManager;
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
    private readonly List<Vector2> additionalTrackedQuestTargets = new List<Vector2>();

    public float Zoom => zoom;
    public float ZoomPercentage => zoom / DefaultZoom * 100f;
    public IReadOnlyList<RuntimeMiniMapTransitionData> Transitions => transitions;
    public float HorizontalPan01 => GetPan01(viewCenterNormalized.x);
    public float VerticalPan01 => GetPan01(viewCenterNormalized.y);
    public event Action ViewChanged;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureRenderLayers();
        if (!Application.isPlaying) return;
        subscribedPointManager = MapPointOfInterestManager.GetOrCreate();
        subscribedPointManager.PointsChanged += SetVerticesDirty;
    }

    protected override void OnDisable()
    {
        if (surfaceLayer != null) surfaceLayer.enabled = false;
        if (wallLayer != null) wallLayer.enabled = false;
        if (markerLayer != null) markerLayer.enabled = false;
        if (subscribedPointManager != null) subscribedPointManager.PointsChanged -= SetVerticesDirty;
        subscribedPointManager = null;
        base.OnDisable();
    }

    public bool TryGetQuestTarget(string questId, out Vector2 position)
    {
        questId = QuestJournalManager.ResolveQuestMapTargetId(questId);
        if (questId == QuestJournalManager.KnightAmbushQuestId &&
            KnightCaptainAmbush.TryGetMarker(geometrySceneName, out position)) return true;
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

    /// <summary>All cached destinations for a task; no scene searches or per-frame allocation.</summary>
    public void GetQuestTargets(string questId, List<Vector2> positions)
    {
        positions.Clear();
        questId = QuestJournalManager.ResolveQuestMapTargetId(questId);
        if (string.IsNullOrEmpty(questId)) return;
        if (questId == QuestJournalManager.KnightAmbushQuestId &&
            KnightCaptainAmbush.TryGetMarker(geometrySceneName, out var captainPosition))
        { positions.Add(captainPosition); return; }
        for (int i = 0; i < questTargets.Count; i++)
            if (questTargets[i].questId == questId && !positions.Contains(questTargets[i].position))
                positions.Add(questTargets[i].position);
    }

    public void SetTrackedQuestTargets(IReadOnlyList<Vector2> positions, bool clampToBorder)
    {
        int count = positions != null ? positions.Count : 0;
        if (count == 0) { SetTrackedQuestTarget(false, Vector2.zero, clampToBorder); return; }
        bool changed = !showTrackedQuestTarget || trackedQuestTargetPosition != positions[0] ||
            clampQuestTargetToBorder != clampToBorder || additionalTrackedQuestTargets.Count != count - 1;
        if (!changed)
            for (int i = 1; i < count; i++)
                if (additionalTrackedQuestTargets[i - 1] != positions[i]) { changed = true; break; }
        if (!changed) return;
        showTrackedQuestTarget = true;
        trackedQuestTargetPosition = positions[0];
        clampQuestTargetToBorder = clampToBorder;
        additionalTrackedQuestTargets.Clear();
        for (int i = 1; i < count; i++) additionalTrackedQuestTargets.Add(positions[i]);
        SetVerticesDirty();
    }

    public void SetTrackedQuestTarget(bool visible, Vector2 worldPosition, bool clampToBorder)
    {
        if (showTrackedQuestTarget == visible && trackedQuestTargetPosition == worldPosition &&
            clampQuestTargetToBorder == clampToBorder && additionalTrackedQuestTargets.Count == 0) return;
        additionalTrackedQuestTargets.Clear();
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

    internal static void GetLockedDoorLineGeometry(Vector2 a, Vector2 b, float scale,
        out Vector2 center, out Vector2 half, out float width)
    {
        width = Mathf.Max(3.2f, 4.5f * scale);
        center = (a + b) * .5f;
        half = (b-a).normalized * Mathf.Max((b-a).magnitude, width * 3f) * .5f;
    }

    public bool TryGetLockedDoorHoverRect(MapPointOfInterestRecord door,
        out Vector2 center, out Vector2 size, out float angle)
    {
        center = size = Vector2.zero; angle = 0f;
        if (door == null || !TryWorldToLocal(door.LineStart, out Vector2 a) ||
            !TryWorldToLocal(door.LineEnd, out Vector2 b)) return false;
        GetLockedDoorLineGeometry(a, b, iconScale, out center, out Vector2 half, out float width);
        if (half.sqrMagnitude < .0001f) return false;
        // Slightly pad the thin line for a forgiving hover target, without changing its drawing.
        size = new Vector2(half.magnitude * 2f + 4f, Mathf.Max(14f, width + 8f));
        angle = Mathf.Atan2(half.y, half.x) * Mathf.Rad2Deg;
        return true;
    }

    public void RebuildFromScene(Scene scene)
    {
        using (SceneLoadMetrics.Measure("Map.Initialize"))
        {
            if (Application.isPlaying && TryLoadCachedScene(scene)) return;
            RebuildFromSceneCore(scene);
        }
    }

    private void RebuildFromSceneCore(Scene scene)
    {
        blockPaths.Clear();
        ResetBridgeLayouts(scene.name);
        transitions.Clear();
        questTargets.Clear();
        hasBounds = false;

        int blocksLayer = LayerMask.NameToLayer("Blocks");
        int visibleNonBlockingLayer = LayerMask.NameToLayer("Visible Non Blocking");
        CaptureSurfaces(scene);
        HashSet<Collider2D> additionalWalls = new HashSet<Collider2D>();
        foreach (MiniMapWallGroup group in FindObjectsOfType<MiniMapWallGroup>(true))
        {
            if (group.gameObject.scene != scene || group.Walls == null) continue;
            foreach (Collider2D wall in group.Walls)
                if (wall != null && wall.gameObject.scene == scene) additionalWalls.Add(wall);
        }
        Collider2D[] colliders = FindObjectsOfType<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider == null || collider.gameObject.scene != scene ||
                !IsMapWall(collider, additionalWalls, blocksLayer, visibleNonBlockingLayer))
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

        staticPathCount = blockPaths.Count;
        foreach (RotatingBridgeMechanism mechanism in FindObjectsOfType<RotatingBridgeMechanism>(true))
        {
            if (mechanism.gameObject.scene != scene || mechanism.BridgeHinge == null) continue;
            DoorHingeInteraction hinge = mechanism.BridgeHinge;
            var original = CaptureBridgePaths(mechanism, colliders, additionalWalls, blocksLayer, visibleNonBlockingLayer, false);
            var rotated = CaptureBridgePaths(mechanism, colliders, additionalWalls, blocksLayer, visibleNonBlockingLayer, true);
            AddBridgeLayout(new RuntimeMiniMapBridgeData
            {
                persistentId = SceneTravelStateManager.GetMapObjectId(hinge.transform),
                originalPaths = original,
                rotatedPaths = rotated
            }, mechanism);
        }
        RefreshBridgeLayouts(true);

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
        ResetBridgeLayouts(snapshot != null ? snapshot.SceneName : string.Empty);
        transitions.Clear();
        questTargets.Clear();
        hasBounds = false;

        if (snapshot != null)
        {
            LoadSurfaces(snapshot);
        }
        else ClearSurfaces();

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

                    // Snapshot arrays are immutable; each view owns only its dynamic state.
                    Vector2[] copy = points;
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

        staticPathCount = blockPaths.Count;
        if (snapshot != null && snapshot.Bridges != null)
            foreach (RuntimeMiniMapBridgeData layout in snapshot.Bridges) AddBridgeLayout(layout, null);
        RefreshBridgeLayouts(true);

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
                blockPaths.GetRange(0, staticPathCount),
                transitions,
                questTargets,
                bridgeLayouts,
                surfaces);
        }
    }

    public void SetCameraPosition(Vector2 worldPosition)
    {
        if (cameraWorldPosition == worldPosition) return;
        cameraWorldPosition = worldPosition;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshBridgeLayouts(false);
        RefreshSurfaceVisibility();

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

    private void ResetBridgeLayouts(string sceneName)
    {
        geometrySceneName = sceneName;
        staticPathCount = 0;
        bridgeLayouts.Clear();
        liveBridges.Clear();
        displayedBridgeStates.Clear();
    }

    private static bool IsMapWall(Collider2D collider, HashSet<Collider2D> additionalWalls,
        int blocksLayer, int visibleNonBlockingLayer)
    {
        // Map geometry is independent of vision occlusion. Capture low walls and
        // railings too, including streamed-out ones, but not interaction trigger areas.
        // Keep the original Blocks / explicit-group policy unchanged.
        return collider.gameObject.layer == blocksLayer || additionalWalls.Contains(collider) ||
               (collider.gameObject.layer == visibleNonBlockingLayer && !collider.isTrigger);
    }

    private static RuntimeMiniMapPathData[] CaptureBridgePaths(
        RotatingBridgeMechanism mechanism, Collider2D[] colliders,
        HashSet<Collider2D> additionalWalls, int blocksLayer, int visibleNonBlockingLayer, bool rotated)
    {
        var paths = new List<Vector2[]>();
        DoorHingeInteraction hinge = mechanism.BridgeHinge;
        Matrix4x4 toEndpoint = hinge.GetMapEndpointMatrix(rotated) * hinge.MapHingeTransform.worldToLocalMatrix;
        foreach (Collider2D collider in colliders)
        {
            // The rotating bridge uses rectangular walls. Never capture its
            // invisible passage safety barriers or ordinary door geometry.
            BoxCollider2D box = collider as BoxCollider2D;
            if (box == null || box.GetComponentInParent<RotatingBridgeMechanism>(true) != mechanism ||
                !IsMapWall(box, additionalWalls, blocksLayer, visibleNonBlockingLayer)) continue;
            paths.Add(RuntimeMiniMapSceneData.CreateBoxPath(
                toEndpoint * box.transform.localToWorldMatrix, box.offset, box.size));
        }
        for (int index = 0; index < 4; index++)
        {
            DoubleSlidingDoor door = mechanism.GetMapPassage(index);
            if (door != null) door.AppendMapEndpointPaths(index % 2 == 0 ? !rotated : rotated, paths);
        }
        var result = new RuntimeMiniMapPathData[paths.Count];
        for (int i = 0; i < paths.Count; i++) result[i] = new RuntimeMiniMapPathData { points = paths[i] };
        return result;
    }

    private void AddBridgeLayout(RuntimeMiniMapBridgeData layout, RotatingBridgeMechanism source)
    {
        bridgeLayouts.Add(layout);
        liveBridges.Add(source);
        displayedBridgeStates.Add(false);
        // Both endpoints contribute to bounds: switching must not recenter/zoom the map.
        EncapsulatePaths(layout.originalPaths);
        EncapsulatePaths(layout.rotatedPaths);
    }

    private void EncapsulatePaths(RuntimeMiniMapPathData[] paths)
    {
        if (paths == null) return;
        foreach (RuntimeMiniMapPathData path in paths)
            if (path.points != null) foreach (Vector2 point in path.points) Encapsulate(point);
    }

    private void RefreshBridgeLayouts(bool force)
    {
        bool changed = force;
        for (int i = 0; i < bridgeLayouts.Count; i++)
        {
            bool rotated = false;
            if (liveBridges[i] != null) rotated = liveBridges[i].MapUsesRotatedLayout;
            else if (SceneTravelStateManager.Instance != null)
                SceneTravelStateManager.Instance.TryGetMapDoorState(
                    geometrySceneName, bridgeLayouts[i].persistentId, out rotated);
            changed |= displayedBridgeStates[i] != rotated;
            displayedBridgeStates[i] = rotated;
        }
        if (!changed) return;
        blockPaths.RemoveRange(staticPathCount, blockPaths.Count - staticPathCount);
        for (int i = 0; i < bridgeLayouts.Count; i++)
        {
            RuntimeMiniMapPathData[] paths = displayedBridgeStates[i]
                ? bridgeLayouts[i].rotatedPaths : bridgeLayouts[i].originalPaths;
            if (paths == null) continue;
            foreach (RuntimeMiniMapPathData path in paths)
                if (path.points != null && path.points.Length >= 2) blockPaths.Add(path.points);
        }
        RebuildFilledWallTriangles();
        SetVerticesDirty();
    }

    internal void PopulateMarkers(VertexHelper vertexHelper)
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

        // Dynamic discovered locks stay aligned with the HUD and the full map.
        var pointManager = MapPointOfInterestManager.Instance;
        if (pointManager != null)
        {
            pointManager.CopyVisibleLockedDoors(geometrySceneName, lockedDoorMarkers);
            foreach (var door in lockedDoorMarkers)
            {
                Vector2 a = MapPoint(door.LineStart, worldCenter, rectCenter, scale);
                Vector2 b = MapPoint(door.LineEnd, worldCenter, rectCenter, scale);
                GetLockedDoorLineGeometry(a, b, iconScale, out Vector2 center, out Vector2 half, out float width);
                AddLine(vertexHelper, center-half, center+half, width, door.IconColor);
            }
        }

        float transitionScale = GetTransitionIconScale(scale);
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
            AddTransitionIcon(vertexHelper, center, iconColor, transitionScale);
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


        for (int targetIndex = 0; showTrackedQuestTarget && targetIndex <= additionalTrackedQuestTargets.Count; targetIndex++)
        {
            Vector2 targetCenter = MapPoint(
                targetIndex == 0 ? trackedQuestTargetPosition : additionalTrackedQuestTargets[targetIndex - 1],
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
        string[] ids = {
            "level0.escape_prison", "level0.obtain_gate_key", "level1.escape_castle",
            "level1.craft_super_bomb", "level1.open_monster_cage", "level1.find_castle_gate_key",
            QuestJournalManager.Level2CityTargetId, QuestJournalManager.Level2TrialTargetId,
            QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.FortressProofQuestId, QuestJournalManager.KnightAmbushQuestId,
            QuestJournalManager.SandwormToolsTarget, QuestJournalManager.SandwormMerchantTarget,
            QuestJournalManager.SandwormDeviceTarget, QuestJournalManager.SandwormMortarTarget,
            QuestJournalManager.CounterfeitMerchantTarget, QuestJournalManager.CounterfeitCraftsmanTarget,
            QuestJournalManager.SummoningCircleTarget,
            QuestJournalManager.GravekeeperCrystalTarget, QuestJournalManager.GravekeeperReturnTarget,
            QuestJournalManager.ArenaRegistrarTarget, QuestJournalManager.ArenaEntranceTarget, QuestJournalManager.ArenaCombatTarget
        };
        foreach (string id in ids)
        {
            if (TryGetSceneQuestTarget(scene, id, out Vector2 position))
                questTargets.Add(new RuntimeMiniMapQuestTargetData { questId = id, position = position });
        }
        // Capture all alternatives, even before acceptance and while streamed out.
        // Repeated stable IDs intentionally represent multiple destinations.
        var injectors = FindObjectsOfType<LiquidInjector>(true);
        Array.Sort(injectors, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        foreach (var injector in injectors)
            if (injector.gameObject.scene == scene && !string.IsNullOrWhiteSpace(injector.PotionInjectionQuestId))
                questTargets.Add(new RuntimeMiniMapQuestTargetData {
                    questId = injector.PotionInjectionQuestId, position = injector.transform.position });
    }

    // The map and region buttons share the same live target rules, including
    // inactive streamed objects. No scene load or map mesh rebuild is needed.
    public static bool TryGetSceneQuestTarget(Scene scene, string questId, out Vector2 position)
    {
        position = Vector2.zero;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        questId = QuestJournalManager.ResolveQuestMapTargetId(questId);
        if (string.IsNullOrEmpty(questId)) return false;
        if (questId == QuestJournalManager.GravekeeperCrystalTarget)
            return TryGetQuestComponentPosition<GraveyardCrystalQuestTarget>(scene, out position);
        if (questId == QuestJournalManager.GravekeeperReturnTarget)
        {
            foreach (var dialogue in FindObjectsOfType<DialogueQuestProgression>(true))
                if (dialogue.gameObject.scene == scene && dialogue.QuestId == QuestJournalManager.GravekeeperQuestId)
                { position = dialogue.transform.position; return true; }
            return false;
        }
        if (questId == QuestJournalManager.KnightAmbushQuestId)
        {
            if (KnightCaptainAmbush.TryGetMarker(scene.name, out position)) return true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                foreach (var director in FindObjectsOfType<KnightCaptainAmbush>(true))
                    if (director.gameObject.scene == scene && director.captain != null)
                    { position = director.captain.transform.position; return true; }
#endif
            return false;
        }
        if (questId == QuestJournalManager.ArenaInjectionQuestId)
        {
            foreach (var injector in FindObjectsOfType<LiquidInjector>(true))
                if (injector.gameObject.scene == scene && injector.PotionInjectionQuestId == questId)
                { position = injector.transform.position; return true; }
            return false;
        }
        if (questId == QuestJournalManager.ArenaRegistrarTarget || questId == QuestJournalManager.ArenaEntranceTarget ||
            questId == QuestJournalManager.ArenaCombatTarget)
        {
            foreach (var arena in FindObjectsOfType<ArenaChallengeController>(true))
            {
                if (arena.gameObject.scene != scene) continue;
                if (questId == QuestJournalManager.ArenaCombatTarget) { position = arena.fightingArea.center; return true; }
                if (questId == QuestJournalManager.ArenaRegistrarTarget && arena.registrar != null)
                { position = arena.registrar.transform.position; return true; }
                if (questId == QuestJournalManager.ArenaEntranceTarget && arena.entrance != null)
                { position = arena.entrance.transform.position; return true; }
            }
            return false;
        }
        // Stage-specific IDs are also captured into unloaded-scene snapshots.
        // These queries run only during map collection/target refresh, not per frame.
        if (questId == QuestJournalManager.SummoningCircleTarget)
        {
            foreach (var circle in FindObjectsOfType<SummoningCircle>(true))
                if (circle.gameObject.scene == scene && circle.JournalQuestId == QuestJournalManager.SummoningQuestId)
                { position = circle.transform.position; return true; }
            return false;
        }
        if (questId == QuestJournalManager.SandwormToolsTarget)
            return TryGetQuestComponentPosition<SynthesisToolsPickupItem>(scene, out position);
        if (questId == QuestJournalManager.FortressProofQuestId)
        {
            foreach (var proof in FindObjectsOfType<TrialProofPickupItem>(true))
                if (proof.gameObject.scene == scene && proof.CompletesQuestOnPickup(questId))
                { position = proof.transform.position; return true; }
            return false;
        }
        if (questId == QuestJournalManager.SandwormDeviceTarget)
            return TryGetQuestComponentPosition<MortarControlDevicePickupItem>(scene, out position);
        if (questId == QuestJournalManager.SandwormMortarTarget)
            return TryGetQuestComponentPosition<FortressMortarRemoteInstallation>(scene, out position);
        if (questId == QuestJournalManager.Level2CityTargetId || questId == QuestJournalManager.Level2TrialTargetId)
        {
            foreach (var target in FindObjectsOfType<Level2HeroQuestTarget>(true))
                if (target != null && target.gameObject.scene == scene && target.MapTargetId == questId)
                { position = target.transform.position; return true; }
            return false;
        }
        string objectName;
        Vector2 positionOffset = Vector2.zero;
        switch (questId)
        {
            case QuestJournalManager.CounterfeitMerchantTarget:
                objectName = "宝石商人";
                break;
            case QuestJournalManager.CounterfeitCraftsmanTarget:
                objectName = "赝品工匠";
                break;
            case QuestJournalManager.SandwormMerchantTarget:
                objectName = "诡异商人";
                break;
            case QuestJournalManager.Level2SandwormQuestId:
                objectName = QuestJournalManager.Level2SandwormTargetName;
                break;
            case "level0.escape_prison":
            case "level1.escape_castle": objectName = "FinalTarget"; break;
            case "level0.obtain_gate_key":
                objectName = "GoldenKey";
                positionOffset = new Vector2(0f, 0.72f);
                break;
            case "level1.craft_super_bomb": objectName = "WorkTable"; break;
            case "level1.open_monster_cage": objectName = "Behemoth"; break;
            case "level1.find_castle_gate_key": objectName = "MainGate"; break;
            default: return false;
        }
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
                position = (Vector2)markerTransform.position + positionOffset;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetQuestComponentPosition<T>(Scene scene, out Vector2 position) where T : Component
    {
        foreach (var item in FindObjectsOfType<T>(true))
            if (item != null && item.gameObject.scene == scene)
            { position = item.transform.position; return true; }
        position = Vector2.zero;
        return false;
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
        wallGeometryVersion++;
        filledWallTriangles.Clear();
        for (int pathIndex = 0; pathIndex < blockPaths.Count; pathIndex++)
        {
            Vector2[] path = blockPaths[pathIndex];
            if (IsClosedPath(path))
            {
                filledWallTriangles.AddRange(GetCachedTriangles(path));
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

    private float GetTransitionIconScale(float worldToMapScale)
    {
        // HUD uses maximum zoom but has a tiny viewport: zoom alone is not a
        // measure of readable map detail. Limit exits to roughly four world
        // units as well, with a small legibility floor. Keep the same density
        // response as before, but use a much simpler and smaller solid symbol.
        float zoomRatio = Mathf.Clamp(zoom / DefaultZoom, 0.5f, 1f);
        float densityRatio = Mathf.Clamp(worldToMapScale * 4f / (28f * iconScale), 0.45f, 1f);
        return iconScale * Mathf.Min(zoomRatio, densityRatio);
    }

    private static void AddTransitionIcon(
        VertexHelper vertexHelper,
        Vector2 center,
        Color color,
        float scale)
    {
        // Two filled quads: the dark rim separates the solid square from white
        // walls. No interior details are lost at overview/HUD resolution, and
        // the square remains distinct from the player and quest diamonds.
        AddLine(vertexHelper, center + Vector2.down * (7f * scale),
            center + Vector2.up * (7f * scale), 14f * scale, new Color32(8, 24, 43, 255));
        AddLine(vertexHelper, center + Vector2.down * (6f * scale),
            center + Vector2.up * (6f * scale), 12f * scale, color);
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

    internal static void AddQuestTargetMarker(
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
