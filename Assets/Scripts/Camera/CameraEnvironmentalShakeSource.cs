using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local visual shake sources, independent of one-shot skill shakes.</summary>
public abstract class CameraEnvironmentalShakeSource : MonoBehaviour
{
    private static readonly List<CameraEnvironmentalShakeSource> sources = new List<CameraEnvironmentalShakeSource>();
    private static readonly Plane[] frustum = new Plane[6];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSources() { sources.Clear(); }

    protected virtual void OnEnable() { if (!sources.Contains(this)) sources.Add(this); }
    protected virtual void OnDisable() { sources.Remove(this); }

    protected abstract Vector2 GetOffset(Camera camera, CameraCircularVision vision, Plane[] cameraFrustum);

    public static Vector2 SampleOffset(Camera camera, CameraCircularVision vision, float deltaTime)
    {
        if (camera == null || deltaTime <= 0f || sources.Count == 0) return Vector2.zero;
        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        Vector2 strongest = Vector2.zero;
        for (int i=sources.Count-1;i>=0;i--)
        {
            var source = sources[i];
            if (source == null) { sources.RemoveAt(i); continue; }
            if (!source.isActiveAndEnabled || source.gameObject.scene != camera.gameObject.scene) continue;
            Vector2 offset = source.GetOffset(camera, vision, frustum);
            // Concurrent scene effects should not multiply displacement without bound.
            if (offset.sqrMagnitude > strongest.sqrMagnitude) strongest = offset;
        }
        return strongest;
    }
}
