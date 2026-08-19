using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraCircularVision : MonoBehaviour
{
    private const int MaximumRevealSources = 8;
    private const int RevealRayCount = 64;
    private const int RevealRayVectorsPerSource = RevealRayCount / 4;
    private const string VisionOverlayName = "Circular Vision Overlay";
    private const string BlocksCameraName = "Blocks Overlay Camera";
    private const string UiCompositeCameraName = "UI CRT Composite Camera";

    [Header("Vision Mask Size")]
    [SerializeField, Min(0.1f), InspectorName("Vision Mask Radius")]
    [Tooltip("World-space radius used by both the visible mask and object streaming range.")]
    private float visionRadius = 5f;

    [Header("Vision Mask Shape")]
    [SerializeField] private int rayCount = 160;
    [SerializeField] private LayerMask blockLayers;
    [SerializeField] private Color darknessColor = new Color(5f / 255f, 18f / 255f, 37f / 255f, 1f);
    [SerializeField] private float blockerSkin = 0.03f;
    [SerializeField] private float coverPadding = 2f;

    private Camera mainCamera;
    private Camera blocksCamera;
    private Camera uiCompositeCamera;
    private MeshFilter overlayMeshFilter;
    private MeshRenderer overlayRenderer;
    private Mesh overlayMesh;
    private Material overlayMaterial;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private readonly List<Vector2> visibleBoundaryPoints = new List<Vector2>();
    private readonly Vector4[] revealShaderSources =
        new Vector4[MaximumRevealSources];
    private readonly Vector4[] revealShaderRayDistances =
        new Vector4[MaximumRevealSources * RevealRayVectorsPerSource];

    private static readonly int RevealSourceCountId =
        Shader.PropertyToID("_RevealSourceCount");
    private static readonly int RevealSourcesId =
        Shader.PropertyToID("_RevealSources");
    private static readonly int RevealRayDistancesId =
        Shader.PropertyToID("_RevealRayDistances");

    public LayerMask BlockLayers => blockLayers;
    public float VisionRadius => visionRadius;
    public float VisionMaskRadius => visionRadius;

    public void SetVisionMaskRadius(float radius)
    {
        visionRadius = Mathf.Max(0.1f, radius);
    }

    public void RefreshSceneCameraSettings()
    {
        mainCamera = GetComponent<Camera>();
        EnsureDefaults();
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
        SyncBlocksCamera();
        SyncUiCompositeCamera();
        RebuildOverlayMesh();
    }

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        EnsureDefaults();
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
    }

    private void LateUpdate()
    {
        EnsureOverlay();
        EnsureBlocksCamera();
        EnsureUiCompositeCamera();
        SyncBlocksCamera();
        SyncUiCompositeCamera();
        RebuildOverlayMesh();
    }

    private void EnsureDefaults()
    {
        if (blockLayers.value == 0)
        {
            int blocksLayer = LayerMask.NameToLayer("Blocks");
            if (blocksLayer >= 0)
            {
                blockLayers = 1 << blocksLayer;
            }
        }

        visionRadius = Mathf.Max(0.1f, visionRadius);
        rayCount = Mathf.Max(24, rayCount);
    }

    private void EnsureOverlay()
    {
        if (overlayMeshFilter != null && overlayRenderer != null)
        {
            EnsureOverlayMaterial();
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
        }

        EnsureOverlayMaterial();
        // Keep the world-darkness mask above ordinary scene sprites but below
        // attribute windows (31000) and the persistent HUD (32760).
        overlayRenderer.sortingOrder = 30000;
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
        return blockLayers.value;
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

    private void RebuildOverlayMesh()
    {
        if (overlayMesh == null)
        {
            return;
        }

        vertices.Clear();
        triangles.Clear();
        visibleBoundaryPoints.Clear();

        Vector2 origin = transform.position;
        float coverRadius = GetCoverRadius();
        float angleStep = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            float angleA = angleStep * i;
            float angleB = angleStep * (i + 1);
            Vector2 directionA = DirectionFromAngle(angleA);
            Vector2 directionB = DirectionFromAngle(angleB);

            Vector2 innerA = origin + directionA * GetVisibleDistance(origin, directionA);
            Vector2 innerB = origin + directionB * GetVisibleDistance(origin, directionB);
            Vector2 outerA = origin + directionA * coverRadius;
            Vector2 outerB = origin + directionB * coverRadius;

            visibleBoundaryPoints.Add(innerA);
            AddQuad(innerA, outerA, outerB, innerB);
        }

        overlayMesh.Clear();
        overlayMesh.SetVertices(vertices);
        overlayMesh.SetTriangles(triangles, 0);
        overlayMesh.RecalculateBounds();

        if (overlayMaterial != null)
        {
            overlayMaterial.color = darknessColor;
            UpdateAdditionalRevealShaderData();
        }
    }

    private void UpdateAdditionalRevealShaderData()
    {
        if (overlayMaterial == null || overlayMaterial.shader == null ||
            overlayMaterial.shader.name !=
                "Hidden/Cogitans/CameraVisionReveal")
        {
            return;
        }

        int count = 0;
        foreach (CameraVisionRevealSource source in
                 CameraVisionRevealSource.ActiveSources)
        {
            if (source == null || !source.isActiveAndEnabled ||
                count >= MaximumRevealSources)
            {
                continue;
            }

            Vector2 position = source.WorldPosition;
            revealShaderSources[count++] = new Vector4(
                position.x,
                position.y,
                source.Radius,
                0f);

            int sourceIndex = count - 1;
            for (int rayIndex = 0; rayIndex < RevealRayCount; rayIndex++)
            {
                Vector2 direction = DirectionFromAngle(
                    360f * rayIndex / RevealRayCount);
                float visibleDistance = GetVisibleDistance(
                    position,
                    direction,
                    source.Radius);
                int packedIndex = sourceIndex * RevealRayVectorsPerSource +
                    rayIndex / 4;
                Vector4 packedDistances =
                    revealShaderRayDistances[packedIndex];
                switch (rayIndex & 3)
                {
                    case 0:
                        packedDistances.x = visibleDistance;
                        break;
                    case 1:
                        packedDistances.y = visibleDistance;
                        break;
                    case 2:
                        packedDistances.z = visibleDistance;
                        break;
                    default:
                        packedDistances.w = visibleDistance;
                        break;
                }
                revealShaderRayDistances[packedIndex] = packedDistances;
            }
        }

        for (int index = count; index < MaximumRevealSources; index++)
        {
            revealShaderSources[index] = Vector4.zero;
        }

        int usedRayVectorCount = count * RevealRayVectorsPerSource;
        for (int index = usedRayVectorCount;
             index < revealShaderRayDistances.Length;
             index++)
        {
            revealShaderRayDistances[index] = Vector4.zero;
        }

        overlayMaterial.SetInt(RevealSourceCountId, count);
        overlayMaterial.SetVectorArray(
            RevealSourcesId,
            revealShaderSources);
        overlayMaterial.SetVectorArray(
            RevealRayDistancesId,
            revealShaderRayDistances);
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
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction,
            maximumDistance,
            blockLayers);
        if (hit.collider == null)
        {
            return maximumDistance;
        }

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

        Vector2 origin = transform.position;
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

        Vector2 origin = transform.position;
        if (worldBounds.Contains(new Vector3(origin.x, origin.y, worldBounds.center.z)))
        {
            return true;
        }

        // Test object corners against the already-generated visible polygon.
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, worldBounds.center.z),
            new Vector3(min.x, max.y, worldBounds.center.z),
            new Vector3(max.x, min.y, worldBounds.center.z),
            new Vector3(max.x, max.y, worldBounds.center.z)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            if (IsPointInsideVisiblePolygon(corners[i]))
            {
                return true;
            }
        }

        for (int i = 0; i < visibleBoundaryPoints.Count; i++)
        {
            Vector2 boundaryPoint = visibleBoundaryPoints[i];
            if (worldBounds.Contains(new Vector3(
                    boundaryPoint.x,
                    boundaryPoint.y,
                    worldBounds.center.z)))
            {
                return true;
            }

            Vector2 nextPoint = visibleBoundaryPoints[(i + 1) % visibleBoundaryPoints.Count];
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
        foreach (CameraVisionRevealSource source in
                 CameraVisionRevealSource.ActiveSources)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            Vector2 sourcePoint = source.WorldPosition;
            if (worldBounds.Contains(new Vector3(
                    sourcePoint.x,
                    sourcePoint.y,
                    worldBounds.center.z)))
            {
                return true;
            }

            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            if (IsVisibleFromRevealSource(source, worldBounds.center) ||
                IsVisibleFromRevealSource(
                    source,
                    new Vector2(min.x, min.y)) ||
                IsVisibleFromRevealSource(
                    source,
                    new Vector2(min.x, max.y)) ||
                IsVisibleFromRevealSource(
                    source,
                    new Vector2(max.x, min.y)) ||
                IsVisibleFromRevealSource(
                    source,
                    new Vector2(max.x, max.y)) ||
                IsVisibleFromRevealSource(
                    source,
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
        foreach (CameraVisionRevealSource source in
                 CameraVisionRevealSource.ActiveSources)
        {
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            if (IsVisibleFromRevealSource(source, worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsVisibleFromRevealSource(
        CameraVisionRevealSource source,
        Vector2 worldPosition)
    {
        Vector2 offset = worldPosition - source.WorldPosition;
        float distance = offset.magnitude;
        if (distance > source.Radius)
        {
            return false;
        }
        if (distance <= 0.0001f)
        {
            return true;
        }

        float visibleDistance = GetVisibleDistance(
            source.WorldPosition,
            offset / distance,
            source.Radius);
        return distance <= visibleDistance + 0.001f;
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
        visionRadius = Mathf.Max(0.1f, visionRadius);
        rayCount = Mathf.Max(24, rayCount);
        blockerSkin = Mathf.Max(0f, blockerSkin);
        coverPadding = Mathf.Max(0f, coverPadding);
    }
}
