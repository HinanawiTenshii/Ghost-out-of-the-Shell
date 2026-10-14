using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local, reversible palette and selective glow experiment. No prefab assets are edited.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Garden Fluorescence Test")]
public sealed class GardenFluorescenceTest : MonoBehaviour
{
    [Header("A/B comparison / 运行时对比")]
    [Tooltip("关闭后恢复原配色并停止新增泛光；不影响原有 UI、灯光或 CRT。")]
    public bool testEnabled = true;
    public bool adjustPalette = true;
    public bool enableBloom = true;
    [Header("Garden scene instances / 仅本场景实例")]
    public Transform fountain;
    public Transform walls;
    public Transform decorations;
    public Transform[] flowerbeds = new Transform[0];
    [Header("Material colours / 保留材质辨识")]
    public Color wallColor = new Color(0.50f, 0.53f, 0.56f, 1f);
    public Color stoneColor = new Color(0.48f, 0.52f, 0.58f, 1f);
    public Color waterColor = new Color(0.035f, 0.29f, 0.52f, 1f);
    public Color waterHighlight = new Color(0.16f, 0.78f, 0.96f, 1f);
    [Header("Two-scale glow / 近光晕与远光晕")]
    [Range(0f, 2f)] public float nearIntensity = 0.85f;
    [Range(0f, 1f)] public float farIntensity = 0.22f;
    [Range(0.3f, 0.9f)] public float threshold = 0.66f;
    [Range(0.02f, 0.4f)] public float softKnee = 0.12f;
    [Range(0.5f, 4f)] public float radius = 1.5f;

    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private readonly List<SelectiveWorldBloom.Surface> surfaces = new List<SelectiveWorldBloom.Surface>();
    private SelectiveWorldBloom bloom;
    private bool applied;
    private bool lastPalette;
    private Color lastWall, lastStone, lastWater, lastHighlight;

    private void LateUpdate()
    {
        if (!testEnabled)
        {
            Restore();
            return;
        }
        if (!applied || lastPalette != adjustPalette || lastWall != wallColor || lastStone != stoneColor ||
            lastWater != waterColor || lastHighlight != waterHighlight)
        {
            RestoreColors();
            surfaces.Clear();
            ConfigurePalette();
            applied = true;
            lastPalette = adjustPalette;
            lastWall = wallColor; lastStone = stoneColor; lastWater = waterColor; lastHighlight = waterHighlight;
            if (bloom != null) bloom.SetSurfaces(surfaces);
        }

        Camera camera = Camera.main;
        if (bloom != null && bloom.GetComponent<Camera>() != camera) ReleaseBloom();
        if (camera == null) return;
        if (bloom == null)
        {
            // Owned by this profile, destroyed on disable / scene unload, including persistent cameras.
            bloom = camera.gameObject.AddComponent<SelectiveWorldBloom>();
            bloom.hideFlags = HideFlags.DontSave;
            bloom.SetSurfaces(surfaces);
        }
        bloom.enabled = enableBloom;
        bloom.nearIntensity = nearIntensity;
        bloom.farIntensity = farIntensity;
        bloom.threshold = threshold;
        bloom.softKnee = softKnee;
        bloom.radius = radius;
    }

    private void ConfigurePalette()
    {
        if (walls != null)
            foreach (SpriteRenderer renderer in walls.GetComponentsInChildren<SpriteRenderer>(true))
                if (IsNearWhite(renderer.color)) Recolor(renderer, wallColor);

        if (fountain != null)
        {
            foreach (SpriteRenderer renderer in fountain.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string part = renderer.name;
                if (part == "Water")
                {
                    Recolor(renderer, waterColor);
                    // Selects only bright *visible* ripples in the basin, not the subdued water fill.
                    surfaces.Add(new SelectiveWorldBloom.Surface(renderer, 1f));
                }
                else if (part == "Spout Water" || part.StartsWith("Water Glint"))
                {
                    Recolor(renderer, waterHighlight);
                    surfaces.Add(new SelectiveWorldBloom.Surface(renderer, 1f));
                }
                else if (renderer.transform == fountain || part == "Center Pedestal" || part == "Nozzle")
                {
                    Recolor(renderer, part == "Nozzle" ? new Color(0.65f, 0.72f, 0.78f, 1f) : stoneColor);
                    // Very restrained stone reflection, never the strength of water highlights.
                    surfaces.Add(new SelectiveWorldBloom.Surface(renderer, 0.12f));
                }
            }
        }
        foreach (Transform bed in flowerbeds)
        {
            if (bed == null) continue;
            foreach (SpriteRenderer renderer in bed.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Stone Border") Recolor(renderer, stoneColor);
            }
        }
        // In Garden, flowers are siblings of the bed prefabs under Decorations.
        // Soil, wood, leaves and entire plant silhouettes do not become neon objects.
        if (decorations != null)
            foreach (SpriteRenderer renderer in decorations.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name == "Flower" || renderer.name == "Flower Center")
                    surfaces.Add(new SelectiveWorldBloom.Surface(renderer, 0.28f));
    }

    public static bool IsNearWhite(Color color)
    {
        float minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        return minimum >= 0.85f && maximum - minimum <= 0.08f;
    }

    private void Recolor(SpriteRenderer renderer, Color color)
    {
        if (!adjustPalette) return;
        if (!originalColors.ContainsKey(renderer)) originalColors.Add(renderer, renderer.color);
        color.a = originalColors[renderer].a;
        renderer.color = color;
    }

    private void RestoreColors()
    {
        foreach (var entry in originalColors)
            if (entry.Key != null) entry.Key.color = entry.Value;
        originalColors.Clear();
    }

    private void ReleaseBloom()
    {
        if (bloom == null) return;
        bloom.enabled = false;
        Destroy(bloom);
        bloom = null;
    }

    private void Restore()
    {
        RestoreColors();
        ReleaseBloom();
        surfaces.Clear();
        applied = false;
    }

    private void OnDisable() { Restore(); }
}
