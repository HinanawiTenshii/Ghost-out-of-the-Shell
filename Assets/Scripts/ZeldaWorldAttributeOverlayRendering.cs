using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies an overlay material to world-space attribute windows so physics-layer
/// geometry (including Blocks) cannot hide their sprites or text.
/// </summary>
public static class ZeldaWorldAttributeOverlayRendering
{
    private const string MaterialResourcePath = "ZeldaWorldAttributeOverlay";

    private static Material overlayMaterial;
    private static readonly Dictionary<Font, Material> FontMaterials = new Dictionary<Font, Material>();

    public static void Apply(SpriteRenderer renderer)
    {
        Material material = GetOverlayMaterial();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    public static void Apply(MeshRenderer renderer, Font font)
    {
        if (renderer == null || font == null)
        {
            return;
        }

        Material baseMaterial = GetOverlayMaterial();
        if (baseMaterial == null)
        {
            renderer.sharedMaterial = font.material;
            return;
        }

        if (!FontMaterials.TryGetValue(font, out Material fontMaterial) || fontMaterial == null)
        {
            fontMaterial = new Material(baseMaterial)
            {
                name = font.name + " Attribute Overlay Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            FontMaterials[font] = fontMaterial;
        }

        // Dynamic fonts can rebuild their atlas after Chinese glyphs are requested.
        // Keep the overlay copy pointed at the current atlas instead of retaining
        // the texture that happened to exist when the material was first created.
        fontMaterial.mainTexture = font.material.mainTexture;

        renderer.sharedMaterial = fontMaterial;
    }

    private static Material GetOverlayMaterial()
    {
        if (overlayMaterial == null)
        {
            overlayMaterial = Resources.Load<Material>(MaterialResourcePath);
        }

        return overlayMaterial;
    }
}
