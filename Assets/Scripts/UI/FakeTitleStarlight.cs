using UnityEngine;
using UnityEngine.UI;

/// <summary>Sparse, sprite-swapped glints. No interpolation or particle simulation.</summary>
[DisallowMultipleComponent]
public sealed class FakeTitleStarlight : MonoBehaviour
{
    [SerializeField] private FakeTitleScreenPresentation presentation;
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite[] frames;
    [Range(2, 12)] [SerializeField] private int framesPerSecond = 6;
    [Range(12, 72)] [SerializeField] private int cycleFrames = 36;
    private static readonly int[] Glint = { 1, 2, 3, 2, 1, 0 };
    private int tick;
    private float nextFrame;

    private void OnEnable()
    {
        tick = 0;
        nextFrame = 0f;
        HideStars();
    }

    private void Update()
    {
        if (presentation == null || !presentation.IsStarlightReady)
        {
            // Also reset when only the presentation component is restarted.
            HideStars();
            tick = 0;
            nextFrame = 0f;
            return;
        }
        if (stars == null || frames == null || frames.Length < 3 || Time.unscaledTime < nextFrame) return;
        nextFrame = Time.unscaledTime + 1f / Mathf.Clamp(framesPerSecond, 2, 12);
        int cycle = Mathf.Clamp(cycleFrames, 12, 72);
        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];
            if (star == null) continue;
            int age = (tick + i * cycle / Mathf.Max(1, stars.Length)) % cycle;
            int frame = age < Glint.Length ? Glint[age] : 0;
            bool visible = frame > 0;
            if (star.enabled != visible) star.enabled = visible;
            if (visible && star.sprite != frames[frame - 1]) star.sprite = frames[frame - 1];
        }
        tick = (tick + 1) % cycle;
    }

    private void OnDisable() { HideStars(); }
    private void HideStars()
    {
        if (stars == null) return;
        foreach (var star in stars) if (star != null) star.enabled = false;
    }
}
