using System.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed partial class CameraVisionObjectStreaming
{
    [Header("Covered Scene Preparation")]
    [SerializeField, Min(0.5f), Tooltip("首轮加载在黑幕内每帧使用的耗时预算（毫秒），不改变正常游玩的加载预算。")]
    private float initialPreparationBudgetMilliseconds = 4f;
    private bool initialPreparationRunning;
    public bool IsInitialViewPrepared { get; private set; }

    private long InitialFrameDeadline() => Stopwatch.GetTimestamp() +
        (long)(Mathf.Max(.5f, initialPreparationBudgetMilliseconds) * Stopwatch.Frequency / 1000.0);

    /// <summary>Called only under black, after player/state/save restoration.</summary>
    public IEnumerator PrepareInitialView()
    {
        IsInitialViewPrepared = false;
        if (!isActiveAndEnabled || !enableVisionStreaming)
        {
            IsInitialViewPrepared = true;
            yield break;
        }
        initialPreparationRunning = true;
        double started = Time.realtimeSinceStartupAsDouble;
        try
        {
            using (SceneLoadMetrics.Measure("Streaming.Prepare"))
            {
                // One fresh scan includes objects created/restored after sceneLoaded.
                // Keep existing records and suspended ownership; never RestoreAll.
                BeginDiscoveryScan();
                while (discoveryQueue.Count > 0)
                {
                    if (!CanContinuePreparation(started)) yield break;
                    DiscoverObjects(int.MaxValue, InitialFrameDeadline());
                    if (discoveryQueue.Count > 0) yield return null;
                }
                yield return null;
                RefreshStreamingViewport();
                UpdateSpatialIndex();
                nextPatrolGroupCheck = 0f;
                UpdatePatrolStreamingGroups();
                yield return null;

                // Second pass resolves dependencies such as distant crystals
                // whose final bound skeleton was suspended later in pass one.
                for (int pass = 0; pass < 2; pass++)
                {
                    long deadline = InitialFrameDeadline();
                    for (int i = 0; i < managedObjects.Count; i++)
                    {
                        if (!CanContinuePreparation(started)) yield break;
                        PrepareInitialObject(managedObjects[i], pass == 1);
                        if (Stopwatch.GetTimestamp() >= deadline)
                        {
                            yield return null;
                            deadline = InitialFrameDeadline();
                        }
                    }
                    yield return null;
                }
                foreach (var item in activationQueue) if (item != null) item.queuedForActivation = false;
                activationQueue.Clear();
                CompletePatrolGroupActivations();
                visibilityCursor = 0;
                sceneRescanTimer = sceneRescanInterval;
                IsInitialViewPrepared = true;
            }
        }
        finally { initialPreparationRunning = false; }
    }

    private bool CanContinuePreparation(double started)
    {
        if (!isActiveAndEnabled || !enableVisionStreaming) return false;
        if (Time.realtimeSinceStartupAsDouble - started < 15.0) return true;
        // A bad/oversized hierarchy must not permanently trap the player in black.
        // The normal budgeted streamer safely continues after this fallback.
        Debug.LogWarning("首轮场景范围整理超时，将由正常加载管理继续处理。", this);
        return false;
    }

    private void PrepareInitialObject(ManagedObject item, bool prepareVisuals)
    {
        GameObject target = item.gameObject;
        if (target == null) return;
        // Never resurrect objects intentionally disabled by saved puzzle state.
        if (!target.activeSelf && !item.suspendedByManager) return;
        bool keep = HasRuntimeStreamingExemption(target.transform) ||
            ContainsActivePlayer(item.movers) || HasActivePlayerAncestor(target.transform) ||
            ContainsStreamingRetainedAi(item.aiCharacters) || ContainsRetainedCrystal(item.energyCrystals) ||
            retainedPatrolObjects.Contains(item) || IsInsideStreamingRetentionRange(item);
        item.lastVisibilityCheckTime = Time.unscaledTime;
        item.outsideTimer = 0f;
        if (keep)
        {
            if (item.suspendedByManager)
            {
                item.suspendedByManager = false;
                item.queuedForActivation = false;
                target.SetActive(true);
            }
            if (prepareVisuals && IsInsideStreamingRetentionRange(item))
            {
                foreach (var ai in item.aiCharacters)
                    if (ai != null) ai.PrepareVisualForSceneReveal();
                if (item.aiCharacters.Length > 0)
                {
                    item.localBounds = CalculateLocalBounds(target);
                    item.hasWorldBounds = false;
                }
            }
        }
        else if (!item.suspendedByManager)
        {
            // Initial pruning has no gameplay unload grace period. It occurs
            // before simulation/reveal, not while the player is moving around.
            item.suspendedByManager = true;
            target.SetActive(false);
        }
    }
}
