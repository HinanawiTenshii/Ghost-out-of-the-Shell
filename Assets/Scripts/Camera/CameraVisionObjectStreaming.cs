using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Suspends spatial scene objects outside the camera viewport plus a preload
/// margin. Character vision/occlusion remains independent of object streaming.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera), typeof(CameraCircularVision))]
public sealed class CameraVisionObjectStreaming : MonoBehaviour
{
    [Header("Streaming Toggle")]
    [SerializeField, Tooltip("Unload objects outside the camera viewport and its preload margin.")]
    private bool enableVisionStreaming = true;

    [Header("Streaming Settings")]
    [SerializeField, Min(0.02f)] private float visibilityCheckInterval = 0.12f;
    [SerializeField, Min(0f)] private float unloadDelay = 0.3f;
    [SerializeField, Min(0.1f)] private float sceneRescanInterval = 1f;
    [SerializeField] private bool restoreObjectsWhenDisabled = true;
    [SerializeField, Min(0f), Tooltip("Additional world-space retention margin for active Blocks objects to prevent boundary flicker.")]
    private float blockVisibilityHysteresis = 0.6f;
    [SerializeField, Min(1), Tooltip("Maximum registered objects whose visibility is evaluated per frame.")]
    private int visibilityChecksPerFrame = 48;
    [SerializeField, Min(1), Tooltip("Background preload activations per frame. Objects close to or inside the viewport are restored immediately to avoid pop-in.")]
    private int maximumActivationsPerFrame = 1;
    [SerializeField, Min(0f), Tooltip("Minimum world-space preload margin outside every camera edge.")]
    private float activationPreloadPadding = 1.25f;
    [SerializeField, Range(0f, 0.5f), Tooltip("Preload margin as a fraction of the shorter orthographic camera dimension. The larger of this and the minimum margin is used.")]
    private float viewportPreloadFraction = 0.1f;
    [SerializeField, Min(0.1f), Tooltip("Maximum real-time milliseconds spent activating objects in one frame. One object is always allowed so loading cannot stall.")]
    private float activationTimeBudgetMilliseconds = 1.5f;
    [SerializeField, Min(1), Tooltip("Maximum objects disabled in one frame.")]
    private int maximumSuspensionsPerFrame = 12;
    [SerializeField, Min(1), Tooltip("Maximum hierarchy transforms inspected per frame while discovering new objects.")]
    private int discoveryTransformsPerFrame = 96;

    private sealed class ManagedObject
    {
        public GameObject gameObject;
        public Bounds localBounds;
        public float outsideTimer;
        public bool suspendedByManager;
        public bool containsVisionBlockLayer;
        public bool containsLever;
        public ZeldaFourWayMover[] movers;
        public ZeldaCharacterAiBase[] aiCharacters;
        public float lastVisibilityCheckTime;
        public bool queuedForActivation;
    }

    private readonly List<ManagedObject> managedObjects = new List<ManagedObject>();
    private readonly Dictionary<GameObject, ManagedObject> managedLookup =
        new Dictionary<GameObject, ManagedObject>();
    private readonly Queue<Transform> discoveryQueue = new Queue<Transform>();
    private readonly List<ManagedObject> activationQueue = new List<ManagedObject>();

    private CameraCircularVision circularVision;
    private Camera streamingCamera;
    private readonly Plane[] streamingPlanes = new Plane[6];
    private bool hasStreamingPlanes;
    private float currentPreloadPadding;
    private float sceneRescanTimer;
    private bool streamingWasEnabled;
    private bool puppetHighlightWasActive;
    private int visibilityCursor;

    public bool StreamingEnabled => enableVisionStreaming;
    public void PrepareSoulTransferView()
    {
        // A one-off restore while fully covered avoids showing budgeted activation in progress.
        RestoreAllObjects();
        if (circularVision != null) circularVision.RefreshSceneCameraSettings();
    }

    public void RefreshSceneCameraSettings()
    {
        circularVision = GetComponent<CameraCircularVision>();
        streamingCamera = GetComponent<Camera>();
        streamingWasEnabled = enableVisionStreaming;
        if (enableVisionStreaming)
        {
            RebuildObjectRegistry();
        }
        else
        {
            RestoreAllObjects();
            managedObjects.Clear();
            managedLookup.Clear();
            discoveryQueue.Clear();
            activationQueue.Clear();
        }
    }

    public bool IsSuspendedByStreaming(GameObject candidate)
    {
        ManagedObject managedObject;
        return candidate != null &&
               managedLookup.TryGetValue(candidate, out managedObject) &&
               managedObject != null &&
               managedObject.suspendedByManager;
    }

    public bool IsSuspendedByStreamingOrAncestor(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform current = candidate.transform;
        while (current != null)
        {
            if (IsSuspendedByStreaming(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void Awake()
    {
        circularVision = GetComponent<CameraCircularVision>();
        streamingCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        streamingWasEnabled = enableVisionStreaming;
        if (enableVisionStreaming)
        {
            RebuildObjectRegistry();
        }
        else
        {
            RestoreAllObjects();
        }
    }

    private void LateUpdate()
    {
        if (!enableVisionStreaming)
        {
            puppetHighlightWasActive = false;
            if (streamingWasEnabled)
            {
                RestoreAllObjects();
                streamingWasEnabled = false;
            }
            return;
        }

        if (!streamingWasEnabled)
        {
            streamingWasEnabled = true;
            RebuildObjectRegistry();
        }

        sceneRescanTimer -= Time.unscaledDeltaTime;

        if (sceneRescanTimer <= 0f && discoveryQueue.Count == 0)
        {
            sceneRescanTimer = sceneRescanInterval;
            BeginDiscoveryScan();
        }

        RefreshStreamingViewport();
        ProcessDiscoveryScan();
        RestoreObjectsInRetentionRegions();
        RestoreObjectsNearViewport();
        QueueLeverHighlightsWhenSelectionStarts();
        UpdateStreaming();
        ProcessActivationQueue();
    }

    private void QueueLeverHighlightsWhenSelectionStarts()
    {
        bool highlighting =
            ClockworkPuppetLeverHighlighter.IsHighlightModeActive;
        if (highlighting && !puppetHighlightWasActive)
        {
            for (int i = 0; i < managedObjects.Count; i++)
            {
                ManagedObject managedObject = managedObjects[i];
                if (managedObject == null || !managedObject.containsLever ||
                    managedObject.gameObject == null ||
                    !IsBoundsInsideCameraViewport(
                        managedObject.gameObject.transform,
                        managedObject.localBounds))
                {
                    continue;
                }

                managedObject.outsideTimer = 0f;
                if (managedObject.suspendedByManager)
                {
                    QueueActivation(managedObject);
                }
            }
        }
        puppetHighlightWasActive = highlighting;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (restoreObjectsWhenDisabled)
        {
            RestoreAllObjects();
        }
    }

    private void OnDestroy()
    {
        RestoreAllObjects();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableVisionStreaming)
        {
            RebuildObjectRegistry();
        }
        else
        {
            RestoreAllObjects();
                managedObjects.Clear();
                managedLookup.Clear();
        }
    }

    private void RebuildObjectRegistry()
    {
        RestoreAllObjects();
        managedObjects.Clear();
        managedLookup.Clear();
        discoveryQueue.Clear();
        activationQueue.Clear();
        puppetHighlightWasActive = false;
        visibilityCursor = 0;
        sceneRescanTimer = sceneRescanInterval;
        BeginDiscoveryScan();
    }

    private void BeginDiscoveryScan()
    {
        discoveryQueue.Clear();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                discoveryQueue.Enqueue(roots[rootIndex].transform);
            }
        }
    }

    private void ProcessDiscoveryScan()
    {
        int remainingBudget = Mathf.Max(1, discoveryTransformsPerFrame);
        while (remainingBudget-- > 0 && discoveryQueue.Count > 0)
        {
            Transform candidateTransform = discoveryQueue.Dequeue();
            if (candidateTransform == null)
            {
                continue;
            }

            GameObject candidate = candidateTransform.gameObject;
            bool alreadyCovered = managedLookup.ContainsKey(candidate) ||
                                  HasManagedAncestor(candidateTransform);
            if (alreadyCovered)
            {
                continue;
            }

            bool excluded = IsExcluded(candidate);
            bool registered = false;
            if (candidate.activeSelf &&
                HasDirectSpatialComponent(candidate) &&
                !excluded)
            {
                ManagedObject managedObject = new ManagedObject
                {
                    gameObject = candidate,
                    localBounds = CalculateLocalBounds(candidate),
                    containsVisionBlockLayer = ContainsVisionBlockLayer(candidate),
                    containsLever =
                        candidate.GetComponentInChildren<LeverData>(true) != null,
                    movers = candidate.GetComponentsInChildren<ZeldaFourWayMover>(true),
                    aiCharacters = candidate.GetComponentsInChildren<ZeldaCharacterAiBase>(true),
                    lastVisibilityCheckTime = Time.unscaledTime - visibilityCheckInterval
                };
                managedObjects.Add(managedObject);
                managedLookup.Add(candidate, managedObject);
                registered = true;
            }

            // Once a parent is managed all of its children share its active
            // state and bounds. An excluded object also excludes its complete
            // hierarchy; otherwise a player's visual/particle children would
            // be registered and suspended independently from the player.
            if (!registered && !excluded)
            {
                for (int childIndex = 0; childIndex < candidateTransform.childCount; childIndex++)
                {
                    discoveryQueue.Enqueue(candidateTransform.GetChild(childIndex));
                }
            }
        }
    }

    private void UpdateStreaming()
    {
        if (circularVision == null)
        {
            circularVision = GetComponent<CameraCircularVision>();
        }

        int checksRemaining = Mathf.Min(
            Mathf.Max(1, visibilityChecksPerFrame),
            managedObjects.Count);
        int suspensionsRemaining = Mathf.Max(1, maximumSuspensionsPerFrame);
        while (checksRemaining-- > 0 && managedObjects.Count > 0)
        {
            if (visibilityCursor >= managedObjects.Count)
            {
                visibilityCursor = 0;
            }

            ManagedObject managedObject = managedObjects[visibilityCursor];
            GameObject target = managedObject.gameObject;
            if (target == null)
            {
                managedObjects.RemoveAt(visibilityCursor);
                continue;
            }

            visibilityCursor++;
            float now = Time.unscaledTime;
            float elapsed = Mathf.Max(0f, now - managedObject.lastVisibilityCheckTime);

            // A character may become player-controlled after it was originally
            // registered as an AI object. Never suspend the current player,
            // even during rapid camera movement or possession transitions.
            if (HasRuntimeStreamingExemption(target.transform) ||
                ContainsActivePlayer(managedObject.movers) ||
                HasActivePlayerAncestor(target.transform) ||
                ContainsStreamingRetainedAi(managedObject.aiCharacters))
            {
                managedObject.outsideTimer = 0f;
                if (managedObject.suspendedByManager)
                {
                    QueueActivation(managedObject);
                }
                continue;
            }

            if (elapsed < visibilityCheckInterval)
            {
                continue;
            }

            managedObject.lastVisibilityCheckTime = now;
            bool visible = IsInsideStreamingRetentionRange(managedObject);
            if (visible)
            {
                managedObject.outsideTimer = 0f;
                if (managedObject.suspendedByManager)
                {
                    QueueActivation(managedObject);
                }
                continue;
            }

            managedObject.outsideTimer += elapsed;
            if (!managedObject.suspendedByManager && target.activeSelf &&
                managedObject.outsideTimer >= unloadDelay && suspensionsRemaining > 0)
            {
                target.SetActive(false);
                managedObject.suspendedByManager = true;
                suspensionsRemaining--;
            }
        }
    }

    private void QueueActivation(ManagedObject managedObject)
    {
        if (managedObject == null || managedObject.queuedForActivation)
        {
            return;
        }

        managedObject.queuedForActivation = true;
        activationQueue.Add(managedObject);
    }

    private void ProcessActivationQueue()
    {
        // The viewport safety pass may have already restored queued objects.
        // Discard them without consuming the background activation budget.
        for (int i = activationQueue.Count - 1; i >= 0; i--)
        {
            ManagedObject pending = activationQueue[i];
            if (pending == null || pending.gameObject == null || !pending.suspendedByManager)
            {
                if (pending != null) pending.queuedForActivation = false;
                activationQueue.RemoveAt(i);
            }
        }

        int remainingBudget = Mathf.Max(1, maximumActivationsPerFrame);
        float activationStartTime = Time.realtimeSinceStartup;
        int activatedThisFrame = 0;
        while (remainingBudget-- > 0 && activationQueue.Count > 0)
        {
            if (activatedThisFrame > 0 &&
                (Time.realtimeSinceStartup - activationStartTime) * 1000f >=
                activationTimeBudgetMilliseconds)
            {
                break;
            }

            int closestIndex = FindClosestActivationIndex();
            ManagedObject managedObject = activationQueue[closestIndex];
            activationQueue.RemoveAt(closestIndex);
            if (managedObject == null)
            {
                continue;
            }

            managedObject.queuedForActivation = false;
            GameObject target = managedObject.gameObject;
            if (target == null || !managedObject.suspendedByManager)
            {
                continue;
            }

            bool stillVisible = IsInsideStreamingRetentionRange(managedObject);
            if (!stillVisible &&
                !HasRuntimeStreamingExemption(target.transform) &&
                !ContainsActivePlayer(managedObject.movers) &&
                !HasActivePlayerAncestor(target.transform) &&
                !ContainsStreamingRetainedAi(managedObject.aiCharacters))
            {
                continue;
            }

            target.SetActive(true);
            managedObject.suspendedByManager = false;
            managedObject.outsideTimer = 0f;
            activatedThisFrame++;
        }
    }

    private int FindClosestActivationIndex()
    {
        int closestIndex = 0;
        float closestDistance = float.PositiveInfinity;
        Vector3 cameraPosition = transform.position;
        for (int i = 0; i < activationQueue.Count; i++)
        {
            ManagedObject candidate = activationQueue[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            Bounds worldBounds = TransformLocalBoundsToWorld(
                candidate.gameObject.transform,
                candidate.localBounds);
            float distance = worldBounds.SqrDistance(cameraPosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private bool IsInsideStreamingRetentionRange(ManagedObject managedObject)
    {
        if (managedObject == null || managedObject.gameObject == null)
        {
            return false;
        }

        Transform target = managedObject.gameObject.transform;
        Bounds worldBounds = TransformLocalBoundsToWorld(
            target,
            managedObject.localBounds);
        if (CameraVisionStreamingRegion.Intersects(managedObject.gameObject.scene, worldBounds))
            return true;
        if (circularVision != null &&
            circularVision.IsWorldBoundsInsideAdditionalReveal(worldBounds))
        {
            // Puppet reveal areas are part of the visible world. Apply this
            // before the Blocks-specific retention branch so walls and large
            // scene objects wake together with ordinary renderers.
            return true;
        }
        float padding = currentPreloadPadding;
        if (managedObject.containsVisionBlockLayer && !managedObject.suspendedByManager)
        {
            padding += blockVisibilityHysteresis;
        }
        return !hasStreamingPlanes || IntersectsStreamingViewport(
            worldBounds, streamingPlanes, padding);
    }

    private void RefreshStreamingViewport()
    {
        if (streamingCamera == null)
        {
            streamingCamera = GetComponent<Camera>();
        }
        hasStreamingPlanes = streamingCamera != null;
        if (!hasStreamingPlanes) return;

        // Executed after camera follow/zoom, every frame, with no per-frame
        // plane array allocation. Also handles aspect, rotation and projection changes.
        GeometryUtility.CalculateFrustumPlanes(streamingCamera, streamingPlanes);
        currentPreloadPadding = Mathf.Max(0f, activationPreloadPadding);
        if (streamingCamera.orthographic)
        {
            float shorterDimension = 2f * streamingCamera.orthographicSize *
                                     Mathf.Min(1f, streamingCamera.aspect);
            currentPreloadPadding = Mathf.Max(currentPreloadPadding,
                shorterDimension * viewportPreloadFraction);
        }
    }

    private bool IsBoundsInsideCameraViewport(Transform target, Bounds localBounds)
    {
        return !hasStreamingPlanes || IntersectsStreamingViewport(
            TransformLocalBoundsToWorld(target, localBounds), streamingPlanes, 0f);
    }

    private static bool IntersectsStreamingViewport(
        Bounds worldBounds, Plane[] planes, float padding)
    {
        Vector3 extents = worldBounds.extents;
        for (int i = 0; i < 6; i++)
        {
            Vector3 normal = planes[i].normal;
            float projectedExtent = Mathf.Abs(normal.x) * extents.x +
                                    Mathf.Abs(normal.y) * extents.y +
                                    Mathf.Abs(normal.z) * extents.z;
            // Unity orders the side planes first, followed by near/far planes.
            // Expand all four edges, including corners, but not the depth range.
            float margin = i < 4 ? padding : 0f;
            if (planes[i].GetDistanceToPoint(worldBounds.center) +
                projectedExtent + margin < 0f)
            {
                return false;
            }
        }
        return true;
    }

    private void RestoreObjectsNearViewport()
    {
        // A cheap bounds-only safety pass must not wait for the round-robin
        // visibility checks or one-object activation budget. The inner half
        // of the preload band provides headroom even on fast pans/zoom-outs.
        // Outer-band loading and all unloading retain their normal budgets.
        float urgentPadding = currentPreloadPadding * 0.5f;
        for (int i = 0; i < managedObjects.Count; i++)
        {
            ManagedObject managedObject = managedObjects[i];
            if (!managedObject.suspendedByManager || managedObject.gameObject == null)
                continue;

            Bounds worldBounds = TransformLocalBoundsToWorld(
                managedObject.gameObject.transform, managedObject.localBounds);
            if (hasStreamingPlanes &&
                !IntersectsStreamingViewport(worldBounds, streamingPlanes, urgentPadding))
                continue;

            managedObject.gameObject.SetActive(true);
            managedObject.suspendedByManager = false;
            managedObject.outsideTimer = 0f;
        }
    }

    /// <summary>Used by newly placed/detonating bombs before sending gameplay events.</summary>
    public static void RefreshRetentionRegionsNow()
    {
        // Only on placement/detonation, not a per-frame scene search.
        foreach (CameraVisionObjectStreaming manager in FindObjectsOfType<CameraVisionObjectStreaming>())
        {
            if (manager.isActiveAndEnabled && manager.enableVisionStreaming)
                manager.RestoreObjectsInRetentionRegions();
        }
    }

    private void RestoreObjectsInRetentionRegions()
    {
        if (!CameraVisionStreamingRegion.HasActiveRegions) return;
        for (int i = 0; i < managedObjects.Count; i++)
        {
            ManagedObject item = managedObjects[i];
            if (item.gameObject == null) continue;
            Bounds bounds = TransformLocalBoundsToWorld(item.gameObject.transform, item.localBounds);
            if (!CameraVisionStreamingRegion.Intersects(item.gameObject.scene, bounds)) continue;

            item.outsideTimer = 0f;
            item.lastVisibilityCheckTime = Time.unscaledTime;
            // Only undo THIS manager's suspension. Puzzle-disabled objects must
            // stay disabled, and do not gain permanent exemption components.
            if (!item.suspendedByManager) continue;
            item.suspendedByManager = false;
            item.gameObject.SetActive(true);
        }
    }

    private static Bounds TransformLocalBoundsToWorld(Transform target, Bounds localBounds)
    {
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        float z = localBounds.center.z;
        Bounds worldBounds = new Bounds(
            target.TransformPoint(new Vector3(min.x, min.y, z)),
            Vector3.zero);
        worldBounds.Encapsulate(target.TransformPoint(new Vector3(min.x, max.y, z)));
        worldBounds.Encapsulate(target.TransformPoint(new Vector3(max.x, min.y, z)));
        worldBounds.Encapsulate(target.TransformPoint(new Vector3(max.x, max.y, z)));

        // Bounds.Contains and the streaming test are two-dimensional, but a
        // small Z thickness keeps Unity's Bounds behavior well-defined.
        Vector3 size = worldBounds.size;
        size.z = Mathf.Max(size.z, 0.01f);
        worldBounds.size = size;
        return worldBounds;
    }

    private bool IsExcluded(GameObject candidate)
    {
        Transform candidateTransform = candidate.transform;
        if (candidateTransform == transform || candidateTransform.IsChildOf(transform) ||
            transform.IsChildOf(candidateTransform))
        {
            return true;
        }

        if (candidate.hideFlags != HideFlags.None ||
            candidate.GetComponentInParent<CameraVisionStreamingExempt>(true) != null ||
            candidate.GetComponentInChildren<Camera>(true) != null ||
            candidate.GetComponentInChildren<Canvas>(true) != null ||
            candidate.GetComponentInChildren<AudioListener>(true) != null)
        {
            return true;
        }

        if (ContainsActivePlayer(candidate) ||
            HasActivePlayerAncestor(candidate.transform) ||
            IsPersistentObject(candidate) || ContainsSingleton(candidate))
        {
            return true;
        }

        return false;
    }

    private bool ContainsVisionBlockLayer(GameObject candidate)
    {
        int blockMask = circularVision != null ? circularVision.BlockLayers.value : 0;
        Transform[] transforms = candidate.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            int layerBit = 1 << transforms[i].gameObject.layer;
            if ((blockMask & layerBit) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsActivePlayer(GameObject candidate)
    {
        return ContainsActivePlayer(candidate.GetComponentsInChildren<ZeldaFourWayMover>(true));
    }

    private static bool ContainsActivePlayer(ZeldaFourWayMover[] movers)
    {
        for (int i = 0; i < movers.Length; i++)
        {
            if (movers[i] != null && movers[i].enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsStreamingRetainedAi(
        ZeldaCharacterAiBase[] aiCharacters)
    {
        if (aiCharacters == null)
        {
            return false;
        }

        for (int i = 0; i < aiCharacters.Length; i++)
        {
            ZeldaCharacterAiBase ai = aiCharacters[i];
            if (ai != null && ai.RequiresVisionStreamingRetention)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasActivePlayerAncestor(Transform candidate)
    {
        Transform current = candidate != null ? candidate.parent : null;
        while (current != null)
        {
            ZeldaFourWayMover mover =
                current.GetComponent<ZeldaFourWayMover>();
            if (mover != null && mover.enabled)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool HasRuntimeStreamingExemption(Transform candidate)
    {
        // Soul/domino marks also add this at runtime. It is not exclusive to
        // remote puppet control: linked characters must keep simulating.
        if (candidate == null)
        {
            return false;
        }

        return candidate.GetComponentInParent<
                   CameraVisionStreamingExempt>(true) != null ||
               candidate.GetComponentInChildren<
                   CameraVisionStreamingExempt>(true) != null;
    }

    private static bool IsPersistentObject(GameObject candidate)
    {
        Scene scene = candidate.scene;
        return !scene.IsValid() || scene.name == "DontDestroyOnLoad";
    }

    private static bool ContainsSingleton(GameObject candidate)
    {
        MonoBehaviour[] behaviours = candidate.GetComponentsInChildren<MonoBehaviour>(true);
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type type = behaviour.GetType();
            try
            {
                PropertyInfo property = type.GetProperty("Instance", flags);
                if (property != null && IsSameHierarchy(property.GetValue(null, null), candidate.transform))
                {
                    return true;
                }

                FieldInfo field = type.GetField("Instance", flags) ?? type.GetField("instance", flags);
                if (field != null && IsSameHierarchy(field.GetValue(null), candidate.transform))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // A third-party singleton accessor may throw while initializing.
                // Treat it as non-detectable and rely on the explicit exemption component.
            }
        }

        return false;
    }

    private static bool IsSameHierarchy(object singletonValue, Transform candidate)
    {
        Component component = singletonValue as Component;
        if (component != null)
        {
            return component.transform == candidate || component.transform.IsChildOf(candidate);
        }

        GameObject singletonObject = singletonValue as GameObject;
        return singletonObject != null &&
               (singletonObject.transform == candidate || singletonObject.transform.IsChildOf(candidate));
    }

    private static bool HasDirectSpatialComponent(GameObject candidate)
    {
        return candidate.GetComponent<Renderer>() != null ||
               candidate.GetComponent<Collider2D>() != null;
    }

    private bool HasManagedAncestor(Transform candidate)
    {
        Transform parent = candidate.parent;
        while (parent != null)
        {
            if (managedLookup.ContainsKey(parent.gameObject))
            {
                return true;
            }
            parent = parent.parent;
        }

        return false;
    }

    private static Bounds CalculateLocalBounds(GameObject target)
    {
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            EncapsulateWorldBounds(target.transform, renderers[i].bounds, ref localBounds, ref initialized);
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            EncapsulateWorldBounds(target.transform, colliders[i].bounds, ref localBounds, ref initialized);
        }

        if (!initialized)
        {
            localBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        return localBounds;
    }

    private static void EncapsulateWorldBounds(
        Transform target,
        Bounds worldBounds,
        ref Bounds localBounds,
        ref bool initialized)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        float z = worldBounds.center.z;
        EncapsulateLocalPoint(target, new Vector3(min.x, min.y, z),
            ref localBounds, ref initialized);
        EncapsulateLocalPoint(target, new Vector3(min.x, max.y, z),
            ref localBounds, ref initialized);
        EncapsulateLocalPoint(target, new Vector3(max.x, min.y, z),
            ref localBounds, ref initialized);
        EncapsulateLocalPoint(target, new Vector3(max.x, max.y, z),
            ref localBounds, ref initialized);
    }

    private static void EncapsulateLocalPoint(
        Transform target,
        Vector3 worldPoint,
        ref Bounds localBounds,
        ref bool initialized)
    {
        Vector3 localPoint = target.InverseTransformPoint(worldPoint);
        if (!initialized)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            initialized = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    private void RestoreAllObjects()
    {
        for (int i = 0; i < managedObjects.Count; i++)
        {
            ManagedObject managedObject = managedObjects[i];
            if (managedObject.gameObject != null && managedObject.suspendedByManager)
            {
                managedObject.gameObject.SetActive(true);
                managedObject.suspendedByManager = false;
            }
        }
    }

    private void OnValidate()
    {
        visibilityCheckInterval = Mathf.Max(0.02f, visibilityCheckInterval);
        unloadDelay = Mathf.Max(0f, unloadDelay);
        sceneRescanInterval = Mathf.Max(0.1f, sceneRescanInterval);
        blockVisibilityHysteresis = Mathf.Max(0f, blockVisibilityHysteresis);
        visibilityChecksPerFrame = Mathf.Max(1, visibilityChecksPerFrame);
        maximumActivationsPerFrame = Mathf.Max(1, maximumActivationsPerFrame);
        activationPreloadPadding = Mathf.Max(0f, activationPreloadPadding);
        viewportPreloadFraction = Mathf.Clamp(viewportPreloadFraction, 0f, 0.5f);
        activationTimeBudgetMilliseconds =
            Mathf.Max(0.1f, activationTimeBudgetMilliseconds);
        maximumSuspensionsPerFrame = Mathf.Max(1, maximumSuspensionsPerFrame);
        discoveryTransformsPerFrame = Mathf.Max(1, discoveryTransformsPerFrame);
    }
}
