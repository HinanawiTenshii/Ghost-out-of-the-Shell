using System.Collections.Generic;
using UnityEngine;

/// <summary>Reversible fade for a gate-linked sprite object, including its child sprites.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Waterway Gate Target Fade")]
public sealed class WaterwayGateTargetFade : MonoBehaviour
{
    private static readonly HashSet<WaterwayGateTargetFade> ActiveFaders = new HashSet<WaterwayGateTargetFade>();
    [SerializeField] private WaterwayGate sourceGate;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.6f;

    private SpriteRenderer[] sprites;
    private Color[] originalColors;
    private float visibility = 1f;
    private bool visibleRequested = true;
    private bool initialized;

    public float Visibility => visibility;
    public bool VisibleRequested => visibleRequested;
    public bool IsFading => initialized && visibility != (visibleRequested ? 1f : 0f);

    // Shared by every controller of the same gate; independent gates remain usable.
    public static bool IsGateTransitioning(WaterwayGate gate)
    {
        if (gate == null) return false;
        if (gate.IsMoving) return true;
        foreach (var fader in ActiveFaders)
        {
            if (fader != null && fader.sourceGate == gate && fader.IsFading) return true;
        }
        return false;
    }

    private void OnEnable() => ActiveFaders.Add(this);
    private void OnDisable() => ActiveFaders.Remove(this);

    private void Awake() => EnsureInitialized();

    private void Start()
    {
        // Initial closed gates must not leave their water visible until first use.
        if (sourceGate != null) SetVisibleImmediately(sourceGate.IsOpenRequested);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) originalColors[i] = sprites[i].color;
        initialized = true;
    }

    // UnityEvents can call this component even when its GameObject is inactive.
    public void FadeIn()
    {
        EnsureInitialized();
        visibleRequested = true;
        if (!gameObject.activeSelf)
        {
            visibility = 0f;
            ApplyOpacity(); // Set transparency BEFORE activation to avoid a full-color flash.
            SetObjectActive(true);
        }
        // An in-progress fade reverses from the current opacity; never restarts.
    }

    public void FadeOut()
    {
        EnsureInitialized();
        visibleRequested = false;
        if (!gameObject.activeSelf)
        {
            visibility = 0f;
            ApplyOpacity();
        }
    }

    public void SetVisibleImmediately(bool visible)
    {
        EnsureInitialized();
        visibleRequested = visible;
        visibility = visible ? 1f : 0f;
        ApplyOpacity();
        SetObjectActive(visible);
    }

    private void Update()
    {
        float target = visibleRequested ? 1f : 0f;
        if (visibility == target) return;
        visibility = Mathf.MoveTowards(visibility, target, Time.deltaTime / Mathf.Max(0.05f, fadeDuration));
        ApplyOpacity();
        // Keep scripts/collision active throughout fade-out; suspend only at zero.
        if (!visibleRequested && visibility <= 0f) SetObjectActive(false);
    }

    private void ApplyOpacity()
    {
        float alpha = Mathf.SmoothStep(0f, 1f, visibility);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color color = originalColors[i];
            color.a *= alpha;
            sprites[i].color = color;
        }
    }

    private void SetObjectActive(bool value)
    {
        if (gameObject.activeSelf == value) return;
        gameObject.SetActive(value);
        Physics2D.SyncTransforms();
        CameraCircularVision.NotifyBlockersChanged();
    }

    private void OnValidate() => fadeDuration = Mathf.Max(0.05f, fadeDuration);
}
