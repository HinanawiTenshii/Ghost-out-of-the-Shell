using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Additional world-space hole in the camera darkness overlay. Runtime agents
/// register themselves here so the visual mask and object streaming use the
/// same reveal area without per-frame scene searches.
/// </summary>
public sealed class CameraVisionRevealSource : MonoBehaviour
{
    private static readonly HashSet<CameraVisionRevealSource> ActiveSet =
        new HashSet<CameraVisionRevealSource>();

    [SerializeField, Min(0.1f)] private float radius = 3f;

    public static IReadOnlyCollection<CameraVisionRevealSource> ActiveSources =>
        ActiveSet;
    public float Radius => Mathf.Max(0.1f, radius);
    public Vector2 WorldPosition => transform.position;

    public void Configure(float configuredRadius)
    {
        radius = Mathf.Max(0.1f, configuredRadius);
    }

    private void OnEnable()
    {
        ActiveSet.Add(this);
    }

    private void OnDisable()
    {
        ActiveSet.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveSet.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveSet.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
    }
#endif
}
