using System.Collections.Generic;
using UnityEngine;

/// <summary>Brief stepped opening only. No layout scaling, reparenting, or content-change replay.</summary>
public sealed class VisualStyleLabTerminalWindow : MonoBehaviour
{
    private struct Content { public CanvasGroup group; public bool owned; public float alpha; }
    private readonly List<Content> content = new List<Content>();
    private VisualStyleLabTerminalInk background;
    private VisualStyleLabTerminalChrome corners;
    private float started;
    private int lastStep = -1;
    private bool configured, opening;
    public int OpenCount { get; private set; }
    public bool IsOpening => opening;

    public void Configure(VisualStyleLabTerminalInk ink, VisualStyleLabTerminalChrome chrome)
    {
        background = ink; corners = chrome; configured = true;
        if (isActiveAndEnabled) Begin();
    }
    private void OnEnable() { if (configured) Begin(); }
    private void Begin()
    {
        ReleaseGroups();
        // Direct children only. Existing masks/scrolling/input remain exactly where they are.
        foreach (Transform child in transform)
        {
            if (child == corners.transform) continue;
            var group = child.GetComponent<CanvasGroup>();
            bool owned = group == null;
            if (owned) group = child.gameObject.AddComponent<CanvasGroup>();
            content.Add(new Content { group = group, owned = owned, alpha = group.alpha });
            group.alpha = 0;
        }
        started = Time.unscaledTime; lastStep = -1; opening = true; OpenCount++;
        background.SetReveal(0);
    }
    private void LateUpdate()
    {
        if (!opening) return;
        float elapsed = Time.unscaledTime - started;
        int step = Mathf.FloorToInt(elapsed * 30);
        if (step == lastStep) return;
        lastStep = step;
        background.SetReveal(Mathf.Clamp01((step - 1) / 3f));
        float alpha = Mathf.Clamp01((step - 3) / 4f);
        foreach (var c in content) if (c.group) c.group.alpha = c.alpha * alpha;
        if (step >= 7) { opening = false; ReleaseGroups(); }
    }
    private void OnDisable() { opening = false; ReleaseGroups(); if (background) background.SetReveal(1); }
    private void ReleaseGroups()
    {
        foreach (var c in content)
        {
            if (!c.group) continue;
            c.group.alpha = c.alpha;
            if (c.owned) Destroy(c.group);
        }
        content.Clear();
    }
}
