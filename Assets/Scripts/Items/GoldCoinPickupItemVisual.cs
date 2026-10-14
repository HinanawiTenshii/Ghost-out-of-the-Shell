using UnityEngine;

/// <summary>Shared world/UI coin: warm gold rim, inset face and a simple stamped lozenge.</summary>
public sealed class GoldCoinPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Gold Coin";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'O': return new Color32(124, 77, 39, 255);
            case 'D': return new Color32(187, 126, 42, 255);
            case 'G': return new Color32(239, 181, 65, 255);
            case 'H': return new Color32(255, 229, 148, 255);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            "................",
            ".....OOOOOO.....",
            "...OOHHHHHHOO...",
            "..OHHGGGGGGDDO..",
            ".OHGGGGGGGGGDDO.",
            ".OHGGGGHGGGGDDO.",
            "OHGGGGHHHGGGGDDO",
            "OHGGGHHGHHGGGDDO",
            "OHGGGHHGHHGGGDDO",
            "OHGGGGHHHGGGGDDO",
            ".OGGGGGHGGGGDDO.",
            ".OGGGGGGGGGDDDO.",
            "..ODGGGGGDDDDO..",
            "...OODDDDDDOO...",
            ".....OOOOOO.....",
            "................"
        };
    }
}
