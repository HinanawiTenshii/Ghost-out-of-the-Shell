using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opt-in phosphor style for menu/window canvases, including dynamically rebuilt content.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class RuntimeUiGlowScope : MonoBehaviour
{
    private Canvas owner;
    private readonly HashSet<Graphic> enhanced = new HashSet<Graphic>();
    private int lastFrame = -1;
    private int nextCleanup;

    public static void Attach(Canvas canvas)
    {
        if (!Application.isPlaying || canvas == null || canvas.GetComponent<RuntimeUiGlowScope>() != null) return;
        canvas.gameObject.AddComponent<RuntimeUiGlowScope>();
    }

    private void OnEnable()
    {
        owner = GetComponent<Canvas>();
        lastFrame = -1;
        Canvas.preWillRenderCanvases += Refresh;
    }

    public static void Detach(Canvas canvas)
    {
        if (!Application.isPlaying || canvas == null) return;
        var scope = canvas.GetComponent<RuntimeUiGlowScope>();
        if (scope == null) return;
        // Disable immediately, then remove the old automatically attached effects.
        // This also handles a live HUD carried across scenes or a script reload.
        scope.enabled = false;
        // One-time cleanup, including hidden HUD children; never touch nested popup canvases.
        var effects = canvas.GetComponentsInChildren<TitleScreenGlow>(true);
        for (int i = 0; i < effects.Length; i++)
        {
            var glow = effects[i];
            var target = glow.GetComponent<Graphic>();
            if (target == null || target.canvas != canvas) continue;
            glow.enabled = false;
            Destroy(glow);
        }
        Destroy(scope);
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Refresh;
        enhanced.Clear();
    }

    private void Refresh()
    {
        if (owner == null || !owner.isActiveAndEnabled || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        // The Canvas registry is already maintained by uGUI. No scene/hierarchy scan per frame.
        var graphics = GraphicRegistry.GetGraphicsForCanvas(owner);
        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic target = graphics[i];
            if (target == null || !target.isActiveAndEnabled || enhanced.Contains(target) || !ShouldEnhance(target)) continue;
            TitleScreenGlow.Attach(target);
            enhanced.Add(target);
        }
        if (Time.frameCount >= nextCleanup)
        {
            enhanced.RemoveWhere(target => target == null);
            nextCleanup = Time.frameCount + 120;
        }
    }

    internal static bool ShouldEnhance(Graphic target)
    {
        if (target == null) return false;
        if (target is Text || target is MapSceneQuestMarkerGraphic) return true;
        // Render textures, map geometry, dimmers and mask geometry should stay crisp/unmodified.
        if (!(target is Image image) || image.GetComponent<Mask>() != null || image.GetComponent<RectMask2D>() != null) return false;
        Rect rect = image.rectTransform.rect;
        float width = Mathf.Abs(rect.width), height = Mathf.Abs(rect.height);
        if (width < .1f || height < .1f) return false; // Retry after layout has assigned its real size.
        bool compact = width <= 96f && height <= 96f;
        if (compact && image.sprite != null) return true; // Hearts, inventory/skill icons, arrows.
        Color tint = image.color;
        float brightest = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        return brightest >= .55f && (compact || Mathf.Min(width, height) <= 24f);
    }
}
