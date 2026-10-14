using UnityEngine;

/// <summary>A narrow golden shard pinched by rough, dull metal tabs; no loop or ornamental wings.</summary>
public sealed class CounterfeitTrialProofPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Counterfeit Trial Proof";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(51, 63, 70, 255);
            case 'M': return new Color32(116, 128, 127, 255);
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
        "....................",
        "........DDD.........",
        ".......DMMMD........",
        ".......DMYGD........",
        "......DMLYGMD.......",
        "......DMLYGGMD......",
        "......DMLYGGMD......",
        "......DMLYGGMD......",
        "......DMMYGGMD......",
        "......DMYGGGMD......",
        "......DMYGBBMD......",
        ".......DMGGBMD......",
        ".......DMMBMD.......",
        "........DMMMD.......",
        "........DDDD........",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
    };
}
