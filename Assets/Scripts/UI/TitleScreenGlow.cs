using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared fluorescent phosphor halos for menu/window text, icons and thin edges.
/// The original class name is retained for existing scene and prefab references.
/// Keeps the existing UI material/atlas and draws the crisp, brighter blue core last.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleScreenGlow : BaseMeshEffect
{
    private const int HaloCopies = 24;
    private const float CyanHaloIntensity = .75f;
    private float haloRadiusScale = 1f;
    private float haloIntensityScale = 1f;
    private readonly List<UIVertex> source = new List<UIVertex>();
    private readonly List<UIVertex> output = new List<UIVertex>();
    private static readonly Vector2[] Directions =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f),
        new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071068f, 0.7071068f),
        new Vector2(-0.7071068f, 0.7071068f),
        new Vector2(0.7071068f, -0.7071068f),
        new Vector2(-0.7071068f, -0.7071068f)
    };

    public static void Attach(Graphic target)
    {
        if (target != null && target.GetComponent<TitleScreenGlow>() == null)
            target.gameObject.AddComponent<TitleScreenGlow>();
    }

    /// <summary>Opt-in halo tuning; default instances retain the shared gameplay style.</summary>
    public void ConfigureHalo(float radiusScale, float intensityScale)
    {
        haloRadiusScale = System.Math.Max(.5f, System.Math.Min(2f, radiusScale));
        haloIntensityScale = System.Math.Max(0f, System.Math.Min(2f, intensityScale));
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;

        source.Clear();
        vertices.GetUIVertexStream(source);
        // Long/localised save names must not exceed uGUI's 16-bit vertex budget.
        if (source.Count > 64000 / (HaloCopies + 1))
        {
            source.Clear();
            return;
        }

        output.Clear();
        int count = source.Count * (HaloCopies + 1);
        if (output.Capacity < count) output.Capacity = count;

        // Distances are reference-canvas pixels, so the halo follows CanvasScaler.
        // Only luminous blue accents are enhanced; muted/disabled labels retain their old halo.
        AppendRing(6f, 0.035f, 0f);
        AppendRing(3f, 0.085f, 0.025f);
        AppendRing(1.25f, 0.16f, 0.055f);
        for (int i = 0; i < source.Count; i++)
        {
            UIVertex core = source[i];
            if (IsLuminousBlue(core.color)) core.color = FluorescentTint(core.color, true);
            output.Add(core);
        }
        vertices.Clear();
        vertices.AddUIVertexTriangleStream(output);
        source.Clear();
        output.Clear();
    }

    private static bool IsLuminousBlue(Color32 color)
    {
        // Exclude dark panel fills, grey disabled labels, white and warm accents.
        return color.b >= 140 && color.g >= 70 && color.b >= color.r + 40;
    }

    private static Color32 FluorescentTint(Color32 color, bool core)
    {
        // Preserve alpha so fades/CanvasGroup transparency do not leave glowing ghosts.
        // Gameplay UI uses cyan, while the title uses blue. Do not recolour cyan HUD accents blue.
        bool cyan = color.g > color.b;
        color.r = (byte)Mathf.RoundToInt(color.r + ((cyan ? (core ? 140f : 55f) : (core ? 82f : 28f)) - color.r) * .75f);
        color.g = (byte)Mathf.RoundToInt(color.g + ((cyan ? 255f : (core ? 186f : 146f)) - color.g) * .75f);
        color.b = (byte)Mathf.RoundToInt(color.b + ((cyan ? 245f : 255f) - color.b) * .75f);
        return color;
    }

    private void AppendRing(float radius, float opacity, float mutedOpacity)
    {
        for (int direction = 0; direction < Directions.Length; direction++)
        {
            Vector2 offset = Directions[direction] * (radius * haloRadiusScale);
            for (int index = 0; index < source.Count; index++)
            {
                UIVertex vertex = source[index];
                Vector3 position = vertex.position;
                position.x += offset.x;
                position.y += offset.y;
                vertex.position = position;
                Color32 color = vertex.color;
                bool blue = IsLuminousBlue(color);
                // Cyan is perceptually brighter than blue: soften only its halo, not the crisp core.
                float intensity = (blue && color.g > color.b ? CyanHaloIntensity : 1f) * (blue ? haloIntensityScale : 1f);
                if (blue) color = FluorescentTint(color, false);
                color.a = (byte)Mathf.RoundToInt(color.a * (blue ? opacity : mutedOpacity) * intensity);
                vertex.color = color;
                output.Add(vertex);
            }
        }
    }
}
