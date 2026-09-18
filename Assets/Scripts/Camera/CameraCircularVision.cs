using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraCircularVision : MonoBehaviour
{
    private sealed class VisibilitySourceMeshCache
    {
        public UnityEngine.Object SourceKey;
        public readonly List<Vector3> Vertices =
            new List<Vector3>(512);
        public readonly List<int> Triangles =
            new List<int>(1024);
        public readonly CameraVisibilityPolygonBuilder.SourceQueryCache
            QueryCache =
                new CameraVisibilityPolygonBuilder.SourceQueryCache();
        public Vector2 LastBuiltPosition;
        public float LastBuiltRadius;
        public float NextRefreshTime;
        public int BlockerRevision = -1;
        public bool IsValid;

        public void Invalidate()
        {
            SourceKey = null;
            Vertices.Clear();
            Triangles.Clear();
            QueryCache.Invalidate();
            BlockerRevision = -1;
            IsValid = false;
        }
    }

    private const int MaximumRevealSources = 8;
    private const int BlockerChangeHistoryCapacity = 64;
    private const int RevealRayCount = 96;
    private const int RevealRayVectorsPerSource = RevealRayCount / 4;
    private const string VisionOverlayName = "Circular Vision Overlay";
    private const string VisibilityStencilName = "Visibility Polygon Stencil";
    private const string BlocksCameraName = "Blocks Overlay Camera";
    private const string UiCompositeCameraName = "UI CRT Composite Camera";

    // Legacy scene fallback for cameras without a controlled character (e.g. title).
    // During gameplay the effective radius comes from character data.
    [SerializeField, HideInInspector]
    private float visionRadius = 5f;
    private float sceneFallbackVisionRadius;

    [Header("Vision Mask Shape")]
    [SerializeField, Range(24, 192), InspectorName("Streaming Boundary Ray Count")]
    [Tooltip("Ray count used by object streaming. The visible shader uses a separate fixed 64-ray fan.")]
    private int rayCount = 96;
    [SerializeField, Tooltip("Vision occluders. Hidden Blocks is always included, but is never redrawn above the vision mask.")]
    private LayerMask blockLayers;
    private int hiddenBlocksLayerMask;
    [SerializeField] private Color darknessColor = new Color(5f / 255f, 18f / 255f, 37f / 255f, 1f);
    [SerializeField] private float blockerSkin = 0.03f;
    [SerializeField] private float coverPadding = 2f;

    [Header("Vision Performance And Smoothing")]
    [SerializeField, Range(10f, 60f)] private float visualRaycastFrequency = 25f;
    [SerializeField, Min(0.02f)] private float streamingBoundaryRefreshInterval = 0.12f;
    [SerializeField, Min(0f)] private float visualOriginSmoothTime;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.06f;
    [SerializeField, Min(0.01f)]
    [Tooltip("Minimum depth jump before two non-coplanar ray hits are treated as an occlusion corner.")]
    private float cornerDepthDiscontinuity = 0.35f;
    [SerializeField, Range(2, 8)]
    [Tooltip("Binary-search steps used only around confirmed occlusion corners.")]
    private int cornerRefinementIterations = 5;
    [SerializeField, Min(0.05f)] private float teleportRefreshDistance = 0.75f;
    [SerializeField, Range(48, 256)]
    [Tooltip("Round-edge segment count used by the exact visibility polygon.")]
    private int visibilityPolygonCircleSegments = 72;
    [SerializeField, Range(0.005f, 0.25f)]
    [Tooltip("Tiny angular offset cast to either side of each blocker corner.")]
    private float visibilityCornerRayOffset = 0.04f;
    [SerializeField, Range(15f, 60f)]
    [Tooltip("Maximum polygon rebuild rate while a reveal source is moving.")]
    private float movingVisibilityRefreshRate = 40f;
    [SerializeField, Min(0.001f)]
    [Tooltip("Minimum source movement before its cached polygon is rebuilt.")]
    private float visibilityRebuildMovementThreshold = 0.02f;
    [SerializeField, Min(0.05f)]
    [Tooltip("Distance a source may move before its nearby-blocker query is refreshed.")]
    private float blockerQueryMovementThreshold = 0.4f;
    [SerializeField, Min(0.1f)]
    [Tooltip("Extra query radius that keeps blocker results valid while a source moves.")]
    private float blockerQueryPadding = 0.5f;
    [SerializeField, Range(1, MaximumRevealSources - 1)]
    [Tooltip("Routine moving extra sources rebuilt per frame. Forced refreshes still rebuild every source immediately.")]
    private int maxMovingAdditionalSourceRebuildsPerFrame = 1;

    private Camera mainCamera;
    private Camera blocksCamera;
    private Camera uiCompositeCamera;
    private MeshFilter overlayMeshFilter;
    private MeshRenderer overlayRenderer;
    private Mesh overlayMesh;
    private Material overlayMaterial;
    private MeshFilter visibilityStencilMeshFilter;
    private MeshRenderer visibilityStencilRenderer;
    private Mesh visibilityStencilMesh;
    private Material visibilityStencilMaterial;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private readonly List<Vector2> visibleBoundaryPoints = new List<Vector2>();
    private readonly List<Vector3> visibilityStencilVertices =
        new List<Vector3>(1024);
    private readonly List<int> visibilityStencilTriangles =
        new List<int>(2048);
    private readonly CameraVisibilityPolygonBuilder visibilityPolygonBuilder =
        new CameraVisibilityPolygonBuilder();
    private readonly VisibilitySourceMeshCache[] visibilitySourceCaches =
        CreateVisibilitySourceCaches();
    private int cachedVisibilitySourceCount;
    private int nextAdditionalVisibilitySourceIndex = 1;

    private struct BlockerChangeRecord
    {
        public int Revision;
        public Bounds Bounds;
        public bool HasBounds;
    }

    private static int blockerGeometryRevision;
    private static readonly BlockerChangeRecord[] blockerChangeHistory =
        new BlockerChangeRecord[BlockerChangeHistoryCapacity];
    private readonly List<CameraVisionRevealSource> revealSourceCandidates =
        new List<CameraVisionRevealSource>();
    private readonly CameraVisionRevealSource[] sampledAdditionalSources =
        new CameraVisionRevealSource[MaximumRevealSources - 1];
    private readonly Vector2[] revealRayDirections =
        new Vector2[RevealRayCount];
    private Vector2[] boundaryRayDirections;
    private int cachedBoundaryRayCount;
    private int sampledRevealSourceCount;
    private Transform sampledPrimaryTransform;
    private readonly Vector4[] targetRevealSources =
        new Vector4[MaximumRevealSources];
    private readonly Vector4[] revealShaderSources =
        new Vector4[MaximumRevealSources];
    private readonly Vector2[] revealSourceSmoothVelocities =
        new Vector2[MaximumRevealSources];
    private readonly Vector2[] sampledRaycastOrigins =
        new Vector2[MaximumRevealSources];
    private readonly float[] targetRevealRayDistances =
        new float[MaximumRevealSources * RevealRayCount];
    private readonly bool[] targetRevealRayBlocked =
        new bool[MaximumRevealSources * RevealRayCount];
    private readonly bool[] targetRevealIntervalDiscontinuity =
        new bool[MaximumRevealSources * RevealRayCount];
    private readonly float[] targetRevealIntervalSplit =
        new float[MaximumRevealSources * RevealRayCount];
    private readonly Collider2D[] targetRevealRayColliders =
        new Collider2D[MaximumRevealSources * RevealRayCount];
    private readonly Vector2[] targetRevealRayHitPoints =
        new Vector2[MaximumRevealSources * RevealRayCount];
    private readonly Vector2[] targetRevealRayHitNormals =
        new Vector2[MaximumRevealSources * RevealRayCount];
    private readonly float[] displayedRevealRayDistances =
        new float[MaximumRevealSources * RevealRayCount];
    private readonly Vector4[] revealShaderRayDistances =
        new Vector4[MaximumRevealSources * RevealRayVectorsPerSource];

    private float visualRaycastTimer;
    private float boundaryRefreshTimer;
    private Vector2 lastBoundaryOrigin;
    private bool hasBoundarySample;
    private bool forceVisualSample = true;
    private bool forceBoundarySample = true;
    private bool coverGeometryDirty = true;
    private bool lastCoverOrthographic;
    private float lastCoverOrthographicSize = float.NaN;
    private float lastCoverAspect = float.NaN;
    private float lastCoverFieldOfView = float.NaN;
    private float lastCoverPadding = float.NaN;

    private static readonly int RevealSourceCountId =
        Shader.PropertyToID("_RevealSourceCount");
    private static readonly int RevealSourcesId =
        Shader.PropertyToID("_RevealSources");
    private static readonly int RevealRayDistancesId =
        Shader.PropertyToID("_RevealRayDistances");
    private static readonly int EdgeSoftnessId =
        Shader.PropertyToID("_EdgeSoftness");

    public LayerMask BlockLayers => blockLayers;
    public float VisionRadius => visionRadius;
    public float VisionMaskRadius => visionRadius;

    public static void NotifyBlockersChanged()
    {
        RegisterBlockerChange(default(Bounds), false);
    }

    public static void NotifyBlockersChanged(Bounds affectedBounds)
    {
        RegisterBlockerChange(affectedBounds, true);
    }

    private static void RegisterBlockerChange(
        Bounds affectedBounds,
        bool hasBounds)
    {
        if (blockerGeometryRevision == int.MaxValue)
        {
            blockerGeometryRevision = 0;
            for (int index = 0;
                 index < blockerChangeHistory.Length;
                 index++)
            {
                blockerChangeHistory[index] =
                    default(BlockerChangeRecord);
            }
        }

        blockerGeometryRevision++;
        int historyIndex = blockerGeometryRevision %
            BlockerChangeHistoryCapacity;
        blockerChangeHistory[historyIndex] = new BlockerChangeRecord
        {
            Revision = blockerGeometryRevision,
            Bounds = affectedBounds,
            HasBounds = hasBounds
        };
    }

    private static VisibilitySourceMeshCache[] CreateVisibilitySourceCaches()
    {
        VisibilitySourceMeshCache[] caches =
            new VisibilitySourceMeshCache[MaximumRevealSources];
        for (int index = 0; index < caches.Length; index++)
        {
            caches[index] = new VisibilitySourceMeshCache();
        }
        return caches;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVisibilityStatics()
    {
        blockerGeometryRevision = 0;
        for (int index = 0;
             index < blockerChangeHistory.Length;
             index++)
        {
            blockerChangeHistory[index] = default(BlockerChangeRecord);
        }
    }

    public void SetVisionMaskRadius(float radius)
    {
        sceneFallbackVisionRadius = Mathf.Max(0.1f, radius);
        ApplyVisionRadius(sceneFallbackVisionRadius);
    }

    private void ApplyVisionRadius(float radius)
    {
        radius = Mathf.Max(0.1f, radius);
        if (Mathf.Approximately(visionRadius, radius)) return;
        visionRadius = radius;
        coverGeometryDirty = true;
        forceVisualSample = true;
        forceBoundarySample = true;
    }

    public void RefreshSceneCameraSettings()
    {
        mainCamera = GetComponent<Camera>();
        visibilityPolygonBuilder.ClearCaches();
        InvalidateVisibilitySourceCaches();
        EnsureDefaults();
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
        SyncBlocksCamera();
        SyncUiCompositeCamera();
        coverGeometryDirty = true;
        forceVisualSample = true;
        forceBoundarySample = true;
        UpdateVision(true);
    }

    private void Awake()
    {
        sceneFallbackVisionRadius = Mathf.Max(0.1f, visionRadius);
        mainCamera = GetComponent<Camera>();
        EnsureDefaults();
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
        UpdateVision(true);
    }

    private void LateUpdate()
    {
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
        SyncBlocksCamera();
        SyncUiCompositeCamera();
        UpdateVision(false);
        // Disable all reveal sources (including the puppet) while retaining the full-screen cover.
        bool transferring = SoulMarkRuntime.IsVisionCovered;
        if (visibilityStencilRenderer != null) visibilityStencilRenderer.enabled = !transferring;
        if (overlayMaterial != null) overlayMaterial.color = transferring ? Color.black : darknessColor;
        if (blocksCamera != null) blocksCamera.enabled = !transferring;
    }

    private void EnsureBlockLayerConfiguration()
    {
        if (blockLayers.value == 0)
        {
            int blocksLayer = LayerMask.NameToLayer("Blocks");
            if (blocksLayer >= 0)
            {
                blockLayers = 1 << blocksLayer;
            }
        }

        int hiddenBlocksLayer = LayerMask.NameToLayer("Hidden Blocks");
        hiddenBlocksLayerMask = hiddenBlocksLayer >= 0 ? 1 << hiddenBlocksLayer : 0;
        // Include hidden walls in every visibility query and streaming retention,
        // even when an older scene/prefab still serializes only the Blocks bit.
        blockLayers = blockLayers.value | hiddenBlocksLayerMask;
    }

    private void EnsureDefaults()
    {
        EnsureBlockLayerConfiguration();

        visionRadius = Mathf.Max(0.1f, visionRadius);
        rayCount = Mathf.Clamp(rayCount, 24, 192);
        visualRaycastFrequency = Mathf.Clamp(visualRaycastFrequency, 10f, 60f);
        streamingBoundaryRefreshInterval =
            Mathf.Max(0.02f, streamingBoundaryRefreshInterval);
        visualOriginSmoothTime = Mathf.Max(0f, visualOriginSmoothTime);
        edgeSoftness = Mathf.Max(0f, edgeSoftness);
        cornerDepthDiscontinuity = Mathf.Max(
            0.01f,
            cornerDepthDiscontinuity);
        cornerRefinementIterations = Mathf.Clamp(
            cornerRefinementIterations,
            2,
            8);
        teleportRefreshDistance = Mathf.Max(0.05f, teleportRefreshDistance);
        visibilityPolygonCircleSegments = Mathf.Clamp(
            visibilityPolygonCircleSegments,
            48,
            256);
        visibilityCornerRayOffset = Mathf.Clamp(
            visibilityCornerRayOffset,
            0.005f,
            0.25f);
        movingVisibilityRefreshRate = Mathf.Clamp(
            movingVisibilityRefreshRate,
            15f,
            60f);
        visibilityRebuildMovementThreshold = Mathf.Max(
            0.001f,
            visibilityRebuildMovementThreshold);
        blockerQueryMovementThreshold = Mathf.Max(
            0.05f,
            blockerQueryMovementThreshold);
        blockerQueryPadding = Mathf.Max(
            blockerQueryMovementThreshold + 0.05f,
            blockerQueryPadding);
        maxMovingAdditionalSourceRebuildsPerFrame = Mathf.Clamp(
            maxMovingAdditionalSourceRebuildsPerFrame,
            1,
            MaximumRevealSources - 1);
        EnsureRayDirectionCaches();
    }

    private void InvalidateVisibilitySourceCaches()
    {
        for (int index = 0; index < visibilitySourceCaches.Length; index++)
        {
            visibilitySourceCaches[index].Invalidate();
        }
        cachedVisibilitySourceCount = 0;
        nextAdditionalVisibilitySourceIndex = 1;
    }

    private void EnsureOverlay()
    {
        if (overlayMeshFilter != null && overlayRenderer != null)
        {
            EnsureOverlayMaterial();
            EnsureVisibilityStencil();
            return;
        }

        Transform existingOverlay = transform.Find(VisionOverlayName);
        GameObject overlayObject = existingOverlay != null
            ? existingOverlay.gameObject
            : new GameObject(VisionOverlayName);

        overlayObject.transform.SetParent(transform, false);
        overlayObject.transform.localPosition = new Vector3(0f, 0f, Mathf.Abs(mainCamera.nearClipPlane) + 0.5f);
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = Vector3.one;

        overlayMeshFilter = overlayObject.GetComponent<MeshFilter>();
        if (overlayMeshFilter == null)
        {
            overlayMeshFilter = overlayObject.AddComponent<MeshFilter>();
        }

        overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
        {
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        }

        overlayMesh = overlayMeshFilter.sharedMesh;
        if (overlayMesh == null)
        {
            overlayMesh = new Mesh();
            overlayMesh.name = "Circular Vision Darkness Mesh";
            overlayMeshFilter.sharedMesh = overlayMesh;
            coverGeometryDirty = true;
        }

        EnsureOverlayMaterial();
        EnsureVisibilityStencil();
        // Keep the world-darkness mask above ordinary scene sprites but below
        // attribute windows (31000) and the persistent HUD (32760).
        overlayRenderer.sortingOrder = 30000;
    }

    private void EnsureVisibilityStencil()
    {
        if (visibilityStencilMeshFilter != null &&
            visibilityStencilRenderer != null)
        {
            EnsureVisibilityStencilMaterial();
            return;
        }

        Transform existing = transform.Find(VisibilityStencilName);
        GameObject stencilObject = existing != null
            ? existing.gameObject
            : new GameObject(VisibilityStencilName);
        stencilObject.transform.SetParent(transform, false);
        stencilObject.transform.localPosition = new Vector3(
            0f,
            0f,
            Mathf.Abs(mainCamera.nearClipPlane) + 0.45f);
        stencilObject.transform.localRotation = Quaternion.identity;
        stencilObject.transform.localScale = Vector3.one;

        visibilityStencilMeshFilter =
            stencilObject.GetComponent<MeshFilter>();
        if (visibilityStencilMeshFilter == null)
        {
            visibilityStencilMeshFilter =
                stencilObject.AddComponent<MeshFilter>();
        }

        visibilityStencilRenderer =
            stencilObject.GetComponent<MeshRenderer>();
        if (visibilityStencilRenderer == null)
        {
            visibilityStencilRenderer =
                stencilObject.AddComponent<MeshRenderer>();
        }

        visibilityStencilMesh = visibilityStencilMeshFilter.sharedMesh;
        if (visibilityStencilMesh == null)
        {
            visibilityStencilMesh = new Mesh
            {
                name = "Exact Camera Visibility Polygon"
            };
            visibilityStencilMesh.indexFormat =
                UnityEngine.Rendering.IndexFormat.UInt32;
            visibilityStencilMesh.MarkDynamic();
            visibilityStencilMeshFilter.sharedMesh = visibilityStencilMesh;
        }

        visibilityStencilRenderer.sortingOrder = 29999;
        visibilityStencilRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        visibilityStencilRenderer.receiveShadows = false;
        EnsureVisibilityStencilMaterial();
    }

    private void EnsureVisibilityStencilMaterial()
    {
        if (visibilityStencilRenderer == null)
        {
            return;
        }

        Shader shader = Shader.Find(
            "Hidden/Cogitans/CameraVisibilityStencil");
        if (shader == null)
        {
            visibilityStencilRenderer.enabled = false;
            return;
        }

        visibilityStencilRenderer.enabled = true;
        if (visibilityStencilMaterial == null ||
            visibilityStencilMaterial.shader != shader)
        {
            visibilityStencilMaterial = new Material(shader)
            {
                name = "Camera Visibility Stencil Material"
            };
        }
        visibilityStencilRenderer.sharedMaterial = visibilityStencilMaterial;
    }

    private void EnsureOverlayMaterial()
    {
        if (overlayRenderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/Cogitans/CameraVisionReveal");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        // Domain reloads can leave the generated child renderer alive while
        // this non-serialized reference is cleared. Reuse its material when
        // possible and replace the old fallback material once the reveal
        // shader has become available.
        if (overlayMaterial == null && overlayRenderer.sharedMaterial != null &&
            overlayRenderer.sharedMaterial.shader == shader)
        {
            overlayMaterial = overlayRenderer.sharedMaterial;
        }

        if (overlayMaterial == null || overlayMaterial.shader != shader)
        {
            overlayMaterial = new Material(shader)
            {
                name = "Circular Vision Reveal Material"
            };
        }

        overlayMaterial.color = darknessColor;
        if (overlayMaterial.HasProperty(EdgeSoftnessId))
        {
            overlayMaterial.SetFloat(EdgeSoftnessId, edgeSoftness);
        }
        overlayRenderer.sharedMaterial = overlayMaterial;
    }

    private void EnsureBlocksCamera()
    {
        if (blocksCamera != null)
        {
            return;
        }

        Transform existingBlocksCamera = transform.Find(BlocksCameraName);
        GameObject blocksCameraObject = existingBlocksCamera != null
            ? existingBlocksCamera.gameObject
            : new GameObject(BlocksCameraName);

        blocksCameraObject.transform.SetParent(transform, false);
        blocksCamera = blocksCameraObject.GetComponent<Camera>();
        if (blocksCamera == null)
        {
            blocksCamera = blocksCameraObject.AddComponent<Camera>();
        }

        blocksCamera.clearFlags = CameraClearFlags.Depth;
        blocksCamera.cullingMask = GetOverlayCullingMask();
        blocksCamera.depth = mainCamera.depth + 2f;
        blocksCamera.enabled = true;
        SyncBlocksCamera();
    }

    private void SyncBlocksCamera()
    {
        if (blocksCamera == null)
        {
            return;
        }

        blocksCamera.transform.localPosition = Vector3.zero;
        blocksCamera.transform.localRotation = Quaternion.identity;
        blocksCamera.orthographic = mainCamera.orthographic;
        blocksCamera.orthographicSize = mainCamera.orthographicSize;
        blocksCamera.fieldOfView = mainCamera.fieldOfView;
        blocksCamera.nearClipPlane = mainCamera.nearClipPlane;
        blocksCamera.farClipPlane = mainCamera.farClipPlane;
        blocksCamera.rect = mainCamera.rect;
        blocksCamera.backgroundColor = mainCamera.backgroundColor;
        blocksCamera.cullingMask = GetOverlayCullingMask();
        blocksCamera.depth = mainCamera.depth + 2f;
    }

    private int GetOverlayCullingMask()
    {
        // Ordinary Blocks are intentionally visible above darkness. Hidden
        // Blocks render only in the main world pass, beneath the same mask
        // that hides characters and props outside the visible polygon.
        return blockLayers.value & ~hiddenBlocksLayerMask;
    }

    private void EnsureUiCompositeCamera()
    {
        if (uiCompositeCamera != null)
        {
            return;
        }

        Transform existing = transform.Find(UiCompositeCameraName);
        GameObject cameraObject = existing != null
            ? existing.gameObject
            : new GameObject(UiCompositeCameraName);
        cameraObject.transform.SetParent(transform, false);
        uiCompositeCamera = cameraObject.GetComponent<Camera>();
        if (uiCompositeCamera == null)
        {
            uiCompositeCamera = cameraObject.AddComponent<Camera>();
        }

        uiCompositeCamera.clearFlags = CameraClearFlags.Depth;
        uiCompositeCamera.depth = mainCamera.depth + 3f;
        uiCompositeCamera.enabled = true;
        SyncUiCompositeCamera();
    }

    private void SyncUiCompositeCamera()
    {
        if (uiCompositeCamera == null)
        {
            return;
        }

        uiCompositeCamera.transform.localPosition = Vector3.zero;
        uiCompositeCamera.transform.localRotation = Quaternion.identity;
        uiCompositeCamera.orthographic = mainCamera.orthographic;
        uiCompositeCamera.orthographicSize = mainCamera.orthographicSize;
        uiCompositeCamera.fieldOfView = mainCamera.fieldOfView;
        uiCompositeCamera.nearClipPlane = mainCamera.nearClipPlane;
        uiCompositeCamera.farClipPlane = mainCamera.farClipPlane;
        uiCompositeCamera.rect = mainCamera.rect;
        uiCompositeCamera.backgroundColor = mainCamera.backgroundColor;
        uiCompositeCamera.cullingMask = GetUiCullingMask();
        uiCompositeCamera.depth = mainCamera.depth + 3f;
    }

    private static int GetUiCullingMask()
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        return uiLayer >= 0 ? 1 << uiLayer : 0;
    }

    private void UpdateVision(bool forceRefresh)
    {
        if (mainCamera == null || overlayMesh == null)
        {
            return;
        }

        ZeldaFourWayMover controlledMover = ZeldaRuntimeRegistry.GetControlledMover();
        ZeldaCharacterData characterData = controlledMover != null ? controlledMover.CharacterData : null;
        ApplyVisionRadius(characterData != null
            ? characterData.GetPlayerVisionRadius(gameObject.scene.path) : sceneFallbackVisionRadius);
        EnsureRayDirectionCaches();
        if (forceRefresh || NeedsCoverGeometryRebuild())
        {
            RebuildCoverMesh();
        }

        float deltaTime = Time.unscaledDeltaTime;
        visualRaycastTimer -= deltaTime;
        boundaryRefreshTimer -= deltaTime;

        Transform primaryTransform = GetPrimaryVisionTransform();
        Vector2 primaryOrigin = GetPrimaryVisionOrigin();
        bool primaryChanged = primaryTransform != sampledPrimaryTransform;
        Vector2 lastTargetOrigin = sampledRevealSourceCount > 0
            ? new Vector2(
                targetRevealSources[0].x,
                targetRevealSources[0].y)
            : primaryOrigin;
        bool primaryTeleported = sampledRevealSourceCount > 0 &&
            (lastTargetOrigin - primaryOrigin).sqrMagnitude >=
            teleportRefreshDistance * teleportRefreshDistance;
        bool selectedSourceUnavailable = HasUnavailableSampledSource();
        bool forceVisibilityRebuild =
            forceRefresh || forceVisualSample || primaryChanged ||
            primaryTeleported || selectedSourceUnavailable;

        if (forceVisibilityRebuild ||
            visualRaycastTimer <= 0f)
        {
            SampleVisualRevealSources(
                primaryTransform,
                primaryOrigin,
                forceRefresh || forceVisualSample || primaryChanged ||
                primaryTeleported || selectedSourceUnavailable);
            visualRaycastTimer = 1f /
                Mathf.Max(10f, visualRaycastFrequency);
            forceVisualSample = false;
        }

        UpdateVisualSourcePositions(primaryOrigin);
        RebuildVisibilityStencilMesh(forceVisibilityRebuild);
        UploadRevealShaderData();

        bool boundaryTeleported = hasBoundarySample &&
            (primaryOrigin - lastBoundaryOrigin).sqrMagnitude >=
            teleportRefreshDistance * teleportRefreshDistance;
        if (forceRefresh || forceBoundarySample || boundaryTeleported ||
            boundaryRefreshTimer <= 0f)
        {
            RefreshVisibleBoundary(primaryOrigin);
            boundaryRefreshTimer = streamingBoundaryRefreshInterval;
            forceBoundarySample = false;
        }
    }

    private bool NeedsCoverGeometryRebuild()
    {
        return coverGeometryDirty ||
            mainCamera.orthographic != lastCoverOrthographic ||
            !Mathf.Approximately(
                mainCamera.orthographicSize,
                lastCoverOrthographicSize) ||
            !Mathf.Approximately(mainCamera.aspect, lastCoverAspect) ||
            !Mathf.Approximately(mainCamera.fieldOfView, lastCoverFieldOfView) ||
            !Mathf.Approximately(coverPadding, lastCoverPadding);
    }

    private void RebuildCoverMesh()
    {
        if (overlayMesh == null)
        {
            return;
        }

        vertices.Clear();
        triangles.Clear();
        AddCameraCoverQuad();
        overlayMesh.Clear();
        overlayMesh.SetVertices(vertices);
        overlayMesh.SetTriangles(triangles, 0);
        overlayMesh.RecalculateBounds();

        lastCoverOrthographic = mainCamera.orthographic;
        lastCoverOrthographicSize = mainCamera.orthographicSize;
        lastCoverAspect = mainCamera.aspect;
        lastCoverFieldOfView = mainCamera.fieldOfView;
        lastCoverPadding = coverPadding;
        coverGeometryDirty = false;
    }

    private void RefreshVisibleBoundary(Vector2 primaryOrigin)
    {
        visibleBoundaryPoints.Clear();
        for (int index = 0; index < rayCount; index++)
        {
            Vector2 direction = boundaryRayDirections[index];
            visibleBoundaryPoints.Add(
                primaryOrigin + direction * GetVisibleDistance(
                    primaryOrigin,
                    direction));
        }
        lastBoundaryOrigin = primaryOrigin;
        hasBoundarySample = true;
    }

    private void SampleVisualRevealSources(
        Transform primaryTransform,
        Vector2 primaryOrigin,
        bool resetSmoothing)
    {
        revealSourceCandidates.Clear();
        foreach (CameraVisionRevealSource source in
                 CameraVisionRevealSource.ActiveSources)
        {
            if (source != null && source.isActiveAndEnabled &&
                IsRevealSourceRelevant(source))
            {
                revealSourceCandidates.Add(source);
            }
        }
        revealSourceCandidates.Sort(CompareRevealSourcesByCameraDistance);

        int nextCount = Mathf.Min(
            MaximumRevealSources,
            1 + revealSourceCandidates.Count);
        bool sourceLayoutChanged =
            nextCount != sampledRevealSourceCount ||
            primaryTransform != sampledPrimaryTransform;
        for (int sourceIndex = 1; sourceIndex < nextCount; sourceIndex++)
        {
            CameraVisionRevealSource nextSource =
                revealSourceCandidates[sourceIndex - 1];
            if (sampledAdditionalSources[sourceIndex - 1] != nextSource)
            {
                sourceLayoutChanged = true;
            }
            sampledAdditionalSources[sourceIndex - 1] = nextSource;
        }
        for (int sourceIndex = nextCount;
             sourceIndex < MaximumRevealSources;
             sourceIndex++)
        {
            sampledAdditionalSources[sourceIndex - 1] = null;
        }

        sampledPrimaryTransform = primaryTransform;
        sampledRevealSourceCount = nextCount;
        WriteTargetRevealSource(0, primaryOrigin, visionRadius);
        for (int sourceIndex = 1; sourceIndex < nextCount; sourceIndex++)
        {
            CameraVisionRevealSource source =
                sampledAdditionalSources[sourceIndex - 1];
            WriteTargetRevealSource(
                sourceIndex,
                source.WorldPosition,
                source.Radius);
        }

        if (resetSmoothing || sourceLayoutChanged)
        {
            ResetDisplayedRevealData();
        }
    }

    private void WriteTargetRevealSource(
        int sourceIndex,
        Vector2 position,
        float radius)
    {
        sampledRaycastOrigins[sourceIndex] = position;
        targetRevealSources[sourceIndex] = new Vector4(
            position.x,
            position.y,
            radius,
            0f);
    }

    private void RebuildVisibilityStencilMesh(bool forceRebuild)
    {
        if (visibilityStencilMesh == null ||
            visibilityStencilRenderer == null)
        {
            return;
        }

        SyncVisibilityStencilWorldTransform();
        float currentTime = Time.unscaledTime;
        float movementThresholdSquared =
            visibilityRebuildMovementThreshold *
            visibilityRebuildMovementThreshold;
        bool combinedMeshDirty =
            forceRebuild ||
            cachedVisibilitySourceCount != sampledRevealSourceCount;

        int additionalSourceCount = Mathf.Max(
            0,
            sampledRevealSourceCount - 1);
        int routineAdditionalRebuildsRemaining =
            maxMovingAdditionalSourceRebuildsPerFrame;
        int additionalStartIndex = additionalSourceCount > 0
            ? 1 + (nextAdditionalVisibilitySourceIndex - 1) %
                additionalSourceCount
            : 1;
        for (int iteration = 0;
             iteration < sampledRevealSourceCount;
             iteration++)
        {
            int sourceIndex = iteration == 0
                ? 0
                : 1 + (additionalStartIndex - 1 + iteration - 1) %
                    additionalSourceCount;
            Vector4 source = targetRevealSources[sourceIndex];
            Vector2 position = new Vector2(source.x, source.y);
            UnityEngine.Object sourceKey = sourceIndex == 0
                ? (sampledPrimaryTransform != null
                    ? (UnityEngine.Object)sampledPrimaryTransform
                    : this)
                : sampledAdditionalSources[sourceIndex - 1];
            VisibilitySourceMeshCache cache =
                visibilitySourceCaches[sourceIndex];
            bool sourceChanged = cache.SourceKey != sourceKey;
            bool radiusChanged = !Mathf.Approximately(
                cache.LastBuiltRadius,
                source.z);
            float movedDistanceSquared =
                (position - cache.LastBuiltPosition).sqrMagnitude;
            bool movedEnough = movedDistanceSquared >=
                movementThresholdSquared;
            bool teleported = movedDistanceSquared >=
                teleportRefreshDistance * teleportRefreshDistance;
            bool blockerChanged = DoesBlockerChangeAffectSource(
                cache.BlockerRevision,
                position,
                source.z + blockerQueryPadding);
            if (!blockerChanged && cache.BlockerRevision !=
                blockerGeometryRevision)
            {
                // Far-away changes cannot affect this cached polygon. Consume
                // their revisions so they are not scanned again next frame.
                cache.BlockerRevision = blockerGeometryRevision;
                cache.QueryCache.BlockerRevision = blockerGeometryRevision;
            }
            bool movingRefreshDue = movedEnough &&
                currentTime >= cache.NextRefreshTime;
            bool mandatoryRebuild = forceRebuild || !cache.IsValid ||
                sourceChanged || radiusChanged || teleported ||
                blockerChanged;
            bool routineMovingRebuild = !mandatoryRebuild &&
                movingRefreshDue;
            if (sourceIndex > 0 && routineMovingRebuild &&
                routineAdditionalRebuildsRemaining <= 0)
            {
                continue;
            }

            bool rebuildSource = mandatoryRebuild || routineMovingRebuild;
            if (!rebuildSource)
            {
                continue;
            }

            cache.Vertices.Clear();
            cache.Triangles.Clear();
            visibilityPolygonBuilder.AppendVisibilityPolygon(
                visibilityStencilRenderer.transform,
                position,
                source.z,
                blockLayers,
                blockerSkin,
                visibilityPolygonCircleSegments,
                visibilityCornerRayOffset,
                cache.QueryCache,
                blockerGeometryRevision,
                blockerQueryMovementThreshold,
                blockerQueryPadding,
                cache.Vertices,
                cache.Triangles);
            cache.SourceKey = sourceKey;
            cache.LastBuiltPosition = position;
            cache.LastBuiltRadius = source.z;
            cache.NextRefreshTime = currentTime +
                1f / Mathf.Max(15f, movingVisibilityRefreshRate);
            cache.BlockerRevision = blockerGeometryRevision;
            cache.IsValid = true;
            combinedMeshDirty = true;
            if (sourceIndex > 0 && routineMovingRebuild)
            {
                routineAdditionalRebuildsRemaining--;
                nextAdditionalVisibilitySourceIndex = sourceIndex + 1;
                if (nextAdditionalVisibilitySourceIndex >=
                    sampledRevealSourceCount)
                {
                    nextAdditionalVisibilitySourceIndex = 1;
                }
            }
        }

        for (int sourceIndex = sampledRevealSourceCount;
             sourceIndex < cachedVisibilitySourceCount;
             sourceIndex++)
        {
            visibilitySourceCaches[sourceIndex].Invalidate();
            combinedMeshDirty = true;
        }
        cachedVisibilitySourceCount = sampledRevealSourceCount;

        if (combinedMeshDirty)
        {
            CombineCachedVisibilityMeshes();
        }

        // Avoid the per-frame vertex scan performed by RecalculateBounds.
        // The renderer only needs a conservative world-space box overlapping
        // the current camera because its transform is kept at world identity.
        float coverDiameter = GetCoverRadius() * 2f;
        visibilityStencilMesh.bounds = new Bounds(
            new Vector3(transform.position.x, transform.position.y, 0f),
            new Vector3(coverDiameter, coverDiameter, 100f));
    }

    private static bool DoesBlockerChangeAffectSource(
        int sourceRevision,
        Vector2 sourcePosition,
        float sourceRadius)
    {
        if (sourceRevision == blockerGeometryRevision)
        {
            return false;
        }
        if (sourceRevision < 0 ||
            blockerGeometryRevision - sourceRevision >
            BlockerChangeHistoryCapacity)
        {
            return true;
        }

        float radiusSquared = sourceRadius * sourceRadius;
        for (int revision = sourceRevision + 1;
             revision <= blockerGeometryRevision;
             revision++)
        {
            BlockerChangeRecord change = blockerChangeHistory[
                revision % BlockerChangeHistoryCapacity];
            if (change.Revision != revision || !change.HasBounds)
            {
                return true;
            }

            Vector3 closest = change.Bounds.ClosestPoint(
                new Vector3(sourcePosition.x, sourcePosition.y, 0f));
            Vector2 separation = new Vector2(
                closest.x - sourcePosition.x,
                closest.y - sourcePosition.y);
            if (separation.sqrMagnitude <= radiusSquared)
            {
                return true;
            }
        }
        return false;
    }

    private void CombineCachedVisibilityMeshes()
    {
        visibilityStencilVertices.Clear();
        visibilityStencilTriangles.Clear();
        for (int sourceIndex = 0;
             sourceIndex < cachedVisibilitySourceCount;
             sourceIndex++)
        {
            VisibilitySourceMeshCache cache =
                visibilitySourceCaches[sourceIndex];
            if (!cache.IsValid)
            {
                continue;
            }

            int vertexOffset = visibilityStencilVertices.Count;
            visibilityStencilVertices.AddRange(cache.Vertices);
            for (int triangleIndex = 0;
                 triangleIndex < cache.Triangles.Count;
                 triangleIndex++)
            {
                visibilityStencilTriangles.Add(
                    vertexOffset + cache.Triangles[triangleIndex]);
            }
        }

        visibilityStencilMesh.Clear(false);
        visibilityStencilMesh.SetVertices(visibilityStencilVertices);
        visibilityStencilMesh.SetTriangles(
            visibilityStencilTriangles,
            0,
            false);
    }

    private void SyncVisibilityStencilWorldTransform()
    {
        Transform stencilTransform =
            visibilityStencilRenderer.transform;
        stencilTransform.position = Vector3.zero;
        stencilTransform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.lossyScale;
        stencilTransform.localScale = new Vector3(
            Mathf.Abs(parentScale.x) > 0.0001f
                ? 1f / parentScale.x
                : 1f,
            Mathf.Abs(parentScale.y) > 0.0001f
                ? 1f / parentScale.y
                : 1f,
            Mathf.Abs(parentScale.z) > 0.0001f
                ? 1f / parentScale.z
                : 1f);
    }

    private bool IsRevealIntervalDiscontinuous(
        int firstIndex,
        int secondIndex)
    {
        bool firstBlocked = targetRevealRayBlocked[firstIndex];
        bool secondBlocked = targetRevealRayBlocked[secondIndex];
        if (firstBlocked != secondBlocked)
        {
            return true;
        }
        if (!firstBlocked)
        {
            return false;
        }

        float depthJump = Mathf.Abs(
            targetRevealRayDistances[firstIndex] -
            targetRevealRayDistances[secondIndex]);
        if (depthJump < cornerDepthDiscontinuity)
        {
            return false;
        }

        Vector2 firstNormal = targetRevealRayHitNormals[firstIndex];
        Vector2 secondNormal = targetRevealRayHitNormals[secondIndex];
        float normalAgreement = Vector2.Dot(firstNormal, secondNormal);
        Vector2 averageNormal = firstNormal + secondNormal;
        if (averageNormal.sqrMagnitude > 0.000001f)
        {
            averageNormal.Normalize();
        }
        float planeSeparation = Mathf.Abs(Vector2.Dot(
            targetRevealRayHitPoints[secondIndex] -
                targetRevealRayHitPoints[firstIndex],
            averageNormal));
        bool samePlane = normalAgreement >= 0.985f &&
            planeSeparation <= Mathf.Max(0.04f, blockerSkin * 2f);
        return !samePlane;
    }

    private float FindRevealIntervalSplit(
        Vector2 origin,
        float radius,
        int firstRayIndex,
        int firstDistanceIndex,
        int secondDistanceIndex)
    {
        float firstSide = 0f;
        float secondSide = 1f;
        for (int iteration = 0;
             iteration < cornerRefinementIterations;
             iteration++)
        {
            float samplePosition = (firstSide + secondSide) * 0.5f;
            float angle = 360f *
                (firstRayIndex + samplePosition) / RevealRayCount;
            Vector2 direction = DirectionFromAngle(angle);
            SampleVisibleRay(
                origin,
                direction,
                radius,
                out Collider2D hitCollider,
                out Vector2 hitPoint,
                out Vector2 hitNormal);
            if (DoesCornerSampleMatchFirstSide(
                    hitCollider,
                    hitPoint,
                    hitNormal,
                    firstDistanceIndex,
                    secondDistanceIndex))
            {
                firstSide = samplePosition;
            }
            else
            {
                secondSide = samplePosition;
            }
        }
        return Mathf.Clamp(
            (firstSide + secondSide) * 0.5f,
            0.001f,
            0.999f);
    }

    private bool DoesCornerSampleMatchFirstSide(
        Collider2D hitCollider,
        Vector2 hitPoint,
        Vector2 hitNormal,
        int firstIndex,
        int secondIndex)
    {
        bool sampleBlocked = hitCollider != null;
        bool firstBlocked = targetRevealRayBlocked[firstIndex];
        bool secondBlocked = targetRevealRayBlocked[secondIndex];
        if (firstBlocked != secondBlocked)
        {
            return sampleBlocked == firstBlocked;
        }
        if (!sampleBlocked)
        {
            return false;
        }

        Collider2D firstCollider = targetRevealRayColliders[firstIndex];
        Collider2D secondCollider = targetRevealRayColliders[secondIndex];
        if (firstCollider != secondCollider)
        {
            if (hitCollider == firstCollider)
            {
                return true;
            }
            if (hitCollider == secondCollider)
            {
                return false;
            }
        }

        float firstPlaneDistance = DistanceFromHitPlane(
            hitPoint,
            hitNormal,
            firstIndex);
        float secondPlaneDistance = DistanceFromHitPlane(
            hitPoint,
            hitNormal,
            secondIndex);
        return firstPlaneDistance <= secondPlaneDistance;
    }

    private float DistanceFromHitPlane(
        Vector2 samplePoint,
        Vector2 sampleNormal,
        int referenceIndex)
    {
        Vector2 referenceNormal =
            targetRevealRayHitNormals[referenceIndex];
        Vector2 combinedNormal = referenceNormal + sampleNormal;
        if (combinedNormal.sqrMagnitude <= 0.000001f)
        {
            combinedNormal = referenceNormal;
        }
        if (combinedNormal.sqrMagnitude > 0.000001f)
        {
            combinedNormal.Normalize();
        }
        return Mathf.Abs(Vector2.Dot(
            samplePoint - targetRevealRayHitPoints[referenceIndex],
            combinedNormal));
    }

    private void UpdateVisualSourcePositions(Vector2 primaryOrigin)
    {
        if (sampledRevealSourceCount <= 0)
        {
            return;
        }

        Vector4 primary = targetRevealSources[0];
        primary.x = primaryOrigin.x;
        primary.y = primaryOrigin.y;
        primary.z = visionRadius;
        targetRevealSources[0] = primary;
        for (int sourceIndex = 1;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            CameraVisionRevealSource source =
                sampledAdditionalSources[sourceIndex - 1];
            if (source == null)
            {
                continue;
            }
            Vector2 position = source.WorldPosition;
            targetRevealSources[sourceIndex] = new Vector4(
                position.x,
                position.y,
                source.Radius,
                0f);
        }
    }

    private void UpdateDisplayedRevealData(float deltaTime)
    {
        for (int sourceIndex = 0;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            Vector4 target = targetRevealSources[sourceIndex];
            Vector4 displayed = revealShaderSources[sourceIndex];
            Vector2 targetPosition = new Vector2(target.x, target.y);
            Vector2 displayedPosition = new Vector2(displayed.x, displayed.y);
            if (deltaTime <= 0f || visualOriginSmoothTime <= 0f)
            {
                displayedPosition = targetPosition;
                revealSourceSmoothVelocities[sourceIndex] = Vector2.zero;
            }
            else
            {
                displayedPosition = Vector2.SmoothDamp(
                    displayedPosition,
                    targetPosition,
                    ref revealSourceSmoothVelocities[sourceIndex],
                    visualOriginSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
            revealShaderSources[sourceIndex] = new Vector4(
                displayedPosition.x,
                displayedPosition.y,
                target.z,
                0f);

            int firstRay = sourceIndex * RevealRayCount;
            Vector2 originDelta =
                displayedPosition - sampledRaycastOrigins[sourceIndex];
            for (int rayIndex = 0; rayIndex < RevealRayCount; rayIndex++)
            {
                int distanceIndex = firstRay + rayIndex;
                float targetDistance =
                    targetRevealRayDistances[distanceIndex];
                if (targetRevealRayBlocked[distanceIndex])
                {
                    // Reproject the current ray onto the plane sampled from
                    // the collider. The previous direction-only correction
                    // was exact only for walls perpendicular to that ray and
                    // therefore left increasingly large trails on distant or
                    // diagonal walls while the source moved.
                    Vector2 hitNormal =
                        targetRevealRayHitNormals[distanceIndex];
                    float rayPlaneDenominator = Vector2.Dot(
                        revealRayDirections[rayIndex],
                        hitNormal);
                    if (Mathf.Abs(rayPlaneDenominator) > 0.0001f)
                    {
                        float planeDistance = Vector2.Dot(
                            targetRevealRayHitPoints[distanceIndex] -
                                displayedPosition,
                            hitNormal) / rayPlaneDenominator;
                        targetDistance = Mathf.Clamp(
                            planeDistance - blockerSkin,
                            0f,
                            target.z);
                    }
                    else
                    {
                        targetDistance = Mathf.Clamp(
                            targetDistance - Vector2.Dot(
                                originDelta,
                                revealRayDirections[rayIndex]),
                            0f,
                            target.z);
                    }
                }
                // Origin compensation already moves a sampled wall boundary
                // continuously between raycast updates. Applying a second
                // expansion delay here makes the boundary trail behind when
                // the character moves away from a wall, producing a dark echo
                // on the opposite side. Commit the compensated result directly.
                displayedRevealRayDistances[distanceIndex] = targetDistance;
            }

            RefreshMovingRevealCornerSplits(
                sourceIndex,
                displayedPosition,
                originDelta,
                target.z);
        }
    }

    private void RefreshMovingRevealCornerSplits(
        int sourceIndex,
        Vector2 currentOrigin,
        Vector2 originDelta,
        float radius)
    {
        if (originDelta.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        int firstRay = sourceIndex * RevealRayCount;
        for (int rayIndex = 0; rayIndex < RevealRayCount; rayIndex++)
        {
            int firstIndex = firstRay + rayIndex;
            if (!targetRevealIntervalDiscontinuity[firstIndex])
            {
                continue;
            }

            int secondIndex = firstRay +
                (rayIndex + 1) % RevealRayCount;
            float split = FindRevealIntervalSplit(
                currentOrigin,
                radius,
                rayIndex,
                firstIndex,
                secondIndex);
            targetRevealIntervalSplit[firstIndex] = split;

            // The corner has crossed one of the interval's endpoint rays.
            // Request a complete fan sample on the next frame so ownership of
            // the discontinuity moves to the adjacent interval immediately.
            if (split <= 0.01f || split >= 0.99f)
            {
                forceVisualSample = true;
            }
        }
    }

    private void ResetDisplayedRevealData()
    {
        for (int sourceIndex = 0;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            revealShaderSources[sourceIndex] =
                targetRevealSources[sourceIndex];
            revealSourceSmoothVelocities[sourceIndex] = Vector2.zero;
            int firstRay = sourceIndex * RevealRayCount;
            for (int rayIndex = 0; rayIndex < RevealRayCount; rayIndex++)
            {
                int distanceIndex = firstRay + rayIndex;
                displayedRevealRayDistances[distanceIndex] =
                    targetRevealRayDistances[distanceIndex];
            }
        }
    }

    private void UploadRevealShaderData()
    {
        if (overlayMaterial == null || overlayMaterial.shader == null ||
            overlayMaterial.shader.name !=
                "Hidden/Cogitans/CameraVisionReveal")
        {
            return;
        }

        // Visibility is now cut from the darkness through the exact polygon
        // stencil. The overlay only needs its existing colour; no sampled ray
        // distances are interpolated in the shader anymore.
        overlayMaterial.color = darknessColor;
    }

    private float GetPackedRevealDistance(int distanceIndex)
    {
        float distance = displayedRevealRayDistances[distanceIndex];
        if (!targetRevealIntervalDiscontinuity[distanceIndex])
        {
            return distance;
        }

        // A negative value marks a discontinuity. Its integer part stores the
        // distance at 1/1024-world-unit precision and its fractional part
        // stores the refined angular split, avoiding another shader array.
        float quantizedDistance = Mathf.Round(
            Mathf.Max(0f, distance) * 1024f);
        return -(quantizedDistance +
            targetRevealIntervalSplit[distanceIndex]);
    }

    private bool HasUnavailableSampledSource()
    {
        for (int sourceIndex = 1;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            CameraVisionRevealSource source =
                sampledAdditionalSources[sourceIndex - 1];
            if (source == null || !source.isActiveAndEnabled ||
                !IsRevealSourceRelevant(source))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsRevealSourceRelevant(CameraVisionRevealSource source)
    {
        if (mainCamera == null)
        {
            return true;
        }

        Vector2 cameraPosition = mainCamera.transform.position;
        Vector2 offset = source.WorldPosition - cameraPosition;
        float padding = source.Radius + coverPadding;
        if (mainCamera.orthographic)
        {
            float halfHeight = mainCamera.orthographicSize + padding;
            float halfWidth = mainCamera.orthographicSize *
                mainCamera.aspect + padding;
            return Mathf.Abs(offset.x) <= halfWidth &&
                Mathf.Abs(offset.y) <= halfHeight;
        }
        return offset.sqrMagnitude <=
            Mathf.Pow(GetCoverRadius() + source.Radius, 2f);
    }

    private int CompareRevealSourcesByCameraDistance(
        CameraVisionRevealSource first,
        CameraVisionRevealSource second)
    {
        Vector2 cameraPosition = mainCamera != null
            ? (Vector2)mainCamera.transform.position
            : Vector2.zero;
        float firstDistance =
            (first.WorldPosition - cameraPosition).sqrMagnitude;
        float secondDistance =
            (second.WorldPosition - cameraPosition).sqrMagnitude;
        return firstDistance.CompareTo(secondDistance);
    }

    private void EnsureRayDirectionCaches()
    {
        if (revealRayDirections[0] == Vector2.zero)
        {
            for (int index = 0; index < RevealRayCount; index++)
            {
                revealRayDirections[index] = DirectionFromAngle(
                    360f * index / RevealRayCount);
            }
        }

        if (boundaryRayDirections != null &&
            cachedBoundaryRayCount == rayCount)
        {
            return;
        }
        boundaryRayDirections = new Vector2[rayCount];
        for (int index = 0; index < rayCount; index++)
        {
            boundaryRayDirections[index] = DirectionFromAngle(
                360f * index / rayCount);
        }
        cachedBoundaryRayCount = rayCount;
        forceBoundarySample = true;
    }

    private void AddCameraCoverQuad()
    {
        Vector2 center = transform.position;
        float halfHeight;
        float halfWidth;
        if (mainCamera != null && mainCamera.orthographic)
        {
            halfHeight = mainCamera.orthographicSize + coverPadding;
            halfWidth = mainCamera.orthographicSize * mainCamera.aspect +
                coverPadding;
        }
        else
        {
            float radius = GetCoverRadius();
            halfHeight = radius;
            halfWidth = radius;
        }

        AddQuad(
            center + new Vector2(-halfWidth, -halfHeight),
            center + new Vector2(-halfWidth, halfHeight),
            center + new Vector2(halfWidth, halfHeight),
            center + new Vector2(halfWidth, -halfHeight));
    }

    private Vector2 GetPrimaryVisionOrigin()
    {
        Transform primaryTransform = GetPrimaryVisionTransform();
        return primaryTransform != null
            ? (Vector2)primaryTransform.position
            : (Vector2)transform.position;
    }

    private static Transform GetPrimaryVisionTransform()
    {
        ZeldaFourWayMover controlledMover =
            ZeldaRuntimeRegistry.GetControlledMover();
        return controlledMover != null ? controlledMover.transform : null;
    }

    private float GetVisibleDistance(Vector2 origin, Vector2 direction)
    {
        return GetVisibleDistance(origin, direction, visionRadius);
    }

    private float GetVisibleDistance(
        Vector2 origin,
        Vector2 direction,
        float maximumDistance)
    {
        return SampleVisibleRay(
            origin,
            direction,
            maximumDistance,
            out _,
            out _,
            out _);
    }

    private float SampleVisibleRay(
        Vector2 origin,
        Vector2 direction,
        float maximumDistance,
        out Collider2D hitCollider,
        out Vector2 hitPoint,
        out Vector2 hitNormal)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction,
            maximumDistance,
            blockLayers);
        hitCollider = hit.collider;
        if (hit.collider == null)
        {
            hitPoint = origin + direction * maximumDistance;
            hitNormal = Vector2.zero;
            return maximumDistance;
        }

        hitPoint = hit.point;
        hitNormal = hit.normal;
        return Mathf.Max(0f, hit.distance - blockerSkin);
    }

    /// <summary>
    /// Uses the same radius and Blocks raycast as the circular vision mesh to
    /// determine whether a world position is currently inside visible space.
    /// </summary>
    public bool IsWorldPositionVisible(Vector3 worldPosition)
    {
        return IsWorldPositionVisible(worldPosition, null);
    }

    /// <summary>
    /// Variant that treats Blocks colliders belonging to the target itself as
    /// visible instead of mistaking the target for an occluding wall.
    /// </summary>
    public bool IsWorldPositionVisible(Vector3 worldPosition, Transform visibleTarget)
    {
        if (IsInsideAdditionalReveal(worldPosition))
        {
            return true;
        }

        Vector2 origin = GetPrimaryVisionOrigin();
        Vector2 offset = (Vector2)worldPosition - origin;
        float distance = offset.magnitude;
        if (distance > visionRadius)
        {
            return false;
        }

        if (distance <= 0.0001f)
        {
            return true;
        }

        Vector2 direction = offset / distance;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, blockLayers);
        if (hit.collider != null)
        {
            Transform hitTransform = hit.collider.transform;
            bool belongsToTarget = visibleTarget != null &&
                                   (hitTransform == visibleTarget || hitTransform.IsChildOf(visibleTarget) ||
                                    visibleTarget.IsChildOf(hitTransform));
            return belongsToTarget;
        }

        return true;
    }

    /// <summary>
    /// Returns true when any portion of a world-space bounds intersects the
    /// same ray-defined visible region used by the circular mask.
    /// </summary>
    public bool IsWorldBoundsVisible(Bounds worldBounds, Transform visibleTarget)
    {
        if (IsWorldBoundsInsideAdditionalReveal(worldBounds))
        {
            return true;
        }

        Vector2 origin = GetPrimaryVisionOrigin();
        Vector2 boundaryOffset = hasBoundarySample
            ? origin - lastBoundaryOrigin
            : Vector2.zero;
        if (worldBounds.Contains(new Vector3(origin.x, origin.y, worldBounds.center.z)))
        {
            return true;
        }

        // Test object corners against the already-generated visible polygon.
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        if (IsPointInsideVisiblePolygon(
                new Vector2(min.x, min.y) - boundaryOffset) ||
            IsPointInsideVisiblePolygon(
                new Vector2(min.x, max.y) - boundaryOffset) ||
            IsPointInsideVisiblePolygon(
                new Vector2(max.x, min.y) - boundaryOffset) ||
            IsPointInsideVisiblePolygon(
                new Vector2(max.x, max.y) - boundaryOffset))
        {
            return true;
        }

        for (int i = 0; i < visibleBoundaryPoints.Count; i++)
        {
            Vector2 boundaryPoint =
                visibleBoundaryPoints[i] + boundaryOffset;
            if (worldBounds.Contains(new Vector3(
                    boundaryPoint.x,
                    boundaryPoint.y,
                    worldBounds.center.z)))
            {
                return true;
            }

            Vector2 nextPoint = visibleBoundaryPoints[
                (i + 1) % visibleBoundaryPoints.Count] + boundaryOffset;
            Vector2 edge = nextPoint - boundaryPoint;
            float edgeLength = edge.magnitude;
            if (edgeLength > 0.000001f &&
                SegmentIntersectsBounds(boundaryPoint, edge / edgeLength, edgeLength, worldBounds))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsWorldBoundsInsideAdditionalReveal(Bounds worldBounds)
    {
        for (int sourceIndex = 1;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            Vector2 sourcePoint = sampledRaycastOrigins[sourceIndex];
            if (worldBounds.Contains(new Vector3(
                    sourcePoint.x,
                    sourcePoint.y,
                    worldBounds.center.z)))
            {
                return true;
            }

            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            if (IsPointVisibleFromSampledSource(
                    sourceIndex,
                    worldBounds.center) ||
                IsPointVisibleFromSampledSource(
                    sourceIndex,
                    new Vector2(min.x, min.y)) ||
                IsPointVisibleFromSampledSource(
                    sourceIndex,
                    new Vector2(min.x, max.y)) ||
                IsPointVisibleFromSampledSource(
                    sourceIndex,
                    new Vector2(max.x, min.y)) ||
                IsPointVisibleFromSampledSource(
                    sourceIndex,
                    new Vector2(max.x, max.y)) ||
                IsPointVisibleFromSampledSource(
                    sourceIndex,
                    new Vector2(
                        Mathf.Clamp(sourcePoint.x, min.x, max.x),
                        Mathf.Clamp(sourcePoint.y, min.y, max.y))))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideAdditionalReveal(Vector2 worldPosition)
    {
        for (int sourceIndex = 1;
             sourceIndex < sampledRevealSourceCount;
             sourceIndex++)
        {
            if (IsPointVisibleFromSampledSource(
                    sourceIndex,
                    worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointVisibleFromSampledSource(
        int sourceIndex,
        Vector2 worldPosition)
    {
        if (sourceIndex <= 0 ||
            sourceIndex >= sampledRevealSourceCount)
        {
            return false;
        }

        Vector2 offset =
            worldPosition - sampledRaycastOrigins[sourceIndex];
        float distance = offset.magnitude;
        float radius = targetRevealSources[sourceIndex].z;
        if (distance > radius)
        {
            return false;
        }
        if (distance <= 0.0001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            sampledRaycastOrigins[sourceIndex],
            offset / distance,
            distance,
            blockLayers);
        return hit.collider == null ||
            hit.distance + blockerSkin >= distance;
    }

    private bool IsPointInsideVisiblePolygon(Vector2 point)
    {
        if (visibleBoundaryPoints.Count < 3)
        {
            return IsWorldPositionVisible(point);
        }

        bool inside = false;
        int previousIndex = visibleBoundaryPoints.Count - 1;
        for (int currentIndex = 0; currentIndex < visibleBoundaryPoints.Count; currentIndex++)
        {
            Vector2 current = visibleBoundaryPoints[currentIndex];
            Vector2 previous = visibleBoundaryPoints[previousIndex];
            bool crossesScanLine = (current.y > point.y) != (previous.y > point.y);
            if (crossesScanLine)
            {
                float crossingX = (previous.x - current.x) *
                                  (point.y - current.y) /
                                  (previous.y - current.y) +
                                  current.x;
                if (point.x < crossingX)
                {
                    inside = !inside;
                }
            }
            previousIndex = currentIndex;
        }

        return inside;
    }

    private static bool SegmentIntersectsBounds(
        Vector2 origin,
        Vector2 direction,
        float segmentLength,
        Bounds bounds)
    {
        float entryDistance = 0f;
        float exitDistance = segmentLength;
        if (!ClipAxis(origin.x, direction.x, bounds.min.x, bounds.max.x,
                ref entryDistance, ref exitDistance) ||
            !ClipAxis(origin.y, direction.y, bounds.min.y, bounds.max.y,
                ref entryDistance, ref exitDistance))
        {
            return false;
        }

        return exitDistance >= 0f && entryDistance <= segmentLength;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref float entryDistance,
        ref float exitDistance)
    {
        if (Mathf.Abs(direction) <= 0.000001f)
        {
            return origin >= minimum && origin <= maximum;
        }

        float first = (minimum - origin) / direction;
        float second = (maximum - origin) / direction;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        entryDistance = Mathf.Max(entryDistance, first);
        exitDistance = Mathf.Min(exitDistance, second);
        return entryDistance <= exitDistance;
    }

    private float GetCoverRadius()
    {
        if (mainCamera != null && mainCamera.orthographic)
        {
            float height = mainCamera.orthographicSize;
            float width = height * mainCamera.aspect;
            return Mathf.Max(visionRadius, Mathf.Sqrt(width * width + height * height)) + coverPadding;
        }

        return visionRadius + coverPadding;
    }

    private void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int startIndex = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(a));
        vertices.Add(transform.InverseTransformPoint(b));
        vertices.Add(transform.InverseTransformPoint(c));
        vertices.Add(transform.InverseTransformPoint(d));

        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void OnValidate()
    {
        EnsureBlockLayerConfiguration();
        visionRadius = Mathf.Max(0.1f, visionRadius);
        rayCount = Mathf.Clamp(rayCount, 24, 192);
        blockerSkin = Mathf.Max(0f, blockerSkin);
        coverPadding = Mathf.Max(0f, coverPadding);
        visualRaycastFrequency = Mathf.Clamp(
            visualRaycastFrequency,
            10f,
            60f);
        streamingBoundaryRefreshInterval = Mathf.Max(
            0.02f,
            streamingBoundaryRefreshInterval);
        visualOriginSmoothTime = Mathf.Max(0f, visualOriginSmoothTime);
        edgeSoftness = Mathf.Max(0f, edgeSoftness);
        cornerDepthDiscontinuity = Mathf.Max(
            0.01f,
            cornerDepthDiscontinuity);
        cornerRefinementIterations = Mathf.Clamp(
            cornerRefinementIterations,
            2,
            8);
        teleportRefreshDistance = Mathf.Max(
            0.05f,
            teleportRefreshDistance);
        visibilityPolygonCircleSegments = Mathf.Clamp(
            visibilityPolygonCircleSegments,
            48,
            256);
        visibilityCornerRayOffset = Mathf.Clamp(
            visibilityCornerRayOffset,
            0.005f,
            0.25f);
        movingVisibilityRefreshRate = Mathf.Clamp(
            movingVisibilityRefreshRate,
            15f,
            60f);
        visibilityRebuildMovementThreshold = Mathf.Max(
            0.001f,
            visibilityRebuildMovementThreshold);
        blockerQueryMovementThreshold = Mathf.Max(
            0.05f,
            blockerQueryMovementThreshold);
        blockerQueryPadding = Mathf.Max(
            blockerQueryMovementThreshold + 0.05f,
            blockerQueryPadding);
        maxMovingAdditionalSourceRebuildsPerFrame = Mathf.Clamp(
            maxMovingAdditionalSourceRebuildsPerFrame,
            1,
            MaximumRevealSources - 1);
        coverGeometryDirty = true;
        forceVisualSample = true;
        forceBoundarySample = true;
        EnsureRayDirectionCaches();
    }
}
