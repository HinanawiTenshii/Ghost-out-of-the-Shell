using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Suspends spatial scene objects outside CameraCircularVision and restores
/// them when their stored bounds enter visible space again.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera), typeof(CameraCircularVision))]
public sealed class CameraVisionObjectStreaming : MonoBehaviour
{
    [Header("Streaming Toggle")]
    [SerializeField, Tooltip("Enable objects to unload outside the camera vision mask.")]
    private bool enableVisionStreaming = true;

    [Header("Streaming Settings")]
    [SerializeField, Min(0.02f)] private float visibilityCheckInterval = 0.12f;
    [SerializeField, Min(0f)] private float unloadDelay = 0.3f;
    [SerializeField, Min(0.1f)] private float sceneRescanInterval = 1f;
    [SerializeField] private bool restoreObjectsWhenDisabled = true;
    [SerializeField, Min(0f), Tooltip("Extra radius retained for Blocks objects to prevent boundary flicker.")]
    private float blockVisibilityHysteresis = 0.6f;
    [SerializeField, Min(1), Tooltip("Maximum registered objects whose visibility is evaluated per frame.")]
    private int visibilityChecksPerFrame = 48;
    [SerializeField, Min(1), Tooltip("Maximum objects enabled in one frame. Lower values reduce loading spikes.")]
    private int maximumActivationsPerFrame = 1;
    [SerializeField, Min(0f), Tooltip("Objects begin loading this far before entering the visible mask.")]
    private float activationPreloadPadding = 1.25f;
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
        public ZeldaFourWayMover[] movers;
        public float lastVisibilityCheckTime;
        public bool queuedForActivation;
    }

    private readonly List<ManagedObject> managedObjects = new List<ManagedObject>();
    private readonly Dictionary<GameObject, ManagedObject> managedLookup =
        new Dictionary<GameObject, ManagedObject>();
    private readonly Queue<Transform> discoveryQueue = new Queue<Transform>();
    private readonly List<ManagedObject> activationQueue = new List<ManagedObject>();

    private CameraCircularVision circularVision;
    private float sceneRescanTimer;
    private bool streamingWasEnabled;
    private int visibilityCursor;

    public bool StreamingEnabled => enableVisionStreaming;

    public void RefreshSceneCameraSettings()
    {
        circularVision = GetComponent<CameraCircularVision>();
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

        ProcessDiscoveryScan();
        UpdateStreaming();
        ProcessActivationQueue();
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
                    movers = candidate.GetComponentsInChildren<ZeldaFourWayMover>(true),
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
            if (ContainsActivePlayer(managedObject.movers) ||
                HasActivePlayerAncestor(target.transform))
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
                !ContainsActivePlayer(managedObject.movers) &&
                !HasActivePlayerAncestor(target.transform))
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
        if (managedObject.containsVisionBlockLayer)
        {
            return IsBlockBoundsNearVision(
                target,
                managedObject.localBounds,
                managedObject.suspendedByManager);
        }

        // Exact mask intersection remains the primary test. The surrounding
        // preload band wakes nearby objects before they become visible and
        // retains them there, preventing activate/deactivate thrashing.
        return IsBoundsVisible(target, managedObject.localBounds) ||
               IsBoundsWithinPreloadRange(target, managedObject.localBounds);
    }

    private bool IsBoundsWithinPreloadRange(Transform target, Bounds localBounds)
    {
        Bounds worldBounds = TransformLocalBoundsToWorld(target, localBounds);
        Vector3 cameraPoint = transform.position;
        cameraPoint.z = worldBounds.center.z;
        float radius = Mathf.Max(
            0.1f,
            circularVision.VisionMaskRadius + activationPreloadPadding);
        return worldBounds.SqrDistance(cameraPoint) <= radius * radius;
    }

    private bool IsBoundsVisible(Transform target, Bounds localBounds)
    {
        Bounds worldBounds = TransformLocalBoundsToWorld(target, localBounds);
        return circularVision.IsWorldBoundsVisible(worldBounds, target);
    }

    private bool IsBlockBoundsNearVision(
        Transform target,
        Bounds localBounds,
        bool currentlySuspended)
    {
        Bounds worldBounds = TransformLocalBoundsToWorld(target, localBounds);
        Vector3 cameraPoint = transform.position;
        cameraPoint.z = worldBounds.center.z;

        // A smaller threshold is used to wake a suspended block and a larger
        // one to retain an active block. This dead band prevents repeated
        // toggling when a large collider sits on the vision boundary.
        float wakePadding =
            blockVisibilityHysteresis * 0.35f + activationPreloadPadding;
        float padding = currentlySuspended
            ? wakePadding
            : Mathf.Max(blockVisibilityHysteresis, wakePadding);
        float radius = Mathf.Max(
            0.1f,
            circularVision.VisionMaskRadius + padding);
        return worldBounds.SqrDistance(cameraPoint) <= radius * radius;
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
        activationTimeBudgetMilliseconds =
            Mathf.Max(0.1f, activationTimeBudgetMilliseconds);
        maximumSuspensionsPerFrame = Mathf.Max(1, maximumSuspensionsPerFrame);
        discoveryTransformsPerFrame = Mathf.Max(1, discoveryTransformsPerFrame);
    }
}
