using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A lifetime-scoped, world-space 2D region that keeps streamed objects simulating.</summary>
[RequireComponent(typeof(CameraVisionStreamingExempt))]
public sealed class CameraVisionStreamingRegion : MonoBehaviour
{
    private static readonly HashSet<CameraVisionStreamingRegion> regions =
        new HashSet<CameraVisionStreamingRegion>();
    private float radius;
    private bool configured;

    public static bool HasActiveRegions => regions.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => regions.Clear();

    private void OnEnable() => regions.Add(this);
    private void OnDisable() => regions.Remove(this);

    public void Configure(float worldRadius)
    {
        radius = Mathf.Max(0f, worldRadius);
        configured = true;
        // Also register after a domain-reload-disabled reset.
        if (isActiveAndEnabled) regions.Add(this);
        // Immediate explosions must wake already-suspended targets before AI
        // notification, rather than waiting for the camera's LateUpdate budget.
        CameraVisionObjectStreaming.RefreshRetentionRegionsNow();
    }

    public static bool Intersects(Scene scene, Bounds bounds)
    {
        foreach (CameraVisionStreamingRegion region in regions)
        {
            if (region == null || !region.configured || !region.isActiveAndEnabled ||
                ZeldaRuntimeRegistry.GetGameplayScene(region.gameObject) != scene)
                continue;

            // Ignore Z: the physics/AI world is 2D, even when art uses depth offsets.
            Vector3 center = region.transform.position;
            float dx = Mathf.Max(0f, Mathf.Abs(bounds.center.x - center.x) - bounds.extents.x);
            float dy = Mathf.Max(0f, Mathf.Abs(bounds.center.y - center.y) - bounds.extents.y);
            if (dx * dx + dy * dy <= region.radius * region.radius) return true;
        }
        return false;
    }
}
