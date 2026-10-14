using UnityEngine;

/// <summary>Unmounted, narrow golden crystal, using the trial proof's existing pixel palette.</summary>
public sealed class GoldenGemPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Golden Gem";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(105, 77, 38, 255);
            case 'B': return new Color32(153, 107, 42, 255);
            case 'G': return new Color32(222, 164, 57, 255);
            case 'Y': return new Color32(249, 200, 83, 255);
            case 'L': return new Color32(255, 229, 148, 255);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[]
    {
        "....................",
        "....................",
        "....................",
        ".........DD.........",
        "........DLYD........",
        ".......DLLYGD.......",
        ".......DLYYGD.......",
        "......DLLYYGBD......",
        "......DLYYYGBD......",
        ".....DLLYYYGBBD.....",
        ".....DLYYYGGBBD.....",
        ".....DLYYYGGBBD.....",
        ".....DLYYYGGBBD.....",
        ".....DLYYGGGBBD.....",
        "......DYYGGBBD......",
        "......DYYGGBBD......",
        ".......DYGGBD.......",
        ".......DYGGBD.......",
        "........DGBD........",
        ".........DD.........",
        "....................",
        "....................",
        "....................",
        "....................",
    };
}
